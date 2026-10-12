using System.Text.Json.Serialization;

namespace GameGuild.Identity.Provisioning.Scim;

/// <summary>RFC 7643 §8.1 name sub-attribute as represented by this provider.</summary>
public sealed record ScimName
{
    [JsonPropertyName("formatted")]
    public string? Formatted { get; init; }

    [JsonPropertyName("givenName")]
    public string? GivenName { get; init; }

    [JsonPropertyName("familyName")]
    public string? FamilyName { get; init; }
}

/// <summary>RFC 7643 §8.2 email sub-attribute. The provider stores one primary e-mail.</summary>
public sealed record ScimEmail
{
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("display")]
    public string? Display { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("primary")]
    public bool? Primary { get; init; }
}

/// <summary>RFC 7643 §8.3 phone number sub-attribute. The provider stores one number.</summary>
public sealed record ScimPhoneNumber
{
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }
}

/// <summary>RFC 7643 §3.1 meta attribute.</summary>
public sealed record ScimMeta
{
    [JsonPropertyName("resourceType")]
    public string ResourceType { get; init; } = string.Empty;

    [JsonPropertyName("created")]
    public DateTime? Created { get; init; }

    [JsonPropertyName("lastModified")]
    public DateTime? LastModified { get; init; }

    [JsonPropertyName("location")]
    public string Location { get; init; } = string.Empty;
}

/// <summary>RFC 7643 §8.4 group member reference.</summary>
public sealed record ScimMember
{
    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;

    [JsonPropertyName("display")]
    public string? Display { get; init; }

    [JsonPropertyName("$ref")]
    public string? Ref { get; init; }
}

/// <summary>
///     SCIM User resource rendered by this provider. Only implemented attributes are
///     exposed; the full core schema advertisement lives in
///     <see cref="ScimDiscoveryDocuments"/>.
/// </summary>
public sealed record ScimUserResource
{
    [JsonPropertyName("schemas")]
    public IReadOnlyList<string> Schemas { get; init; } = [ScimConstants.UserSchemaUrn];

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; init; }

    [JsonPropertyName("userName")]
    public string UserName { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public ScimName? Name { get; init; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("emails")]
    public IReadOnlyList<ScimEmail> Emails { get; init; } = Array.Empty<ScimEmail>();

    [JsonPropertyName("phoneNumbers")]
    public IReadOnlyList<ScimPhoneNumber> PhoneNumbers { get; init; } = Array.Empty<ScimPhoneNumber>();

    [JsonPropertyName("active")]
    public bool Active { get; init; }

    [JsonPropertyName("meta")]
    public ScimMeta Meta { get; init; } = new();
}

/// <summary>SCIM Group resource rendered by this provider (backed by a tenant role).</summary>
public sealed record ScimGroupResource
{
    [JsonPropertyName("schemas")]
    public IReadOnlyList<string> Schemas { get; init; } = [ScimConstants.GroupSchemaUrn];

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; init; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("members")]
    public IReadOnlyList<ScimMember> Members { get; init; } = Array.Empty<ScimMember>();

    [JsonPropertyName("meta")]
    public ScimMeta Meta { get; init; } = new();
}
