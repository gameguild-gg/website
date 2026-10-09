using FluentAssertions;
using Xunit;

namespace GameGuild.SharedKernel.UnitTests;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessResult()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_ShouldCreateFailureResult()
    {
        var error = Error.Failure("test", "Test error");
        var result = Result.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Success_Generic_ShouldCreateSuccessResultWithValue()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_Generic_ShouldCreateFailureResult()
    {
        var error = Error.NotFound("test", "Not found");
        var result = Result.Failure<int>(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Value_ShouldThrow_OnFailureResult()
    {
        var error = Error.Failure("test", "error");
        var result = Result.Failure<int>(error);

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*failed*");
    }

    [Fact]
    public void ValueOrDefault_ShouldReturnValue_OnSuccess()
    {
        var result = Result.Success(42);

        result.ValueOrDefault(0).Should().Be(42);
    }

    [Fact]
    public void ValueOrDefault_ShouldReturnFallback_OnFailure()
    {
        var result = Result.Failure<int>(Error.Failure("test", "err"));

        result.ValueOrDefault(99).Should().Be(99);
    }

    [Fact]
    public void ImplicitConversion_ShouldCreateSuccess_FromNonNullValue()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void ImplicitConversion_ShouldCreateFailure_FromNull()
    {
        Result<string> result = (string?)null;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.NullValue);
    }

    [Fact]
    public void Combine_ShouldReturnSuccess_WhenAllSucceed()
    {
        var result = Result.Combine(Result.Success(), Result.Success());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Combine_ShouldReturnFailure_WhenAnyFails()
    {
        var error = Error.Failure("test", "error");
        var result = Result.Combine(Result.Success(), Result.Failure(error));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Ensure_ShouldReturnSuccess_WhenPredicateIsTrue()
    {
        var result = Result.Success().Ensure(() => true, Error.Failure("test", "error"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Ensure_ShouldReturnFailure_WhenPredicateIsFalse()
    {
        var error = Error.Failure("test", "failed");
        var result = Result.Success().Ensure(() => false, error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Ensure_ShouldNotRun_OnFailure()
    {
        var error = Error.Failure("original", "original error");
        var result = Result.Failure(error).Ensure(() => false, Error.Failure("new", "new error"));

        result.Error.Code.Should().Be("original");
    }

    [Fact]
    public void Tap_ShouldExecuteAction_OnSuccess()
    {
        var called = false;
        Result.Success().Tap(() => called = true);

        called.Should().BeTrue();
    }

    [Fact]
    public void Tap_ShouldNotExecuteAction_OnFailure()
    {
        var called = false;
        Result.Failure(Error.Failure("test", "err")).Tap(() => called = true);

        called.Should().BeFalse();
    }

    [Fact]
    public void Match_ShouldReturnSuccessValue_OnSuccess()
    {
        var result = Result.Success().Match(() => "ok", _ => "fail");

        result.Should().Be("ok");
    }

    [Fact]
    public void Match_ShouldReturnFailureValue_OnFailure()
    {
        var error = Error.Failure("test", "err");
        var result = Result.Failure(error).Match(() => "ok", e => e.Code);

        result.Should().Be("test");
    }

    [Fact]
    public void ValidationFailure_ShouldCreateAggregateError()
    {
        var errors = new[] { Error.Validation("a", "error a"), Error.Validation("b", "error b") };
        var result = Result.ValidationFailure(errors);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<AggregateValidationError>();
    }

    [Fact]
    public void Map_ShouldTransformValue_OnSuccess()
    {
        var result = Result.Success(5).Map(x => x * 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(10);
    }

    [Fact]
    public void Map_ShouldPropagateError_OnFailure()
    {
        var error = Error.Failure("test", "err");
        var result = Result.Failure<int>(error).Map(x => x * 2);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Bind_ShouldChainOperations_OnSuccess()
    {
        var result = Result.Success(5).Bind(x => Result.Success(x.ToString()));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("5");
    }

    [Fact]
    public void Bind_ShouldPropagateError_OnFailure()
    {
        var error = Error.Failure("test", "err");
        var result = Result.Failure<int>(error).Bind(x => Result.Success(x.ToString()));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Ensure_Generic_ShouldReturnFailure_WhenPredicateFails()
    {
        var error = Error.Failure("bad", "bad value");
        var result = Result.Success(5).Ensure(x => x > 10, error);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Ensure_Generic_ShouldReturnSuccess_WhenPredicatePasses()
    {
        var result = Result.Success(5).Ensure(x => x > 0, Error.Failure("bad", "bad value"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(5);
    }

    [Fact]
    public void Ensure_Generic_ShouldPreserveFailureWithoutRunningPredicate()
    {
        var originalError = Error.Failure("original", "original failure");
        var original = Result.Failure<int>(originalError);
        var predicateCalls = 0;

        var result = original.Ensure(_ =>
        {
            predicateCalls++;
            return true;
        }, Error.Failure("replacement", "replacement failure"));

        result.Should().BeSameAs(original);
        result.Error.Should().Be(originalError);
        predicateCalls.Should().Be(0);
    }

    [Fact]
    public void Tap_Generic_ShouldExecuteAction_OnSuccess()
    {
        var captured = 0;
        Result.Success(42).Tap(x => captured = x);

        captured.Should().Be(42);
    }

    [Fact]
    public void Tap_Generic_ShouldNotExecuteAction_OnFailure()
    {
        var captured = 0;
        Result.Failure<int>(Error.Failure("test", "err")).Tap(x => captured = x);

        captured.Should().Be(0);
    }

    [Fact]
    public void Match_Generic_ShouldReturnSuccessValue()
    {
        var result = Result.Success(42).Match(v => v.ToString(), e => e.Code);

        result.Should().Be("42");
    }

    [Fact]
    public void Match_Generic_ShouldReturnFailureValue()
    {
        var error = Error.Failure("ERR", "error");
        var result = Result.Failure<int>(error).Match(v => v.ToString(), e => e.Code);

        result.Should().Be("ERR");
    }

    [Fact]
    public void ValidationFailure_Generic_ShouldCreateFailure()
    {
        var error = Error.Validation("field", "required");
        var result = Result<int>.ValidationFailure(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSuccessWithError()
    {
        var act = () => Result.Failure(Error.None);

        act.Should().Throw<ArgumentException>();
    }
}
