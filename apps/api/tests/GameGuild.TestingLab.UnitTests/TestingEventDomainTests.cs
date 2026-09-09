using FluentAssertions;
using GameGuild.TestingLab;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameGuild.TestingLab.UnitTests;

public sealed class TestingEventDomainTests
{
    [Fact]
    public void InPersonSlot_RequiresCampusAndRoom()
    {
        var act = () => TestingEventSlot.Create(
            Guid.NewGuid(),
            TestingEventMode.InPerson,
            SystemClock.UtcNow.AddDays(1),
            SystemClock.UtcNow.AddDays(1).AddHours(2),
            10,
            3,
            null,
            null,
            null,
            Guid.NewGuid());

        act.Should().Throw<ArgumentException>().WithMessage("*campus*room*");
    }

    [Fact]
    public void OnlineSlot_AllowsUnlimitedCapacity()
    {
        var slot = TestingEventSlot.Create(
            Guid.NewGuid(),
            TestingEventMode.Online,
            SystemClock.UtcNow.AddDays(1),
            SystemClock.UtcNow.AddDays(1).AddHours(2),
            null,
            null,
            null,
            null,
            "https://meet.example.com/testing-lab",
            Guid.NewGuid());

        slot.MaxTesters.Should().BeNull();
        slot.MaxProjects.Should().BeNull();
        slot.IsTesterCapacityUnlimited.Should().BeTrue();
        slot.IsProjectCapacityUnlimited.Should().BeTrue();
    }

    [Fact]
    public void Application_SubmissionDoesNotReserveSlot()
    {
        var application = TestingProjectApplication.Submit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            "Weeknights after 18:00",
            Guid.NewGuid());

        application.Status.Should().Be(TestingApplicationStatus.Pending);
        application.AssignedSlotId.Should().BeNull();
        application.DecidedAt.Should().BeNull();
    }

    [Fact]
    public void Reject_RequiresRationale()
    {
        var application = NewApplication();

        var act = () => application.Reject(Guid.NewGuid(), " ");

        act.Should().Throw<ArgumentException>().WithMessage("*rationale*");
    }

    [Fact]
    public void Approve_AssignsExactlyOneSlot()
    {
        var application = NewApplication();
        var slotId = Guid.NewGuid();

        application.BeginReview();
        application.Approve(Guid.NewGuid(), slotId, "Accepted for the showcase.");

        application.Status.Should().Be(TestingApplicationStatus.Approved);
        application.AssignedSlotId.Should().Be(slotId);
        application.DecidedAt.Should().NotBeNull();

        var secondApproval = () => application.Approve(Guid.NewGuid(), Guid.NewGuid(), "Move it.");
        secondApproval.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FeedbackObligation_BlocksCompletionUntilFulfilled()
    {
        var obligation = TestingFeedbackObligation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        obligation.IsFulfilled.Should().BeFalse();
        obligation.Fulfill(Guid.NewGuid());
        obligation.IsFulfilled.Should().BeTrue();
        obligation.FulfilledAt.Should().NotBeNull();
    }

    [Fact]
    public void Model_DefinesUniqueCommitteeMemberVotePerApplication()
    {
        using var context = new TestingEventModelContext();
        var vote = context.Model.FindEntityType(typeof(TestingApplicationVote));
        var uniqueIndex = vote!.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(TestingApplicationVote.ApplicationId), nameof(TestingApplicationVote.ReviewerId)]));

        uniqueIndex.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void UpdateEvent_RejectsApplicationWindowAfterStart()
    {
        var testingEvent = TestingEvent.Create(
            "Showcase",
            TestingEventMode.Online,
            Guid.NewGuid(),
            SystemClock.UtcNow,
            SystemClock.UtcNow.AddDays(1),
            SystemClock.UtcNow.AddDays(2),
            SystemClock.UtcNow.AddDays(3),
            true,
            TestingEventApprovalMode.ManagerOnly,
            Guid.NewGuid());

        var act = () => testingEvent.Update(
            "Showcase",
            null,
            TestingEventMode.Online,
            TestingEventApprovalMode.ManagerOnly,
            SystemClock.UtcNow,
            SystemClock.UtcNow.AddDays(3),
            SystemClock.UtcNow.AddDays(2),
            SystemClock.UtcNow.AddDays(4),
            true);

        act.Should().Throw<ArgumentException>().WithMessage("*start after applications close*");
    }

    [Fact]
    public void Event_PersistsAValidIanaTimeZone()
    {
        var startsAt = new DateTime(2026, 8, 8, 21, 0, 0, DateTimeKind.Utc);

        var testingEvent = TestingEvent.Create(
            "Showcase",
            TestingEventMode.Online,
            Guid.NewGuid(),
            startsAt.AddDays(-7),
            startsAt.AddDays(-1),
            startsAt,
            startsAt.AddHours(2),
            true,
            TestingEventApprovalMode.ManagerOnly,
            Guid.NewGuid(),
            timeZoneId: "America/Sao_Paulo");

        testingEvent.TimeZoneId.Should().Be("America/Sao_Paulo");
    }

    [Fact]
    public void Event_RejectsAnUnknownTimeZone()
    {
        var startsAt = SystemClock.UtcNow.AddDays(2);

        var act = () => TestingEvent.Create(
            "Showcase",
            TestingEventMode.Online,
            Guid.NewGuid(),
            startsAt.AddDays(-7),
            startsAt.AddDays(-1),
            startsAt,
            startsAt.AddHours(2),
            true,
            TestingEventApprovalMode.ManagerOnly,
            Guid.NewGuid(),
            timeZoneId: "Mars/Olympus_Mons");

        act.Should().Throw<ArgumentException>().WithMessage("*time zone*");
    }

    [Fact]
    public void ReassignSlot_ReplacesRatherThanDuplicatesAssignment()
    {
        var application = NewApplication();
        var firstSlotId = Guid.NewGuid();
        var secondSlotId = Guid.NewGuid();
        application.Approve(Guid.NewGuid(), firstSlotId, null);

        application.ReassignSlot(secondSlotId);

        application.AssignedSlotId.Should().Be(secondSlotId);
    }
    private static TestingProjectApplication NewApplication() => TestingProjectApplication.Submit(
        Guid.NewGuid(),
        Guid.NewGuid(),
        null,
        Guid.NewGuid(),
        null,
        Guid.NewGuid());

    private sealed class TestingEventModelContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder.UseInMemoryDatabase($"testing-event-model-{Guid.NewGuid():N}");

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new TestingLabModelConfiguration().Configure(modelBuilder);
    }
}
