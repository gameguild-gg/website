using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Materializes verified policy bundles into <see cref="PolicyDefinitionEntity"/> rows so the
///     dynamic authorization policy provider (<c>DbAuthorizationPolicyProvider</c> →
///     <c>IPolicyDefinitionStore</c>) serves policies that shipped inside signed bundles.
/// </summary>
public interface IPolicyBundlePolicyMaterializer
{
    /// <summary>
    ///     Upserts the policy definitions carried by a deployed bundle and bumps their policy
    ///     versions so version-aware policy caches invalidate.
    /// </summary>
    /// <param name="bundle">The deployed bundle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of policy definitions written.</returns>
    Task<int> MaterializeAsync(PolicyBundle bundle, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes the policy definitions previously written for a bundle (rollback/deprecation).
    /// </summary>
    /// <param name="bundle">The rolled-back bundle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of policy definitions removed.</returns>
    Task<int> DematerializeAsync(PolicyBundle bundle, CancellationToken cancellationToken = default);
}

/// <summary>
///     Default materializer: a bundle's <c>PolicyData</c> is a JSON array of policy definition
///     DTOs, each materialized as a marker-annotated <see cref="PolicyDefinitionEntity"/>.
/// </summary>
public sealed class PolicyBundlePolicyMaterializer(
    IPolicyDefinitionRepository policyRepository,
    ILogger<PolicyBundlePolicyMaterializer> logger
) : IPolicyBundlePolicyMaterializer
{
    /// <summary>Description marker prefix tying a policy definition to its source bundle.</summary>
    public const string SourceMarkerPrefix = "policy-bundle:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IPolicyDefinitionRepository _policyRepository =
        policyRepository ?? throw new ArgumentNullException(nameof(policyRepository));

    private readonly ILogger<PolicyBundlePolicyMaterializer> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<int> MaterializeAsync(PolicyBundle bundle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var policies = ParsePolicies(bundle);
        var tenantGuid = bundle.TenantId?.Value;
        var written = 0;

        foreach (var policy in policies)
        {
            var existing = await _policyRepository
                .GetByNameAsync(policy.PolicyName, tenantGuid, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                var entity = new PolicyDefinitionEntity
                {
                    PolicyName = policy.PolicyName,
                    TenantId = tenantGuid,
                    RequireAuthentication = policy.RequireAuthentication,
                    RequiredPermissionsJson = SerializeList(policy.RequiredPermissions),
                    RequiredRolesJson = SerializeList(policy.RequiredRoles),
                    RequireAccessControlListAccess = policy.RequireAccessControlListAccess,
                    ResourceType = policy.ResourceType,
                    MinimumAccessLevel = policy.MinimumAccessLevel,
                    IsTenantScoped = tenantGuid is not null,
                    PolicyVersion = Math.Max(1, policy.PolicyVersion),
                    IsActive = true,
                    Description = BuildMarker(bundle),
                    UseRuleBasedEvaluation = policy.UseRuleBasedEvaluation,
                    RulesJson = policy.RulesJson
                };
                await _policyRepository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
                written++;
            }
            else
            {
                existing.RequireAuthentication = policy.RequireAuthentication;
                existing.RequiredPermissionsJson = SerializeList(policy.RequiredPermissions);
                existing.RequiredRolesJson = SerializeList(policy.RequiredRoles);
                existing.RequireAccessControlListAccess = policy.RequireAccessControlListAccess;
                existing.ResourceType = policy.ResourceType;
                existing.MinimumAccessLevel = policy.MinimumAccessLevel;
                existing.IsActive = true;
                existing.Description = BuildMarker(bundle);
                existing.UseRuleBasedEvaluation = policy.UseRuleBasedEvaluation;
                existing.RulesJson = policy.RulesJson;
                // Bump the version so version-aware policy caches invalidate.
                existing.PolicyVersion = Math.Max(existing.PolicyVersion, policy.PolicyVersion) + 1;
                await _policyRepository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
                written++;
            }
        }

        await _policyRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Materialized {PolicyCount} policy definitions from bundle {BundleId} ('{BundleName}')",
            written, bundle.Id, bundle.Name);

        return written;
    }

    /// <inheritdoc />
    public async Task<int> DematerializeAsync(PolicyBundle bundle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var marker = BuildMarker(bundle);
        var candidates = bundle.TenantId is { } tenantId
            ? await _policyRepository
                .GetByTenantAsync(tenantId.Value, includeGlobal: false, cancellationToken)
                .ConfigureAwait(false)
            : await _policyRepository.GetGlobalPoliciesAsync(cancellationToken).ConfigureAwait(false);

        var removed = 0;
        foreach (var entity in candidates.Where(p =>
                     p.Description is not null
                     && p.Description.StartsWith(marker, StringComparison.Ordinal)))
        {
            await _policyRepository.DeleteAsync(entity, cancellationToken).ConfigureAwait(false);
            removed++;
        }

        await _policyRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Removed {PolicyCount} policy definitions previously materialized from bundle {BundleId}",
            removed, bundle.Id);

        return removed;
    }

    private static List<BundlePolicyDefinitionDto> ParsePolicies(PolicyBundle bundle)
    {
        try
        {
            var policies = JsonSerializer.Deserialize<List<BundlePolicyDefinitionDto>>(
                bundle.PolicyData,
                JsonOptions);
            if (policies is null || policies.Count == 0)
            {
                throw new PolicyBundleSignatureException(
                $"Bundle '{bundle.Name}' carries no policy definitions in its PolicyData.");
            }

            foreach (var policy in policies)
            {
                if (string.IsNullOrWhiteSpace(policy.PolicyName))
                {
                    throw new PolicyBundleSignatureException(
                    $"Bundle '{bundle.Name}' contains a policy definition without a policyName.");
                }
            }

            return policies;
        }
        catch (JsonException ex)
        {
            throw new PolicyBundleSignatureException(
                $"Bundle '{bundle.Name}' PolicyData is not a valid policy definition array: {ex.Message}",
                ex);
        }
    }

    private static string SerializeList(List<string>? values) =>
        JsonSerializer.Serialize(values ?? []);

    private static string BuildMarker(PolicyBundle bundle) =>
        $"{SourceMarkerPrefix}{bundle.Id:N}";

    /// <summary>Policy definition DTO carried inside a bundle's PolicyData.</summary>
    public sealed record BundlePolicyDefinitionDto
    {
        /// <summary>Gets the unique policy name.</summary>
        public string PolicyName { get; init; } = string.Empty;

        /// <summary>Gets a value indicating whether authentication is required.</summary>
        public bool RequireAuthentication { get; init; } = true;

        /// <summary>Gets the required permission keys.</summary>
        public List<string>? RequiredPermissions { get; init; }

        /// <summary>Gets the required role names.</summary>
        public List<string>? RequiredRoles { get; init; }

        /// <summary>Gets a value indicating whether ACL (DAC) access is required.</summary>
        public bool RequireAccessControlListAccess { get; init; }

        /// <summary>Gets the DAC resource type.</summary>
        public string? ResourceType { get; init; }

        /// <summary>Gets the minimum access level.</summary>
        public string? MinimumAccessLevel { get; init; }

        /// <summary>Gets the policy version proposed by the bundle.</summary>
        public long PolicyVersion { get; init; } = 1;

        /// <summary>Gets a value indicating whether rule-based evaluation is used.</summary>
        public bool UseRuleBasedEvaluation { get; init; }

        /// <summary>Gets the rule definitions as raw JSON.</summary>
        public string? RulesJson { get; init; }
    }
}
