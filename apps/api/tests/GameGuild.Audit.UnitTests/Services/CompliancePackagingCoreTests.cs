using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameGuild.Compliance.Audit;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class CompliancePackagingCoreTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddDays(1);
    private static readonly Guid TenantId = Guid.Parse("88888888-8888-4888-8888-888888888888");
    private static readonly Guid UserId = Guid.Parse("99999999-9999-4999-8999-999999999999");

    [Fact]
    public void Build_includes_verifiable_manifest_index_documents_and_validation()
    {
        var builder = CreateBuilder();
        var document = Document();
        var artifact = Build(builder, document);
        artifact.Manifest.Validation.ReadyForAuditorReview.Should().BeTrue();
        builder.Verify(artifact.ZipContent, TenantId, artifact.Manifest.PackageId).IsValid.Should().BeTrue();
        Convert.ToHexString(SHA256.HashData(artifact.ZipContent)).ToLowerInvariant().Should().Be(artifact.ArtifactSha256);
        using var zip = new ZipArchive(new MemoryStream(artifact.ZipContent), ZipArchiveMode.Read);
        zip.GetEntry("index.csv").Should().NotBeNull();
        zip.GetEntry($"documents/{document.Id:D}/content.json").Should().NotBeNull();
        zip.GetEntry("validation.json").Should().NotBeNull();
        artifact.Manifest.Validation.Assumptions.Should().Contain(value => value.Contains("certification", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("template.json")]
    [InlineData("validation.json")]
    [InlineData("index.csv")]
    [InlineData("evidence/operations.json")]
    public void Verify_detects_changed_evidence(string path)
    {
        var builder = CreateBuilder();
        var artifact = Build(builder, Document());
        var modified = Rewrite(artifact.ZipContent, (name, bytes) => name == path ? Encoding.UTF8.GetBytes("changed") : bytes);
        builder.Verify(modified, TenantId, artifact.Manifest.PackageId).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("manifest.json")]
    [InlineData("seal.json")]
    public void Verify_detects_changes_to_seal_or_manifest(string path)
    {
        var builder = CreateBuilder();
        var artifact = Build(builder, Document());
        var modified = Rewrite(artifact.ZipContent, (name, bytes) => name == path ? Encoding.UTF8.GetBytes("{}") : bytes);
        builder.Verify(modified, TenantId, artifact.Manifest.PackageId).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("extra.txt")]
    [InlineData("../escape.txt")]
    [InlineData("C:/absolute.txt")]
    [InlineData("evidence\\operations.json")]
    [InlineData("manifest.json")]
    public void Verify_rejects_extra_unsafe_or_duplicate_entries(string path)
    {
        var builder = CreateBuilder();
        var artifact = Build(builder, Document());
        var modified = Rewrite(artifact.ZipContent, (_, bytes) => bytes, path);
        builder.Verify(modified, TenantId, artifact.Manifest.PackageId).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Verify_binds_tenant_and_package_identity_and_uses_only_trusted_keys()
    {
        var builder = CreateBuilder();
        var artifact = Build(builder, Document());
        builder.Verify(artifact.ZipContent, Guid.NewGuid(), artifact.Manifest.PackageId).IsValid.Should().BeFalse();
        builder.Verify(artifact.ZipContent, TenantId, Guid.NewGuid()).IsValid.Should().BeFalse();
        CreateBuilder().Verify(artifact.ZipContent, TenantId, artifact.Manifest.PackageId).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Build_fails_when_private_signing_key_is_unavailable()
    {
        var builder = new ComplianceArtifactBuilder(new ComplianceEvidenceValidationEngine(),
            new EcdsaCryptographicSigningService(Options.Create(new AuditSigningOptions())));
        Action build = () => Build(builder, Document());
        build.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Verify_rejects_malformed_and_oversized_archives()
    {
        var builder = CreateBuilder();
        builder.Verify([1, 2, 3], TenantId, Guid.NewGuid()).IsValid.Should().BeFalse();
        builder.Verify(new byte[ComplianceArtifactBuilder.MaximumArchiveBytes + 1], TenantId, Guid.NewGuid()).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Verify_rejects_a_highly_compressed_oversized_entry()
    {
        var builder = CreateBuilder();
        var artifact = Build(builder, Document());
        using var bytes = new MemoryStream();
        bytes.Write(artifact.ZipContent);
        using (var archive = new ZipArchive(bytes, ZipArchiveMode.Update, true))
        {
            using var entry = archive.CreateEntry("bomb.json", CompressionLevel.SmallestSize).Open();
            entry.Write(new byte[ComplianceEvidenceValidationEngine.MaximumDatasetBytes + 1]);
        }
        var compressed = bytes.ToArray();
        compressed.Length.Should().BeLessThan(100000);
        builder.Verify(compressed, TenantId, artifact.Manifest.PackageId).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Verify_rejects_duplicate_json_properties_in_seal()
    {
        var builder = CreateBuilder();
        var artifact = Build(builder, Document());
        var modified = Rewrite(artifact.ZipContent, (name, bytes) => name == "seal.json"
            ? Encoding.UTF8.GetBytes("{\"algorithm\":\"ECDSA-SHA256-P1363\",\"algorithm\":\"other\"}") : bytes);
        builder.Verify(modified, TenantId, artifact.Manifest.PackageId).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Build_rejects_oversized_documents_before_sealing()
    {
        var builder = CreateBuilder();
        var document = Document() with { Content = new byte[ComplianceEvidenceValidationEngine.MaximumDocumentBytes + 1] };
        Action build = () => Build(builder, document);
        build.Should().Throw<CompliancePackagingValidationException>();
    }

    [Fact]
    public void Evidence_validation_reports_pending_rejected_or_changed_documents()
    {
        var engine = new ComplianceEvidenceValidationEngine();
        var pending = Document() with { Review = ComplianceDocumentReview.Pending, ReviewedByUserId = null, ReviewedAtUtc = null };
        Inspect(engine, pending).Gaps.Should().Contain(gap => gap.Code == "DocumentNotApproved");
        var rejected = Document() with { Review = ComplianceDocumentReview.Rejected };
        Inspect(engine, rejected).ReadyForAuditorReview.Should().BeFalse();
        var changed = Document() with { Content = Encoding.UTF8.GetBytes("changed") };
        Inspect(engine, changed).Gaps.Should().Contain(gap => gap.Code == "DocumentHashMismatch");
    }

    [Fact]
    public void Json_document_validation_uses_actual_content_instead_of_declared_fields()
    {
        var bytes = Encoding.UTF8.GetBytes("{\"frameworkVersion\":\"v1\",\"assessmentStatus\":\"satisfactory\"}");
        var document = Document() with
        {
            Content = bytes, ContentSha256 = Hash(bytes),
            ValidationFields = new Dictionary<string, string> { ["owner"] = "pretend-owner" }
        };
        Inspect(new ComplianceEvidenceValidationEngine(), document).Gaps.Should().Contain(gap => gap.Code == "DocumentFieldMissing");
    }

    [Fact]
    public void Boolean_fields_cannot_substitute_for_required_assessment_content()
    {
        var bytes = Encoding.UTF8.GetBytes("{\"frameworkVersion\":\"v1\",\"assessmentStatus\":\"satisfactory\",\"owner\":false}");
        var document = Document() with { Content = bytes, ContentSha256 = Hash(bytes) };
        Inspect(new ComplianceEvidenceValidationEngine(), document).Gaps.Should().Contain(gap => gap.Code == "DocumentFieldMissing");
    }

    [Fact]
    public void Source_timestamps_cannot_be_invented_or_moved_outside_the_period()
    {
        var source = Dataset() with { FirstObservedUtc = Start.AddDays(-10) };
        Inspect(new ComplianceEvidenceValidationEngine(), Document(), [source]).Gaps.Should().Contain(gap => gap.Code == "SourceTimelineMismatch");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new[] { new { observedAtUtc = Start.AddDays(-1) } });
        var outside = Dataset() with
        {
            Content = bytes, RecordCount = 1, FirstObservedUtc = Start.AddDays(-1), LastObservedUtc = Start.AddDays(-1),
            ObservedDatesUtc = [DateOnly.FromDateTime(Start.AddDays(-1))]
        };
        Inspect(new ComplianceEvidenceValidationEngine(), Document(), [outside]).Gaps.Should().Contain(gap => gap.Code == "SourceOutsidePeriod");
    }

    [Fact]
    public void Deficient_or_wrong_version_assessments_block_readiness()
    {
        var document = Document("deficient");
        Inspect(new ComplianceEvidenceValidationEngine(), document).Gaps.Should().Contain(gap => gap.Code == "DeclaredControlDeficiency");
        var oldVersion = Document(version: "v0");
        Inspect(new ComplianceEvidenceValidationEngine(), oldVersion).Gaps.Should().Contain(gap => gap.Code == "FrameworkVersionMismatch");
    }

    [Fact]
    public void Period_validation_reports_missing_days_and_short_document_coverage()
    {
        var engine = new ComplianceEvidenceValidationEngine();
        var datasets = new[] { Dataset() with { ObservedDatesUtc = [DateOnly.FromDateTime(Start)] } };
        Inspect(engine, Document(), datasets).Gaps.Should().Contain(gap => gap.Code == "TimelineGap");
        Inspect(engine, Document() with { ValidUntilUtc = Start }).Gaps.Should().Contain(gap => gap.Code == "DocumentPeriodGap");
    }

    [Fact]
    public void Source_collection_errors_block_readiness_and_survive_signed_artifact()
    {
        var builder = CreateBuilder();
        var data = Dataset() with { ValidationErrors = ["Snapshot was truncated at the collection limit."] };
        var artifact = Build(builder, Document(), [data]);
        artifact.Manifest.Validation.ReadyForAuditorReview.Should().BeFalse();
        artifact.Manifest.Validation.Gaps.Should().Contain(gap => gap.Code == "SourceValidationFailed");
        builder.Verify(artifact.ZipContent, TenantId, artifact.Manifest.PackageId).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Exclusion_requires_an_approved_applicability_document_for_the_control()
    {
        var document = Document();
        var request = Request(document) with
        {
            Exclusions = [new ComplianceScopeExclusion { ControlId = "C1", Rationale = "Not applicable", ApplicabilityDocumentId = document.Id }]
        };
        var report = new ComplianceEvidenceValidationEngine().Inspect(Template(), request, [document], [Dataset()], End.AddDays(1));
        report.Gaps.Should().Contain(gap => gap.Code == "InvalidScopeExclusion");
    }

    [Fact]
    public void Reviewed_exclusion_is_reported_explicitly_and_does_not_require_event_rows()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { frameworkVersion = "v1", assessmentStatus = "satisfactory", rationale = "Service does not process this category" });
        var document = Document() with { Type = "applicability", Content = bytes, ContentSha256 = Hash(bytes) };
        var request = Request(document) with
        {
            Exclusions = [new ComplianceScopeExclusion { ControlId = "C1", Rationale = "Outside scope", ApplicabilityDocumentId = document.Id }]
        };
        var report = new ComplianceEvidenceValidationEngine().Inspect(Template(), request, [document], [], End.AddDays(1));
        report.ReadyForAuditorReview.Should().BeTrue();
        report.Controls.Single().Status.Should().Be("ReviewedExclusion");
    }

    [Fact]
    public void Duplicate_json_fields_and_inconsistent_source_counts_are_rejected()
    {
        var bytes = Encoding.UTF8.GetBytes("{\"owner\":\"a\",\"owner\":\"b\",\"frameworkVersion\":\"v1\",\"assessmentStatus\":\"satisfactory\"}");
        var document = Document() with { Content = bytes, ContentSha256 = Hash(bytes) };
        Inspect(new ComplianceEvidenceValidationEngine(), document).Gaps.Should().Contain(gap => gap.Code == "InvalidDocumentJson");
        Inspect(new ComplianceEvidenceValidationEngine(), Document(), [Dataset() with { RecordCount = 1 }])
            .Gaps.Should().Contain(gap => gap.Code == "SourceCountMismatch");
    }

    [Fact]
    public void Missing_sources_and_documents_are_reported_as_gaps()
    {
        var request = Request(Document()) with { DocumentIds = [] };
        var report = new ComplianceEvidenceValidationEngine().Inspect(Template(), request, [], [], End.AddDays(1));
        report.ReadyForAuditorReview.Should().BeFalse();
        report.Gaps.Should().Contain(gap => gap.Code == "MissingAutomaticEvidence");
        report.Gaps.Should().Contain(gap => gap.Code == "MissingDocument");
    }

    [Fact]
    public void No_incident_rows_are_distinguished_from_missing_operational_evidence()
    {
        var template = Template() with
        {
            Controls = [new("C1", "https://example.com/standard#C1", [ComplianceEvidenceKind.Incidents], ["assessment"])]
        };
        var document = Document();
        var incidents = new ComplianceEvidenceDataset(ComplianceEvidenceKind.Incidents, "AuditLogs", Encoding.UTF8.GetBytes("[]"), 0, null, null, [], []);
        new ComplianceEvidenceValidationEngine().Inspect(template, Request(document), [document], [incidents], End.AddDays(1))
            .ReadyForAuditorReview.Should().BeTrue();
        Inspect(new ComplianceEvidenceValidationEngine(), document,
            [incidents with { Kind = ComplianceEvidenceKind.Operations }]).Gaps.Should().Contain(gap => gap.Code == "NoObservedEvidence");
    }

    [Fact]
    public void Point_in_time_templates_reject_period_requests()
    {
        var document = Document();
        var template = Template() with { PeriodMode = ComplianceEvidencePeriodMode.PointInTime };
        Action validate = () => new ComplianceEvidenceValidationEngine().Inspect(template, Request(document), [document], [Dataset()], End.AddDays(1));
        validate.Should().Throw<CompliancePackagingValidationException>();
    }

    [Fact]
    public void Structured_control_assessments_report_deficiencies_only_on_the_affected_control()
    {
        var template = Template() with
        {
            Controls = [new("C1", "https://example.com/standard", [ComplianceEvidenceKind.Operations], ["assessment"]),
                new("C2", "https://example.com/standard", [ComplianceEvidenceKind.Operations], ["assessment"])],
            Documents = [new("assessment", ["frameworkVersion", "assessmentStatus", "owner", "controlAssessments"], true, true)]
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            frameworkVersion = "v1", assessmentStatus = "satisfactory", owner = "Assessor",
            controlAssessments = new Dictionary<string, object>
            {
                ["C1"] = new { owner = "Assessor", controlImplementation = "Implemented", effectivenessEvidence = "Reviewed", assessmentStatus = "satisfactory" },
                ["C2"] = new { owner = "Assessor", controlImplementation = "Partial", effectivenessEvidence = "Missing safeguard", assessmentStatus = "deficient" }
            }
        });
        var document = Document() with { Content = bytes, ContentSha256 = Hash(bytes), ControlIds = ["C1", "C2"] };
        var report = new ComplianceEvidenceValidationEngine().Inspect(template, Request(document), [document], [Dataset()], End.AddDays(1));
        report.ReadyForAuditorReview.Should().BeFalse();
        report.Controls.Single(item => item.ControlId == "C1").Gaps.Should().BeEmpty();
        report.Controls.Single(item => item.ControlId == "C2").Gaps.Should().Contain(item => item.Code == "DeclaredControlDeficiency");
    }

    [Fact]
    public void Cancellation_does_not_return_a_successful_artifact_or_verification()
    {
        var builder = CreateBuilder();
        var document = Document();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action build = () => builder.Build(Guid.NewGuid(), TenantId, UserId, End.AddDays(1), Template(), Request(document), [document], [Dataset()], cancellation.Token);
        build.Should().Throw<OperationCanceledException>();
        Action verify = () => builder.Verify([1], TenantId, Guid.NewGuid(), cancellation.Token);
        verify.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Input_validation_rejects_unknown_controls_duplicate_documents_and_future_periods()
    {
        var engine = new ComplianceEvidenceValidationEngine();
        var document = Document();
        Action unknown = () => engine.Inspect(Template(), Request(document), [document with { ControlIds = ["UNKNOWN"] }], [Dataset()], End.AddDays(1));
        unknown.Should().Throw<CompliancePackagingValidationException>();
        Action duplicate = () => engine.Inspect(Template(), Request(document), [document, document], [Dataset()], End.AddDays(1));
        duplicate.Should().Throw<CompliancePackagingValidationException>();
        Action future = () => engine.Inspect(Template(), Request(document), [document], [Dataset()], Start);
        future.Should().Throw<CompliancePackagingValidationException>();
    }

    private static CompliancePackageValidationReport Inspect(ComplianceEvidenceValidationEngine engine,
        ComplianceDocumentSnapshot document, IReadOnlyList<ComplianceEvidenceDataset>? datasets = null) =>
        engine.Inspect(Template(), Request(document), [document], datasets ?? [Dataset()], End.AddDays(1));

    private static CompliancePackageArtifact Build(ComplianceArtifactBuilder builder,
        ComplianceDocumentSnapshot document, IReadOnlyList<ComplianceEvidenceDataset>? datasets = null) =>
        builder.Build(Guid.NewGuid(), TenantId, UserId, End.AddDays(1), Template(), Request(document), [document], datasets ?? [Dataset()]);

    private static ComplianceArtifactBuilder CreateBuilder()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = new AuditSigningOptions { ActiveKeyId = "test-key" };
        options.Keys["test-key"] = new AuditSigningKeyOptions { PrivateKeyPem = key.ExportECPrivateKeyPem() };
        return new ComplianceArtifactBuilder(new ComplianceEvidenceValidationEngine(),
            new EcdsaCryptographicSigningService(Options.Create(options)));
    }

    private static ComplianceFrameworkTemplate Template() => new("test-v1", ComplianceFramework.Custom, "v1",
        ComplianceEvidencePeriodMode.Period, ["https://example.com/standard"],
        [new("C1", "https://example.com/standard#C1", [ComplianceEvidenceKind.Operations], ["assessment"])],
        [new("assessment", ["frameworkVersion", "assessmentStatus", "owner"], true)]);

    private static CreateCompliancePackageRequest Request(ComplianceDocumentSnapshot document) => new()
    {
        Name = "Example package", TemplateId = "test-v1", PeriodStartUtc = Start, PeriodEndUtc = End,
        DocumentIds = [document.Id]
    };

    private static ComplianceDocumentSnapshot Document(string status = "satisfactory", string version = "v1")
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { frameworkVersion = version, assessmentStatus = status, owner = "Security officer" });
        return new(Guid.NewGuid(), "Assessment", "assessment", "application/json", Hash(bytes), bytes,
            "https://example.com/assessment", Start, End, ["C1"], new Dictionary<string, string>(),
            ComplianceDocumentReview.Approved, UserId, Guid.NewGuid(), End, 2);
    }

    private static ComplianceEvidenceDataset Dataset() => new(ComplianceEvidenceKind.Operations, "AuditLogs",
        JsonSerializer.SerializeToUtf8Bytes(new[] { new { observedAtUtc = Start }, new { observedAtUtc = End } }), 2, Start, End,
        [DateOnly.FromDateTime(Start), DateOnly.FromDateTime(End)], []);

    private static string Hash(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static byte[] Rewrite(byte[] original, Func<string, byte[], byte[]> change, string? extra = null)
    {
        using var source = new ZipArchive(new MemoryStream(original), ZipArchiveMode.Read);
        using var output = new MemoryStream();
        using (var destination = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var entry in source.Entries)
            {
                using var input = entry.Open();
                using var bytes = new MemoryStream();
                input.CopyTo(bytes);
                using var target = destination.CreateEntry(entry.FullName).Open();
                target.Write(change(entry.FullName, bytes.ToArray()));
            }
            if (extra is not null)
            {
                using var target = destination.CreateEntry(extra).Open();
                target.Write(Encoding.UTF8.GetBytes("unlisted"));
            }
        }
        return output.ToArray();
    }
}
