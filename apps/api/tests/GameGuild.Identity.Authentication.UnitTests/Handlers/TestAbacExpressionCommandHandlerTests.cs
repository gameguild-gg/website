using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

/// <summary>
///     Tests for the TestAbacExpression command handler (issue #293): the ABAC
///     test-expression HTTP command is backed by a real handler that validates and
///     evaluates the expression payload against a hypothetical context, read-only.
/// </summary>
public class TestAbacExpressionCommandHandlerTests
{
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();

    private TestAbacExpressionCommandHandler CreateSut() => new(
        _actorAccessor.Object,
        NullLogger<TestAbacExpressionCommandHandler>.Instance);

    private void SetAuthenticatedActor() =>
        _actorAccessor
            .SetupGet(a => a.ActorContext)
            .Returns(ActorContextBuilder.ForUser(Guid.NewGuid()).Build());

    [Fact]
    public async Task ValidExpression_EvaluatesAgainstTestContext()
    {
        SetAuthenticatedActor();
        var command = new TestAbacExpressionCommand
        {
            JsonExpression = @"{""user.department"": ""IT"", ""user.clearanceLevel"": {""$gte"": 3}}",
            TestContext = new AbacEvaluationContext()
        };
        command.TestContext.AddUserAttribute("subject.department", "IT");
        command.TestContext.AddUserAttribute("subject.clearanceLevel", 5);

        var result = await CreateSut().Handle(command, CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.ValidationErrors.Should().BeEmpty();
        result.EvaluationResult.Should().BeTrue();
        result.DebugInfo.Should().ContainKey("attributeCount").WhoseValue.Should().Be(2);
    }

    [Fact]
    public async Task NonMatchingExpression_EvaluatesFalse()
    {
        SetAuthenticatedActor();
        var command = new TestAbacExpressionCommand
        {
            JsonExpression = @"{""user.department"": ""Sales""}",
            TestContext = new AbacEvaluationContext()
        };
        command.TestContext.AddUserAttribute("subject.department", "IT");

        var result = await CreateSut().Handle(command, CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.EvaluationResult.Should().BeFalse();
    }

    [Fact]
    public async Task StructurallyInvalidExpression_ReportsValidationErrors_WithoutEvaluating()
    {
        SetAuthenticatedActor();
        var command = new TestAbacExpressionCommand
        {
            JsonExpression = @"{""user.level"": {""$bogus"": 1}}",
            TestContext = new AbacEvaluationContext()
        };

        var result = await CreateSut().Handle(command, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().Contain(e => e.Contains("$bogus"));
        result.EvaluationResult.Should().BeNull();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MalformedJson_FailsValidation()
    {
        SetAuthenticatedActor();
        var command = new TestAbacExpressionCommand { JsonExpression = "{nope" };

        var result = await CreateSut().Handle(command, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UnauthenticatedCaller_IsRejected()
    {
        _actorAccessor
            .SetupGet(a => a.ActorContext)
            .Returns(GameGuild.Identity.Context.Actors.ActorContext.Anonymous);

        var act = () => CreateSut().Handle(new TestAbacExpressionCommand { JsonExpression = "{}" }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
