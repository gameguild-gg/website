using System;

namespace GameGuild.CQRS;

/// <summary>
///     Represents a void type — used as the response type for requests that don't return a value.
///     Analogous to <c>void</c> but usable as a generic type parameter.
/// </summary>
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
{
    /// <summary>
    ///     Default and only value of <see cref="Unit"/>.
    /// </summary>
    public static readonly Unit Value = default;

    /// <summary>
    ///     Returns a completed task with <see cref="Unit"/> value.
    /// </summary>
    public static readonly Task<Unit> Task = System.Threading.Tasks.Task.FromResult(Value);

    /// <inheritdoc />
    public bool Equals(Unit other) => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <inheritdoc />
    public int CompareTo(Unit other) => 0;

    /// <inheritdoc />
    public int CompareTo(object? obj) => 0;

    /// <inheritdoc />
    public override string ToString() => "()";

    public static bool operator ==(Unit left, Unit right) => true;
    public static bool operator !=(Unit left, Unit right) => false;
}
