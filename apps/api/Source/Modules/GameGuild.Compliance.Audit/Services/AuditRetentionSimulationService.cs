using System.Text.Json;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Compliance.Audit;

public interface IAuditRetentionDataSource
{
    Task<AuditRetentionDataSnapshot> CaptureAsync(Guid tenantId, DateTime asOfUtc, int historicalDays, CancellationToken cancellationToken);
}

public interface IAuditRetentionSimulationService
{
    Task<AuditRetentionConfigurationResponse?> GetConfigurationAsync(bool includeInherited, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditRetentionPolicyTemplate>> GetPolicyTemplatesAsync(CancellationToken cancellationToken);
    Task<AuditRetentionConfigurationResponse> ConfigureAsync(ConfigureAuditRetentionRequest request, CancellationToken cancellationToken);
    Task<AuditRetentionSimulationResponse?> RunAsync(RunAuditRetentionSimulationRequest request, CancellationToken cancellationToken);
    Task<AuditRetentionSimulationResponse?> GetRunAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditRetentionSimulationSummary>> GetRunsAsync(int skip, int take, CancellationToken cancellationToken);
}

public sealed class AuditRetentionSimulationService(
    IActorContextAccessor actors,
    IAuditRetentionSimulationRepository repository,
    IAuditRetentionDataSource dataSource,
    AuditRetentionSimulationEngine engine,
    IAuditService audit,
    TimeProvider timeProvider) : IAuditRetentionSimulationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AuditRetentionConfigurationResponse?> GetConfigurationAsync(bool includeInherited, CancellationToken cancellationToken)
    {
        var (tenant, _) = await RequireAdministratorAsync().ConfigureAwait(false);
        var configuration = await repository.GetConfigurationAsync(tenant, cancellationToken).ConfigureAwait(false);
        if (configuration is not null) { return MapConfiguration(configuration); }
        return includeInherited
            ? new AuditRetentionConfigurationResponse(
                Guid.Empty, tenant, 0, Guid.Empty, AuditRetentionPolicyTemplates.Baseline.PublishedAtUtc,
                AuditRetentionPolicyTemplates.Baseline.ToConfigurationRequest(),
                AuditRetentionPolicyTemplates.Baseline.Id)
            : null;
    }

    public async Task<IReadOnlyList<AuditRetentionPolicyTemplate>> GetPolicyTemplatesAsync(CancellationToken cancellationToken)
    {
        await RequireAdministratorAsync().ConfigureAwait(false);
        return AuditRetentionPolicyTemplates.All;
    }

    public async Task<AuditRetentionConfigurationResponse> ConfigureAsync(ConfigureAuditRetentionRequest request, CancellationToken cancellationToken)
    {
        var (tenant, user) = await RequireAdministratorAsync().ConfigureAwait(false);
        AuditRetentionInputValidation.Validate(request);
        var existing = await repository.GetConfigurationAsync(tenant, cancellationToken).ConfigureAwait(false);
        if (request.ExpectedRevision != (existing?.Revision ?? 0) || existing?.Revision == int.MaxValue)
        {
            throw new AuditRetentionConcurrencyException();
        }
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var configuration = existing ?? new AuditRetentionConfiguration { TenantId = tenant, CreatedAt = now };
        configuration.Revision++;
        configuration.UpdatedAt = now;
        configuration.UpdatedByUserId = user;
        configuration.ConfigurationJson = JsonSerializer.Serialize(request with { ExpectedRevision = configuration.Revision }, JsonOptions);
        await repository.SaveConfigurationAsync(configuration, existing is null, cancellationToken).ConfigureAwait(false);
        await AuditAsync("AuditRetentionSimulationConfigured", tenant, user, configuration.Id,
            new { configuration.Revision }).ConfigureAwait(false);
        return MapConfiguration(configuration);
    }

    public async Task<AuditRetentionSimulationResponse?> RunAsync(RunAuditRetentionSimulationRequest request, CancellationToken cancellationToken)
    {
        var (tenant, user) = await RequireAdministratorAsync().ConfigureAwait(false);
        AuditRetentionInputValidation.Validate(request);
        var configuration = await repository.GetConfigurationAsync(tenant, cancellationToken).ConfigureAwait(false);
        if (configuration is null) { return null; }
        var settings = Deserialize<ConfigureAuditRetentionRequest>(configuration.ConfigurationJson);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var snapshot = await dataSource.CaptureAsync(tenant, now, request.HistoricalDays, cancellationToken).ConfigureAwait(false);
        var report = engine.Simulate(settings, request, snapshot, now, cancellationToken);
        var run = new AuditRetentionSimulationRun
        {
            TenantId = tenant, CreatedByUserId = user, CreatedAt = now, UpdatedAt = now,
            ConfigurationRevision = configuration.Revision, ConfigurationJson = configuration.ConfigurationJson,
            RequestJson = JsonSerializer.Serialize(request, JsonOptions), ReportJson = JsonSerializer.Serialize(report, JsonOptions),
            ModelVersion = report.ModelVersion, Currency = report.Currency, ForecastMonths = report.ForecastMonths,
            BaselineTotalCost = report.Baseline.TotalCost, RecommendedTotalCost = report.Recommendation.SuggestedScenario?.TotalCost
        };
        await repository.AddRunAsync(run, cancellationToken).ConfigureAwait(false);
        await AuditAsync("AuditRetentionSimulationCreated", tenant, user, run.Id,
            new { run.ConfigurationRevision, run.ModelVersion, run.ForecastMonths }).ConfigureAwait(false);
        return MapRun(run);
    }

    public async Task<AuditRetentionSimulationResponse?> GetRunAsync(Guid id, CancellationToken cancellationToken)
    {
        var (tenant, _) = await RequireAdministratorAsync().ConfigureAwait(false);
        var run = await repository.GetRunAsync(tenant, id, cancellationToken).ConfigureAwait(false);
        return run is null ? null : MapRun(run);
    }

    public async Task<IReadOnlyList<AuditRetentionSimulationSummary>> GetRunsAsync(int skip, int take, CancellationToken cancellationToken)
    {
        var (tenant, _) = await RequireAdministratorAsync().ConfigureAwait(false);
        if (skip < 0 || take is < 1 or > 100)
        {
            throw new AuditRetentionValidationException(new Dictionary<string, string[]> { ["Pagination"] = ["Use skip >= 0 and 1 <= take <= 100."] });
        }
        return await repository.GetRunsAsync(tenant, skip, take, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(Guid Tenant, Guid User)> RequireAdministratorAsync()
    {
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || !actor.IsTenantAdmin || actor.TenantId is null || actor.TenantId == Guid.Empty ||
            actor.SubjectIdAsGuid is null || actor.SubjectIdAsGuid == Guid.Empty)
        {
            await audit.LogPermissionDenyAsync(actor.SubjectIdAsGuid, "audit:retention-simulate", "AuditRetentionSimulation", null,
                "An authenticated tenant administrator with a tenant context is required.", actor.TenantId).ConfigureAwait(false);
            throw new UnauthorizedAccessException("A tenant administrator and tenant context are required.");
        }
        return (actor.TenantId.Value, actor.SubjectIdAsGuid.Value);
    }

    private Task AuditAsync(string action, Guid tenant, Guid user, Guid resource, object metadata) =>
        audit.LogAsync(new CreateAuditLogRequest
        {
            ActionType = action, ResourceType = "AuditRetentionSimulation", ResourceId = resource.ToString(),
            TenantId = tenant, UserId = user, Category = AuditCategory.Admin, RiskLevel = AuditRiskLevel.Medium,
            Success = true, Description = action, Metadata = metadata
        });

    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new InvalidOperationException("Stored retention simulation data is invalid.");
    private static AuditRetentionConfigurationResponse MapConfiguration(AuditRetentionConfiguration configuration) =>
        new(configuration.Id, configuration.TenantId!.Value, configuration.Revision, configuration.UpdatedByUserId,
            configuration.UpdatedAt, Deserialize<ConfigureAuditRetentionRequest>(configuration.ConfigurationJson));
    private static AuditRetentionSimulationResponse MapRun(AuditRetentionSimulationRun run) =>
        new(run.Id, run.TenantId!.Value, run.CreatedByUserId, run.CreatedAt, run.ConfigurationRevision,
            Deserialize<ConfigureAuditRetentionRequest>(run.ConfigurationJson), Deserialize<RunAuditRetentionSimulationRequest>(run.RequestJson),
            Deserialize<AuditRetentionSimulationReport>(run.ReportJson));
}
