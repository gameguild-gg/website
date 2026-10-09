using System.Diagnostics.CodeAnalysis;

namespace GameGuild;

/// <summary>
///     Represents the outcome of an operation that can either succeed or fail with an <see cref="Error" />.
///     This is the single source of truth for operation results across all modules.
///     Use <see cref="Result{TValue}" /> when the operation produces a value on success.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None || !isSuccess && error == Error.None)
        {
            throw new ArgumentException("Invalid error", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The error describing the failure. Equals <see cref="Error.None" /> on success.</summary>
    public Error Error { get; }

    // ── Factory methods ──────────────────────────────────────────────────

    /// <summary>Creates a successful result with no value.</summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>Creates a successful result carrying <paramref name="value" />.</summary>
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    /// <summary>Creates a failure result from <paramref name="error" />.</summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>Creates a typed failure result from <paramref name="error" />.</summary>
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    // ── Validation helpers ───────────────────────────────────────────────

    /// <summary>
    ///     Creates a validation-failure result from a collection of individual errors.
    ///     Convenience factory that wraps errors inside a <see cref="AggregateValidationError" />.
    /// </summary>
    public static Result ValidationFailure(IEnumerable<Error> errors)
        => Failure(new AggregateValidationError(errors.ToArray()));

    // ── Combinators ──────────────────────────────────────────────────────

    /// <summary>
    ///     Combines multiple results — returns the first failure, or a success if all succeed.
    /// </summary>
    public static Result Combine(params Result[] results)
    {
        var failures = results.Where(r => r.IsFailure).ToArray();
        return failures.Length == 0 ? Success() : Failure(new AggregateValidationError(failures.Select(r => r.Error).ToArray()));
    }

    /// <summary>
    ///     If successful, runs the predicate. If the predicate fails, returns a failure with <paramref name="error" />.
    /// </summary>
    public Result Ensure(Func<bool> predicate, Error error)
    {
        if (IsFailure)
        {
            return this;
        }
        return predicate() ? this : Failure(error);
    }

    /// <summary>
    ///     Executes <paramref name="action" /> on success, returns this result unchanged.
    /// </summary>
    public Result Tap(Action action)
    {
        if (IsSuccess)
        {
            action();
        }

        return this;
    }

    /// <summary>
    ///     Pattern-matches on the result, invoking the appropriate function.
    /// </summary>
    public T Match<T>(Func<T> onSuccess, Func<Error, T> onFailure)
        => IsSuccess ? onSuccess() : onFailure(Error);
}

/// <summary>
///     Represents the outcome of an operation that produces a <typeparamref name="TValue" /> on success.
/// </summary>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error) : base(isSuccess, error) { _value = value; }

    /// <summary>
    ///     The success value. Throws <see cref="InvalidOperationException" /> when accessed on a failure.
    /// </summary>
    [NotNull]
    public TValue Value
        => IsSuccess ? _value! : throw new InvalidOperationException(
            $"Cannot access the value of a failed result. Error: {Error.Code} — {Error.Description}");

    /// <summary>
    ///     Returns the value if successful, otherwise the provided <paramref name="fallback" />.
    /// </summary>
    public TValue ValueOrDefault(TValue fallback) => IsSuccess ? _value! : fallback;

    /// <summary>Implicitly converts a non-null value to a success result.</summary>
    public static implicit operator Result<TValue>(TValue? value)
        => value is not null ? Success(value) : Failure<TValue>(Error.NullValue);

    /// <summary>Creates a typed validation-failure result.</summary>
    public static Result<TValue> ValidationFailure(Error error) => new(default, false, error);

    // ── Monadic combinators ──────────────────────────────────────────────

    /// <summary>
    ///     Transforms the success value using <paramref name="mapper" />.
    /// </summary>
    public Result<TOut> Map<TOut>(Func<TValue, TOut> mapper)
        => IsSuccess ? Success(mapper(_value!)) : Failure<TOut>(Error);

    /// <summary>
    ///     Chains a dependent operation that itself returns a <see cref="Result{TOut}" />.
    /// </summary>
    public Result<TOut> Bind<TOut>(Func<TValue, Result<TOut>> binder)
        => IsSuccess ? binder(_value!) : Failure<TOut>(Error);

    /// <summary>
    ///     If successful, runs the predicate. If the predicate fails, returns a failure.
    /// </summary>
    public Result<TValue> Ensure(Func<TValue, bool> predicate, Error error)
    {
        if (IsFailure)
        {
            return this;
        }
        return predicate(_value!) ? this : Failure<TValue>(error);
    }

    /// <summary>
    ///     Executes <paramref name="action" /> on success and returns this result unchanged.
    /// </summary>
    public Result<TValue> Tap(Action<TValue> action)
    {
        if (IsSuccess)
        {
            action(_value!);
        }

        return this;
    }

    /// <summary>
    ///     Pattern-matches on the result, invoking the appropriate function.
    /// </summary>
    public T Match<T>(Func<TValue, T> onSuccess, Func<Error, T> onFailure)
        => IsSuccess ? onSuccess(_value!) : onFailure(Error);
}
