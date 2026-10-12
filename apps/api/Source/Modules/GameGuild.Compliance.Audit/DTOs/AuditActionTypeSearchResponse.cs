namespace GameGuild.Compliance.Audit;

public sealed class AuditActionTypeSearchResponse
{
    public AuditLogResponse Results { get; init; } = new();

    public IReadOnlyList<AuditActionTypeFrequency> Frequency { get; init; } = [];

    public IReadOnlyList<AuditActionTypeTrend> Trends { get; init; } = [];

    public IReadOnlyList<AuditRelatedAction> RelatedActions { get; init; } = [];
}

public sealed record AuditActionTypeFrequency(string ActionType, int EventCount);

public sealed record AuditActionTypeTrend(string ActionType, DateTime StartUtc, int EventCount);

public sealed record AuditRelatedAction(string ActionType, int CorrelationCount);

public sealed class AuditActionTypeSearchResult
{
    public IReadOnlyList<AuditLog> Logs { get; init; } = [];

    public int TotalCount { get; init; }

    public int Skip { get; init; }

    public int Take { get; init; }

    public IReadOnlyList<AuditActionTypeFrequency> Frequency { get; init; } = [];

    public IReadOnlyList<AuditActionTypeTrend> Trends { get; init; } = [];

    public IReadOnlyList<AuditRelatedAction> RelatedActions { get; init; } = [];
}

public sealed class AuditActionTypeExportResult
{
    public int TotalCount { get; init; }

    public IReadOnlyList<AuditLog> Logs { get; init; } = [];

    public bool ExceedsLimit { get; init; }
}

public sealed class AuditActionTypeTaxonomyResponse
{
    public IReadOnlyList<AuditActionTypeDescriptor> ActionTypes { get; init; } = [];

    public IReadOnlyList<AuditActionTypeGroupDescriptor> Groups { get; init; } = [];
}

public sealed record AuditActionTypeDescriptor(
    string ActionType,
    IReadOnlyList<string> CategoryPath,
    IReadOnlyList<string> Groups);

public sealed record AuditActionTypeGroupDescriptor(string Name, string Description);
