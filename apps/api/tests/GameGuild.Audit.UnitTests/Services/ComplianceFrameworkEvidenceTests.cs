using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GameGuild.Compliance.Audit;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class ComplianceFrameworkEvidenceTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddDays(1);

    [Fact]
    public void Iso_management_clauses_cannot_be_excluded()
    {
        var scenario = Iso();
        scenario.Request.Exclusions.Add(new() { ControlId = "ISMS.4", Rationale = "Omit context", ApplicabilityDocumentId = scenario.Documents[0].Id });
        Assert.Throws<CompliancePackagingValidationException>(() => Inspect(scenario));
    }

    [Fact]
    public void SoA_requires_all_Annex_A_decisions_and_records_custom_necessary_controls()
    {
        var scenario = Iso();
        Assert.True(Inspect(scenario).ReadyForAuditorReview);
        scenario.Change("iso-soa", body => body["annexADecisions"]!.AsObject().Remove("A.5.1"));
        Assert.Contains(Inspect(scenario).Gaps, gap => gap.Code == "SoADecisionMissing" && gap.ControlId == "A.5.1");
    }

    [Fact]
    public void SoA_exclusion_must_match_the_requested_scope_and_reviewed_reason()
    {
        var scenario = Iso();
        scenario.Change("iso-soa", body => body["annexADecisions"]!["A.5.1"] = new JsonObject
        {
            ["applicable"] = false, ["exclusionJustification"] = "Covered by custom control", ["necessaryControlIds"] = new JsonArray("CUSTOM.1")
        });
        Assert.Contains(Inspect(scenario).Gaps, gap => gap.Code == "SoAScopeMismatch");
        var soa = scenario.Documents.Single(item => item.Type == "iso-soa");
        scenario.Request.Exclusions.Add(new() { ControlId = "A.5.1", Rationale = "Covered by custom control", ApplicabilityDocumentId = soa.Id });
        var report = Inspect(scenario);
        Assert.True(report.ReadyForAuditorReview, JsonSerializer.Serialize(report.Gaps));
        Assert.Equal("ReviewedExclusion", report.Controls.Single(item => item.ControlId == "A.5.1").Status);
    }

    [Fact]
    public void Unimplemented_custom_controls_remain_visible_as_deficiencies()
    {
        var scenario = Iso();
        scenario.Change("iso-soa", body => body["necessaryControls"]!.AsArray().Last()!["implementationStatus"] = "planned");
        Assert.Contains(Inspect(scenario).Gaps, gap => gap.Code == "ControlImplementationPending" && gap.ControlId == "ISMS.6");
    }

    [Fact]
    public void Planned_necessary_controls_mark_their_mapped_Annex_A_entries_as_deficient()
    {
        var scenario = Iso();
        scenario.Change("iso-soa", body => body["necessaryControls"]![0]!["implementationStatus"] = "planned");
        var report = Inspect(scenario);
        Assert.Contains(report.Gaps, gap => gap.Code == "ControlImplementationPending" && gap.ControlId == "A.5.1");
        Assert.Equal("EvidenceGap", report.Controls.Single(item => item.ControlId == "A.5.1").Status);
    }

    [Fact]
    public void Required_DPIA_cannot_be_satisfied_by_a_screening_without_the_assessment()
    {
        var scenario = Gdpr();
        Assert.True(Inspect(scenario).ReadyForAuditorReview);
        scenario.Documents.RemoveAll(item => item.Type == "gdpr-dpia");
        scenario.RefreshRequest();
        Assert.Contains(Inspect(scenario).Gaps, gap => gap.Code == "MissingDPIA" && gap.ControlId == "GDPR.Art.35");
    }

    [Theory]
    [InlineData("processingDescription")]
    [InlineData("processingPurposes")]
    [InlineData("necessityProportionality")]
    [InlineData("individualRightsRisks")]
    [InlineData("mitigations")]
    public void DPIA_requires_its_actual_documented_parts(string field)
    {
        var scenario = Gdpr();
        scenario.Change("gdpr-dpia", body => body.Remove(field));
        Assert.False(Inspect(scenario).ReadyForAuditorReview);
    }

    [Fact]
    public void Residual_high_risk_requires_completed_prior_consultation()
    {
        var scenario = Gdpr();
        scenario.Change("gdpr-dpia", body => body["residualRiskDecision"]!["level"] = "high");
        Assert.Contains(Inspect(scenario).Gaps, gap => gap.Code == "PriorConsultationMissing" && gap.ControlId == "GDPR.Art.36");
    }

    [Fact]
    public void Assessment_after_the_declared_processing_start_is_a_timeline_gap()
    {
        var scenario = Gdpr();
        scenario.Change("gdpr-dpia", body => body["assessmentTiming"]!["assessedAtUtc"] = End.ToString("O"));
        Assert.Contains(Inspect(scenario).Gaps, gap => gap.Code == "DPIATimingGap");
    }

    [Fact]
    public void Every_scoped_processing_activity_requires_a_reviewed_screening()
    {
        var scenario = Gdpr();
        scenario.Change("gdpr-accountability", body => body["processingScope"]!["activityIds"]!.AsArray().Add("ACT-2"));
        Assert.Contains(Inspect(scenario).Gaps, gap => gap.Code == "DPIAScreeningMissing");
    }

    [Theory]
    [InlineData("ISO")]
    [InlineData("GDPR")]
    public void Native_scope_documents_require_actual_JSON_and_cannot_use_declared_PDF_fields(string framework)
    {
        var scenario = framework == "ISO" ? Iso() : Gdpr();
        var type = framework == "ISO" ? "iso-soa" : "gdpr-accountability";
        var index = scenario.Documents.FindIndex(item => item.Type == type);
        var document = scenario.Documents[index];
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.7 synthetic declaration");
        scenario.Documents[index] = document with { MediaType = "application/pdf", Content = bytes, ContentSha256 = Hash(bytes) };
        Assert.Contains(Inspect(scenario).Gaps, gap => gap.Code == "StructuredDocumentRequired");
    }

    [Fact]
    public void SoA_unknown_references_duplicate_necessary_controls_and_unresolved_decisions_remain_gaps()
    {
        var scenario = Iso();
        scenario.Change("iso-soa", body =>
        {
            body["necessaryControls"]![0]!["annexAReferences"] = new JsonArray("A.99.1");
            body["necessaryControls"]!.AsArray().Add(body["necessaryControls"]![0]!.DeepClone());
            body["annexADecisions"]!["A.5.1"]!["necessaryControlIds"] = new JsonArray("UNKNOWN");
        });
        var report = Inspect(scenario);
        Assert.Contains(report.Gaps, gap => gap.Code == "NecessaryControlsInvalid");
        Assert.Contains(report.Gaps, gap => gap.Code == "NecessaryControlReferencesInvalid");
        Assert.Contains(report.Gaps, gap => gap.Code == "SoAControlReferenceInvalid");
    }

    [Fact]
    public void Native_registers_cannot_capture_two_conflicting_scope_revisions()
    {
        var scenario = Gdpr();
        var scope = scenario.Documents.Single(item => item.Type == "gdpr-accountability");
        scenario.Documents.Add(scope with { Id = Guid.NewGuid() });
        scenario.RefreshRequest();
        Assert.Throws<CompliancePackagingValidationException>(() => Inspect(scenario));
    }

    [Fact]
    public void Document_approval_and_period_coverage_are_required_for_conditional_DPIAs()
    {
        var scenario = Gdpr();
        var index = scenario.Documents.FindIndex(item => item.Type == "gdpr-dpia");
        scenario.Documents[index] = scenario.Documents[index] with { Review = ComplianceDocumentReview.Pending, ValidUntilUtc = Start };
        var report = Inspect(scenario);
        Assert.Contains(report.Gaps, gap => gap.Code == "MissingDPIA");
        Assert.Contains(report.Gaps, gap => gap.Code == "DocumentNotApproved");
        Assert.Contains(report.Gaps, gap => gap.Code == "DocumentPeriodGap");
    }

    [Fact]
    public void A_reviewed_screening_can_document_that_no_DPIA_is_required()
    {
        var scenario = Gdpr();
        scenario.Change("gdpr-dpia-screening", body => body["screenings"]![0]!["dpiaRequired"] = false);
        scenario.Documents.RemoveAll(item => item.Type == "gdpr-dpia");
        scenario.RefreshRequest();
        Assert.True(Inspect(scenario).ReadyForAuditorReview);
    }

    [Fact]
    public void String_booleans_and_out_of_scope_DPIAs_are_not_accepted_as_screening_decisions()
    {
        var scenario = Gdpr();
        scenario.Change("gdpr-dpia-screening", body => body["screenings"]![0]!["dpiaRequired"] = "false");
        scenario.Change("gdpr-dpia", body => body["activityId"] = "UNKNOWN");
        var report = Inspect(scenario);
        Assert.Contains(report.Gaps, gap => gap.Code == "DPIAScreeningInvalid");
        Assert.Contains(report.Gaps, gap => gap.Code == "ProcessingScopeMismatch");
    }

    [Theory]
    [InlineData("controller", "purposes")]
    [InlineData("processor", "controllers")]
    public void Processing_records_require_fields_for_the_declared_role(string role, string missing)
    {
        var scenario = Gdpr();
        scenario.Change("gdpr-accountability", body => body["processingScope"]!["roles"] = new JsonArray(role));
        scenario.Change("gdpr-processing-records", body =>
        {
            body["processingActivities"]![0]!["role"] = role;
            body["processingActivities"]![0]!["processorContact"] = "Test processor";
            body["processingActivities"]![0]!["controllers"] = "Reviewed controllers";
            body["processingActivities"]![0]!["processingCategories"] = "Reviewed processing categories";
            body["processingActivities"]![0]!.AsObject().Remove(missing);
        });
        Assert.Contains(Inspect(scenario).Gaps, gap => gap.Code == "NativeEvidenceFieldInvalid" && gap.ControlId == "GDPR.Art.30");
    }

    [Fact]
    public void Completed_prior_consultation_resolves_the_high_residual_risk_gap()
    {
        var scenario = Gdpr();
        scenario.Change("gdpr-dpia", body => body["residualRiskDecision"]!["level"] = "high");
        scenario.Add("gdpr-prior-consultation", new JsonObject
        {
            ["activityId"] = "ACT-1", ["authority"] = "Test authority", ["consultationStatus"] = "completed",
            ["submissionReference"] = "SUB-1", ["outcomeReference"] = "OUT-1",
            ["submittedAtUtc"] = Start.AddDays(-3).ToString("O"), ["completedAtUtc"] = Start.AddDays(-2).ToString("O")
        }, "GDPR.Art.36");
        Assert.True(Inspect(scenario).ReadyForAuditorReview);
        scenario.Change("gdpr-prior-consultation", body => body["consultationStatus"] = "pending");
        Assert.Contains(Inspect(scenario).Gaps, gap => gap.Code == "PriorConsultationMissing");
    }

    [Fact]
    public void Completed_consultation_after_processing_began_is_still_a_timing_gap()
    {
        var scenario = Gdpr();
        scenario.Change("gdpr-dpia", body => body["residualRiskDecision"]!["level"] = "high");
        scenario.Add("gdpr-prior-consultation", new JsonObject
        {
            ["activityId"] = "ACT-1", ["authority"] = "Test authority", ["consultationStatus"] = "completed",
            ["submissionReference"] = "SUB-1", ["outcomeReference"] = "OUT-1",
            ["submittedAtUtc"] = Start.AddDays(-1).ToString("O"), ["completedAtUtc"] = End.ToString("O")
        }, "GDPR.Art.36");
        Assert.Contains(Inspect(scenario).Gaps, gap => gap.Code == "PriorConsultationTimingGap");
    }

    [Fact]
    public void Periodic_review_preserves_the_original_pre_processing_assessment_evidence()
    {
        var scenario = Gdpr();
        scenario.Change("gdpr-dpia", body =>
        {
            body["assessmentTiming"]!["basis"] = "periodic-review";
            body["assessmentTiming"]!["assessedAtUtc"] = End.ToString("O");
            body["assessmentTiming"]!["originalAssessmentAtUtc"] = Start.AddDays(-1).ToString("O");
            body["assessmentTiming"]!["originalAssessmentReference"] = "Prior assessment revision";
        });
        Assert.True(Inspect(scenario).ReadyForAuditorReview);
        scenario.Change("gdpr-dpia", body => body["assessmentTiming"]!["originalAssessmentAtUtc"] = End.ToString("O"));
        Assert.Contains(Inspect(scenario).Gaps, gap => gap.Code == "DPIATimingGap");
    }

    [Theory]
    [InlineData("GDPR.Art.3")]
    [InlineData("GDPR.Art.35")]
    [InlineData("GDPR.Art.36")]
    public void Scope_and_DPIA_screening_cannot_be_bypassed_by_generic_exclusions(string controlId)
    {
        var scenario = Gdpr();
        scenario.Request.Exclusions.Add(new() { ControlId = controlId, Rationale = "Omit conditional assessment", ApplicabilityDocumentId = scenario.Documents[0].Id });
        Assert.Throws<CompliancePackagingValidationException>(() => Inspect(scenario));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_reviewer_files_detect_removal_or_tampering(bool remove)
    {
        var scenario = Iso();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = new AuditSigningOptions { ActiveKeyId = "tamper-test" };
        options.Keys["tamper-test"] = new AuditSigningKeyOptions { PrivateKeyPem = key.ExportECPrivateKeyPem() };
        var builder = new ComplianceArtifactBuilder(new ComplianceEvidenceValidationEngine(), new EcdsaCryptographicSigningService(Options.Create(options)));
        var artifact = builder.Build(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), End.AddDays(1), scenario.Template, scenario.Request, scenario.Documents, Datasets(scenario.Template));
        using var output = new MemoryStream();
        output.Write(artifact.ZipContent);
        using (var zip = new ZipArchive(output, ZipArchiveMode.Update, true))
        {
            zip.GetEntry("review/iso27001/statement-of-applicability.csv")!.Delete();
            if (!remove)
            {
                using var writer = new StreamWriter(zip.CreateEntry("review/iso27001/statement-of-applicability.csv").Open());
                writer.Write("modified");
            }
        }
        Assert.False(builder.Verify(output.ToArray(), artifact.Manifest.TenantId, artifact.Manifest.PackageId).IsValid);
    }

    [Fact]
    public void Native_reviewer_CSV_neutralizes_spreadsheet_formulas()
    {
        var scenario = Iso();
        scenario.Change("iso-soa", body => body["necessaryControls"]!.AsArray().Last()!["inclusionJustification"] = "  =HYPERLINK(\"https://example.test\")");
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = new AuditSigningOptions { ActiveKeyId = "csv-test" };
        options.Keys["csv-test"] = new AuditSigningKeyOptions { PrivateKeyPem = key.ExportECPrivateKeyPem() };
        var builder = new ComplianceArtifactBuilder(new ComplianceEvidenceValidationEngine(), new EcdsaCryptographicSigningService(Options.Create(options)));
        var artifact = builder.Build(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), End.AddDays(1), scenario.Template, scenario.Request, scenario.Documents, Datasets(scenario.Template));
        using var zip = new ZipArchive(new MemoryStream(artifact.ZipContent), ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("review/iso27001/statement-of-applicability.csv")!.Open());
        Assert.Contains("\"'  =HYPERLINK", reader.ReadToEnd());
    }

    [Fact]
    public void Native_reviewer_formats_are_signed_and_cross_reference_uploaded_documents()
    {
        foreach (var scenario in new[] { Iso(), Gdpr() })
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var options = new AuditSigningOptions { ActiveKeyId = "framework-test" };
            options.Keys["framework-test"] = new AuditSigningKeyOptions { PrivateKeyPem = key.ExportECPrivateKeyPem() };
            var builder = new ComplianceArtifactBuilder(new ComplianceEvidenceValidationEngine(), new EcdsaCryptographicSigningService(Options.Create(options)));
            var artifact = builder.Build(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), End.AddDays(1),
                scenario.Template, scenario.Request, scenario.Documents, Datasets(scenario.Template));
            var path = scenario.Template.Framework == ComplianceFramework.ISO27001
                ? "review/iso27001/statement-of-applicability.csv" : "review/gdpr/dpia-index.csv";
            Assert.Contains(artifact.Manifest.Entries, entry => entry.Path == path);
            using var zip = new ZipArchive(new MemoryStream(artifact.ZipContent), ZipArchiveMode.Read);
            using var stream = new StreamReader(zip.GetEntry(path)!.Open());
            var csv = stream.ReadToEnd();
            Assert.Contains(scenario.Template.Framework == ComplianceFramework.ISO27001 ? "CUSTOM.1" : "ACT-1", csv);
            Assert.True(builder.Verify(artifact.ZipContent, artifact.Manifest.TenantId, artifact.Manifest.PackageId).IsValid);
        }
    }

    private static CompliancePackageValidationReport Inspect(Scenario scenario) => new ComplianceEvidenceValidationEngine().Inspect(
        scenario.Template, scenario.Request, scenario.Documents, Datasets(scenario.Template), End.AddDays(1));

    private static IReadOnlyList<ComplianceEvidenceDataset> Datasets(ComplianceFrameworkTemplate template) => template.Controls.SelectMany(item => item.AutomaticEvidence)
        .Distinct().Select(kind => new ComplianceEvidenceDataset(kind, "Synthetic test observations", JsonSerializer.SerializeToUtf8Bytes(new[]
        { new { observedAtUtc = Start }, new { observedAtUtc = End } }), 2, Start, End, [DateOnly.FromDateTime(Start), DateOnly.FromDateTime(End)], [])).ToArray();

    internal static Scenario Iso()
    {
        var scenario = new Scenario("iso27001-2022-isms-evidence-v2");
        scenario.Change("iso-context", body => body["climateRelevanceAssessment"] = new JsonObject { ["relevant"] = false, ["rationale"] = "Reviewed context", ["interestedPartyRequirements"] = "Reviewed requirements" });
        var annex = scenario.Template.Controls.Where(item => item.Id.StartsWith("A.", StringComparison.Ordinal)).Select(item => item.Id).ToArray();
        scenario.Change("iso-soa", body =>
        {
            body["annexADecisions"] = JsonSerializer.SerializeToNode(annex.ToDictionary(id => id, id => new
            { applicable = true, inclusionJustification = "Risk treatment", necessaryControlIds = new[] { "NC-" + id } }));
            body["necessaryControls"] = JsonSerializer.SerializeToNode(annex.Select(id => new
            { id = "NC-" + id, description = "Assessed control", inclusionJustification = "Risk register RA-1", implementationStatus = "implemented", effectivenessEvidence = "Reviewed test evidence", annexAReferences = new[] { id } })
            .Append(new { id = "CUSTOM.1", description = "Custom necessary control", inclusionJustification = "Organisation risk", implementationStatus = "implemented", effectivenessEvidence = "Reviewed custom control", annexAReferences = Array.Empty<string>() }));
        });
        return scenario;
    }

    internal static Scenario Gdpr()
    {
        var scenario = new Scenario("gdpr-2016-679-evidence-v1");
        scenario.Change("gdpr-accountability", body => body["processingScope"] = new JsonObject
        { ["activityIds"] = new JsonArray("ACT-1"), ["roles"] = new JsonArray("controller"), ["rationale"] = "Reviewed territorial and processing scope" });
        scenario.Change("gdpr-processing-records", body => body["processingActivities"] = JsonSerializer.SerializeToNode(new[]
        { new { activityId = "ACT-1", role = "controller", controllerContact = "Synthetic controller", purposes = "Service provision", subjectCategories = "Users", dataCategories = "Contact", recipientCategories = "Processor", transfers = "None", retention = "Reviewed schedule", securityMeasures = "Reviewed safeguards" } }));
        scenario.Change("gdpr-dpia-screening", body => body["screenings"] = JsonSerializer.SerializeToNode(new[]
        { new { activityId = "ACT-1", dpiaRequired = true, rationale = "Reviewed risk", supervisoryAuthorityListsReview = "DPA lists reviewed", reviewTriggers = "Change in processing risk" } }));
        scenario.Change("gdpr-dpia", body =>
        {
            body["activityId"] = "ACT-1";
            body["residualRiskDecision"] = new JsonObject { ["level"] = "low", ["rationale"] = "Mitigations reviewed" };
            body["consultation"] = new JsonObject { ["dpoAdvice"] = "Reviewed DPO advice", ["dataSubjectViews"] = "Reviewed consultation or documented rationale" };
            body["assessmentTiming"] = new JsonObject { ["assessedAtUtc"] = Start.AddDays(-1).ToString("O"), ["effectiveAtUtc"] = Start.ToString("O"), ["basis"] = "initial-processing" };
        });
        return scenario;
    }

    internal sealed class Scenario
    {
        public ComplianceFrameworkTemplate Template { get; }
        public List<ComplianceDocumentSnapshot> Documents { get; } = [];
        public CreateCompliancePackageRequest Request { get; private set; }
        public Scenario(string id)
        {
            Template = new ComplianceFrameworkCatalog().Find(id)!;
            foreach (var requirement in Template.Documents.Where(item => item.Type != "gdpr-prior-consultation"))
            {
                var body = new JsonObject();
                foreach (var field in requirement.RequiredFields) { body[field] = "Reviewed synthetic evidence"; }
                body["frameworkVersion"] = Template.Version;
                body["assessmentStatus"] = "satisfactory";
                if (requirement.RequiresControlAssessments)
                {
                    body["controlAssessments"] = JsonSerializer.SerializeToNode(Template.Controls.ToDictionary(item => item.Id, item => new
                    { owner = "Assessor", controlImplementation = "Implemented process", effectivenessEvidence = "Reviewed evidence reference", assessmentStatus = "satisfactory" }));
                }
                var ids = Template.Controls.Where(item => item.RequiredDocumentTypes.Contains(requirement.Type)).Select(item => item.Id).ToArray();
                if (requirement.Type == "gdpr-dpia") { ids = ["GDPR.Art.35", "GDPR.Art.36"]; }
                Documents.Add(Snapshot(requirement.Type, body, ids));
            }
            Request = NewRequest();
        }
        public void Change(string type, Action<JsonObject> change)
        {
            var index = Documents.FindIndex(item => item.Type == type);
            var document = Documents[index];
            var body = JsonNode.Parse(document.Content)!.AsObject();
            change(body);
            var bytes = Encoding.UTF8.GetBytes(body.ToJsonString());
            Documents[index] = document with { Content = bytes, ContentSha256 = Hash(bytes) };
        }
        public void RefreshRequest() => Request = NewRequest();
        public void Add(string type, JsonObject body, params string[] ids)
        {
            body["frameworkVersion"] = Template.Version;
            body["assessmentStatus"] = "satisfactory";
            body["owner"] = "Test reviewer";
            Documents.Add(Snapshot(type, body, ids));
            RefreshRequest();
        }
        private CreateCompliancePackageRequest NewRequest() => new() { Name = "Framework evidence tests", TemplateId = Template.Id,
            PeriodStartUtc = Start, PeriodEndUtc = End, DocumentIds = Documents.Select(item => item.Id).ToList() };
        private ComplianceDocumentSnapshot Snapshot(string type, JsonObject body, IReadOnlyList<string> ids)
        {
            var content = Encoding.UTF8.GetBytes(body.ToJsonString());
            return new(Guid.NewGuid(), "Reviewed synthetic " + type, type, "application/json", Hash(content), content,
                "https://example.test/evidence", Start, End, ids, new Dictionary<string, string>(), ComplianceDocumentReview.Approved,
                Guid.NewGuid(), Guid.NewGuid(), End, 2, Template.Id);
        }
    }
    private static string Hash(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
