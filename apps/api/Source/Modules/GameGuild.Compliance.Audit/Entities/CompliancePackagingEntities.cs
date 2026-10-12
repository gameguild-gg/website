namespace GameGuild.Compliance.Audit;

/// <summary>Immutable uploaded content and metadata; only review fields advance through the service.</summary>
public sealed class ComplianceEvidenceDocument : EntityBase
{
    public string TemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
    public string ContentSha256 { get; set; } = string.Empty;
    public string SourceUri { get; set; } = string.Empty;
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidUntilUtc { get; set; }
    public string ControlIdsJson { get; set; } = "[]";
    public string ValidationFieldsJson { get; set; } = "{}";
    public Guid UploadedByUserId { get; set; }
    public ComplianceDocumentReview Review { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNotes { get; set; }
    public int Revision { get; set; } = 1;
}

/// <summary>Append-only signed capture. Later document reviews never update its ZIP or manifests.</summary>
public sealed class ComplianceSealedPackage : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public Guid PreparedByUserId { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public bool ReadyForAuditorReview { get; set; }
    public int GapCount { get; set; }
    public int ArtifactLength { get; set; }
    public string ArtifactSha256 { get; set; } = string.Empty;
    public string SigningKeyId { get; set; } = string.Empty;
    public byte[] ArtifactContent { get; set; } = [];
    public string ManifestJson { get; set; } = string.Empty;
    public string SealJson { get; set; } = string.Empty;
}
