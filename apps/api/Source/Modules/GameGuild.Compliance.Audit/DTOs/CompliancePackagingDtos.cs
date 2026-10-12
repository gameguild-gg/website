using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Compliance.Audit;

[JsonConverter(typeof(JsonStringEnumConverter<ComplianceEvidenceKind>))]
public enum ComplianceEvidenceKind { Operations, Authentication, Authorization, Incidents, Integrity, Retention }

[JsonConverter(typeof(JsonStringEnumConverter<ComplianceDocumentReview>))]
public enum ComplianceDocumentReview { Pending, Approved, Rejected }

[JsonConverter(typeof(JsonStringEnumConverter<ComplianceEvidencePeriodMode>))]
public enum ComplianceEvidencePeriodMode { PointInTime, Period }

/// <summary>References and collection mappings, not a reproduction of a licensed standard or a certification.</summary>
public sealed record ComplianceControlTemplate(
    string Id, string SourceUri, IReadOnlyList<ComplianceEvidenceKind> AutomaticEvidence,
    IReadOnlyList<string> RequiredDocumentTypes);

public sealed record ComplianceDocumentRequirement(
    string Type, IReadOnlyList<string> RequiredFields, bool RequiresPeriodCoverage, bool RequiresControlAssessments = false);

public sealed record ComplianceFrameworkTemplate(
    string Id, ComplianceFramework Framework, string Version, ComplianceEvidencePeriodMode PeriodMode,
    IReadOnlyList<string> Sources, IReadOnlyList<ComplianceControlTemplate> Controls,
    IReadOnlyList<ComplianceDocumentRequirement> Documents);

public sealed record UploadComplianceDocumentRequest
{
    [Required, MaxLength(100)] public string TemplateId { get; init; } = string.Empty;
    [Required, MaxLength(200)] public string Name { get; init; } = string.Empty;
    [Required, MaxLength(80)] public string Type { get; init; } = string.Empty;
    [Required, MaxLength(120)] public string MediaType { get; init; } = "application/json";
    [Required, MaxLength(1500000)] public string ContentBase64 { get; init; } = string.Empty;
    [Required, MaxLength(2048)] public string SourceUri { get; init; } = string.Empty;
    public DateTimeOffset ValidFromUtc { get; init; }
    public DateTimeOffset ValidUntilUtc { get; init; }
    [Required, MaxLength(1500)] public List<string> ControlIds { get; init; } = [];
    /// <summary>Declared structured fields for PDF/text evidence; JSON evidence uses fields from its actual contents.</summary>
    [Required] public Dictionary<string, string> ValidationFields { get; init; } = [];
}

public sealed record ReviewComplianceDocumentRequest
{
    [Range(1, int.MaxValue)] public int ExpectedRevision { get; init; }
    public ComplianceDocumentReview Decision { get; init; }
    [Required, MaxLength(2000)] public string Notes { get; init; } = string.Empty;
}

public sealed record ComplianceScopeExclusion
{
    [Required, MaxLength(100)] public string ControlId { get; init; } = string.Empty;
    [Required, MaxLength(2000)] public string Rationale { get; init; } = string.Empty;
    public Guid ApplicabilityDocumentId { get; init; }
}

public sealed record CreateCompliancePackageRequest
{
    [Required, MaxLength(200)] public string Name { get; init; } = string.Empty;
    [Required, MaxLength(100)] public string TemplateId { get; init; } = string.Empty;
    public DateTimeOffset PeriodStartUtc { get; init; }
    public DateTimeOffset PeriodEndUtc { get; init; }
    [Required, MaxLength(100)] public List<Guid> DocumentIds { get; init; } = [];
    [Required, MaxLength(1500)] public List<ComplianceScopeExclusion> Exclusions { get; init; } = [];
}

public sealed record CompliancePackagingListRequest
{
    [FromQuery(Name = "skip"), Range(0, int.MaxValue)] public int Skip { get; init; }
    [FromQuery(Name = "take"), Range(1, 100)] public int Take { get; init; } = 25;
}

public sealed record ComplianceDocumentSnapshot(
    Guid Id, string Name, string Type, string MediaType, string ContentSha256, byte[] Content,
    string SourceUri, DateTime ValidFromUtc, DateTime ValidUntilUtc, IReadOnlyList<string> ControlIds,
    IReadOnlyDictionary<string, string> ValidationFields, ComplianceDocumentReview Review,
    Guid UploadedByUserId, Guid? ReviewedByUserId, DateTime? ReviewedAtUtc, int Revision, string? TemplateId = null);

/// <summary>Document metadata only. Uploaded bytes are included in separately audited sealed package downloads.</summary>
public sealed record ComplianceDocumentResponse(
    Guid Id, string TemplateId, string Name, string Type, string MediaType, int ContentLength, string ContentSha256,
    string SourceUri, DateTime ValidFromUtc, DateTime ValidUntilUtc, IReadOnlyList<string> ControlIds,
    ComplianceDocumentReview Review, Guid UploadedByUserId, Guid? ReviewedByUserId,
    DateTime? ReviewedAtUtc, string? ReviewNotes, int Revision);

public sealed record CompliancePackageSummary(Guid Id, string Name, string TemplateId, Guid PreparedByUserId,
    DateTime CapturedAtUtc, DateTime PeriodStartUtc, DateTime PeriodEndUtc, bool ReadyForAuditorReview,
    int GapCount, int ArtifactLength, string ArtifactSha256, string SigningKeyId);

public sealed record CompliancePackageResponse(CompliancePackageSummary Summary,
    ComplianceArtifactManifest Manifest, ComplianceArtifactSeal Seal);

public sealed record CompliancePackageDownload(byte[] Content, string FileName, string Sha256);

public sealed class CompliancePackagingConcurrencyException() : Exception("The document revision changed. Reload it before reviewing.");
public sealed class CompliancePackagingIntegrityException() : Exception("The stored evidence package could not be verified.");
public sealed class CompliancePackagingSigningUnavailableException(Exception innerException)
    : Exception("A configured private audit signing key is required to prepare evidence packages.", innerException);

public sealed record ComplianceEvidenceDataset(
    ComplianceEvidenceKind Kind, string Source, byte[] Content, int RecordCount,
    DateTime? FirstObservedUtc, DateTime? LastObservedUtc, IReadOnlyList<DateOnly> ObservedDatesUtc,
    IReadOnlyList<string> ValidationErrors);

public sealed record ComplianceEvidenceGap(string ControlId, string Code, string Detail, Guid? DocumentId);
public sealed record ComplianceControlEvidenceResult(
    string ControlId, string Status, IReadOnlyList<string> EvidencePaths, IReadOnlyList<Guid> DocumentIds,
    IReadOnlyList<ComplianceEvidenceGap> Gaps);
public sealed record CompliancePackageValidationReport(
    bool ReadyForAuditorReview, IReadOnlyList<ComplianceControlEvidenceResult> Controls,
    IReadOnlyList<ComplianceEvidenceGap> Gaps, IReadOnlyList<string> Assumptions);

public sealed record ComplianceArtifactEntry(string Path, string MediaType, int Length, string Sha256);
public sealed record ComplianceArtifactManifest(
    string FormatVersion, Guid PackageId, Guid TenantId, Guid PreparedByUserId, DateTime CapturedAtUtc,
    string Name, DateTime PeriodStartUtc, DateTime PeriodEndUtc, string TemplateSha256,
    ComplianceFrameworkTemplate Template, IReadOnlyList<ComplianceArtifactEntry> Entries,
    CompliancePackageValidationReport Validation);
public sealed record ComplianceArtifactSeal(string Algorithm, string KeyId, string ManifestSha256, string Signature);
public sealed record CompliancePackageArtifact(
    byte[] ZipContent, string ArtifactSha256, ComplianceArtifactManifest Manifest, ComplianceArtifactSeal Seal);
public sealed record ComplianceArtifactVerification(bool IsValid, IReadOnlyList<string> Errors);

public sealed class CompliancePackagingValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("Compliance packaging input or evidence is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
