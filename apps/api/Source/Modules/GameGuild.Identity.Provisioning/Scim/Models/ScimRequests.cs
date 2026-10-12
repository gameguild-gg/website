using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace GameGuild.Identity.Provisioning.Scim;

/// <summary>
///     Inbound SCIM User payload for POST (create) and PUT (replace). Unknown
/// attributes (including extension schemas) are ignored rather than rejected.
/// </summary>
public sealed record ScimUserRequest
{
    [JsonPropertyName("schemas")]
    public IReadOnlyList<string>? Schemas { get; init; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; init; }

    [JsonPropertyName("userName")]
    public string? UserName { get; init; }

    [JsonPropertyName("name")]
    public ScimName? Name { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("emails")]
    public IReadOnlyList<ScimEmail>? Emails { get; init; }

    [JsonPropertyName("phoneNumbers")]
    public IReadOnlyList<ScimPhoneNumber>? PhoneNumbers { get; init; }

    [JsonPropertyName("active")]
    public bool? Active { get; init; }

    [JsonPropertyName("password")]
    public string? Password
    {
        get => null;
        init => _ = value; // accepted per RFC 7643 §7.1 but intentionally not stored
    }

    /// <summary>
    ///     Resolves the user name: displayName, then name.formatted, then
    ///     given+family, then userName.
    /// </summary>
    public string ResolveDisplayName()
        => !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName.Trim()
           : !string.IsNullOrWhiteSpace(Name?.Formatted) ? Name.Formatted.Trim()
           : HasNameParts(Name) ? $"{Name!.GivenName} {Name.FamilyName}".Trim()
           : UserName?.Trim() ?? string.Empty;

    /// <summary>Resolves the primary e-mail from the e-mail collection.</summary>
    public string? ResolvePrimaryEmail()
    {
        var emails = Emails ?? Array.Empty<ScimEmail>();
        if (emails.Count == 0)
        {
            return null;
        }

        var primary = emails.FirstOrDefault(email => email.Primary == true)
                      ?? emails.FirstOrDefault(email => !string.IsNullOrWhiteSpace(email.Value));
        return primary?.Value?.Trim();
    }

    public string? ResolvePhoneNumber()
        => (PhoneNumbers ?? Array.Empty<ScimPhoneNumber>()).FirstOrDefault(phone => !string.IsNullOrWhiteSpace(phone.Value))?.Value?.Trim();

    private static bool HasNameParts(ScimName? name)
        => name is not null
           && (!string.IsNullOrWhiteSpace(name.GivenName) || !string.IsNullOrWhiteSpace(name.FamilyName));
}

/// <summary>Inbound SCIM Group payload for POST (create) and PUT (replace).</summary>
public sealed record ScimGroupRequest
{
    [JsonPropertyName("schemas")]
    public IReadOnlyList<string>? Schemas { get; init; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("members")]
    public IReadOnlyList<ScimMember>? Members { get; init; }
}

/// <summary>RFC 7644 §3.5.2 PatchOp request body.</summary>
public sealed record ScimPatchRequest
{
    [JsonPropertyName("schemas")]
    public IReadOnlyList<string>? Schemas { get; init; }

    [JsonPropertyName("Operations")]
    public IReadOnlyList<ScimPatchOperation>? Operations { get; init; }
}

/// <summary>
///     One PATCH operation. <c>op</c> is add/remove/replace (case-insensitive);
///     <c>value</c> is preserved as a raw JSON node because PATCH values are
///     attribute-dependent.
/// </summary>
public sealed record ScimPatchOperation
{
    [JsonPropertyName("op")]
    public string? Op { get; init; }

    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public JsonNode? Value { get; init; }

    public string NormalizeOp()
    {
        var normalized = (Op ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "add" => "add",
            "remove" => "remove",
            "replace" => "replace",
            _ => throw ScimException.InvalidValue($"The patch operation '{Op}' is not one of add, remove, replace.")
        };
    }
}

/// <summary>RFC 7644 §3.7 BulkRequest body.</summary>
public sealed record ScimBulkRequest
{
    [JsonPropertyName("schemas")]
    public IReadOnlyList<string>? Schemas { get; init; }

    [JsonPropertyName("failOnErrors")]
    public int? FailOnErrors { get; init; }

    [JsonPropertyName("Operations")]
    public IReadOnlyList<ScimBulkOperation>? Operations { get; init; }
}

/// <summary>One bulk operation.</summary>
public sealed record ScimBulkOperation
{
    [JsonPropertyName("method")]
    public string? Method { get; init; }

    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("bulkId")]
    public string? BulkId { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("data")]
    public JsonNode? Data { get; init; }
}

/// <summary>RFC 7644 §3.7 BulkResponse body.</summary>
public sealed record ScimBulkResponse
{
    [JsonPropertyName("schemas")]
    public IReadOnlyList<string> Schemas { get; init; } = [ScimConstants.BulkResponseSchema];

    [JsonPropertyName("Operations")]
    public IReadOnlyList<ScimBulkResponseOperation> Operations { get; init; } = Array.Empty<ScimBulkResponseOperation>();
}

/// <summary>One bulk response operation.</summary>
public sealed record ScimBulkResponseOperation
{
    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("bulkId")]
    public string? BulkId { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("location")]
    public string? Location { get; init; }

    [JsonPropertyName("status")]
    public ScimBulkStatus Status { get; init; } = new();
}

/// <summary>Bulk operation status envelope.</summary>
public sealed record ScimBulkStatus
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("scimType")]
    public string? ScimType { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }
}
