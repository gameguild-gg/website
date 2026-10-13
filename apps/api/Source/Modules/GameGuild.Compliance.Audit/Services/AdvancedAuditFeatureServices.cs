namespace GameGuild.Compliance.Audit;

/// <summary>
/// Comprehensive service interface for all 6 advanced audit features.
/// </summary>
public interface IAdvancedAuditFeatureServices
{
    // Feature 2: Scheduled audit export jobs
    Task<ScheduledAuditExport> CreateScheduledExportAsync(ScheduledAuditExport export);
    Task<AuditExportHistory> ExecuteScheduledExportAsync(Guid scheduledExportId);
    Task<IEnumerable<ScheduledAuditExport>> GetScheduledExportsAsync(Guid tenantId);

    // Feature 3: Retention policy simulation
    Task<RetentionPolicySimulation> CreateRetentionPolicyAsync(RetentionPolicySimulation policy);
    Task<RetentionPolicySimulation> SimulateForecastAsync(Guid policyId, int forecastDays);
    Task<IEnumerable<RetentionPolicySimulation>> GetRetentionPoliciesAsync(Guid tenantId);

    // Feature 4: PII redaction
    Task<string> RedactPiiAsync(string content, Guid tenantId);
    Task<PiiRedactionRule> CreateRedactionRuleAsync(PiiRedactionRule rule);
    Task<IEnumerable<PiiRedactionRule>> GetRedactionRulesAsync(Guid tenantId);

    // Feature 5: Saved queries
    Task<SavedAuditQuery> CreateSavedQueryAsync(SavedAuditQuery query);
    Task<object> ExecuteSavedQueryAsync(Guid queryId, Guid userId);
    Task<IEnumerable<SavedAuditQuery>> GetSavedQueriesAsync(Guid tenantId, Guid userId);

    // Feature 6: Audit replay
    Task<AuditReplaySession> CreateReplaySessionAsync(AuditReplaySession session);
    Task<AuditReplaySession> ExecuteReplayAsync(Guid sessionId);
    Task<IEnumerable<AuditReplaySession>> GetReplaySessionsAsync(Guid tenantId);
}

/// <summary>
/// Implementation of advanced audit feature services.
/// </summary>
public sealed class AdvancedAuditFeatureServices : IAdvancedAuditFeatureServices
{
    private readonly IAdvancedAuditRepository _repository;
    private readonly IScheduledExportService _exportService;
    private readonly IPiiRedactionService _redactionService;
    private readonly IAuditQueryService _queryService;
    private readonly IAuditReplayService _replayService;

    public AdvancedAuditFeatureServices(
        IAdvancedAuditRepository repository,
        IScheduledExportService exportService,
        IPiiRedactionService redactionService,
        IAuditQueryService queryService,
        IAuditReplayService replayService)
    {
        _repository = repository;
        _exportService = exportService;
        _redactionService = redactionService;
        _queryService = queryService;
        _replayService = replayService;
    }

    // Feature 2: Scheduled exports
    public async Task<ScheduledAuditExport> CreateScheduledExportAsync(ScheduledAuditExport export)
    {
        await _repository.AddScheduledExportAsync(export).ConfigureAwait(false);
        return export;
    }

    public async Task<AuditExportHistory> ExecuteScheduledExportAsync(Guid scheduledExportId)
    {
        return await _exportService.ExecuteExportAsync(scheduledExportId).ConfigureAwait(false);
    }

    public async Task<IEnumerable<ScheduledAuditExport>> GetScheduledExportsAsync(Guid tenantId)
    {
        return await _repository.GetScheduledExportsAsync(tenantId).ConfigureAwait(false);
    }

    // Feature 3: Retention simulation
    public async Task<RetentionPolicySimulation> CreateRetentionPolicyAsync(RetentionPolicySimulation policy)
    {
        await _repository.AddRetentionPolicyAsync(policy).ConfigureAwait(false);
        return policy;
    }

    public async Task<RetentionPolicySimulation> SimulateForecastAsync(Guid policyId, int forecastDays)
    {
        var policy = await _repository.GetRetentionPolicyAsync(policyId).ConfigureAwait(false);
        if (policy == null) throw new InvalidOperationException("Policy not found");

        policy.CalculateForecast(forecastDays);
        policy.GenerateRecommendations();
        await _repository.UpdateRetentionPolicyAsync(policy).ConfigureAwait(false);
        return policy;
    }

    public async Task<IEnumerable<RetentionPolicySimulation>> GetRetentionPoliciesAsync(Guid tenantId)
    {
        return await _repository.GetRetentionPoliciesAsync(tenantId).ConfigureAwait(false);
    }

    // Feature 4: PII redaction (delegates to the canonical pipeline in Services/PiiRedaction)
    public async Task<string> RedactPiiAsync(string content, Guid tenantId)
    {
        var result = await _redactionService.RedactTextAsync(content, tenantId, PiiRedactionSource.Manual, null, CancellationToken.None).ConfigureAwait(false);
        return result.RedactedContent;
    }

    public async Task<PiiRedactionRule> CreateRedactionRuleAsync(PiiRedactionRule rule)
    {
        await _repository.AddRedactionRuleAsync(rule).ConfigureAwait(false);
        return rule;
    }

    public async Task<IEnumerable<PiiRedactionRule>> GetRedactionRulesAsync(Guid tenantId)
    {
        return await _repository.GetRedactionRulesAsync(tenantId).ConfigureAwait(false);
    }

    // Feature 5: Saved queries
    public async Task<SavedAuditQuery> CreateSavedQueryAsync(SavedAuditQuery query)
    {
        await _repository.AddSavedQueryAsync(query).ConfigureAwait(false);
        return query;
    }

    public async Task<object> ExecuteSavedQueryAsync(Guid queryId, Guid userId)
    {
        return await _queryService.ExecuteQueryAsync(queryId, userId).ConfigureAwait(false);
    }

    public async Task<IEnumerable<SavedAuditQuery>> GetSavedQueriesAsync(Guid tenantId, Guid userId)
    {
        return await _repository.GetSavedQueriesAsync(tenantId, userId).ConfigureAwait(false);
    }

    // Feature 6: Audit replay
    public async Task<AuditReplaySession> CreateReplaySessionAsync(AuditReplaySession session)
    {
        await _repository.AddReplaySessionAsync(session).ConfigureAwait(false);
        return session;
    }

    public async Task<AuditReplaySession> ExecuteReplayAsync(Guid sessionId)
    {
        return await _replayService.ExecuteReplayAsync(sessionId).ConfigureAwait(false);
    }

    public async Task<IEnumerable<AuditReplaySession>> GetReplaySessionsAsync(Guid tenantId)
    {
        return await _repository.GetReplaySessionsAsync(tenantId).ConfigureAwait(false);
    }
}

// Repository interface
public interface IAdvancedAuditRepository
{
    // Scheduled exports
    Task AddScheduledExportAsync(ScheduledAuditExport export);
    Task<IEnumerable<ScheduledAuditExport>> GetScheduledExportsAsync(Guid tenantId);

    // Retention policies
    Task AddRetentionPolicyAsync(RetentionPolicySimulation policy);
    Task UpdateRetentionPolicyAsync(RetentionPolicySimulation policy);
    Task<RetentionPolicySimulation?> GetRetentionPolicyAsync(Guid policyId);
    Task<IEnumerable<RetentionPolicySimulation>> GetRetentionPoliciesAsync(Guid tenantId);

    // Redaction rules
    Task AddRedactionRuleAsync(PiiRedactionRule rule);
    Task<IEnumerable<PiiRedactionRule>> GetRedactionRulesAsync(Guid tenantId);

    // Saved queries
    Task AddSavedQueryAsync(SavedAuditQuery query);
    Task<IEnumerable<SavedAuditQuery>> GetSavedQueriesAsync(Guid tenantId, Guid userId);

    // Replay sessions
    Task AddReplaySessionAsync(AuditReplaySession session);
    Task<IEnumerable<AuditReplaySession>> GetReplaySessionsAsync(Guid tenantId);
}

// Supporting service interfaces
public interface IScheduledExportService
{
    Task<AuditExportHistory> ExecuteExportAsync(Guid scheduledExportId);
}

// IPiiRedactionService now lives in Services/PiiRedaction/PiiRedactionService.cs (issue #192 pipeline).

public interface IAuditQueryService
{
    Task<object> ExecuteQueryAsync(Guid queryId, Guid userId);
}

public interface IAuditReplayService
{
    Task<AuditReplaySession> ExecuteReplayAsync(Guid sessionId);
}
