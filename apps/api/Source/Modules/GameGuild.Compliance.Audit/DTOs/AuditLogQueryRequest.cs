namespace GameGuild.Compliance.Audit;

public class AuditLogQueryRequest
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
    /// Optional free-text search over descriptions and serialized context/metadata
    /// (case-insensitive, max 200 characters, empty means no filter).
    /// </summary>
    public string? SearchText { get; set; }

    public int Skip { get; set; } = 0;

    public int Take { get; set; } = 100;
}
