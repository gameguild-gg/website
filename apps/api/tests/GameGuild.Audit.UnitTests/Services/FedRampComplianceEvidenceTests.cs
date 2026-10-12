using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using GameGuild.Compliance.Audit;
using GameGuild.Tests.Audit.Unit.Fixtures;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class FedRampComplianceEvidenceTests
{
    [Theory]
    [InlineData("B", "Program", 155, 158)]
    [InlineData("B", "Agency", 155, 162)]
    [InlineData("C", "Program", 322, 158)]
    [InlineData("C", "Agency", 322, 162)]
    [InlineData("D", "Program", 409, 158)]
    [InlineData("D", "Agency", 409, 162)]
    public void Tailored_provider_profiles_require_real_schema_payloads_and_all_baseline_decisions(string classId, string path, int controls, int rules)
    {
        var scenario = FedRampEvidenceScenario.Create(classId, path);
        Assert.Equal(ComplianceFramework.FedRAMP, scenario.Template.Framework);
        Assert.Equal(controls + rules, scenario.Template.Controls.Count);
        Assert.Contains(scenario.Template.Sources, uri => uri.Contains("58487bda77d76d9ce334304ec2e779ece7cc7d54", StringComparison.Ordinal));
        var report = scenario.Inspect();
        Assert.True(report.ReadyForAuditorReview, JsonSerializer.Serialize(report.Gaps));
        Assert.Equal(controls + rules, report.Controls.Count);
    }

    [Theory]
    [InlineData("type", "20x")]
    [InlineData("class", "A")]
    [InlineData("path", "Agency")]
    [InlineData("rulesVersion", "current")]
    public void Profile_declarations_cannot_change_the_pinned_certification_target(string field, string value)
    {
        var scenario = FedRampEvidenceScenario.Create();
        scenario.Change("fedramp-profile", root => root["certificationProfile"]![field] = value);
        Assert.Contains(scenario.Inspect().Gaps, gap => gap.Code == "FedRampProfileMismatch");
    }

    [Theory]
    [InlineData("logo")]
    [InlineData("email")]
    [InlineData("contacts")]
    [InlineData("authenticationInstructions")]
    [InlineData("uri")]
    public void Official_schema_evaluates_external_refs_formats_contains_and_conditionals(string defect)
    {
        var scenario = FedRampEvidenceScenario.Create();
        scenario.Change("fedramp-overview", root =>
        {
            var payload = root["payload"]!;
            switch (defect)
            {
                case "logo": payload["serviceIdentification"]!["logo"] = "https://example.test/not-an-image.txt"; break;
                case "uri": payload["serviceIdentification"]!["website"] = "not a URI"; break;
                case "email": payload["contactInformation"]![0]!["contactEmail"] = "invalid-email"; break;
                case "contacts": payload["contactInformation"]!.AsArray().RemoveAt(1); break;
                case "authenticationInstructions": payload["serviceProperties"]!["trustCenter"] = new JsonObject { ["repositoryType"] = new JsonArray("Trust Center"),
                    ["repositoryDescription"] = "Synthetic repository", ["url"] = "https://example.test/private", ["authenticationRequired"] = true }; break;
                default: throw new InvalidOperationException();
            }
        });
        Assert.Contains(scenario.Inspect().Gaps, gap => gap.Code == "FedRampSchemaInvalid");
    }

    [Theory]
    [InlineData("packageVerifiedAtUtc")]
    [InlineData("packageValidatedAtUtc")]
    public void Application_provider_verification_and_validation_expire_after_seven_days(string field)
    {
        var scenario = FedRampEvidenceScenario.Create();
        scenario.Change("fedramp-profile", root => root[field] = scenario.CapturedAtUtc.AddDays(-8).ToString("O"));
        Assert.Contains(scenario.Inspect().Gaps, gap => gap.Code == "FedRampPackageStale");
    }

    [Fact]
    public void Independent_assessment_requires_fresh_recognized_and_captured_report()
    {
        var scenario = FedRampEvidenceScenario.Create();
        scenario.Change("fedramp-profile", root => root["independentAssessment"]!["completedAtUtc"] = scenario.CapturedAtUtc.AddMonths(-4).ToString("O"));
        scenario.Documents.RemoveAll(item => item.Type == "fedramp-supporting-evidence"); scenario.Refresh();
        var report = scenario.Inspect();
        Assert.Contains(report.Gaps, gap => gap.Code == "FedRampIndependentAssessmentStale");
        Assert.Contains(report.Gaps, gap => gap.Code == "FedRampAssessmentReportMissing");
    }

    [Fact]
    public void Missing_parameters_duplicate_records_and_partial_implementations_remain_control_gaps()
    {
        var scenario = FedRampEvidenceScenario.Create();
        scenario.Change("fedramp-sdr", root =>
        {
            var rows = root["payload"]!["securityControls"]!.AsArray();
            rows[0]!["parameterValues"] = new JsonArray(); rows.Add(rows[0]!.DeepClone());
            root["decisions"]!["AC-01"]!["implementationStatus"] = "Planned";
        });
        var report = scenario.Inspect();
        Assert.Contains(report.Gaps, gap => gap.Code == "FedRampSdrIdentifiersInvalid");
        Assert.Contains(report.Gaps, gap => gap.ControlId == "AC-01" && gap.Code == "FedRampImplementationPending");
        // Inspect parameter absence independently of the duplicate identifier defect.
        scenario.Change("fedramp-sdr", root => root["payload"]!["securityControls"]!.AsArray().RemoveAt(scenario.Template.Controls.Count - 158));
        Assert.Contains(scenario.Inspect().Gaps, gap => gap.ControlId == "AC-01" && gap.Code == "FedRampParametersMissing");
    }

    [Fact]
    public void Conditional_non_applicability_requires_risk_and_senior_official_acceptance()
    {
        var scenario = FedRampEvidenceScenario.Create();
        scenario.Change("fedramp-sdr", root => root["decisions"]!["IEC-CSO-IIR"]!.AsObject().Remove("seniorOfficialAcceptance"));
        Assert.Contains(scenario.Inspect().Gaps, gap => gap.ControlId == "IEC-CSO-IIR" && gap.Code == "NativeEvidenceFieldInvalid");
    }

    [Fact]
    public void Rule_specific_schema_artifacts_are_required_when_the_rule_applies()
    {
        var scenario = FedRampEvidenceScenario.Create();
        scenario.Change("fedramp-sdr", root => root["decisions"]!["IEC-CSO-IIR"]!["applicable"] = true);
        Assert.Contains(scenario.Inspect().Gaps, gap => gap.ControlId == "IEC-CSO-IIR" && gap.Code == "FedRampRuleArtifactMissing");
    }

    [Fact]
    public void Generic_exclusion_cannot_hide_a_required_baseline_record()
    {
        var scenario = FedRampEvidenceScenario.Create();
        scenario.Request.Exclusions.Add(new() { ControlId = "AC-01", Rationale = "Bypass", ApplicabilityDocumentId = scenario.Documents[0].Id });
        Assert.Throws<CompliancePackagingValidationException>(() => scenario.Inspect());
    }

    [Fact]
    public void Captured_incident_artifact_uses_its_pinned_schema_and_matching_report_type()
    {
        var scenario = FedRampEvidenceScenario.Create();
        scenario.Change("fedramp-sdr", root =>
        {
            root["decisions"]!["IEC-CSO-IIR"]!["applicable"] = true;
            root["decisions"]!["IEC-CSO-IIR"]!["implementationStatus"] = "Implemented";
            root["payload"]!["fedRampRequirements"]!.AsArray().Single(row => row!["frrID"]!.GetValue<string>() == "IEC-CSO-IIR")!["frrImplementationStatus"] = "Implemented";
        });
        var body = FedRampEvidenceScenario.Payload("incident-report", new JsonObject
        { ["certificationPackageOverviewUri"] = "https://evidence.example.test/overview.json", ["reportType"] = "Initial", ["providerTrackingId"] = "TEST-1" });
        scenario.Add("fedramp-artifact", body, ["IEC-CSO-IIR"]);
        Assert.True(scenario.Inspect().ReadyForAuditorReview);
        scenario.Change("fedramp-artifact", root => root["payload"]!["reportType"] = "Final");
        Assert.Contains(scenario.Inspect().Gaps, gap => gap.Code == "FedRampReportTypeMismatch");
        Assert.Contains(scenario.Inspect().Gaps, gap => gap.Code == "FedRampSchemaInvalid");
    }

    [Fact]
    public void User_schema_URI_cannot_expand_the_bundled_registry()
    {
        var scenario = FedRampEvidenceScenario.Create();
        scenario.Change("fedramp-sdr", root => root["schemaUri"] = "https://untrusted.example.test/custom.json");
        Assert.Contains(scenario.Inspect().Gaps, gap => gap.Code == "FedRampSchemaInvalid");
    }

    [Fact]
    public void Cross_package_overview_references_and_stale_SDR_updates_are_gaps()
    {
        var scenario = FedRampEvidenceScenario.Create();
        scenario.Change("fedramp-sdr", root =>
        { root["payload"]!["certificationPackageOverviewUri"] = "https://other.example.test/overview.json";
          root["payload"]!["metadata"]!["lastUpdated"] = scenario.CapturedAtUtc.AddDays(-8).ToString("O"); });
        var report = scenario.Inspect();
        Assert.Contains(report.Gaps, gap => gap.Code == "FedRampOverviewReferenceMismatch");
        Assert.Contains(report.Gaps, gap => gap.Code == "FedRampPackageStale");
    }

    [Fact]
    public void Report_period_and_planning_horizon_are_validated_from_actual_payload()
    {
        var scenario = FedRampEvidenceScenario.Create();
        scenario.Change("fedramp-ocr", root => root["payload"]!["plannedCertificationDataChanges"]!["planningHorizonThrough"] = "2026-09-03");
        Assert.Contains(scenario.Inspect().Gaps, gap => gap.Code == "FedRampReportTimelineGap");
    }

    [Fact]
    public void Signed_native_formats_are_typed_bounded_and_formula_safe()
    {
        var scenario = FedRampEvidenceScenario.Create();
        scenario.Change("fedramp-sdr", root => root["decisions"]!["AC-01"]!["verification"] = "=HYPERLINK(\"https://example.test\")");
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = new AuditSigningOptions { ActiveKeyId = "fedramp-test" };
        options.Keys[options.ActiveKeyId] = new() { PrivateKeyPem = key.ExportPkcs8PrivateKeyPem(), PublicKeyPem = key.ExportSubjectPublicKeyInfoPem() };
        var builder = new ComplianceArtifactBuilder(new(), new EcdsaCryptographicSigningService(Options.Create(options)));
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var packageId = Guid.NewGuid();
        var artifact = builder.Build(packageId, tenant, actor, scenario.CapturedAtUtc, scenario.Template, scenario.Request, scenario.Documents, scenario.Datasets);
        var verification = builder.Verify(artifact.ZipContent, tenant, packageId);
        Assert.True(verification.IsValid, JsonSerializer.Serialize(verification.Errors));
        using var zip = new ZipArchive(new MemoryStream(artifact.ZipContent), ZipArchiveMode.Read);
        Assert.NotNull(zip.GetEntry("review/fedramp/security-decision-record.txt"));
        Assert.Contains(artifact.Manifest.Entries, entry => entry.Path == "review/fedramp/overview.json" && entry.MediaType == "application/json");
        using var reader = new StreamReader(zip.GetEntry("review/fedramp/decisions.csv")!.Open());
        Assert.Contains("'=HYPERLINK", reader.ReadToEnd());
    }
}
