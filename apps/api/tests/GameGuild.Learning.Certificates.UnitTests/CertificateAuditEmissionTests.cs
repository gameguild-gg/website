using FluentAssertions;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

using LearningProgram = GameGuild.Learning.Courses.Program;

namespace GameGuild.Learning.Certificates.Tests;

/// <summary>
/// Universal audit coverage for certificate mutations (issues #180/#181/#184): issuing and
/// revoking certificates (and template mutations) emit learning audit events, and an audit
/// emission failure rolls the mutation back (fail closed — the operation reports failure and
/// nothing is persisted).
/// </summary>
public class CertificateAuditEmissionTests
{
    [Fact]
    public async Task IssuingAndRevokingCertificatesEmitsAuditEvents()
    {
        await using var context = CreateSqliteContext();
        var sink = new RecordingLearningAuditEventSink();
        var service = new CertificateService(context, NullLogger<CertificateService>.Instance, sink);
        var (templateId, enrollmentId, userId, courseId) = await SeedAsync(context);

        var issued = await service.IssueCertificateAsync(templateId, enrollmentId, userId, courseId);
        var revoked = await service.RevokeCertificateAsync(issued.Value.Id, "compliance hold");

        issued.IsSuccess.Should().BeTrue();
        revoked.IsSuccess.Should().BeTrue();
        sink.Events.Select(auditEvent => auditEvent.ActionType).Should().Equal(
            "Learning.Certificates.CertificateIssued",
            "Learning.Certificates.CertificateRevoked");
        sink.Events.Should().OnlyContain(auditEvent => auditEvent.ResourceType == "Certificate");
        sink.Events[0].ActorUserId.Should().Be(userId);
    }

    [Fact]
    public async Task TemplateMutationsEmitAuditEvents()
    {
        await using var context = CreateSqliteContext();
        var sink = new RecordingLearningAuditEventSink();
        var service = new CertificateTemplateService(context, NullLogger<CertificateTemplateService>.Instance, sink);
        var courseId = Guid.NewGuid();

        var template = CertificateTemplate.Create(courseId, "Completion", "<html />");
        (await service.CreateTemplateAsync(template)).IsSuccess.Should().BeTrue();
        template.SetDefault(true);
        (await service.UpdateTemplateAsync(template)).IsSuccess.Should().BeTrue();
        (await service.SetDefaultTemplateAsync(courseId, template.Id)).IsSuccess.Should().BeTrue();
        (await service.DeleteTemplateAsync(template.Id)).IsSuccess.Should().BeTrue();

        sink.Events.Select(auditEvent => auditEvent.ActionType).Should().Equal(
            "Learning.Certificates.TemplateCreated",
            "Learning.Certificates.TemplateUpdated",
            "Learning.Certificates.TemplateDefaultChanged",
            "Learning.Certificates.TemplateDeleted");
    }

    [Fact]
    public async Task AuditEmissionFailureBlocksCertificateIssuance()
    {
        await using var context = CreateSqliteContext();
        var sink = new ThrowingLearningAuditEventSink();
        var service = new CertificateService(context, NullLogger<CertificateService>.Instance, sink);
        var (templateId, enrollmentId, userId, courseId) = await SeedAsync(context);

        var result = await service.IssueCertificateAsync(templateId, enrollmentId, userId, courseId);

        result.IsSuccess.Should().BeFalse();
        (await context.Set<Certificate>().AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AuditEmissionFailureBlocksCertificateRevocation()
    {
        await using var context = CreateSqliteContext();
        var recordingSink = new RecordingLearningAuditEventSink();
        var issuing = new CertificateService(context, NullLogger<CertificateService>.Instance, recordingSink);
        var (templateId, enrollmentId, userId, courseId) = await SeedAsync(context);
        var issued = await issuing.IssueCertificateAsync(templateId, enrollmentId, userId, courseId);
        issued.IsSuccess.Should().BeTrue();

        var revoking = new CertificateService(context, NullLogger<CertificateService>.Instance, new ThrowingLearningAuditEventSink());
        var result = await revoking.RevokeCertificateAsync(issued.Value.Id, "compliance hold");

        result.IsSuccess.Should().BeFalse();
        var stored = await context.Set<Certificate>().AsNoTracking().SingleAsync(certificate => certificate.Id == issued.Value.Id);
        stored.Status.Should().NotBe(CertificateStatus.Revoked);
    }

    private static async Task<(Guid TemplateId, Guid EnrollmentId, Guid UserId, Guid CourseId)> SeedAsync(CertificateAuditSqliteContext context)
    {
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var template = CertificateTemplate.Create(courseId, "Default completion", "<html />");
        var user = new User { Id = userId, Email = "recipient@example.com", Username = "recipient", Name = "Recipient" };
        var program = new LearningProgram { Id = courseId, Title = "Audited Course" };
        context.Set<CertificateTemplate>().Add(template);
        context.Set<User>().Add(user);
        context.Set<LearningProgram>().Add(program);
        await context.SaveChangesAsync();
        return (template.Id, Guid.NewGuid(), userId, courseId);
    }

    private static CertificateAuditSqliteContext CreateSqliteContext()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<CertificateAuditSqliteContext>()
            .UseSqlite(connection)
            .Options;
        var context = new CertificateAuditSqliteContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class CertificateAuditSqliteContext(DbContextOptions<CertificateAuditSqliteContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Database.BeginTransactionAsync(cancellationToken);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new CertificatesModelConfiguration().Configure(modelBuilder);
            modelBuilder.Entity<User>(user =>
            {
                user.HasKey(entity => entity.Id);
                user.Ignore(entity => entity.Profile);
                user.Ignore(entity => entity.Metadata);
                user.Ignore(entity => entity.Preferences);
                user.Ignore(entity => entity.Notifications);
                user.Ignore(entity => entity.TenantMemberships);
            });
            modelBuilder.Entity<LearningProgram>(program =>
            {
                program.HasKey(entity => entity.Id);
                program.Ignore(entity => entity.ProgramContents);
                program.Ignore(entity => entity.ProgramUsers);
                program.Ignore(entity => entity.ProgramRatings);
                program.Ignore(entity => entity.ProgramWishlists);
            });
        }
    }
}
