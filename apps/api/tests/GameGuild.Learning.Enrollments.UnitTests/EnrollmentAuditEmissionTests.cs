using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace GameGuild.Learning.Enrollments.UnitTests;

/// <summary>
/// Universal audit coverage for enrollment mutations (issues #180/#181/#184): each mutating
/// enrollment command emits one learning audit event, and an audit emission failure rolls the
/// mutation back inside the owning transaction (fail closed).
/// </summary>
public class EnrollmentAuditEmissionTests
{
    [Fact]
    public async Task EveryEnrollmentMutationCommandEmitsItsAuditEvent()
    {
        await using var db = CreateSqliteContext();
        var sink = new RecordingLearningAuditEventSink();
        var service = new EnrollmentService(new EnrollmentRepository(db), db, sink);
        var courseId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var enrolled = await service.EnrollAsync(new EnrollUserCommand(courseId, userId, null));
        await service.UpdateProgressAsync(enrolled.Id, 42);
        await service.SetStatusAsync(enrolled.Id, EnrollmentStatus.Paused);
        await service.SetStatusAsync(enrolled.Id, EnrollmentStatus.Completed);

        sink.Events.Select(auditEvent => auditEvent.ActionType).Should().Equal(
            "Learning.Enrollments.UserEnrolled",
            "Learning.Enrollments.ProgressUpdated",
            "Learning.Enrollments.StatusChanged",
            "Learning.Enrollments.StatusChanged");
        sink.Events.Should().OnlyContain(auditEvent => auditEvent.ResourceType == "Enrollment");
        sink.Events.Should().OnlyContain(auditEvent => auditEvent.ResourceId == enrolled.Id.ToString());
        sink.Events[0].ActorUserId.Should().Be(userId);
    }

    [Fact]
    public async Task AuditEmissionFailureRollsBackTheEnrollment()
    {
        await using var db = CreateSqliteContext();
        var sink = new ThrowingLearningAuditEventSink();
        var service = new EnrollmentService(new EnrollmentRepository(db), db, sink);

        var act = () => service.EnrollAsync(new EnrollUserCommand(Guid.NewGuid(), Guid.NewGuid(), null));

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await db.Set<Enrollment>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AuditEmissionFailureRollsBackTheStatusChange()
    {
        await using var db = CreateSqliteContext();
        var seeded = Enrollment.Create(Guid.NewGuid(), Guid.NewGuid());
        db.Set<Enrollment>().Add(seeded);
        await db.SaveChangesAsync();
        var sink = new ThrowingLearningAuditEventSink();
        var service = new EnrollmentService(new EnrollmentRepository(db), db, sink);

        var act = () => service.SetStatusAsync(seeded.Id, EnrollmentStatus.Dropped);

        await act.Should().ThrowAsync<InvalidOperationException>();
        var unchanged = await db.Set<Enrollment>().AsNoTracking().SingleAsync(enrollment => enrollment.Id == seeded.Id);
        unchanged.Status.Should().Be(EnrollmentStatus.Active);
        unchanged.DroppedAt.Should().BeNull();
    }

    private static EnrollmentAuditSqliteContext CreateSqliteContext()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<EnrollmentAuditSqliteContext>()
            .UseSqlite(connection)
            .Options;
        var context = new EnrollmentAuditSqliteContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class EnrollmentAuditSqliteContext(DbContextOptions<EnrollmentAuditSqliteContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Database.BeginTransactionAsync(cancellationToken);
    }
}
