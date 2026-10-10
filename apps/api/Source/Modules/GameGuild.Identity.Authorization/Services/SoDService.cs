using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Service for managing Separation of Duties (SoD) rules
/// </summary>
public class SoDService(
    ISoDRuleRepository ruleRepository,
    ISoDViolationRepository violationRepository,
    ILogger<SoDService> logger,
    IPermissionQueryService? permissionQueryService = null,
    ITenantPermissionRepository? tenantPermissionRepository = null
) : ISoDService
{
    private readonly ILogger<SoDService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly ISoDRuleRepository _ruleRepository =
        ruleRepository ?? throw new ArgumentNullException(nameof(ruleRepository));

    private readonly ISoDViolationRepository _violationRepository =
        violationRepository ?? throw new ArgumentNullException(nameof(violationRepository));

    private readonly IPermissionQueryService? _permissionQueryService = permissionQueryService;

    private readonly ITenantPermissionRepository? _tenantPermissionRepository = tenantPermissionRepository;

    public async Task<SoDRule> CreateRuleAsync(
        SoDRule rule,
        CancellationToken cancellationToken = default
    ) => await _ruleRepository.CreateAsync(rule, cancellationToken);

    public async Task<SoDRule> UpdateRuleAsync(
        SoDRule rule,
        CancellationToken cancellationToken = default
    ) => await _ruleRepository.UpdateAsync(rule, cancellationToken);

    public async Task<bool> DeleteRuleAsync(
        Guid ruleId,
        CancellationToken cancellationToken = default
    )
    {
        await _ruleRepository.DeleteAsync(ruleId, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<SoDRule?> GetRuleByIdAsync(
        Guid ruleId,
        CancellationToken cancellationToken = default
    ) => await _ruleRepository.GetByIdAsync(ruleId, cancellationToken);

    public async Task<List<SoDRule>> GetRulesForTenantAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    ) => await _ruleRepository.GetByTenantAsync(tenantId, cancellationToken);

    public async Task<List<SoDRule>> GetActiveRulesAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    ) => await _ruleRepository.GetActiveRulesAsync(tenantId, cancellationToken);

    public async Task<List<SoDViolation>> DetectViolationsAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var rules = await _ruleRepository.GetActiveRulesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var violations = new List<SoDViolation>();

        foreach (var rule in rules)
        {
            var hasConflict = await HasRuleViolationAsync(rule, userId, tenantId, cancellationToken).ConfigureAwait(false);

            if (hasConflict)
            {
                var existing = await _violationRepository.GetByUserAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
                var activeViolation = existing.FirstOrDefault(v => v.RuleId == rule.Id && v.Status == SoDViolationStatus.Active);
                if (activeViolation != null)
                {
                    violations.Add(activeViolation);
                    continue;
                }

                var violation = new SoDViolation
                {
                    RuleId = rule.Id,
                    UserId = userId,
                    TenantId = tenantId,
                    ConflictingItems = rule.ConflictingPermissions,
                    Status = SoDViolationStatus.Active,
                    ViolationDetails = $"{rule.Name}: {rule.Description}"
                };
                violations.Add(violation);
                rule.RecordViolation();
                await _ruleRepository.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);
                await _violationRepository.CreateAsync(violation, cancellationToken).ConfigureAwait(false);
            }
        }

        return violations;
    }

    public async Task<List<SoDViolation>> GetViolationsForUserAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    ) => await _violationRepository.GetByUserAsync(userId, tenantId, cancellationToken);

    public async Task<List<SoDViolation>> GetActiveViolationsAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    ) => await _violationRepository.GetActiveViolationsAsync(tenantId, cancellationToken);

    public async Task<SoDViolation> ResolveViolationAsync(
        Guid violationId,
        Guid resolvedBy,
        SoDResolutionAction action,
        string notes,
        CancellationToken cancellationToken = default
    )
    {
        var violation = await _violationRepository.GetByIdAsync(violationId, cancellationToken).ConfigureAwait(false);

        if (violation == null)
        {
            throw new InvalidOperationException($"Violation {violationId} not found");
        }

        violation.Resolve(resolvedBy, action, notes);
        await _violationRepository.UpdateAsync(violation, cancellationToken).ConfigureAwait(false);

        return violation;
    }

    public async Task<SoDViolation> GrantExceptionAsync(
        Guid violationId,
        Guid approvedBy,
        string justification,
        CancellationToken cancellationToken = default
    )
    {
        var violation = await _violationRepository.GetByIdAsync(violationId, cancellationToken).ConfigureAwait(false);

        if (violation == null)
        {
            throw new InvalidOperationException($"Violation {violationId} not found");
        }

        violation.MarkAsException(approvedBy, justification);
        await _violationRepository.UpdateAsync(violation, cancellationToken).ConfigureAwait(false);

        return violation;
    }

    public async Task<SoDViolation> AcknowledgeViolationAsync(
        Guid violationId,
        CancellationToken cancellationToken = default
    )
    {
        var violation = await _violationRepository.GetByIdAsync(violationId, cancellationToken).ConfigureAwait(false);

        if (violation == null)
        {
            throw new InvalidOperationException($"Violation {violationId} not found");
        }

        violation.Acknowledge();
        await _violationRepository.UpdateAsync(violation, cancellationToken).ConfigureAwait(false);

        return violation;
    }

    public async Task<int> ScanForViolationsAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Scanning for SoD violations in tenant {TenantId}", tenantId);

        if (_tenantPermissionRepository == null)
        {
            _logger.LogWarning("Cannot scan SoD violations because ITenantPermissionRepository is not registered");
            return 0;
        }

        var candidateUsers = await GetCandidateUserIdsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var violationCount = 0;

        foreach (var userId in candidateUsers)
        {
            var detected = await DetectViolationsAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
            violationCount += detected.Count;
        }

        return violationCount;
    }

    private static Task<bool> CheckRuleViolationAsync(
        SoDRule rule,
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken
    )
    {
        _ = rule;
        _ = userId;
        _ = tenantId;
        _ = cancellationToken;
        return Task.FromResult(false);
    }

    private async Task<bool> HasRuleViolationAsync(
        SoDRule rule,
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken
    )
    {
        if (_permissionQueryService == null)
        {
            _logger.LogWarning("Cannot evaluate SoD rule {RuleId} because IPermissionQueryService is not registered", rule.Id);
            return false;
        }

        var effectivePermissions = await _permissionQueryService
            .GetEffectivePermissionsAsync(userId, tenantId, cancellationToken)
            .ConfigureAwait(false);

        return HasPermissionConflict(rule, effectivePermissions);
    }

    private async Task<IReadOnlyCollection<Guid>> GetCandidateUserIdsAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        if (tenantId.HasValue)
        {
            return (await _tenantPermissionRepository!
                    .GetByTenantAsync(tenantId.Value, cancellationToken)
                    .ConfigureAwait(false))
                .Where(permission => permission.UserId.HasValue && permission.IsActive && !permission.IsExpired())
                .Select(permission => permission.UserId!.Value)
                .Distinct()
                .ToArray();
        }

        var tenantIds = (await _ruleRepository.GetActiveRulesAsync(null, cancellationToken).ConfigureAwait(false))
            .Where(rule => rule.TenantId != null)
            .Select(rule => rule.TenantId!.Value.Value)
            .Distinct()
            .ToArray();

        var users = new HashSet<Guid>();
        foreach (var ruleTenantId in tenantIds)
        {
            var permissions = await _tenantPermissionRepository!
                .GetByTenantAsync(ruleTenantId, cancellationToken)
                .ConfigureAwait(false);

            foreach (var permission in permissions.Where(p => p.UserId.HasValue && p.IsActive && !p.IsExpired()))
            {
                users.Add(permission.UserId!.Value);
            }
        }

        return users.ToArray();
    }

    private static bool HasPermissionConflict(SoDRule rule, IEnumerable<string> effectivePermissions)
    {
        var conflictingPermissions = ParseConflictingPermissions(rule.ConflictingPermissions);
        if (conflictingPermissions.Length < 2)
        {
            return false;
        }

        var granted = new HashSet<string>(effectivePermissions, StringComparer.OrdinalIgnoreCase);
        return conflictingPermissions.Count(granted.Contains) >= 2;
    }

    private static string[] ParseConflictingPermissions(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(raw) is { Length: > 0 } parsed
                ? parsed.Where(permission => !string.IsNullOrWhiteSpace(permission)).Select(permission => permission.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                : [];
        }
        catch (JsonException)
        {
            return raw.Split([',', ';', '\n', '\r', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
