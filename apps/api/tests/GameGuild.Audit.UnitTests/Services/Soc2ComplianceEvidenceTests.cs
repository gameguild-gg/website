using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using GameGuild.Compliance.Audit;
using GameGuild.Tests.Audit.Unit.Fixtures;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class Soc2ComplianceEvidenceTests
{
    public static IEnumerable<object[]> Profiles() => Enumerable.Range(1, 2)
        .SelectMany(type => Enumerable.Range(0, 16).Select(mask => new object[] { type, mask }));

    [Fact]
    public void Catalog_offers_versioned_SOC2_type_I_and_II_category_scopes()
    {
        var templates = new ComplianceFrameworkCatalog().GetTemplates();
        Assert.Equal(16, templates.Count(item => item.Framework == ComplianceFramework.SOC2Type1));
        Assert.Equal(16, templates.Count(item => item.Framework == ComplianceFramework.SOC2Type2));
        var first = templates.Single(item => item.Id == "soc2-tsc2017-type1-security-evidence-v1");
        var second = templates.Single(item => item.Id == "soc2-tsc2017-type2-security-evidence-v1");
        Assert.Equal(ComplianceEvidencePeriodMode.PointInTime, first.PeriodMode);
        Assert.Equal(ComplianceEvidencePeriodMode.Period, second.PeriodMode);
        Assert.Equal(33, first.Controls.Count);
        Assert.Equal(33, second.Controls.Count);
        Assert.Contains(first.Controls, item => item.Id == "CC9.2");
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Every_category_scope_validates_actual_captured_evidence(int type, int mask)
    {
        var scenario = Soc2EvidenceScenario.Create(type, mask);
        var expected = 33 + ((mask & 1) != 0 ? 3 : 0) + ((mask & 2) != 0 ? 5 : 0) +
            ((mask & 4) != 0 ? 2 : 0) + ((mask & 8) != 0 ? 18 : 0);
        Assert.Equal(expected, scenario.Template.Controls.Count);
        Assert.InRange(scenario.Template.Id.Length, 1, 100);
        var report = scenario.Inspect();
        Assert.True(report.ReadyForAuditorReview, JsonSerializer.Serialize(report.Gaps));
        Assert.Equal(expected, report.Controls.Count);
        if (type == 1) { Assert.Empty(scenario.Datasets); }
        else { Assert.Equal(5, scenario.Datasets.Count); }
    }

    [Theory]
    [InlineData("reportType", "Soc2ScopeMismatch")]
    [InlineData("categories", "Soc2ScopeMismatch")]
    [InlineData("duplicateCategories", "Soc2ScopeMismatch")]
    [InlineData("components", "NativeEvidenceFieldInvalid")]
    [InlineData("userControls", "Soc2SystemDescriptionIncomplete")]
    [InlineData("subserviceRegister", "Soc2SystemDescriptionIncomplete")]
    [InlineData("period", "Soc2PeriodMismatch")]
    public void System_scope_is_validated_from_actual_JSON(string defect, string code)
    {
        var scenario = Soc2EvidenceScenario.Create(2, 15);
        scenario.Change("soc2-system-description", root =>
        {
            switch (defect)
            {
                case "reportType": root["reportType"] = "2"; break;
                case "categories": root["categories"] = new JsonArray("Security"); break;
                case "duplicateCategories": root["categories"]!.AsArray().Add("Security"); break;
                case "components": root["components"]!["people"] = true; break;
                case "userControls": root["complementaryUserEntityControls"] = "none"; break;
                case "subserviceRegister": root["subserviceOrganizations"] = new JsonObject(); break;
                case "period": root["periodEndUtc"] = scenario.Request.PeriodEndUtc.AddSeconds(-1).ToString("O"); break;
                default: throw new InvalidOperationException();
            }
        });
        AssertGap(scenario, code);
    }

    [Theory]
    [InlineData("fairPresentation", "Soc2AssertionInvalid")]
    [InlineData("suitableDesign", "Soc2AssertionInvalid")]
    [InlineData("operatingEffectiveness", "Soc2AssertionInvalid")]
    [InlineData("reportType", "Soc2AssertionInvalid")]
    [InlineData("future", "Soc2AssertionDateInvalid")]
    [InlineData("beforePeriodEnd", "Soc2AssertionDateInvalid")]
    [InlineData("nonUtc", "Soc2AssertionDateInvalid")]
    public void Assertions_require_typed_affirmations_and_valid_signing_dates(string defect, string code)
    {
        var scenario = Soc2EvidenceScenario.Create(2, 0);
        scenario.Change("soc2-management-assertion", root =>
        {
            switch (defect)
            {
                case "future": root["signedAtUtc"] = scenario.CapturedAtUtc.AddSeconds(1).ToString("O"); break;
                case "beforePeriodEnd": root["signedAtUtc"] = scenario.Request.PeriodEndUtc.AddSeconds(-1).ToString("O"); break;
                case "nonUtc": root["signedAtUtc"] = "2026-09-02T00:00:00"; break;
                case "reportType": root["reportType"] = 1; break;
                default: root[defect] = "true"; break;
            }
        });
        AssertGap(scenario, code);
    }

    [Theory]
    [InlineData("missingCriterion", "Soc2CriteriaMappingMismatch")]
    [InlineData("unknownCriterion", "Soc2CriteriaMappingMismatch")]
    [InlineData("unknownControl", "Soc2MappedControlMissing")]
    [InlineData("duplicateControl", "Soc2ControlIdentifierInvalid")]
    [InlineData("unmappedControl", "Soc2UnmappedControl")]
    [InlineData("missingRationale", "Soc2CriterionAssessmentIncomplete")]
    [InlineData("designConclusion", "Soc2DesignDeficiency")]
    [InlineData("selfReference", "Soc2DesignEvidenceMissing")]
    public void Criterion_and_control_mappings_cannot_hide_missing_design_evidence(string defect, string code)
    {
        var scenario = Soc2EvidenceScenario.Create(1, 0);
        scenario.Change("soc2-control-matrix", root =>
        {
            var mappings = root["criteriaMappings"]!.AsObject();
            var rows = root["controls"]!.AsArray();
            switch (defect)
            {
                case "missingCriterion": mappings.Remove("CC1.1"); break;
                case "unknownCriterion": mappings["UNKNOWN"] = mappings["CC1.1"]!.DeepClone(); break;
                case "unknownControl": mappings["CC1.1"]!["controlIds"] = new JsonArray("unknown"); break;
                case "duplicateControl": rows.Add(rows[0]!.DeepClone()); break;
                case "unmappedControl":
                    var extra = rows[0]!.DeepClone(); extra["controlId"] = "unmapped"; rows.Add(extra); break;
                case "missingRationale": mappings["CC1.1"]!.AsObject().Remove("mappingRationale"); break;
                case "designConclusion": rows[0]!["designConclusion"] = "unknown"; break;
                case "selfReference": rows[0]!["designEvidenceDocumentIds"] =
                    new JsonArray(scenario.Documents.Single(item => item.Type == "soc2-control-matrix").Id.ToString("D")); break;
                default: throw new InvalidOperationException();
            }
        });
        AssertGap(scenario, code);
    }

    [Theory]
    [InlineData("procedure", "Soc2OperatingDeficiency")]
    [InlineData("deviation", "Soc2OperatingDeficiency")]
    [InlineData("conclusion", "Soc2OperatingDeficiency")]
    [InlineData("population", "Soc2SampleEvidenceInvalid")]
    [InlineData("sampleCount", "Soc2SampleEvidenceInvalid")]
    [InlineData("duplicateSample", "Soc2SampleEvidenceInvalid")]
    [InlineData("failedSample", "Soc2SampleEvidenceInvalid")]
    [InlineData("outsidePeriod", "Soc2SampleEvidenceInvalid")]
    [InlineData("nonUtc", "Soc2SampleEvidenceInvalid")]
    [InlineData("missingReport", "Soc2SampleEvidenceInvalid")]
    [InlineData("lateStart", "Soc2OperatingPeriodGap")]
    [InlineData("earlyEnd", "Soc2OperatingPeriodGap")]
    [InlineData("futureEnd", "Soc2OperatingPeriodGap")]
    public void Type_II_requires_complete_operating_tests_and_actual_sample_documents(string defect, string code)
    {
        var scenario = Soc2EvidenceScenario.Create(2, 0);
        scenario.Change("soc2-control-matrix", root =>
        {
            var row = root["controls"]![0]!;
            var sample = row["samples"]![0]!;
            switch (defect)
            {
                case "procedure": row.AsObject().Remove("testProcedures"); break;
                case "deviation": row["deviationCount"] = 1; break;
                case "conclusion": row["operatingConclusion"] = "unknown"; break;
                case "population": row["populationCount"] = 0; break;
                case "sampleCount": row["sampleCount"] = "1"; break;
                case "duplicateSample": row["sampleCount"] = 2; row["samples"]!.AsArray().Add(sample.DeepClone()); break;
                case "failedSample": sample["result"] = "fail"; break;
                case "outsidePeriod": sample["observedAtUtc"] = scenario.Request.PeriodStartUtc.AddSeconds(-1).ToString("O"); break;
                case "nonUtc": sample["observedAtUtc"] = "2026-09-01T00:00:00"; break;
                case "missingReport": sample["evidenceDocumentId"] = Guid.NewGuid().ToString("D"); break;
                case "lateStart": row["testedFromUtc"] = scenario.Request.PeriodStartUtc.AddSeconds(1).ToString("O"); break;
                case "earlyEnd": row["testedUntilUtc"] = scenario.Request.PeriodEndUtc.AddSeconds(-1).ToString("O"); break;
                case "futureEnd": row["testedUntilUtc"] = scenario.CapturedAtUtc.AddSeconds(1).ToString("O"); break;
                default: throw new InvalidOperationException();
            }
        });
        AssertGap(scenario, code);
    }

    [Theory]
    [InlineData("inclusive")]
    [InlineData("carve-out")]
    public void Subservice_boundaries_require_captured_controls_or_monitoring(string method)
    {
        var scenario = Soc2EvidenceScenario.Create(2, 0);
        scenario.Change("soc2-system-description", root => root["subserviceOrganizations"]!.AsArray().Add(new JsonObject
        {
            ["name"] = "Synthetic hosting", ["serviceDescription"] = "Synthetic hosting provider", ["boundary"] = "Hosting",
            ["method"] = method, ["includedControlIds"] = new JsonArray("control-CC1.1"),
            ["complementarySubserviceControls"] = "Synthetic complementary controls",
            ["monitoringEvidenceDocumentIds"] = new JsonArray(scenario.Documents[0].Id.ToString("D"))
        }));
        Assert.True(scenario.Inspect().ReadyForAuditorReview);
        scenario.Change("soc2-system-description", root =>
        {
            var service = root["subserviceOrganizations"]![0]!;
            service[method == "inclusive" ? "includedControlIds" : "monitoringEvidenceDocumentIds"] = new JsonArray("missing");
        });
        AssertGap(scenario, "Soc2SubserviceEvidenceIncomplete");
    }

    [Theory]
    [InlineData("missing", "Soc2DesignEvidenceMissing")]
    [InlineData("pending", "DocumentNotApproved")]
    [InlineData("stale", "DocumentPeriodGap")]
    [InlineData("tampered", "DocumentHashMismatch")]
    [InlineData("unmapped", "Soc2DesignEvidenceMissing")]
    public void Linked_supporting_evidence_is_captured_approved_mapped_and_hash_checked(string defect, string code)
    {
        var scenario = Soc2EvidenceScenario.Create(2, 0);
        var support = scenario.Documents[0];
        if (defect == "missing") { scenario.Documents.RemoveAt(0); scenario.Request.DocumentIds.Remove(support.Id); }
        else
        {
            scenario.Documents[0] = defect switch
            {
                "pending" => support with { Review = ComplianceDocumentReview.Pending },
                "stale" => support with { ValidUntilUtc = scenario.Request.PeriodEndUtc.UtcDateTime.AddSeconds(-1) },
                "tampered" => support with { ContentSha256 = new string('0', 64) },
                "unmapped" => support with { ControlIds = ["CC9.2"] },
                _ => throw new InvalidOperationException()
            };
        }
        AssertGap(scenario, code);
    }

    [Fact]
    public void Reviewer_declarations_cannot_replace_native_JSON()
    {
        var scenario = Soc2EvidenceScenario.Create(1, 0);
        var index = scenario.Documents.FindIndex(item => item.Type == "soc2-system-description");
        scenario.Documents[index] = scenario.Documents[index] with
        { MediaType = "text/plain", ValidationFields = new Dictionary<string, string> { ["reportType"] = "1", ["categories"] = "[\"Security\"]" } };
        AssertGap(scenario, "StructuredDocumentRequired");
    }

    [Fact]
    public void Missing_native_documents_and_generic_exclusions_remain_unready()
    {
        var scenario = Soc2EvidenceScenario.Create(1, 0);
        var assertion = scenario.Documents.Single(item => item.Type == "soc2-management-assertion");
        scenario.Documents.Remove(assertion); scenario.Request.DocumentIds.Remove(assertion.Id);
        AssertGap(scenario, "Soc2NativeDocumentMissing");
        scenario.Request.Exclusions.Add(new() { ControlId = "CC1.1", Rationale = "Bypass", ApplicabilityDocumentId = scenario.Documents[0].Id });
        Assert.Throws<CompliancePackagingValidationException>(() => scenario.Inspect());
    }

    [Fact]
    public void Document_budget_is_bounded_and_cancellation_is_observed()
    {
        var scenario = Soc2EvidenceScenario.Create(1, 0);
        while (scenario.Documents.Count < 90)
        { scenario.Add("soc2-supporting-evidence", new() { ["evidenceDescription"] = "Additional synthetic evidence" }, ["CC1.1"]); }
        Assert.True(scenario.Inspect().ReadyForAuditorReview);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => new ComplianceEvidenceValidationEngine().Inspect(scenario.Template,
            scenario.Request, scenario.Documents, scenario.Datasets, scenario.CapturedAtUtc, cancellation.Token));
        scenario.Add("soc2-supporting-evidence", new() { ["evidenceDescription"] = "Too many" }, ["CC1.1"]);
        Assert.Throws<CompliancePackagingValidationException>(() => scenario.Inspect());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Signed_SOC2_formats_include_typed_sources_tests_and_formula_safe_indexes(int type)
    {
        var scenario = Soc2EvidenceScenario.Create(type, 15);
        scenario.Change("soc2-control-matrix", root => root["controls"]![0]!["owner"] = "=HYPERLINK(\"https://example.test\")");
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = new AuditSigningOptions { ActiveKeyId = "soc2-test" };
        options.Keys[options.ActiveKeyId] = new() { PrivateKeyPem = key.ExportPkcs8PrivateKeyPem(), PublicKeyPem = key.ExportSubjectPublicKeyInfoPem() };
        var builder = new ComplianceArtifactBuilder(new(), new EcdsaCryptographicSigningService(Options.Create(options)));
        var tenant = Guid.NewGuid(); var packageId = Guid.NewGuid();
        var artifact = builder.Build(packageId, tenant, Guid.NewGuid(), scenario.CapturedAtUtc, scenario.Template,
            scenario.Request, scenario.Documents, scenario.Datasets);
        Assert.True(artifact.Manifest.Validation.ReadyForAuditorReview);
        Assert.True(builder.Verify(artifact.ZipContent, tenant, packageId).IsValid);
        using var zip = new ZipArchive(new MemoryStream(artifact.ZipContent), ZipArchiveMode.Read);
        Assert.Equal(9, zip.Entries.Count(entry => entry.FullName.StartsWith("review/soc2/", StringComparison.Ordinal)));
        Assert.Contains(artifact.Manifest.Entries, item => item.Path == "review/soc2/control-tests.csv" && item.MediaType == "text/csv");
        using var csv = new StreamReader(zip.GetEntry("review/soc2/criteria.csv")!.Open());
        Assert.Contains("'=HYPERLINK", csv.ReadToEnd());
        using var source = new StreamReader(zip.GetEntry("review/soc2/control-matrix.json")!.Open());
        using var matrix = JsonDocument.Parse(source.ReadToEnd());
        Assert.Equal(61, matrix.RootElement.GetProperty("controls").GetArrayLength());
        Assert.NotNull(zip.GetEntry($"documents/{scenario.Documents[0].Id:D}/content.json"));
        Assert.False(builder.Verify(artifact.ZipContent, Guid.NewGuid(), packageId).IsValid);
    }

    private static void AssertGap(Soc2EvidenceScenario scenario, string code)
    {
        var report = scenario.Inspect();
        Assert.False(report.ReadyForAuditorReview);
        Assert.Contains(report.Gaps, gap => gap.Code == code);
    }
}
