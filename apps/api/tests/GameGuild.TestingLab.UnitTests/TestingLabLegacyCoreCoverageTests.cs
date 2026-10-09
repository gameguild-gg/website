using System.Reflection;
using FluentAssertions;
using Moq;
using Xunit;

namespace GameGuild.TestingLab.UnitTests;

public sealed class TestingRequestDomainCoverageTests
{
    [Fact]
    public void ComputedProperties_DescribeAvailabilityCapacityAndDuration()
    {
        var now = SystemClock.UtcNow;
        var request = new TestingRequest
        {
            TenantId = null,
            Status = TestingRequestStatus.Active,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(2),
            MaxTesters = 3,
            CurrentTesterCount = 1
        };

        request.IsGlobal.Should().BeTrue();
        request.IsActive.Should().BeTrue();
        request.AcceptsNewTesters.Should().BeTrue();
        request.AvailableSpots.Should().Be(2);
        request.Duration.Should().BeCloseTo(TimeSpan.FromDays(3), TimeSpan.FromSeconds(1));
        request.DaysRemaining.Should().NotBeNull();

        request.TenantId = Guid.NewGuid();
        request.CurrentTesterCount = 5;
        request.IsGlobal.Should().BeFalse();
        request.AcceptsNewTesters.Should().BeFalse();
        request.AvailableSpots.Should().Be(0);

        request.MaxTesters = null;
        request.AcceptsNewTesters.Should().BeTrue();
        request.AvailableSpots.Should().BeNull();

        request.Status = TestingRequestStatus.Draft;
        request.IsActive.Should().BeFalse();
        request.AcceptsNewTesters.Should().BeFalse();
        request.DaysRemaining.Should().BeNull();
    }

    [Fact]
    public void Lifecycle_EnforcesTransitionsAndMaintainsTesterCapacity()
    {
        var request = new TestingRequest { Status = TestingRequestStatus.Draft, MaxTesters = 1 };
        request.Activate();
        request.Status.Should().Be(TestingRequestStatus.Active);
        request.Pause();
        request.Status.Should().Be(TestingRequestStatus.Paused);
        request.Activate();

        request.AddTester();
        request.CurrentTesterCount.Should().Be(1);
        var full = () => request.AddTester();
        full.Should().Throw<InvalidOperationException>();
        request.RemoveTester();
        request.RemoveTester();
        request.CurrentTesterCount.Should().Be(0);

        request.MaxTesters = null;
        request.AddTester();
        request.CurrentTesterCount.Should().Be(1);

        request.SetPriority(TestingPriority.Critical);
        request.SetEstimatedDuration(4);
        request.Priority.Should().Be(TestingPriority.Critical);
        request.EstimatedDurationHours.Should().Be(4);

        request.Complete();
        request.Complete();
        var cancelCompleted = () => request.Cancel();
        cancelCompleted.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Lifecycle_RejectsInvalidActivationAndPauseAndAllowsCancellation()
    {
        var request = new TestingRequest { Status = TestingRequestStatus.Open };

        var activate = () => request.Activate();
        var pause = () => request.Pause();
        activate.Should().Throw<InvalidOperationException>();
        pause.Should().Throw<InvalidOperationException>();

        request.Cancel();
        request.Status.Should().Be(TestingRequestStatus.Cancelled);
    }
}

public sealed class TestingSessionDomainCoverageTests
{
    [Fact]
    public void ComputedPropertiesAndRegistration_ReflectStateAndCapacity()
    {
        var userId = Guid.NewGuid();
        var session = NewSession(SessionStatus.Scheduled);

        session.IsGlobal.Should().BeTrue();
        session.IsActive.Should().BeFalse();
        session.IsCompleted.Should().BeFalse();
        session.AllowsRegistration.Should().BeTrue();
        session.AvailableSpots.Should().Be(2);
        session.Duration.Should().BeCloseTo(TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(1));
        session.CanUserRegister(userId).Should().BeTrue();

        session.Registrations.Add(new SessionRegistration { UserId = userId });
        session.CanUserRegister(userId).Should().BeFalse();
        session.RegisteredTesterCount = 5;
        session.AvailableSpots.Should().Be(0);
        session.AllowsRegistration.Should().BeFalse();

        session.Status = SessionStatus.Active;
        session.RegisteredTesterCount = 0;
        session.AllowsRegistration.Should().BeFalse();
        session.CanUserRegister(Guid.NewGuid()).Should().BeFalse();

        session.TenantId = Guid.NewGuid();
        session.IsGlobal.Should().BeFalse();
    }

    [Fact]
    public void Lifecycle_EnforcesTransitionsAndNeverProducesNegativeCounts()
    {
        var session = NewSession(SessionStatus.Scheduled);
        session.Start();
        session.IsActive.Should().BeTrue();
        var startTwice = () => session.Start();
        startTwice.Should().Throw<InvalidOperationException>();

        session.IncrementTesterCount();
        session.DecrementTesterCount();
        session.DecrementTesterCount();
        session.RegisteredTesterCount.Should().Be(0);

        session.Complete();
        session.IsCompleted.Should().BeTrue();
        var completeTwice = () => session.Complete();
        var cancelCompleted = () => session.Cancel();
        completeTwice.Should().Throw<InvalidOperationException>();
        cancelCompleted.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_AllowsNonCompletedSession()
    {
        var session = NewSession(SessionStatus.Scheduled);
        session.Cancel();
        session.Status.Should().Be(SessionStatus.Cancelled);
    }

    private static TestingSession NewSession(SessionStatus status) => new()
    {
        Status = status,
        StartTime = SystemClock.UtcNow,
        EndTime = SystemClock.UtcNow.AddHours(1),
        MaxTesters = 2
    };
}

public sealed class TestingFeedbackFormDomainCoverageTests
{
    [Fact]
    public void FormAliasesTagsLifecycleAndSubmissionCount_WorkTogether()
    {
        var form = new TestingFeedbackForm { TenantId = null };
        form.FormSchema = "{\"type\":\"object\"}";
        form.FormData.Should().Be(form.FormSchema);
        form.IsGlobal.Should().BeTrue();
        form.SubmissionCount.Should().Be(0);
        form.TagArray.Should().BeEmpty();

        form.SetTags(" bugs ", "", "usability");
        form.TagArray.Should().Equal(" bugs ", "usability");
        form.Deactivate();
        form.IsActive.Should().BeFalse();
        form.Activate();
        form.IsActive.Should().BeTrue();
        form.UpdateFormData("updated");
        form.FormData.Should().Be("updated");
        form.FormVersion.Should().Be(2);

        form.Feedback.Add(new TestingFeedback());
        form.SubmissionCount.Should().Be(1);
        form.Feedback = null!;
        form.SubmissionCount.Should().Be(0);
        form.TenantId = Guid.NewGuid();
        form.IsGlobal.Should().BeFalse();
    }
}

public sealed class TestServiceDelegationCoverageTests
{
    [Fact]
    public async Task CompositeService_ForwardsEveryPublicOperationExactlyOnce()
    {
        var requestOps = new Mock<ITestingRequestOperations>();
        var sessionOps = new Mock<ITestingSessionOperations>();
        var participantOps = new Mock<ITestingParticipantOperations>();
        var feedbackOps = new Mock<ITestingFeedbackOperations>();
        var locationOps = new Mock<ITestingLocationOperations>();
        var mocks = new Mock[] { requestOps, sessionOps, participantOps, feedbackOps, locationOps };
        var service = new TestService(
            requestOps.Object,
            sessionOps.Object,
            participantOps.Object,
            feedbackOps.Object,
            locationOps.Object);

        var methods = typeof(TestService).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        foreach (var method in methods)
        {
            var invocationCount = mocks.Sum(mock => mock.Invocations.Count);
            var result = method.Invoke(service, method.GetParameters().Select(parameter => CreateArgument(parameter.ParameterType)).ToArray());
            if (result is Task task)
            {
                await task;
            }

            mocks.Sum(mock => mock.Invocations.Count).Should().Be(invocationCount + 1, method.Name);
            mocks.SelectMany(mock => mock.Invocations).Last().Method.Name.Should().Be(method.Name);
        }

        methods.Should().HaveCountGreaterThan(50);
    }

    private static object? CreateArgument(Type type)
    {
        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable != null)
        {
            return null;
        }

        if (type == typeof(string))
        {
            return "value";
        }

        if (type == typeof(Guid))
        {
            return Guid.NewGuid();
        }

        if (type == typeof(int))
        {
            return 1;
        }

        if (type == typeof(bool))
        {
            return false;
        }

        if (type == typeof(CancellationToken))
        {
            return CancellationToken.None;
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).GetValue(0);
        }

        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }

        var parameterless = type.GetConstructor(Type.EmptyTypes);
        if (parameterless != null)
        {
            return parameterless.Invoke(null);
        }

        var constructor = type.GetConstructors().OrderBy(candidate => candidate.GetParameters().Length).First();
        return constructor.Invoke(constructor.GetParameters().Select(parameter => CreateArgument(parameter.ParameterType)).ToArray());
    }
}
