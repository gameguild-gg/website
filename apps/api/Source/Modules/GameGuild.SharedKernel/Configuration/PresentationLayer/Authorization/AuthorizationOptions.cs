namespace GameGuild.Configuration.PresentationLayer.Authorization;

public sealed class AuthorizationOptions : BaseOptions
{
    /// <summary>
    ///     The configuration section name.
    /// </summary>
    public const string SectionName = "Authorization";

    public string DefaultPolicy { get; set; } = "Default";

    /// <summary>
    ///     Name of a registered static policy applied to endpoints without authorization metadata.
    ///     When unset, ASP.NET Core keeps its default behavior for such endpoints.
    /// </summary>
    public string? FallbackPolicyName { get; set; }

    public bool RequireAuthenticatedUser { get; set; } = true;

    /// <summary>
    ///     The system account ID that receives all permissions (wildcard).
    ///     This should be a well-known GUID configured in environment settings.
    /// </summary>
    /// <remarks>
    ///     Default is a well-known system account GUID. Override in production via configuration.
    /// </remarks>
    public Guid SystemAccountId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    ///     Static ASP.NET Core policies configured by the host. Database-backed policies
    ///     remain the source of truth for names registered in the authorization module.
    /// </summary>
    public Dictionary<string, ConfiguredAuthorizationPolicyOptions> Policies { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Maps an elevated role to the roles it inherits. For example, an Administrator
    ///     entry containing Editor means an Administrator satisfies policies requiring Editor.
    /// </summary>
    public Dictionary<string, List<string>> RoleHierarchy { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Explicit claim value mappings applied after authentication and before authorization.
    ///     Only values listed in each mapping are copied to the target claim type.
    /// </summary>
    public List<AuthorizationClaimTransformationOptions> ClaimTransformations { get; set; } = [];

    public override void Validate()
    {
        base.Validate();

        if (string.IsNullOrWhiteSpace(DefaultPolicy))
        {
            throw new InvalidOperationException("Default policy cannot be null or empty.");
        }

        if (FallbackPolicyName is not null && string.IsNullOrWhiteSpace(FallbackPolicyName))
        {
            throw new InvalidOperationException("Fallback policy name cannot be empty when configured.");
        }
        
        if (SystemAccountId == Guid.Empty)
        {
            throw new InvalidOperationException("SystemAccountId cannot be empty GUID.");
        }

        ValidatePolicies();
        ValidateRoleHierarchy();
        ValidateClaimTransformations();
    }

    public static AuthorizationOptions CreateDefault() { return new AuthorizationOptions(); }

    private void ValidatePolicies()
    {
        if (Policies is null)
        {
            throw new InvalidOperationException("Authorization policies cannot be null.");
        }

        if (Policies.Keys.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Authorization policy names cannot be null or empty.");
        }

        if (Policies.Keys.GroupBy(name => name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("Authorization policy names must be unique, ignoring case.");
        }

        foreach (var (name, policy) in Policies)
        {
            if (policy is null)
            {
                throw new InvalidOperationException($"Authorization policy '{name}' cannot be null.");
            }

            if (policy.Roles is null || policy.Claims is null || policy.AuthenticationSchemes is null)
            {
                throw new InvalidOperationException($"Authorization policy '{name}' contains a null collection.");
            }

            if (policy.Roles.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException($"Authorization policy '{name}' contains an empty role.");
            }

            if (policy.AuthenticationSchemes.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException($"Authorization policy '{name}' contains an empty authentication scheme.");
            }

            if (policy.Claims.Any(claim => claim is null || string.IsNullOrWhiteSpace(claim.Type) || claim.AllowedValues is null))
            {
                throw new InvalidOperationException($"Authorization policy '{name}' contains an invalid claim requirement.");
            }

            if (!policy.RequireAuthenticatedUser && policy.Roles.Count == 0 && policy.Claims.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Authorization policy '{name}' must require authentication, a role, or a claim.");
            }
        }
    }

    private void ValidateRoleHierarchy()
    {
        if (RoleHierarchy is null)
        {
            throw new InvalidOperationException("Authorization role hierarchy cannot be null.");
        }

        if (RoleHierarchy.Keys.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Authorization role names cannot be null or empty.");
        }

        if (RoleHierarchy.Keys.GroupBy(role => role, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("Authorization role names must be unique, ignoring case.");
        }

        foreach (var (role, inheritedRoles) in RoleHierarchy)
        {
            if (inheritedRoles is null || inheritedRoles.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException($"Role '{role}' contains an invalid inherited role.");
            }

            if (inheritedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Role '{role}' cannot inherit itself.");
            }
        }

        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in RoleHierarchy.Keys)
        {
            Visit(role);
        }

        void Visit(string role)
        {
            if (visited.Contains(role))
            {
                return;
            }

            if (!visiting.Add(role))
            {
                throw new InvalidOperationException($"Authorization role hierarchy contains a cycle at '{role}'.");
            }

            if (RoleHierarchy.TryGetValue(role, out var inheritedRoles))
            {
                foreach (var inheritedRole in inheritedRoles)
                {
                    if (RoleHierarchy.ContainsKey(inheritedRole))
                    {
                        Visit(inheritedRole);
                    }
                }
            }

            visiting.Remove(role);
            visited.Add(role);
        }
    }

    private void ValidateClaimTransformations()
    {
        if (ClaimTransformations is null)
        {
            throw new InvalidOperationException("Authorization claim transformations cannot be null.");
        }

        foreach (var transformation in ClaimTransformations)
        {
            if (transformation is null ||
                string.IsNullOrWhiteSpace(transformation.SourceClaimType) ||
                string.IsNullOrWhiteSpace(transformation.TargetClaimType) ||
                transformation.ValueMappings is null ||
                transformation.ValueMappings.Count == 0 ||
                transformation.ValueMappings.Any(mapping =>
                    string.IsNullOrWhiteSpace(mapping.Key) || string.IsNullOrWhiteSpace(mapping.Value)))
            {
                throw new InvalidOperationException(
                    "Each authorization claim transformation must define source and target claim types and at least one non-empty value mapping.");
            }
        }
    }
}
