using System.Text.Json;
using Microsoft.Extensions.Logging;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Compliance.Audit;

/// <summary>Thrown when a caller lacks the administrator context the pipeline surface requires.</summary>
public sealed class PiiRedactionUnauthorizedException(string message) : UnauthorizedAccessException(message);

/// <summary>Thrown when a rule mutation loses the optimistic-concurrency race.</summary>
public sealed class PiiRedactionConcurrencyException() : Exception("The PII redaction rule was modified by another actor; reload and retry.");

/// <summary>Thrown when a rule payload fails shape validation.</summary>
public sealed class PiiRedactionValidationException(IReadOnlyDictionary<string, string[]> errors) : Exception("The PII redaction rule payload is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

/// <summary>Administration surface for the redaction pipeline: guarded, versioned, audited rule CRUD plus vault access.</summary>
public interface IPiiRedactionAdminService
{
    Task<PiiRedactionPipelineStatusResponse> GetStatusAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PiiRedactionRuleResponse>> ListRulesAsync(CancellationToken cancellationToken);

    Task<PiiRedactionRuleResponse> GetRuleAsync(Guid ruleId, CancellationToken cancellationToken);

    Task<PiiRedactionRuleResponse> CreateRuleAsync(UpsertPiiRedactionRuleRequest request, CancellationToken cancellationToken);

    Task<PiiRedactionRuleResponse> UpdateRuleAsync(Guid ruleId, int expectedRevision, UpsertPiiRedactionRuleRequest request, CancellationToken cancellationToken);

    Task DeleteRuleAsync(Guid ruleId, int expectedRevision, CancellationToken cancellationToken);

    Task<PiiRedactionTestRunResponse> TestAsync(PiiRedactionTestRunRequest request, CancellationToken cancellationToken);

    Task<PiiTokenResolutionResponse> ResolveTokenAsync(string token, string reason, CancellationToken cancellationToken);
}

public sealed record UpsertPiiRedactionRuleRequest
{
    public string RuleName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public PiiRulePatternType PatternType { get; init; }

    public string? RegexPattern { get; init; }

    public PiiPresetPattern? Preset { get; init; }

    public PiiRuleAction Action { get; init; }

    public PiiRuleScope Scope { get; init; }

    public IReadOnlyList<string> TargetFields { get; init; } = [];

    public int Priority { get; init; }

    public string? Replacement { get; init; }

    public bool IsEnabled { get; init; } = true;
}

public sealed record PiiRedactionRuleResponse(
    Guid Id,
    Guid? TenantId,
    string RuleName,
    string? Description,
    bool IsEnabled,
    int Priority,
    PiiRulePatternType PatternType,
    string? RegexPattern,
    PiiPresetPattern? Preset,
    PiiRuleAction Action,
    PiiRuleScope Scope,
    IReadOnlyList<string> TargetFields,
    string Replacement,
    int Revision,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record PiiRedactionPipelineStatusResponse(
    bool GlobalSnapshotLoaded,
    DateTime? GlobalSnapshotLoadedAt,
    int CachedScopeCount,
    int GlobalEnabledRuleCount,
    int TenantEnabledRuleCount,
    long VaultEntryCount,
    DateTime? LastRefreshAttemptAt,
    string? LastRefreshError);

public sealed record PiiRedactionTestRunFieldResult(string Field, string Original, string Redacted, bool Changed, Guid? AppliedRuleId, string? AppliedAction);

public sealed record PiiRedactionTestRunResponse(
    bool Changed,
    bool FailClosed,
    string? FailureReason,
    int RedactedValueCount,
    int TokenizedValueCount,
    IReadOnlyList<PiiRedactionTestRunFieldResult> Fields);

public sealed record PiiRedactionTestRunRequest
{
    public IReadOnlyDictionary<string, string> SampleFields { get; init; } = new Dictionary<string, string>();
}

public sealed record PiiTokenResolutionResponse(string Token, string Value, Guid? TenantId, DateTime FirstSeenAt, long UseCount);

/// <summary>Default administration implementation mirroring the retention-simulation guard pattern.</summary>
public sealed class PiiRedactionAdminService(
    IApplicationDbContext context,
    IPiiRedactionRuleProvider ruleProvider,
    IPiiTokenSource tokenizer,
    IEncryptionService encryptionService,
    IActorContextAccessor actors,
    IAuditService audit,
    ILogger<PiiRedactionAdminService> logger) : IPiiRedactionAdminService
{
    private const string ResourceType = "PiiRedactionRule";
    private const string PermissionName = "audit:pii-redaction-pipeline";

    public async Task<PiiRedactionPipelineStatusResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        var (tenant, _) = await RequireAdministratorAsync().ConfigureAwait(false);
        var cacheStatus = ruleProvider.GetCacheStatus();
        var tenantRules = await context.Set<PiiRedactionRule>()
            .AsNoTracking()
            .CountAsync(rule => rule.TenantId == tenant && rule.IsEnabled, cancellationToken)
            .ConfigureAwait(false);
        var vaultEntries = await context.Set<PiiTokenVaultEntry>()
            .AsNoTracking()
            .LongCountAsync(entry => entry.TenantId == tenant, cancellationToken)
            .ConfigureAwait(false);

        return new PiiRedactionPipelineStatusResponse(
            cacheStatus.GlobalSnapshotLoaded,
            cacheStatus.GlobalLoadedAt,
            cacheStatus.CachedTenantScopes,
            cacheStatus.GlobalRuleCount,
            tenantRules,
            vaultEntries,
            cacheStatus.LastRefreshAttemptAt,
            cacheStatus.LastRefreshError);
    }

    public async Task<IReadOnlyList<PiiRedactionRuleResponse>> ListRulesAsync(CancellationToken cancellationToken)
    {
        var (tenant, _) = await RequireAdministratorAsync().ConfigureAwait(false);
        var rules = await context.Set<PiiRedactionRule>()
            .AsNoTracking()
            .Where(rule => rule.TenantId == tenant || rule.TenantId == null)
            .OrderBy(rule => rule.Scope)
            .ThenBy(rule => rule.Priority)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rules.Select(Map).ToList();
    }

    public async Task<PiiRedactionRuleResponse> GetRuleAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        var (tenant, _) = await RequireAdministratorAsync().ConfigureAwait(false);
        var rule = await LoadRuleAsync(tenant, ruleId, cancellationToken).ConfigureAwait(false);
        return Map(rule);
    }

    public async Task<PiiRedactionRuleResponse> CreateRuleAsync(UpsertPiiRedactionRuleRequest request, CancellationToken cancellationToken)
    {
        var (tenant, user) = await RequireAdministratorAsync().ConfigureAwait(false);
        EnsureValid(request);

        var rule = PiiRedactionRule.Create(
            tenant,
            request.RuleName.Trim(),
            request.PatternType,
            request.RegexPattern?.Trim(),
            request.Preset,
            request.Action,
            request.Scope,
            request.TargetFields,
            request.Priority,
            request.Replacement,
            user);

        context.Set<PiiRedactionRule>().Add(rule);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
        ruleProvider.Invalidate(tenant);

        await AuditRuleMutationAsync(tenant, user, AuditActionTypes.PiiRedactionRuleCreated, rule).ConfigureAwait(false);
        logger.LogInformation("Created PII redaction rule {RuleId} for tenant {TenantId}", rule.Id, tenant);
        return Map(rule);
    }

    public async Task<PiiRedactionRuleResponse> UpdateRuleAsync(Guid ruleId, int expectedRevision, UpsertPiiRedactionRuleRequest request, CancellationToken cancellationToken)
    {
        var (tenant, user) = await RequireAdministratorAsync().ConfigureAwait(false);
        EnsureValid(request);

        var rule = await LoadRuleAsync(tenant, ruleId, cancellationToken).ConfigureAwait(false);
        if (rule.Revision != expectedRevision)
        {
            throw new PiiRedactionConcurrencyException();
        }

        rule.Update(
            request.RuleName.Trim(),
            request.Description,
            request.PatternType,
            request.RegexPattern?.Trim(),
            request.Preset,
            request.Action,
            request.Scope,
            request.TargetFields,
            request.Priority,
            request.Replacement,
            request.IsEnabled,
            user);

        await SaveAsync(cancellationToken).ConfigureAwait(false);
        ruleProvider.Invalidate(tenant);

        await AuditRuleMutationAsync(tenant, user, AuditActionTypes.PiiRedactionRuleUpdated, rule).ConfigureAwait(false);
        logger.LogInformation("Updated PII redaction rule {RuleId} to revision {Revision}", rule.Id, rule.Revision);
        return Map(rule);
    }

    public async Task DeleteRuleAsync(Guid ruleId, int expectedRevision, CancellationToken cancellationToken)
    {
        var (tenant, user) = await RequireAdministratorAsync().ConfigureAwait(false);
        var rule = await LoadRuleAsync(tenant, ruleId, cancellationToken).ConfigureAwait(false);
        if (rule.Revision != expectedRevision)
        {
            throw new PiiRedactionConcurrencyException();
        }

        context.Set<PiiRedactionRule>().Remove(rule);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
        ruleProvider.Invalidate(tenant);

        await audit.LogAsync(new CreateAuditLogRequest
        {
            ActionType = AuditActionTypes.PiiRedactionRuleDeleted,
            ResourceType = ResourceType,
            ResourceId = rule.Id.ToString(),
            TenantId = tenant,
            UserId = user,
            Description = $"Deleted PII redaction rule '{rule.RuleName}'",
            Metadata = new { RuleName = rule.RuleName, rule.Action, rule.Scope },
            Success = true,
            RiskLevel = AuditRiskLevel.High,
            Category = AuditCategory.Admin
        }).ConfigureAwait(false);
        logger.LogInformation("Deleted PII redaction rule {RuleId}", rule.Id);
    }

    /// <summary>
    /// Dry-runs the current enabled rules against sample field values without persisting anything —
    /// the pattern-recognition verification surface for administrators.
    /// </summary>
    public async Task<PiiRedactionTestRunResponse> TestAsync(PiiRedactionTestRunRequest request, CancellationToken cancellationToken)
    {
        var (tenant, _) = await RequireAdministratorAsync().ConfigureAwait(false);

        PiiRuleSet rules;
        try
        {
            rules = await ruleProvider.GetTenantRuleSetAsync(tenant, cancellationToken).ConfigureAwait(false);
        }
        catch (PiiRedactionUnavailableException exception)
        {
            return new PiiRedactionTestRunResponse(
                Changed: true,
                FailClosed: true,
                FailureReason: exception.InnerException!.Message,
                RedactedValueCount: request.SampleFields.Count,
                TokenizedValueCount: 0,
                Fields: request.SampleFields
                    .Select(pair => new PiiRedactionTestRunFieldResult(pair.Key, pair.Value, PiiRedactionFailClosed.Marker, true, null, null))
                    .ToList());
        }

        var fields = new List<PiiRedactionTestRunFieldResult>();
        var redacted = 0;
        var tokenized = 0;
        var changed = false;
        foreach (var (field, value) in request.SampleFields)
        {
            var outcome = PiiRedactionEngine.ApplyToValue(value, field, rules.Rules, tokenizer);
            if (outcome.Changed)
            {
                changed = true;
                if (outcome.Action == PiiRuleAction.Tokenize)
                {
                    tokenized += outcome.MatchCount;
                }
                else
                {
                    redacted += outcome.MatchCount;
                }
            }

            fields.Add(new PiiRedactionTestRunFieldResult(
                field,
                value,
                outcome.Value ?? value,
                outcome.Changed,
                outcome.AppliedRuleId,
                outcome.Action?.ToString()));
        }

        return new PiiRedactionTestRunResponse(changed, false, null, redacted, tokenized, fields);
    }

    /// <summary>
    /// Resolves a deterministic token back to its original value for an authorized investigation.
    /// High-sensitivity: requires a reason, is audited at critical risk, and is tenant-scoped.
    /// </summary>
    public async Task<PiiTokenResolutionResponse> ResolveTokenAsync(string token, string reason, CancellationToken cancellationToken)
    {
        var (tenant, user) = await RequireAdministratorAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new PiiRedactionValidationException(new Dictionary<string, string[]> { ["token"] = ["A token is required."] });
        }
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
        {
            throw new PiiRedactionValidationException(new Dictionary<string, string[]> { ["reason"] = ["An investigation reason of at least 10 characters is required to detokenize."] });
        }

        var scopeKey = PiiRedactionRuleProvider.ScopeKey(tenant);
        var entry = await context.Set<PiiTokenVaultEntry>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Token == token && item.ScopeKey == scopeKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException(
                "Token was not found in the vault for this tenant. Tokens produced while platform encryption was unavailable are deterministic but not reversible.");

        string value;
        try
        {
            value = encryptionService.Decrypt(entry.EncryptedValue);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "The vault entry could not be decrypted with the current platform key; it may have been written under a rotated key.", exception);
        }

        await audit.LogAsync(new CreateAuditLogRequest
        {
            ActionType = AuditActionTypes.PiiTokenResolved,
            ResourceType = "PiiTokenVault",
            ResourceId = entry.Id.ToString(),
            TenantId = tenant,
            UserId = user,
            Description = "PII token resolved for an authorized investigation",
            Metadata = new { entry.Token, entry.FirstSeenAt, entry.UseCount, Reason = LogRedaction.Sanitize(reason.Trim()) },
            Success = true,
            RiskLevel = AuditRiskLevel.Critical,
            Category = AuditCategory.Security
        }).ConfigureAwait(false);

        return new PiiTokenResolutionResponse(entry.Token, value, entry.TenantId, entry.FirstSeenAt, entry.UseCount);
    }

    private async Task<PiiRedactionRule> LoadRuleAsync(Guid tenant, Guid ruleId, CancellationToken cancellationToken)
    {
        var rule = await context.Set<PiiRedactionRule>()
            .SingleOrDefaultAsync(item => item.Id == ruleId && (item.TenantId == tenant || item.TenantId == null), cancellationToken)
            .ConfigureAwait(false);
        return rule ?? throw new KeyNotFoundException($"Rule {ruleId} was not found in this tenant or the global scope.");
    }

    private async Task<(Guid Tenant, Guid User)> RequireAdministratorAsync()
    {
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || !actor.IsTenantAdmin || actor.TenantId is null || actor.TenantId == Guid.Empty ||
            actor.SubjectIdAsGuid is null || actor.SubjectIdAsGuid == Guid.Empty)
        {
            await audit.LogPermissionDenyAsync(
                actor.SubjectIdAsGuid,
                PermissionName,
                ResourceType,
                null,
                "An authenticated tenant administrator with a tenant context is required.",
                actor.TenantId).ConfigureAwait(false);
            throw new PiiRedactionUnauthorizedException("A tenant administrator and tenant context are required.");
        }

        return (actor.TenantId.Value, actor.SubjectIdAsGuid.Value);
    }

    private static void EnsureValid(UpsertPiiRedactionRuleRequest request)
    {
        var errors = PiiRedactionRule.Validate(
            request.RuleName,
            request.PatternType,
            request.RegexPattern,
            request.Preset,
            request.Scope,
            request.TargetFields,
            request.Replacement);
        if (errors.Count > 0)
        {
            throw new PiiRedactionValidationException(errors);
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new PiiRedactionConcurrencyException();
        }
    }

    private Task AuditRuleMutationAsync(Guid tenant, Guid user, string action, PiiRedactionRule rule) =>
        audit.LogAsync(new CreateAuditLogRequest
        {
            ActionType = action,
            ResourceType = ResourceType,
            ResourceId = rule.Id.ToString(),
            TenantId = tenant,
            UserId = user,
            Description = $"{action} on PII redaction rule '{rule.RuleName}'",
            Metadata = new { RuleName = rule.RuleName, rule.PatternType, rule.Action, rule.Scope, rule.Priority, rule.IsEnabled, rule.Revision },
            Success = true,
            RiskLevel = AuditRiskLevel.Medium,
            Category = AuditCategory.Admin
        });

    private static PiiRedactionRuleResponse Map(PiiRedactionRule rule) => new(
        rule.Id,
        rule.TenantId,
        rule.RuleName,
        rule.Description,
        rule.IsEnabled,
        rule.Priority,
        rule.PatternType,
        rule.RegexPattern,
        rule.Preset,
        rule.Action,
        rule.Scope,
        rule.TargetFields,
        rule.Replacement,
        rule.Revision,
        rule.CreatedAt,
        rule.UpdatedAt);
}
