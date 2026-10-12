using GameGuild.Identity.Users;

namespace GameGuild.Identity.Provisioning.Scim;

/// <summary>
///     Entity ⇄ SCIM resource mapping for Users. The platform stores a single full
/// name; given/family name parts round-trip through a first-token split.
/// </summary>
public static class ScimUserMapper
{
    public static ScimUserResource ToResource(User user, string? externalId)
    {
        var displayName = user.Name;
        var givenName = FirstToken(displayName);
        var familyName = givenName is null ? displayName : displayName[givenName.Length..].Trim();

        return new ScimUserResource
        {
            Id = user.Id.ToString(),
            ExternalId = externalId,
            UserName = user.Username ?? user.Email,
            Name = new ScimName
            {
                Formatted = displayName,
                GivenName = givenName,
                FamilyName = string.IsNullOrWhiteSpace(familyName) ? null : familyName
            },
            DisplayName = displayName,
            Emails = string.IsNullOrWhiteSpace(user.Email)
                ? Array.Empty<ScimEmail>()
                : [new ScimEmail { Value = user.Email, Type = "work", Primary = true }],
            PhoneNumbers = string.IsNullOrWhiteSpace(user.PhoneNumber)
                ? Array.Empty<ScimPhoneNumber>()
                : [new ScimPhoneNumber { Value = user.PhoneNumber, Type = "work" }],
            Active = user.IsActive && !user.IsSuspended && !user.IsDeleted,
            Meta = new ScimMeta
            {
                ResourceType = "User",
                Created = user.CreatedAt,
                LastModified = user.UpdatedAt,
                Location = $"{ScimConstants.UsersPath}/{user.Id}"
            }
        };
    }

    public static ScimUserResource ToResource(ScimUserView view)
    {
        var givenName = FirstToken(view.DisplayName);
        var familyName = givenName is null ? view.DisplayName : view.DisplayName[givenName.Length..].Trim();

        return new ScimUserResource
        {
            Id = view.UserId.ToString(),
            ExternalId = view.ExternalId,
            UserName = view.UserName ?? view.Email,
            Name = new ScimName
            {
                Formatted = view.DisplayName,
                GivenName = givenName,
                FamilyName = string.IsNullOrWhiteSpace(familyName) ? null : familyName
            },
            DisplayName = view.DisplayName,
            Emails = string.IsNullOrWhiteSpace(view.Email)
                ? Array.Empty<ScimEmail>()
                : [new ScimEmail { Value = view.Email, Type = "work", Primary = true }],
            PhoneNumbers = string.IsNullOrWhiteSpace(view.PhoneNumber)
                ? Array.Empty<ScimPhoneNumber>()
                : [new ScimPhoneNumber { Value = view.PhoneNumber, Type = "work" }],
            Active = view.Active,
            Meta = new ScimMeta
            {
                ResourceType = "User",
                Created = view.CreatedAt,
                LastModified = view.UpdatedAt,
                Location = $"{ScimConstants.UsersPath}/{view.UserId}"
            }
        };
    }

    private static string? FirstToken(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var separator = displayName.IndexOf(' ');
        return separator < 0 ? displayName : displayName[..separator];
    }
}

/// <summary>Entity ⇄ SCIM resource mapping for Groups (tenant roles).</summary>
public static class ScimGroupMapper
{
    public static ScimGroupResource ToResource(ScimGroupView view, IReadOnlyList<ScimMember> members)
    {
        return new ScimGroupResource
        {
            Id = view.RoleId.ToString(),
            ExternalId = view.ExternalId,
            DisplayName = view.DisplayName,
            Members = members,
            Meta = new ScimMeta
            {
                ResourceType = "Group",
                Created = view.CreatedAt,
                LastModified = view.UpdatedAt,
                Location = $"{ScimConstants.GroupsPath}/{view.RoleId}"
            }
        };
    }
}
