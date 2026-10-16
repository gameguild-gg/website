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
    public void PeerReviewWindow_IsNotReimplementedFromMutableAssessmentDates()
    {
        var method = typeof(PeerReviewsController)
            .GetMethod("IsReviewWindowOpen", BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().BeNull(
            "the frozen ReviewStage deadline and canonical claim lease are the only peer-review timing authority");
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

}
