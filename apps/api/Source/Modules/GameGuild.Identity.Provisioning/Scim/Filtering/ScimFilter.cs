namespace GameGuild.Identity.Provisioning.Scim.Filtering;

/// <summary>
///     Parsed SCIM filter AST (RFC 7644 §3.4.2.2). The service provider supports the
///     string/boolean comparison operators <c>eq ne co sw ew</c>, presence <c>pr</c>,
///     and the logical connectives <c>and or not</c> with parentheses. Ordering
///     operators (<c>gt lt ge le</c>) and grouping filters over sub-attributes
///     (for example <c>emails[type eq "work"]</c>) are rejected with
///     <c>invalidFilter</c> because no mapped attribute supports them.
/// </summary>
public abstract record ScimFilter
{
    public sealed record And(ScimFilter Left, ScimFilter Right) : ScimFilter;

    public sealed record Or(ScimFilter Left, ScimFilter Right) : ScimFilter;

    public sealed record Not(ScimFilter Inner) : ScimFilter;

    /// <summary>Attribute comparison. <c>Value</c> is null only for <c>pr</c>.</summary>
    public sealed record Compare(string Attribute, ScimFilterOperator Operator, string? Value) : ScimFilter;
}

public enum ScimFilterOperator
{
    Eq,
    Ne,
    Co,
    Sw,
    Ew,
    Pr
}
