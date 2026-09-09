using FluentAssertions;

namespace GameGuild.Tests.SharedKernel.Unit;

public sealed class CommandOutcomeTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void IsFailure_UsesExplicitIsFailureProperty(bool isFailure, bool expected)
    {
        CommandOutcome.IsFailure(new FailureResponse(isFailure)).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void IsFailure_UsesExplicitIsSuccessProperty(bool isSuccess, bool expected)
    {
        CommandOutcome.IsFailure(new SuccessResponse(isSuccess)).Should().Be(expected);
    }

    [Theory]
    [InlineData(200, false)]
    [InlineData(400, true)]
    [InlineData(503, true)]
    public void IsFailure_RecognizesResponseStatusCodes(int statusCode, bool expected)
    {
        CommandOutcome.IsFailure(new StatusResponse(statusCode)).Should().Be(expected);
    }

    [Fact]
    public void IsFailure_RecognizesNestedResponseFailure()
    {
        CommandOutcome.IsFailure(new NestedResponse(new StatusResponse(409))).Should().BeTrue();
    }

    private sealed record FailureResponse(bool IsFailure);
    private sealed record SuccessResponse(bool IsSuccess);
    private sealed record StatusResponse(int StatusCode);
    private sealed record NestedResponse(object Result);
}
