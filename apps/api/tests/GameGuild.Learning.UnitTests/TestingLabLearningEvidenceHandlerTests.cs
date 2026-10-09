using FluentAssertions;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Enrollments;
using GameGuild.Learning.Grading.Contracts;
using GameGuild.Learning.TestingLab;
using GameGuild.TestingLab;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Learning.UnitTests;

public sealed class TestingLabLearningEvidenceHandlerTests : IDisposable
{
    private readonly TestContext _context;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _activityId = Guid.NewGuid();
    private readonly Guid _programUserId = Guid.NewGuid();

    public TestingLabLearningEvidenceHandlerTests()
    {
        _context = new TestContext(new DbContextOptionsBuilder<TestContext>()
            .UseInMemoryDatabase($"testing-learning-consumer-{Guid.NewGuid():N}")
            .Options);
        _context.ProgramContents.Add(new ProgramContent
        {
            Id = _activityId,
            ProgramId = _courseId,
            Title = "Testing Lab participation",
            Type = ProgramContentType.Assignment,
            TenantId = _tenantId
        });
        _context.ProgramUsers.Add(new ProgramUser
        {
            Id = _programUserId,
            ProgramId = _courseId,
            UserId = _userId,
            JoinedAt = SystemClock.UtcNow,
            IsActive = true,
            TenantId = _tenantId
        });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Handle_WhenEvidenceIsValid_ShouldCreateCompletedInteractionAndReceipt()
    {
        var notification = CreateNotification();

        await CreateHandler().Handle(notification, default);

        var interaction = await _context.ContentInteractions.AsNoTracking().SingleAsync();
        interaction.UserId.Should().Be(_userId);
        interaction.ContentId.Should().Be(_activityId);
        interaction.ProgramUserId.Should().Be(_programUserId);
        interaction.IsCompleted.Should().BeTrue();
        interaction.ProgressPercentage.Should().Be(PercentValue.Hundred);
        var receipt = await _context.EvidenceReceipts.AsNoTracking().SingleAsync();
        receipt.EvidenceId.Should().Be(notification.EvidenceId);
        receipt.RegistrationId.Should().Be(notification.EvidenceId);
    }

    [Fact]
    public async Task Handle_WhenInteractionExists_ShouldCompleteItWithoutCreatingAnother()
    {
        _context.ContentInteractions.Add(new ContentInteraction
        {
            UserId = _userId,
            ContentId = _activityId,
            ProgramUserId = _programUserId,
            TenantId = _tenantId
        });
        await _context.SaveChangesAsync();

        await CreateHandler().Handle(CreateNotification(), default);

        (await _context.ContentInteractions.SingleAsync()).IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenEvidenceIsDeliveredTwice_ShouldConsumeItExactlyOnce()
    {
        var notification = CreateNotification();
        var handler = CreateHandler();

        await handler.Handle(notification, default);
        await handler.Handle(notification, default);

        (await _context.EvidenceReceipts.CountAsync()).Should().Be(1);
        (await _context.ContentInteractions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenActivityDoesNotBelongToCourse_ShouldRejectEvidence()
    {
        var notification = CreateNotification(courseId: Guid.NewGuid());

        var act = () => CreateHandler().Handle(notification, default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*activity*course*");
        (await _context.EvidenceReceipts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenActiveCourseEnrollmentDoesNotExist_ShouldRejectEvidence()
    {
        (await _context.ProgramUsers.SingleAsync()).Deactivate();
        await _context.SaveChangesAsync();

        var act = () => CreateHandler().Handle(CreateNotification(), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*active course enrollment*");
        (await _context.EvidenceReceipts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenCohortIsLinked_ShouldRequireMatchingActiveEnrollment()
    {
        var cohortId = Guid.NewGuid();
        var notification = CreateNotification(cohortId: cohortId);

        var missing = () => CreateHandler().Handle(notification, default);
        await missing.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cohort enrollment*");

        _context.Enrollments.Add(Enrollment.Create(_courseId, _userId, cohortId));
        await _context.SaveChangesAsync();
        await CreateHandler().Handle(notification, default);

        (await _context.EvidenceReceipts.CountAsync()).Should().Be(1);
    }

    private TestingLabLearningEvidenceHandler CreateHandler() => new(
        _context,
        NullLogger<TestingLabLearningEvidenceHandler>.Instance);

    private TestingLearningEvidenceCompletedEvent CreateNotification(
        Guid? courseId = null,
        Guid? cohortId = null)
    {
        var registrationId = Guid.NewGuid();
        return new TestingLearningEvidenceCompletedEvent(
            registrationId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            _userId,
            courseId ?? _courseId,
            cohortId,
            _activityId,
            TestingLearningCompletionRequirement.Attendance,
            SystemClock.UtcNow,
            _tenantId);
    }

    private sealed class TestContext(DbContextOptions<TestContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public DbSet<ProgramContent> ProgramContents => Set<ProgramContent>();
        public DbSet<ProgramUser> ProgramUsers => Set<ProgramUser>();
        public DbSet<ContentInteraction> ContentInteractions => Set<ContentInteraction>();
        public DbSet<Enrollment> Enrollments => Set<Enrollment>();
        public DbSet<TestingLabLearningEvidenceReceipt> EvidenceReceipts =>
            Set<TestingLabLearningEvidenceReceipt>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProgramContent>(builder =>
            {
                builder.Ignore(content => content.Program);
                builder.Ignore(content => content.Parent);
                builder.Ignore(content => content.Children);
                builder.Ignore(content => content.ContentInteractions);
            });
            modelBuilder.Entity<ProgramUser>(builder =>
            {
                builder.Ignore(enrollment => enrollment.User);
                builder.Ignore(enrollment => enrollment.Program);
                builder.Ignore(enrollment => enrollment.ContentInteractions);
                builder.Ignore(enrollment => enrollment.ReceivedGrades);
                builder.Ignore(enrollment => enrollment.GivenGrades);
                builder.Ignore(enrollment => enrollment.ProgramRatings);
            });
            modelBuilder.Entity<ContentInteraction>(builder =>
            {
                builder.Ignore(interaction => interaction.User);
                builder.Ignore(interaction => interaction.Content);
                builder.Ignore(interaction => interaction.ProgramUser);
                builder.Ignore(interaction => interaction.ActivityGrades);
                builder.Ignore(interaction => interaction.Events);
            });
            modelBuilder.Entity<Enrollment>();
            new TestingLabLearningModelConfiguration().Configure(modelBuilder);
            base.OnModelCreating(modelBuilder);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
