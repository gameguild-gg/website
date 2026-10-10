namespace GameGuild.Identity.Provisioning;

/// <summary>
///     SCIM protocol error carrying the RFC 7644 §3.12 <c>scimType</c> detail and the
///     HTTP status the provider must answer with.
/// </summary>
public sealed class ScimException : Exception
{
    public ScimException(int status, string? scimType, string detail) : base(detail)
    {
        Status = status;
        ScimType = scimType;
        Detail = detail;
    }

    /// <summary>HTTP status code for the error response.</summary>
    public int Status { get; }

    /// <summary>RFC 7644 §3.12 scimType keyword, when applicable.</summary>
    public string? ScimType { get; }

    /// <summary>Human-readable detail rendered in the error body.</summary>
    public string Detail { get; }

    public static ScimException InvalidFilter(string detail)
        => new(400, "invalidFilter", detail);

    public static ScimException InvalidPath(string detail)
        => new(400, "invalidPath", detail);

    public static ScimException InvalidValue(string detail)
        => new(400, "invalidValue", detail);

    public static ScimException Uniqueness(string detail)
        => new(409, "uniqueness", detail);

    public static ScimException Mutability(string detail)
        => new(400, "mutability", detail);

    public static ScimException NoTarget(string detail)
        => new(400, "noTarget", detail);

    public static ScimException NotFound(string detail)
        => new(404, null, detail);

    public static ScimException TooMany(string detail)
        => new(400, "tooMany", detail);

    public static ScimException BadPayload(string detail)
        => new(400, null, detail);
}

/// <summary>
///     RFC 7644 §3.12 error body: <c>{schemas, status, scimType?, detail}</c>.
/// </summary>
public sealed record ScimErrorBody(
    IReadOnlyList<string> Schemas,
    int Status,
    string? ScimType,
    string Detail)
{
    public static ScimErrorBody From(ScimException exception)
        => new([ScimConstants.ErrorSchema], exception.Status, exception.ScimType, exception.Detail);
}
