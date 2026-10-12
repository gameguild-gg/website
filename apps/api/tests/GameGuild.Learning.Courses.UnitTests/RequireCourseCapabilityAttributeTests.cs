using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests;

public sealed class RequireCourseCapabilityAttributeTests
{
    [Fact]
    public async Task RouteCourseId_WithCapability_AllowsRequest()
    {
        var courseId = Guid.NewGuid();
        var (attribute, evaluator, context) = CreateContext();
        context.HttpContext.Request.RouteValues["courseId"] = courseId;
        evaluator.Setup(service => service.HasCapabilityAsync(
                courseId,
                CourseCapability.StaffReview,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await attribute.OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task QueryCourseId_WithCapability_AllowsRequest()
    {
        var courseId = Guid.NewGuid();
        var (attribute, evaluator, context) = CreateContext();
        context.HttpContext.Request.QueryString = new QueryString($"?courseId={courseId}");
        evaluator.Setup(service => service.HasCapabilityAsync(
                courseId,
                CourseCapability.StaffReview,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await attribute.OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task ConflictingRouteAndQueryCourseIds_FailClosed()
    {
        var (attribute, evaluator, context) = CreateContext();
        context.HttpContext.Request.RouteValues["courseId"] = Guid.NewGuid();
        context.HttpContext.Request.QueryString = new QueryString($"?courseId={Guid.NewGuid()}");

        await attribute.OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ForbidResult>();
        evaluator.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task MissingOrInvalidCourseId_FailsClosed(string queryValue)
    {
        var (attribute, evaluator, context) = CreateContext();
        if (queryValue.Length > 0)
        {
            context.HttpContext.Request.QueryString = new QueryString($"?courseId={queryValue}");
        }

        await attribute.OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ForbidResult>();
        evaluator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MissingCapability_FailsClosed()
    {
        var courseId = Guid.NewGuid();
        var (attribute, evaluator, context) = CreateContext();
        context.HttpContext.Request.RouteValues["courseId"] = courseId;
        evaluator.Setup(service => service.HasCapabilityAsync(
                courseId,
                CourseCapability.StaffReview,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await attribute.OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ForbidResult>();
    }

    private static (
        RequireCourseCapabilityAttribute Attribute,
        Mock<ICourseAccessEvaluator> Evaluator,
        AuthorizationFilterContext Context) CreateContext()
    {
        var evaluator = new Mock<ICourseAccessEvaluator>();
        var services = new ServiceCollection()
            .AddSingleton(evaluator.Object)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return (
            new RequireCourseCapabilityAttribute(CourseCapability.StaffReview, "courseId"),
            evaluator,
            new AuthorizationFilterContext(actionContext, []));
    }
}
