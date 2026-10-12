using System.Security.Claims;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests;

public sealed class CourseContentAccessRuleEvaluatorTests
{
    private readonly Mock<ICourseAccessEvaluator> _courseAccess = new();

    [Fact]
    public void RuleType_IdentifiesCourseContentAccessContract()
    {
        var evaluator = new CourseContentAccessRuleEvaluator(_courseAccess.Object);

        evaluator.RuleType.Should().Be(RuleTypes.CourseContentAccess);
    }

    [Fact]
    public async Task PublicOutline_AllowsOnlyPublishedPublicCourse()
    {
        var allowed = await EvaluateAsync(new Program
        {
            Status = ContentStatus.Published,
            Visibility = ContentVisibility.Public
        }, "PublicOutline");
        var denied = await EvaluateAsync(new Program
        {
            Status = ContentStatus.Draft,
            Visibility = ContentVisibility.Public
        }, "PublicOutline");

        allowed.IsSuccess.Should().BeTrue();
        denied.IsSuccess.Should().BeFalse();
        _courseAccess.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Learner_RequiresLearnCapability()
    {
        var course = new Program { Id = Guid.NewGuid() };
        SetCapabilities(course, canLearn: true);

        var allowed = await EvaluateAsync(course, "Learner");
        SetCapabilities(course);
        var denied = await EvaluateAsync(course, "Learner");

        allowed.IsSuccess.Should().BeTrue();
        denied.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, true, false, false)]
    [InlineData("Edit", true, false, false)]
    [InlineData("Publish", false, true, false)]
    [InlineData("StaffReview", false, false, true)]
    public async Task Manage_RequiresTheConfiguredCapabilityWithoutPromotion(
        string? requestedCapability,
        bool canEdit,
        bool canPublish,
        bool canReviewAsStaff)
    {
        var course = new Program { Id = Guid.NewGuid() };
        SetCapabilities(course, canEdit: canEdit, canPublish: canPublish, canReviewAsStaff: canReviewAsStaff);

        var result = await EvaluateAsync(course, "Manage", requestedCapability);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Manage_DoesNotPromoteStaffReviewToEdit()
    {
        var course = new Program { Id = Guid.NewGuid() };
        SetCapabilities(course, canReviewAsStaff: true);

        var edit = await EvaluateAsync(course, "Manage", "Edit");
        var review = await EvaluateAsync(course, "Manage", "StaffReview");

        edit.IsSuccess.Should().BeFalse();
        review.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Manage_FailsClosedForInvalidConfiguredCapability()
    {
        var course = new Program { Id = Guid.NewGuid() };
        SetCapabilities(course, canEdit: true);

        var result = await EvaluateAsync(course, "Manage", "Invalid");

        result.IsSuccess.Should().BeFalse();
        _courseAccess.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Evaluator_FailsClosedForWrongResourceOrUnknownAccessMode()
    {
        var wrongResource = await EvaluateAsync(new object(), "Learner");
        var unknownMode = await EvaluateAsync(new Program(), "Unknown");

        wrongResource.IsSuccess.Should().BeFalse();
        unknownMode.IsSuccess.Should().BeFalse();
    }

    private async Task<RuleEvaluationResult> EvaluateAsync(
        object resource,
        string access,
        string? capability = null)
    {
        var evaluator = new CourseContentAccessRuleEvaluator(_courseAccess.Object);
        var context = new AuthorizationHandlerContext([], AuthenticatedUser(), resource);
        var capabilityJson = capability is null ? string.Empty : $",\n  \"capability\": \"{capability}\"";
        var parameters = RuleParameters.FromJson($$"""
        {
          "access": "{{access}}"{{capabilityJson}}
        }
        """);

        return await evaluator.EvaluateAsync(context, parameters);
    }

    private void SetCapabilities(
        Program course,
        bool canLearn = false,
        bool canEdit = false,
        bool canPublish = false,
        bool canReviewAsStaff = false)
    {
        _courseAccess
            .Setup(evaluator => evaluator.GetCapabilitiesAsync(
                course,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CourseAccessCapabilities(
                course.Id,
                true,
                true,
                false,
                canLearn,
                canLearn,
                canEdit,
                canPublish,
                canReviewAsStaff));
    }

    private static ClaimsPrincipal AuthenticatedUser() =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
            "Test"));
}
