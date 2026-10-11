using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.CQRS;
using GameGuild.Identity.Tenants;

namespace GameGuild.Identity.Authentication;

/// <summary>
/// Uses the same eligible membership and reserved-role rules as authenticated session issuance.
/// Caller claims cannot choose the subject's roles or create tenant access.
/// </summary>
public sealed class MfaSubjectRequirementPolicy(ISender sender, MfaOptions mfaOptions)
    : IMfaSubjectRequirementPolicy
{
    public Task<MfaRequirementDecision> EvaluateAsync(Guid subjectId, Guid? requestedTenantId) =>
        EvaluateAsync(subjectId, requestedTenantId, CancellationToken.None);

    public async Task<MfaRequirementDecision> EvaluateAsync(
        Guid subjectId,
        Guid? requestedTenantId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (subjectId == Guid.Empty)
        {
            throw new ArgumentException("A verified subject is required.", nameof(subjectId));
        }
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(mfaOptions);
        var validation = mfaOptions.Validate();
        if (!validation.IsValid)
        {
            throw new InvalidOperationException("MFA policy configuration is invalid.");
        }
        var enabled = mfaOptions.Enabled;
        var requireByDefault = mfaOptions.RequireMfaByDefault;
        if (!enabled && requireByDefault)
        {
            throw new InvalidOperationException("MFA policy configuration is invalid.");
        }

        var memberships = await sender.Send(new GetUserMembershipsQuery(subjectId, true), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Subject memberships are unavailable.");
        cancellationToken.ThrowIfCancellationRequested();
        var eligible = memberships.Memberships
            .Where(membership => membership.TenantId != Guid.Empty && membership.TenantIsActive &&
                membership.IsActive && membership.LeftAt is null &&
                (string.IsNullOrWhiteSpace(membership.InviteStatus) ||
                 string.Equals(membership.InviteStatus, "Accepted", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var context = TenantAccessContextResolver.Resolve(
            new GetUserMembershipsResponse { Memberships = eligible, TotalCount = eligible.Count },
            requestedTenantId);
        if (context.TenantId is not { } tenantId)
        {
            throw new AuthenticationRequiredException("No authorized tenant is available for authentication.");
        }

        var roles = context.Roles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(role => role.ToUpperInvariant())
            .ToArray();
        var requiresMfa = enabled && (requireByDefault || roles.Any(IsElevatedRole));
        var fingerprintSource = JsonSerializer.Serialize(new
        {
            Version = "mfa-subject-policy-v1", SubjectId = subjectId, TenantId = tenantId,
            Enabled = enabled, RequireByDefault = requireByDefault, Roles = roles
        });
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource)))
            .ToLowerInvariant();
        return new MfaRequirementDecision(subjectId, tenantId, requiresMfa, Array.AsReadOnly(roles), fingerprint);
    }

    private static bool IsElevatedRole(string role) =>
        role is "SYSTEMADMIN" or "ADMIN" or "TENANTADMIN" or "OWNER";
}
