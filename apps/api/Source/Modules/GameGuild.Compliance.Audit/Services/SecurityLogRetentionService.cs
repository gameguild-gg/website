using System.Text.Json;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Compliance.Audit;

/// <summary>Validation errors raised by security log retention configuration or enforcement.</summary>
public sealed class SecurityLogRetentionValidationException(IReadOnlyDictionary<string, string[]> errors) : Exception("The security log retention request is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public interface ISecurityLogRetentionRepository
{
    Task<SecurityLogRetentionPolicy?> GetPolicyAsync(Guid tenantId, CancellationToken cancellationToken);

    Task SavePolicyAsync(SecurityLogRetentionPolicy policy, bool isNew, CancellationToken cancellationToken);

    Task<IReadOnlyList<SecurityLogRetentionPolicy>> GetAllPoliciesAsync(CancellationToken cancellationToken);

    Task AddExecutionAsync(SecurityLogRetentionExecution execution, CancellationToken cancellationToken);

    Task<IReadOnlyList<SecurityLogRetentionExecution>> GetExecutionsAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken);
}

public sealed class SecurityLogRetentionRepository(IApplicationDbContext context) : ISecurityLogRetentionRepository
{
    public Task<SecurityLogRetentionPolicy?> GetPolicyAsync(Guid tenantId, CancellationToken cancellationToken) =>
        context.Set<SecurityLogRetentionPolicy>().SingleOrDefaultAsync(policy => policy.TenantId == tenantId, cancellationToken);

    public async Task SavePolicyAsync(SecurityLogRetentionPolicy policy, bool isNew, CancellationToken cancellationToken)
    {
        if (isNew)
        {
            context.Set<SecurityLogRetentionPolicy>().Add(policy);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new SecurityLogRetentionValidationException(new Dictionary<string, string[]>
            {
                ["ExpectedRevision"] = ["The retention policy was modified by another administrator; reload and retry."]
            });
        }
    }

    public async Task<IReadOnlyList<SecurityLogRetentionPolicy>> GetAllPoliciesAsync(CancellationToken cancellationToken) =>
        await context.Set<SecurityLogRetentionPolicy>().AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AddExecutionAsync(SecurityLogRetentionExecution execution, CancellationToken cancellationToken)
    {
        context.Set<SecurityLogRetentionExecution>().Add(execution);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SecurityLogRetentionExecution>> GetExecutionsAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken) =>
        await context.Set<SecurityLogRetentionExecution>().AsNoTracking()
            .Where(execution => execution.TenantId == tenantId)
            .OrderByDescending(execution => execution.ExecutedAtUtc)
            .ThenByDescending(execution => execution.Id)
            .Skip(skip).Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
}

public interface ISecurityLogRetentionService
{
    Task<SecurityLogRetentionPolicyResponse?> GetPolicyAsync(CancellationToken cancellationToken);

    Task<SecurityLogRetentionPolicyResponse> ConfigureAsync(ConfigureSecurityLogRetentionRequest request, CancellationToken cancellationToken);

    Task<SecurityLogRetentionExecutionResponse?> EnforceForCurrentTenantAsync(EnforceSecurityLogRetentionRequest request, CancellationToken cancellationToken);

    Task<SecurityLogRetentionExecutionResponse?> EnforceForTenantAsync(Guid tenantId, Guid? triggeredByUserId, bool dryRun, CancellationToken cancellationToken);

    Task<IReadOnlyList<SecurityLogRetentionExecutionResponse>> GetExecutionsAsync(int skip, int take, CancellationToken cancellationToken);

    /// <summary>Returns aged audit rows moved into the archive storage tier for the current tenant (issue #193).</summary>
    Task<ArchivedSecurityLogListResponse> GetArchivedAsync(int skip, int take, CancellationToken cancellationToken);

    /// <summary>Returns the storage-tier placement report (hot vs archive row counts) for the current tenant (issue #193).</summary>
    Task<SecurityLogStorageTierReportResponse> GetStorageTierReportAsync(CancellationToken cancellationToken);
}

/// <summary>
///     Applies tenant retention policies to the central security audit log. Enforcement only deletes
///     <c>AuditLogs</c> rows whose per-category retention window has elapsed, never deletes while a
///     legal hold is active, and records every pass (manual, dry-run, or scheduled) as an execution row.
/// </summary>
public sealed class SecurityLogRetentionService(
    IActorContextAccessor actors,
    ISecurityLogRetentionRepository repository,
    IServiceScopeFactory scopeFactory,
    ISecurityEventLogger securityEvents,
    TimeProvider timeProvider,
    IOptions<SecurityEventPipelineOptions> options,
    ILogger<SecurityLogRetentionService> logger) : ISecurityLogRetentionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SecurityEventPipelineOptions pipelineOptions = options.Value;

    public async Task<SecurityLogRetentionPolicyResponse?> GetPolicyAsync(CancellationToken cancellationToken)
    {
        var (tenant, _) = await RequireAdministratorAsync().ConfigureAwait(false);
        var policy = await repository.GetPolicyAsync(tenant, cancellationToken).ConfigureAwait(false);
        return policy is null ? null : MapPolicy(policy, timeProvider.GetUtcNow().UtcDateTime);
    }

    public async Task<SecurityLogRetentionPolicyResponse> ConfigureAsync(ConfigureSecurityLogRetentionRequest request, CancellationToken cancellationToken)
    {
        var (tenant, user) = await RequireAdministratorAsync().ConfigureAwait(false);
        Validate(request);

        var existing = await repository.GetPolicyAsync(tenant, cancellationToken).ConfigureAwait(false);
        if (request.ExpectedRevision != (existing?.Revision ?? 0))
        {
            throw new SecurityLogRetentionValidationException(new Dictionary<string, string[]>
            {
                ["ExpectedRevision"] = [$"Expected revision {request.ExpectedRevision} does not match the stored revision {existing?.Revision ?? 0}."]
            });
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var overridesJson = SerializeOverrides(request.CategoryOverrides);
        if (existing is null)
        {
            existing = SecurityLogRetentionPolicy.Create(tenant, request.RetentionDays, overridesJson, request.LegalHoldUntilUtc, user, now, request.ArchiveAfterDays);
            await repository.SavePolicyAsync(existing, isNew: true, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            existing.Update(request.RetentionDays, overridesJson, request.LegalHoldUntilUtc, user, now, request.ArchiveAfterDays);
            await repository.SavePolicyAsync(existing, isNew: false, cancellationToken).ConfigureAwait(false);
        }

        // Retention policy changes are themselves security events: they alter how long security
        // evidence is preserved, so they flow through the durable security event pipeline.
        await securityEvents.RecordAsync(new CreateAuditLogRequest
        {
            ActionType = AuditActionTypes.SystemConfigChanged,
            ResourceType = "SecurityLogRetentionPolicy",
            ResourceId = existing.Id.ToString(),
            UserId = user,
            TenantId = tenant,
            Description = $"Security log retention policy revision {existing.Revision}: {request.RetentionDays} days.",
            Metadata = new { existing.Revision, request.RetentionDays, request.LegalHoldUntilUtc, request.CategoryOverrides },
            Success = true,
            RiskLevel = AuditRiskLevel.High,
            Category = AuditCategory.Admin
        }, cancellationToken).ConfigureAwait(false);

        return MapPolicy(existing, now);
    }

    public async Task<SecurityLogRetentionExecutionResponse?> EnforceForCurrentTenantAsync(EnforceSecurityLogRetentionRequest request, CancellationToken cancellationToken)
    {
        var (tenant, user) = await RequireAdministratorAsync().ConfigureAwait(false);
        return await EnforceForTenantAsync(tenant, user, request.DryRun, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SecurityLogRetentionExecutionResponse?> EnforceForTenantAsync(Guid tenantId, Guid? triggeredByUserId, bool dryRun, CancellationToken cancellationToken)
    {
        var policy = await repository.GetPolicyAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (policy is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var legalHoldActive = policy.LegalHoldUntilUtc.HasValue && policy.LegalHoldUntilUtc.Value > now;
        var overrides = DeserializeOverrides(policy.CategoryOverridesJson);
        var defaultCutoff = now.AddDays(-policy.RetentionDays);

        int deletedCount;
        int evaluatedCount;
        int archivedCount;
        if (legalHoldActive)
        {
            // A legal hold suspends every deletion and every archival move for the tenant; the pass
            // is still recorded so the suspension is visible in the retention execution history.
            evaluatedCount = 0;
            deletedCount = 0;
            archivedCount = 0;
        }
        else
        {
            var candidateIds = await LoadRetentionCandidateIdsAsync(tenantId, now, defaultCutoff, overrides, cancellationToken).ConfigureAwait(false);
            evaluatedCount = candidateIds.Count;
            deletedCount = 0;

            if (!dryRun && candidateIds.Count > 0)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var batchSize = Math.Max(1, pipelineOptions.RetentionBatchSize);
                foreach (var batch in candidateIds.Chunk(batchSize))
                {
                    var ids = batch.ToHashSet();
                    var rows = await context.Set<AuditLog>().Where(log => ids.Contains(log.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
                    context.Set<AuditLog>().RemoveRange(rows);
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    deletedCount += rows.Count;
                }
            }

            if (dryRun)
            {
                deletedCount = evaluatedCount;
            }

            // Storage-tier archival (issue #193): rows past the archive threshold but still within
            // their retention obligation move from the hot audit table into the archive tier. Rows
            // already past their retention obligation are deleted above, never archived.
            archivedCount = policy.ArchiveAfterDays is { } archiveAfterDays && archiveAfterDays > 0
                ? await ArchiveAgedRowsAsync(tenantId, now, archiveAfterDays, candidateIds.ToHashSet(), dryRun, cancellationToken).ConfigureAwait(false)
                : 0;

            // Archive-tier purge: archived rows whose full retention window has elapsed are removed
            // permanently, mirroring the hot-tier rules (default window plus category overrides).
            if (!dryRun)
            {
                deletedCount += await PurgeExpiredArchiveRowsAsync(tenantId, now, defaultCutoff, overrides, cancellationToken).ConfigureAwait(false);
            }
        }

        var execution = SecurityLogRetentionExecution.Record(
            tenantId, now, triggeredByUserId, defaultCutoff, policy.RetentionDays, legalHoldActive, dryRun, deletedCount, evaluatedCount, archivedCount);
        await repository.AddExecutionAsync(execution, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Security log retention pass for tenant {TenantId}: dryRun={DryRun}, legalHold={LegalHold}, evaluated={Evaluated}, deleted={Deleted}, archived={Archived}",
            tenantId, dryRun, legalHoldActive, evaluatedCount, deletedCount, archivedCount);

        return MapExecution(execution);
    }

    public async Task<IReadOnlyList<SecurityLogRetentionExecutionResponse>> GetExecutionsAsync(int skip, int take, CancellationToken cancellationToken)
    {
        var (tenant, _) = await RequireAdministratorAsync().ConfigureAwait(false);
        if (skip < 0 || take is < 1 or > 100)
        {
            throw new SecurityLogRetentionValidationException(new Dictionary<string, string[]> { ["Pagination"] = ["Use skip >= 0 and 1 <= take <= 100."] });
        }

        var executions = await repository.GetExecutionsAsync(tenant, skip, take, cancellationToken).ConfigureAwait(false);
        return executions.Select(MapExecution).ToArray();
    }

    /// <summary>
    ///     Serves aged audit rows that were moved into the archive storage tier (issue #193): archived
    ///     evidence stays accessible for investigations while the hot tier stays small.
    /// </summary>
    public async Task<ArchivedSecurityLogListResponse> GetArchivedAsync(int skip, int take, CancellationToken cancellationToken)
    {
        var (tenant, _) = await RequireAdministratorAsync().ConfigureAwait(false);
        if (skip < 0 || take is < 1 or > 100)
        {
            throw new SecurityLogRetentionValidationException(new Dictionary<string, string[]> { ["Pagination"] = ["Use skip >= 0 and 1 <= take <= 100."] });
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var rows = await context.Set<ArchivedSecurityAuditLog>().AsNoTracking()
            .Where(archived => archived.TenantId == tenant)
            .OrderByDescending(archived => archived.OriginalCreatedAt)
            .ThenByDescending(archived => archived.Id)
            .Skip(skip).Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var totalCount = await context.Set<ArchivedSecurityAuditLog>().AsNoTracking()
            .CountAsync(archived => archived.TenantId == tenant, cancellationToken)
            .ConfigureAwait(false);

        return new ArchivedSecurityLogListResponse(
            rows.Select(archived => new ArchivedSecurityLogResponse(
                archived.Id,
                archived.SourceAuditLogId,
                archived.ActionType,
                archived.ResourceType,
                archived.ResourceId,
                archived.UserId,
                archived.TenantId,
                archived.Description,
                archived.Success,
                archived.ErrorMessage,
                archived.RiskLevel,
                archived.Category,
                archived.CorrelationId,
                archived.OriginalCreatedAt,
                archived.ArchivedAtUtc)).ToArray(),
            totalCount);
    }

    /// <summary>
    ///     Storage-tier placement report (issue #193): live hot-tier and archive-tier row counts for the
    ///     tenant plus the configured windows, so cost placement is observable next to accessibility.
    /// </summary>
    public async Task<SecurityLogStorageTierReportResponse> GetStorageTierReportAsync(CancellationToken cancellationToken)
    {
        var (tenant, _) = await RequireAdministratorAsync().ConfigureAwait(false);
        var policy = await repository.GetPolicyAsync(tenant, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var hotRowCount = await context.Set<AuditLog>().AsNoTracking()
            .CountAsync(log => log.TenantId == tenant, cancellationToken)
            .ConfigureAwait(false);
        var archiveRowCount = await context.Set<ArchivedSecurityAuditLog>().AsNoTracking()
            .CountAsync(archived => archived.TenantId == tenant, cancellationToken)
            .ConfigureAwait(false);

        return new SecurityLogStorageTierReportResponse(
            tenant,
            hotRowCount,
            archiveRowCount,
            policy?.ArchiveAfterDays,
            policy?.RetentionDays ?? 0,
            policy?.LegalHoldUntilUtc.HasValue == true && policy.LegalHoldUntilUtc.Value > now);
    }

    /// <summary>
    ///     Loads the ids of audit rows whose retention window has elapsed: categories without an
    ///     override use the policy-wide cutoff, each overridden category uses its own cutoff.
    /// </summary>
    private async Task<List<Guid>> LoadRetentionCandidateIdsAsync(
        Guid tenantId,
        DateTime nowUtc,
        DateTime defaultCutoff,
        IReadOnlyDictionary<AuditCategory, int> overrides,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var overridden = overrides.Keys.ToList();

        var query = context.Set<AuditLog>().AsNoTracking()
            .Where(log => log.TenantId == tenantId && log.CreatedAt < defaultCutoff && !overridden.Contains(log.Category))
            .Select(log => log.Id);

        foreach (var (category, days) in overrides)
        {
            var categoryCutoff = nowUtc.AddDays(-days);
            query = query.Union(
                context.Set<AuditLog>().AsNoTracking()
                    .Where(log => log.TenantId == tenantId && log.Category == category && log.CreatedAt < categoryCutoff)
                    .Select(log => log.Id));
        }

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Moves hot-tier audit rows older than the archive threshold into the archive tier
    ///     (issue #193). Rows already past their deletion cutoff are never archived — they belong to
    ///     the deletion pass. In dry-run mode the move is only counted. Idempotent: the unique
    ///     (tenant, source row) index makes re-archival of the same row impossible, and rows are
    ///     removed from the hot table in the same batched save that inserts the archive copy.
    /// </summary>
    private async Task<int> ArchiveAgedRowsAsync(
        Guid tenantId,
        DateTime nowUtc,
        int archiveAfterDays,
        HashSet<Guid> deletionCandidateIds,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var archiveCutoff = nowUtc.AddDays(-archiveAfterDays);
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var batchSize = Math.Max(1, pipelineOptions.RetentionBatchSize);

        var candidateIds = await context.Set<AuditLog>().AsNoTracking()
            .Where(log => log.TenantId == tenantId
                          && log.CreatedAt < archiveCutoff
                          && !deletionCandidateIds.Contains(log.Id))
            .Select(log => log.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (dryRun)
        {
            return candidateIds.Count;
        }

        var archivedCount = 0;
        foreach (var batch in candidateIds.Chunk(batchSize))
        {
            var ids = batch.ToHashSet();
            var rows = await context.Set<AuditLog>().Where(log => ids.Contains(log.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
            context.Set<ArchivedSecurityAuditLog>().AddRange(rows.Select(row => ArchivedSecurityAuditLog.FromAuditLog(row, nowUtc)));
            context.Set<AuditLog>().RemoveRange(rows);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            archivedCount += rows.Count;
        }

        return archivedCount;
    }

    /// <summary>
    ///     Permanently removes archive-tier rows whose full retention window has elapsed, using the
    ///     same default-plus-category-override rules as the hot tier (measured from the original row
    ///     creation instant).
    /// </summary>
    private async Task<int> PurgeExpiredArchiveRowsAsync(
        Guid tenantId,
        DateTime nowUtc,
        DateTime defaultCutoff,
        IReadOnlyDictionary<AuditCategory, int> overrides,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var overridden = overrides.Keys.ToList();

        var query = context.Set<ArchivedSecurityAuditLog>().AsNoTracking()
            .Where(archived => archived.TenantId == tenantId
                               && archived.OriginalCreatedAt < defaultCutoff
                               && !overridden.Contains(archived.Category))
            .Select(archived => archived.Id);

        foreach (var (category, days) in overrides)
        {
            var categoryCutoff = nowUtc.AddDays(-days);
            query = query.Union(
                context.Set<ArchivedSecurityAuditLog>().AsNoTracking()
                    .Where(archived => archived.TenantId == tenantId
                                       && archived.Category == category
                                       && archived.OriginalCreatedAt < categoryCutoff)
                    .Select(archived => archived.Id));
        }

        var candidateIds = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        if (candidateIds.Count == 0)
        {
            return 0;
        }

        var batchSize = Math.Max(1, pipelineOptions.RetentionBatchSize);
        var deletedCount = 0;
        foreach (var batch in candidateIds.Chunk(batchSize))
        {
            var ids = batch.ToHashSet();
            var rows = await context.Set<ArchivedSecurityAuditLog>().Where(archived => ids.Contains(archived.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
            context.Set<ArchivedSecurityAuditLog>().RemoveRange(rows);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            deletedCount += rows.Count;
        }

        return deletedCount;
    }

    private async Task<(Guid Tenant, Guid User)> RequireAdministratorAsync()
    {
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || !actor.IsTenantAdmin || actor.TenantId is null || actor.TenantId == Guid.Empty ||
            actor.SubjectIdAsGuid is null || actor.SubjectIdAsGuid == Guid.Empty)
        {
            logger.LogWarning("Security log retention access denied for actor {Actor}", actor.SubjectIdAsGuid);
            throw new UnauthorizedAccessException("A tenant administrator and tenant context are required.");
        }

        return (actor.TenantId.Value, actor.SubjectIdAsGuid.Value);
    }

    private static void Validate(ConfigureSecurityLogRetentionRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.RetentionDays is < 30 or > 3650)
        {
            errors["RetentionDays"] = ["Retention days must be between 30 and 3650."];
        }

        if (request.CategoryOverrides is { Count: > 0 })
        {
            foreach (var (category, days) in request.CategoryOverrides)
            {
                if (!Enum.TryParse<AuditCategory>(category, ignoreCase: false, out _))
                {
                    errors[$"CategoryOverrides[{category}]"] = [$"'{category}' is not a valid audit category name."];
                }
                else if (days is < 30 or > 3650)
                {
                    errors[$"CategoryOverrides[{category}]"] = ["Category retention days must be between 30 and 3650."];
                }
            }
        }

        // Storage-tier archival must happen strictly before the retention window ends: archiving at
        // or after the deletion cutoff would be a wasted copy of a row that is about to be purged.
        if (request.ArchiveAfterDays is { } archiveAfterDays)
        {
            if (archiveAfterDays is < 1 or > 3650)
            {
                errors["ArchiveAfterDays"] = ["Archive after days must be between 1 and 3650."];
            }
            else if (archiveAfterDays >= request.RetentionDays)
            {
                errors["ArchiveAfterDays"] = [$"Archive after days ({archiveAfterDays}) must be shorter than the retention window ({request.RetentionDays})."];
            }
        }

        if (errors.Count > 0)
        {
            throw new SecurityLogRetentionValidationException(errors);
        }
    }

    private static string? SerializeOverrides(Dictionary<string, int>? overrides) =>
        overrides is null || overrides.Count == 0 ? null : JsonSerializer.Serialize(overrides, JsonOptions);

    private static IReadOnlyDictionary<AuditCategory, int> DeserializeOverrides(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<AuditCategory, int>();
        }

        var raw = JsonSerializer.Deserialize<Dictionary<string, int>>(json, JsonOptions) ?? [];
        return raw
            .Where(pair => Enum.TryParse<AuditCategory>(pair.Key, ignoreCase: false, out _))
            .ToDictionary(pair => Enum.Parse<AuditCategory>(pair.Key, ignoreCase: false), pair => pair.Value);
    }

    private static SecurityLogRetentionPolicyResponse MapPolicy(SecurityLogRetentionPolicy policy, DateTime nowUtc)
    {
        var overrides = DeserializeOverrides(policy.CategoryOverridesJson)
            .ToDictionary(pair => pair.Key.ToString(), pair => pair.Value);
        return new SecurityLogRetentionPolicyResponse(
            policy.Id,
            policy.TenantId,
            policy.Revision,
            policy.RetentionDays,
            overrides,
            policy.LegalHoldUntilUtc,
            policy.UpdatedByUserId,
            policy.ConfiguredAtUtc,
            policy.LegalHoldUntilUtc.HasValue && policy.LegalHoldUntilUtc.Value > nowUtc,
            policy.ArchiveAfterDays);
    }

    private static SecurityLogRetentionExecutionResponse MapExecution(SecurityLogRetentionExecution execution) =>
        new(execution.Id, execution.TenantId, execution.ExecutedAtUtc, execution.TriggeredByUserId, execution.CutoffUtc,
            execution.PolicyRetentionDays, execution.LegalHoldActive, execution.DryRun, execution.DeletedCount, execution.EvaluatedCount,
            execution.ArchivedCount);
}
