namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Scope values a SCIM provisioning token can carry. The wildcard <c>*</c>
/// satisfies any requirement, mirroring API-key scopes.
/// </summary>
public static class ScimScopes
{
    /// <summary>Grants the read surface (GET on every /scim/v2 endpoint).</summary>
    public const string Read = "scim:read";

    /// <summary>Grants the mutation surface (POST/PUT/PATCH/DELETE).</summary>
    public const string Write = "scim:write";

    public static readonly string[] All = [Read, Write];

    public static void RequireRead(ScimProvisioningActor actor)
    {
        if (!actor.HasScope(Read))
        {
            throw new ScimException(403, null, $"The provisioning token lacks the '{Read}' scope.");
        }
    }

    public static void RequireWrite(ScimProvisioningActor actor)
    {
        if (!actor.HasScope(Write))
        {
            throw new ScimException(403, null, $"The provisioning token lacks the '{Write}' scope.");
        }
    }
}
