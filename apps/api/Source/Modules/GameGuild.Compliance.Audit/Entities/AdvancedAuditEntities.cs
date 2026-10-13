
namespace GameGuild.Compliance.Audit;

/// <summary>
/// Retention policy with simulation engine for storage cost forecasting and "what-if" analysis.
/// </summary>
public sealed class RetentionPolicySimulation : EntityBase
{
    public new Guid? TenantId { get; private set; }
    public string PolicyName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int RetentionDays { get; private set; }
    public string[] ApplicableEventTypes { get; private set; } = Array.Empty<string>();
    public bool IsActive { get; private set; }

    // Storage metrics
    public long CurrentStorageSizeBytes { get; private set; }
    public long EstimatedStorageSizeBytes { get; private set; }
    public double AverageGrowthRatePerDay { get; private set; }
    public long RecordCount { get; private set; }
    public long EstimatedRecordCount { get; private set; }

    // Cost calculation
    public decimal CostPerGbPerMonth { get; private set; }
    public decimal CurrentMonthlyCost { get; private set; }
    public decimal EstimatedMonthlyCost { get; private set; }
    public decimal ProjectedAnnualCost { get; private set; }

    // Forecasting
    public DateTime ForecastStartDate { get; private set; }
    public DateTime ForecastEndDate { get; private set; }
    public int ForecastDays { get; private set; }
    public string? ForecastModel { get; private set; }

    // Recommendations
    public string? RecommendedRetentionDays { get; private set; }
    public string? RecommendedActions { get; private set; }
    public decimal? PotentialSavings { get; private set; }

    private RetentionPolicySimulation() { }

    public static RetentionPolicySimulation Create(
        Guid tenantId,
        string policyName,
        int retentionDays,
        string[] applicableEventTypes,
        decimal costPerGbPerMonth)
    {
        return new RetentionPolicySimulation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PolicyName = policyName,
            RetentionDays = retentionDays,
            ApplicableEventTypes = applicableEventTypes,
            CostPerGbPerMonth = costPerGbPerMonth,
            IsActive = true
        };
    }

    public void UpdateStorageMetrics(long currentStorageBytes, long recordCount, double growthRate)
    {
        CurrentStorageSizeBytes = currentStorageBytes;
        RecordCount = recordCount;
        AverageGrowthRatePerDay = growthRate;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void CalculateForecast(int forecastDays)
    {
        ForecastDays = forecastDays;
        ForecastStartDate = SystemClock.UtcNow;
        ForecastEndDate = SystemClock.UtcNow.AddDays(forecastDays);

        // Linear growth projection
        EstimatedStorageSizeBytes = CurrentStorageSizeBytes + (long)(AverageGrowthRatePerDay * forecastDays);
        EstimatedRecordCount = RecordCount + (long)((RecordCount / (double)RetentionDays) * forecastDays);

        // Cost calculation
        CurrentMonthlyCost = (CurrentStorageSizeBytes / (decimal)(1024 * 1024 * 1024)) * CostPerGbPerMonth;
        EstimatedMonthlyCost = (EstimatedStorageSizeBytes / (decimal)(1024 * 1024 * 1024)) * CostPerGbPerMonth;
        ProjectedAnnualCost = EstimatedMonthlyCost * 12;

        UpdatedAt = SystemClock.UtcNow;
    }

    public void GenerateRecommendations()
    {
        var optimalRetentionDays = CalculateOptimalRetention();
        RecommendedRetentionDays = optimalRetentionDays.ToString();

        if (optimalRetentionDays < RetentionDays)
        {
            var savingsPercentage = ((RetentionDays - optimalRetentionDays) / (double)RetentionDays) * 100;
            PotentialSavings = CurrentMonthlyCost * (decimal)(savingsPercentage / 100);
            RecommendedActions = $"Reduce retention from {RetentionDays} to {optimalRetentionDays} days to save {savingsPercentage:F1}%";
        }
        else
        {
            RecommendedActions = $"Current retention policy of {RetentionDays} days is optimal";
        }

        UpdatedAt = SystemClock.UtcNow;
    }

    private int CalculateOptimalRetention()
    {
        // Simple heuristic: balance between compliance requirements (90 days minimum) and cost
        if (RetentionDays <= 90) return RetentionDays;
        if (AverageGrowthRatePerDay > 1000000) return 90; // High growth, recommend minimum
        if (CurrentMonthlyCost > 1000) return Math.Max(90, RetentionDays / 2); // High cost, recommend reduction
        return RetentionDays;
    }
}

/// <summary>
/// Saved audit query for reuse and role-based access.
/// </summary>
public sealed class SavedAuditQuery : EntityBase
{
    public new Guid? TenantId { get; private set; }
    public string QueryName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string QueryDsl { get; private set; } = string.Empty;
    public bool IsPublic { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    // Access control
    public string[] AllowedRoles { get; private set; } = Array.Empty<string>();
    public Guid[] AllowedUserIds { get; private set; } = Array.Empty<Guid>();

    // Change tracking: bumped on every update, mirrored into SavedAuditQueryRevision rows.
    public int CurrentRevision { get; private set; } = 1;

    // Usage tracking
    public int ExecutionCount { get; private set; }
    public DateTime? LastExecutedAt { get; private set; }
    public Guid? LastExecutedByUserId { get; private set; }

    private SavedAuditQuery() { }

    public static SavedAuditQuery Create(
        Guid tenantId,
        string queryName,
        string queryDsl,
        Guid createdByUserId,
        bool isPublic = false,
        string? description = null)
    {
        return new SavedAuditQuery
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            QueryName = queryName,
            Description = description,
            QueryDsl = queryDsl,
            CreatedByUserId = createdByUserId,
            IsPublic = isPublic,
            CurrentRevision = 1
        };
    }

    /// <summary>
    /// Apply an update and advance the revision counter. Returns the new revision number so the
    /// caller can persist the matching <see cref="SavedAuditQueryRevision"/> snapshot.
    /// </summary>
    public int UpdateDetails(string queryName, string? description, string queryDsl, bool isPublic)
    {
        QueryName = queryName;
        Description = description;
        QueryDsl = queryDsl;
        IsPublic = isPublic;
        CurrentRevision++;
        UpdatedAt = SystemClock.UtcNow;
        return CurrentRevision;
    }

    public void RecordExecution(Guid executedByUserId)
    {
        ExecutionCount++;
        LastExecutedAt = SystemClock.UtcNow;
        LastExecutedByUserId = executedByUserId;
        UpdatedAt = SystemClock.UtcNow;
    }
}

/// <summary>
/// Audit stream replay session for incident investigation.
/// </summary>
public sealed class AuditReplaySession : EntityBase
{
    public new Guid? TenantId { get; private set; }
    public string SessionName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public ReplayStatus Status { get; private set; }

    // Replay filters
    public DateTime ReplayStartTime { get; private set; }
    public DateTime ReplayEndTime { get; private set; }
    public Guid? FilterByUserId { get; private set; }
    public Guid? FilterByEntityId { get; private set; }
    public string? FilterByEntityType { get; private set; }
    public string[] FilterByActions { get; private set; } = Array.Empty<string>();

    // Replay results
    public int TotalEventsReplayed { get; private set; }
    public int StateSnapshotsCreated { get; private set; }
    public string? TimelineVisualizationUrl { get; private set; }
    public string? FinalStateJson { get; private set; }

    // Investigation
    public string? IncidentReference { get; private set; }
    public string? Findings { get; private set; }

    private AuditReplaySession() { }

    public static AuditReplaySession Create(
        Guid tenantId,
        string sessionName,
        DateTime startTime,
        DateTime endTime,
        Guid createdByUserId)
    {
        return new AuditReplaySession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionName = sessionName,
            ReplayStartTime = startTime,
            ReplayEndTime = endTime,
            CreatedByUserId = createdByUserId,
            Status = ReplayStatus.Created
        };
    }

    public void StartReplay()
    {
        Status = ReplayStatus.InProgress;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void CompleteReplay(int totalEvents, int snapshots, string? timelineUrl, string? finalStateJson)
    {
        Status = ReplayStatus.Completed;
        TotalEventsReplayed = totalEvents;
        StateSnapshotsCreated = snapshots;
        TimelineVisualizationUrl = timelineUrl;
        FinalStateJson = finalStateJson;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void RecordFindings(string findings)
    {
        Findings = findings;
        UpdatedAt = SystemClock.UtcNow;
    }
}

public enum ReplayStatus
{
    Created = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
