using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Courses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public sealed class ControllerBranchCoverageTests
{
    [Fact]
    public void PeerReviewWindow_UsesDueAvailabilityLateDeadlineAndOpenEndedFallbacks()
    {
        var method = typeof(PeerReviewsController)
            .GetMethod("IsReviewWindowOpen", BindingFlags.NonPublic | BindingFlags.Static)!;
        var now = DateTime.UtcNow;
        var openEnded = Assessment.Create(Guid.NewGuid(), "Open", AssessmentType.PeerReview, Score(100));
        var due = Assessment.Create(Guid.NewGuid(), "Due", AssessmentType.PeerReview, Score(100));
        due.SetDeliverySchedule(null, null, now.AddHours(1), false, null);
        var pastDue = Assessment.Create(Guid.NewGuid(), "Past due", AssessmentType.PeerReview, Score(100));
        pastDue.SetDeliverySchedule(null, null, now.AddHours(-1), false, null);
        var availability = Assessment.Create(Guid.NewGuid(), "Availability", AssessmentType.PeerReview, Score(100));
        availability.SetDeliverySchedule(null, now.AddHours(-1), null, false, null);
        var lateDeadline = Assessment.Create(Guid.NewGuid(), "Late", AssessmentType.PeerReview, Score(100));
        SetProperty(lateDeadline, nameof(Assessment.LateSubmissionDeadline), now.AddHours(1));

        Invoke(method, openEnded).Should().BeTrue();
        Invoke(method, due).Should().BeTrue();
        Invoke(method, pastDue).Should().BeFalse();
        Invoke(method, availability).Should().BeFalse();
        Invoke(method, lateDeadline).Should().BeTrue();
    }

    [Fact]
    public void RubricRejection_MapsValidationErrorsToBadRequest()
    {
        var controller = new RubricsController(
            Mock.Of<IRubricService>(),
            Mock.Of<IAssessmentService>(),
            Mock.Of<ICourseAccessEvaluator>(),
            NullLogger<RubricsController>.Instance,
            Mock.Of<ISender>());
        var method = typeof(RubricsController)
            .GetMethod("RubricRejection", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var result = (ActionResult)method.Invoke(
            controller, [Error.Validation("Rubric.Invalid", "Invalid rubric")])!;

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    private static bool Invoke(MethodInfo method, Assessment assessment) =>
        (bool)method.Invoke(null, [assessment])!;

    private static void SetProperty<T>(Assessment assessment, string name, T value) =>
        typeof(Assessment).GetProperty(name)!.SetValue(assessment, value);
}
