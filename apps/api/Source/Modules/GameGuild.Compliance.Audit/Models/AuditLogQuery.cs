namespace GameGuild.Compliance.Audit;

/// <summary>
/// Query parameters for retrieving audit logs
/// </summary>
public class AuditLogQuery
{
    public Guid? UserId { get; set; }

    public Guid? TenantId { get; set; }

    public string? ActionType { get; set; }

    public string? ResourceType { get; set; }

    public AuditCategory? Category { get; set; }

    public AuditRiskLevel? RiskLevel { get; set; }

    public bool? Success { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? IpAddress { get; set; }

    /// <summary>
    /// Free-text search over descriptions and serialized context/metadata, matched in
    /// the database (ILIKE). Null or empty means no filter.
    /// </summary>
    public string? SearchText { get; set; }

    public int Skip { get; set; } = 0;

    public int Take { get; set; } = 100;
}
