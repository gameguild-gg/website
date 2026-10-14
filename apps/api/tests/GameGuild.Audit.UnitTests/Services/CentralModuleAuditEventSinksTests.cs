using FluentAssertions;
using GameGuild.Compliance.Audit;
using GameGuild.Content.Pages;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning;
using GameGuild.Teams;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

/// <summary>
/// Universal audit coverage (issues #180/#181/#184): the central bridges for the content,
/// learning, and teams module sinks persist one central audit row through the caller's command
/// context, enrich acting identity from the ambient actor context, refuse to record outside the
/// owning transaction, and propagate persistence failures (fail closed).
/// </summary>
public sealed class CentralModuleAuditEventSinksTests
{
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private IActorContextAccessor ActorAccessor() =>
        Mock.Of<IActorContextAccessor>(accessor => accessor.ActorContext == new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = _actorId.ToString(),
            TenantId = _tenantId,
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        });

    [Fact]
    public async Task ContentPagesBridgePersistsTheMappedAuditRow()
    {
        var (sink, auditSet, context) = CreateContentSink(null, null);

        await sink.RecordAsync(new ContentPagesAuditEvent(
            "Content.Pages.PagePublished", "Page", "page-1", Description: "Page published",
            Metadata: new { Slug = "home" }), CancellationToken.None);

        auditSet.Verify(set => set.Add(It.Is<AuditLog>(row =>
            row.ActionType == "Content.Pages.PagePublished"
            && row.ResourceType == "Page"
            && row.ResourceId == "page-1"
            && row.UserId == _actorId
            && row.TenantId == _tenantId
            && row.Category == AuditCategory.Data
            && row.Success)), Times.Once);
        context.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LearningBridgePersistsTheMappedAuditRow()
    {
        var (sink, auditSet, context) = CreateLearningSink();

        await sink.RecordAsync(new LearningAuditEvent(
            "Learning.Certificates.CertificateIssued", "Certificate", "certificate-1",
            ActorUserId: Guid.NewGuid(), TenantId: Guid.NewGuid()), CancellationToken.None);

        auditSet.Verify(set => set.Add(It.Is<AuditLog>(row =>
            row.ActionType == "Learning.Certificates.CertificateIssued"
            && row.ResourceType == "Certificate"
            && row.UserId != _actorId
            && row.TenantId != _tenantId)), Times.Once);
    }

    [Fact]
    public async Task TeamsBridgePersistsTheMappedAuditRow()
    {
        var (sink, auditSet, _) = CreateTeamsSink();

        await sink.RecordAsync(new TeamsAuditEvent(
            "Teams.TeamCreated", "Team", "team-1", Success: false, ErrorMessage: "boom"), CancellationToken.None);

        auditSet.Verify(set => set.Add(It.Is<AuditLog>(row =>
            row.ActionType == "Teams.TeamCreated"
            && !row.Success
            && row.ErrorMessage == "boom"
            && row.RiskLevel == AuditRiskLevel.Medium)), Times.Once);
    }

    [Fact]
    public async Task PersistenceFailurePropagatesSoTheMutationRollsBack()
    {
        var auditSet = new Mock<DbSet<AuditLog>>();
        var context = new Mock<IApplicationDbContext>();
        context.Setup(db => db.Set<AuditLog>()).Returns(auditSet.Object);
        var failure = new InvalidOperationException("Synthetic audit persistence failure.");
        context.Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var sink = new CentralContentPagesAuditEventSink(
            context.Object, ActorAccessor(), Mock.Of<IHttpContextAccessor>());

        var act = () => sink.RecordAsync(new ContentPagesAuditEvent("Content.Pages.PageCreated", "Page", "page-1"), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().Be(failure);
    }

    [Fact]
    public async Task RecordingWithoutTheOwningTransactionFailsClosedOnRelationalStores()
    {
        await using var relational = new BridgeSqliteContext();
        await relational.Database.EnsureCreatedAsync();
        var sink = new CentralLearningAuditEventSink(relational, ActorAccessor(), Mock.Of<IHttpContextAccessor>());

        var act = () => sink.RecordAsync(new LearningAuditEvent("Learning.Enrollments.UserEnrolled", "Enrollment", "enrollment-1"), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("owning command transaction");
    }

    [Fact]
    public async Task RecordingInsideTheOwningTransactionPersistsTheRow()
    {
        await using var relational = new BridgeSqliteContext();
        await relational.Database.EnsureCreatedAsync();
        var sink = new CentralLearningAuditEventSink(relational, ActorAccessor(), Mock.Of<IHttpContextAccessor>());

        await using var transaction = await relational.BeginAuditedMutationTransactionAsync();
        await relational.SaveChangesAsync();
        await sink.RecordAsync(new LearningAuditEvent("Learning.Enrollments.UserEnrolled", "Enrollment", "enrollment-1"), CancellationToken.None);
        await transaction.CommitAuditedMutationAsync();

        (await relational.Set<AuditLog>().AsNoTracking().CountAsync()).Should().Be(1);
    }

    private (CentralContentPagesAuditEventSink Sink, Mock<DbSet<AuditLog>> AuditSet, Mock<IApplicationDbContext> Context) CreateContentSink(Guid? userId, Guid? tenantId)
    {
        var (auditSet, context) = CreateMocks();
        return (new CentralContentPagesAuditEventSink(context.Object, ActorAccessor(), Mock.Of<IHttpContextAccessor>()), auditSet, context);
    }

    private (CentralLearningAuditEventSink Sink, Mock<DbSet<AuditLog>> AuditSet, Mock<IApplicationDbContext> Context) CreateLearningSink()
    {
        var (auditSet, context) = CreateMocks();
        return (new CentralLearningAuditEventSink(context.Object, ActorAccessor(), Mock.Of<IHttpContextAccessor>()), auditSet, context);
    }

    private (CentralTeamsAuditEventSink Sink, Mock<DbSet<AuditLog>> AuditSet, Mock<IApplicationDbContext> Context) CreateTeamsSink()
    {
        var (auditSet, context) = CreateMocks();
        return (new CentralTeamsAuditEventSink(context.Object, ActorAccessor(), Mock.Of<IHttpContextAccessor>()), auditSet, context);
    }

    private (Mock<DbSet<AuditLog>> AuditSet, Mock<IApplicationDbContext> Context) CreateMocks()
    {
        var auditSet = new Mock<DbSet<AuditLog>>();
        var context = new Mock<IApplicationDbContext>();
        context.Setup(db => db.Set<AuditLog>()).Returns(auditSet.Object);
        context.Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return (auditSet, context);
    }

    private sealed class BridgeSqliteContext(DbContextOptions<BridgeSqliteContext>? options = null)
        : DbContext(options ?? CreateOptions()), IApplicationDbContext
    {
        private static DbContextOptions<BridgeSqliteContext> CreateOptions()
        {
            var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            connection.Open();
            return new DbContextOptionsBuilder<BridgeSqliteContext>().UseSqlite(connection).Options;
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Database.BeginTransactionAsync(cancellationToken);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AuditLog>(entity => entity.HasKey(auditLog => auditLog.Id));
        }
    }
}
