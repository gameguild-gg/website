using System.Text.Json.Serialization;

namespace GameGuild.Identity.Provisioning.Scim;

/// <summary>
///     RFC 7644 §3.4.2.3 ListResponse envelope. <c>startIndex</c> is 1-based.
/// </summary>
public sealed record ScimListResponse<TResource>
{
    public IReadOnlyList<string> Schemas { get; init; } = [ScimConstants.ListResponseSchema];

    public int TotalResults { get; init; }

    public int StartIndex { get; init; } = 1;

    public int ItemsPerPage { get; init; }

    /// <summary>
    ///     RFC 7644 §3.4.2.3 spells this member <c>Resources</c> with a capital R while
    ///     every other member is camelCase, so the name is pinned explicitly instead of
    ///     following the host's camelCase policy (which would emit "resources").
    /// </summary>
    [JsonPropertyName("Resources")]
    public IReadOnlyList<TResource> Resources { get; init; } = Array.Empty<TResource>();
}

/// <summary>
///     RFC 7644 §3.4.2.4 pagination parameters parsed from the query string.
/// </summary>
public sealed record ScimPageRequest(int StartIndex, int Count)
{
    public static ScimPageRequest Parse(string? startIndex, string? count, ScimProvisioningOptions options)
    {
        var parsedStart = 1;
        if (!string.IsNullOrWhiteSpace(startIndex))
        {
            if (!int.TryParse(startIndex, out parsedStart) || parsedStart < 1)
            {
                throw ScimException.BadPayload("startIndex must be an integer greater than or equal to 1.");
            }
        }

        var parsedCount = options.DefaultPageSize;
        if (!string.IsNullOrWhiteSpace(count))
        {
            if (!int.TryParse(count, out parsedCount) || parsedCount < 0)
            {
                throw ScimException.BadPayload("count must be a non-negative integer.");
            }
        }

        // count=0 asks for only the totals; otherwise clamp to the configured maximum.
        if (parsedCount > options.MaxPageSize)
        {
            parsedCount = options.MaxPageSize;
        }

        return new ScimPageRequest(parsedStart, parsedCount);
    }
}
