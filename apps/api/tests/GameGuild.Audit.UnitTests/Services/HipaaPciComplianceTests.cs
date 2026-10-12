using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GameGuild.Compliance.Audit;
using Microsoft.Extensions.Options;
using Xunit;
using static GameGuild.Tests.Audit.Unit.Services.ComplianceFrameworkEvidenceTests;

namespace GameGuild.Tests.Audit.Unit.Services;

/// <summary>HIPAA Security Rule and PCI DSS SAQ A catalog profiles: completeness, signed bundles and fail-closed rationale enforcement.</summary>
public sealed class HipaaPciComplianceTests
{
    private const string HipaaId = "hipaa-45cfr164-security-evidence-v1";
    private const string PciId = "pci-dss-v4.0.1-saqa-evidence-v1";
    private static readonly DateTime Start = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddDays(1);

    [Fact]
    public void Catalog_exposes_both_industry_profiles_with_bounded_complete_templates()
    {
        var catalog = new ComplianceFrameworkCatalog();
        var templates = catalog.GetTemplates();
        Assert.Contains(templates, item => item.Id == HipaaId);
        Assert.Contains(templates, item => item.Id == PciId);
        foreach (var id in new[] { HipaaId, PciId })
        {
            var template = catalog.Find(id);
            Assert.NotNull(template);
            // Fail closed at the template definition: every catalog control maps to at least one
            // evidence artifact (automatic evidence) or a reviewed document requirement.
            Assert.All(template!.Controls, control =>
            {
                Assert.False(control.AutomaticEvidence.Count == 0 && control.RequiredDocumentTypes.Count == 0,
                    $"{control.Id} has no evidence or document mapping");
                Assert.All(control.RequiredDocumentTypes,
                    type => Assert.Contains(template.Documents, document => document.Type == type));
            });
            Assert.All(template.Documents, document =>
            {
                Assert.Contains("frameworkVersion", document.RequiredFields);
                Assert.Contains("assessmentStatus", document.RequiredFields);
                // Every declared document type is required by at least one control: no dangling types.
                Assert.Contains(template.Controls, control => control.RequiredDocumentTypes.Contains(document.Type));
            });
            Assert.All(template.Sources, source => Assert.StartsWith("https://", source, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Hipaa_profile_maps_the_security_rule_families_to_existing_evidence_sources()
    {
        var template = new ComplianceFrameworkCatalog().Find(HipaaId)!;
        Assert.Equal(ComplianceFramework.HIPAA, template.Framework);
        Assert.Equal(66, template.Controls.Count);
        Assert.Contains(template.Controls, item => item.Id == "164.308(a)(1)(i)");
        Assert.Contains(template.Controls, item => item.Id == "164.310(a)(1)");
        Assert.Contains(template.Controls, item => item.Id == "164.314(b)");
        Assert.Contains(template.Controls, item => item.Id == "164.316(b)(2)(i)");
        Assert.Contains(template.Controls, item => item.Id == "164.404");
        // Audit logging, access control/authorization, MFA/authentication and tamper evidence map
        // to the collected datasets; encryption and physical safeguards map to reviewed declarations.
        var audit = template.Controls.Single(item => item.Id == "164.312(b)");
        Assert.Contains(ComplianceEvidenceKind.Operations, audit.AutomaticEvidence);
        Assert.Contains(ComplianceEvidenceKind.Integrity, audit.AutomaticEvidence);
        Assert.Contains(ComplianceEvidenceKind.Authentication, template.Controls.Single(item => item.Id == "164.312(d)").AutomaticEvidence);
        Assert.Contains(ComplianceEvidenceKind.Authorization, template.Controls.Single(item => item.Id == "164.312(a)(1)").AutomaticEvidence);
        Assert.Contains(ComplianceEvidenceKind.Incidents, template.Controls.Single(item => item.Id == "164.308(a)(6)(i)").AutomaticEvidence);
        Assert.Equal(["hipaa-technical-safeguards"],
            template.Controls.Single(item => item.Id == "164.312(a)(2)(iv)").RequiredDocumentTypes);
        Assert.Equal(["hipaa-physical-safeguards"], template.Controls.Single(item => item.Id == "164.310(c)").RequiredDocumentTypes);
        Assert.Equal(["hipaa-breach-notification"], template.Controls.Single(item => item.Id == "164.404").RequiredDocumentTypes);
    }

    [Fact]
    public void Pci_profile_documents_the_honest_SAQ_A_subset()
    {
        var template = new ComplianceFrameworkCatalog().Find(PciId)!;
        Assert.Equal(ComplianceFramework.PCI_DSS, template.Framework);
        Assert.Equal(12, template.Controls.Count);
        // Requirements resolved by the reviewed SAQ A rationale rather than an owned control.
        foreach (var id in new[] { "PCI.1", "PCI.2", "PCI.5", "PCI.9" })
        {
            var control = template.Controls.Single(item => item.Id == id);
            Assert.Empty(control.AutomaticEvidence);
            Assert.Equal(["pci-saqa-eligibility"], control.RequiredDocumentTypes);
        }
        // Applicable requirements map to the owned controls and collected evidence.
        Assert.Contains(ComplianceEvidenceKind.Authorization, template.Controls.Single(item => item.Id == "PCI.7").AutomaticEvidence);
        Assert.Contains(ComplianceEvidenceKind.Authentication, template.Controls.Single(item => item.Id == "PCI.8").AutomaticEvidence);
        var monitoring = template.Controls.Single(item => item.Id == "PCI.10");
        Assert.Contains(ComplianceEvidenceKind.Operations, monitoring.AutomaticEvidence);
        Assert.Contains(ComplianceEvidenceKind.Integrity, monitoring.AutomaticEvidence);
        Assert.Equal(["pci-vulnerability-management"],
            template.Controls.Single(item => item.Id == "PCI.6").RequiredDocumentTypes);
        Assert.Contains("pci-saqa-eligibility", template.Controls.Single(item => item.Id == "PCI.11").RequiredDocumentTypes);
    }

    [Fact]
    public void Complete_HIPAA_and_PCI_evidence_produce_verifiable_signed_bundles()
    {
        foreach (var scenario in new[] { Hipaa(), Pci() })
        {
            var report = Inspect(scenario);
            Assert.True(report.ReadyForAuditorReview, JsonSerializer.Serialize(report.Gaps));
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var options = new AuditSigningOptions { ActiveKeyId = "industry-test" };
            options.Keys["industry-test"] = new AuditSigningKeyOptions { PrivateKeyPem = key.ExportECPrivateKeyPem() };
            var builder = new ComplianceArtifactBuilder(new ComplianceEvidenceValidationEngine(),
                new EcdsaCryptographicSigningService(Options.Create(options)));
            var artifact = builder.Build(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), End.AddDays(1),
                scenario.Template, scenario.Request, scenario.Documents, Datasets(scenario.Template));
            Assert.True(builder.Verify(artifact.ZipContent, artifact.Manifest.TenantId, artifact.Manifest.PackageId).IsValid);
            var expectedReviewPath = scenario.Template.Framework == ComplianceFramework.HIPAA
                ? "review/hipaa/safeguards.csv" : "review/pci/requirements.csv";
            Assert.Contains(artifact.Manifest.Entries, entry => entry.Path == expectedReviewPath);
            using var zip = new ZipArchive(new MemoryStream(artifact.ZipContent), ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry(expectedReviewPath)!.Open());
            var csv = reader.ReadToEnd();
            Assert.Contains(scenario.Template.Framework == ComplianceFramework.HIPAA ? "164.312(b)" : "PCI.1", csv);
            Assert.Contains(scenario.Template.Framework == ComplianceFramework.HIPAA ? "technical" : "saqa-declaration", csv);
        }
    }

    [Fact]
    public void Hipaa_packages_include_the_breach_notification_and_SAQ_A_posture_reviewer_files()
    {
        foreach (var (scenario, path) in new[] { (Hipaa(), "review/hipaa/breach-notification.csv"), (Pci(), "review/pci/saqa-posture.json") })
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var options = new AuditSigningOptions { ActiveKeyId = "reviewer-test" };
            options.Keys["reviewer-test"] = new AuditSigningKeyOptions { PrivateKeyPem = key.ExportECPrivateKeyPem() };
            var builder = new ComplianceArtifactBuilder(new ComplianceEvidenceValidationEngine(),
                new EcdsaCryptographicSigningService(Options.Create(options)));
            var artifact = builder.Build(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), End.AddDays(1),
                scenario.Template, scenario.Request, scenario.Documents, Datasets(scenario.Template));
            using var zip = new ZipArchive(new MemoryStream(artifact.ZipContent), ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry(path)!.Open());
            var content = reader.ReadToEnd();
            if (scenario.Template.Framework == ComplianceFramework.HIPAA) { Assert.Contains("164.404", content); }
            else { Assert.Contains("tokenizationOnly", content); }
            Assert.True(builder.Verify(artifact.ZipContent, artifact.Manifest.TenantId, artifact.Manifest.PackageId).IsValid);
        }
    }

    [Fact]
    public void Missing_SAQ_A_rationale_or_declaration_keeps_the_requirement_a_gap()
    {
        var scenario = Pci();
        scenario.Change("pci-saqa-eligibility", body => body["requirementRationales"]!["PCI.1"] = "");
        Assert.Contains(Inspect(scenario).Gaps, gap => gap.Code == "PciRequirementRationaleMissing" && gap.ControlId == "PCI.1");

        scenario = Pci();
        scenario.Documents.RemoveAll(item => item.Type == "pci-saqa-eligibility");
        scenario.RefreshRequest();
        var report = Inspect(scenario);
        Assert.False(report.ReadyForAuditorReview);
        Assert.Contains(report.Gaps, gap => gap.Code == "MissingDocument" && gap.ControlId == "PCI.1");
        Assert.Equal("EvidenceGap", report.Controls.Single(item => item.ControlId == "PCI.1").Status);
    }

    [Fact]
    public void A_SAQ_A_declaration_that_breaks_the_token_only_posture_fails_every_justified_requirement()
    {
        var scenario = Pci();
        scenario.Change("pci-saqa-eligibility", body => body["panStoragePosition"] = "platform-database");
        var report = Inspect(scenario);
        Assert.False(report.ReadyForAuditorReview);
        Assert.Contains(report.Gaps, gap => gap.Code == "PciSaqaPostureInvalid");
        Assert.Contains(report.Gaps, gap => gap.Code == "PciSaqaPostureMismatch" && gap.ControlId == "PCI.1");
    }

    [Fact]
    public void Pci_exclusions_are_rejected_even_with_an_applicability_document()
    {
        var scenario = Pci();
        scenario.Add("applicability", new JsonObject { ["rationale"] = "Provider owns the control" }, "PCI.7");
        scenario.Request.Exclusions.Add(new() { ControlId = "PCI.7", Rationale = "Outsourced", ApplicabilityDocumentId = scenario.Documents.Last(item => item.Type == "applicability").Id });
        Assert.Throws<CompliancePackagingValidationException>(() => Inspect(scenario));
    }

    [Fact]
    public void Hipaa_required_safeguards_cannot_be_excluded_but_addressable_ones_can()
    {
        var scenario = Hipaa();
        var applicability = scenario.Documents.Last(item => item.Type == "hipaa-technical-safeguards");
        scenario.Request.Exclusions.Add(new() { ControlId = "164.312(b)", Rationale = "Skip audit controls", ApplicabilityDocumentId = applicability.Id });
        Assert.Throws<CompliancePackagingValidationException>(() => Inspect(scenario));

        // Automatic logoff (§164.312(a)(2)(iii)) is an addressable specification: a reviewed
        // applicability document with an equivalent documented control resolves it.
        scenario = Hipaa();
        scenario.Add("applicability", new JsonObject
        {
            ["rationale"] = "Documented equivalent: platform-managed session lifetimes with reauthentication."
        }, "164.312(a)(2)(iii)");
        var document = scenario.Documents.Last(item => item.Type == "applicability");
        scenario.Request.Exclusions.Add(new() { ControlId = "164.312(a)(2)(iii)", Rationale = "Equivalent documented control",
            ApplicabilityDocumentId = document.Id });
        var report = Inspect(scenario);
        Assert.True(report.ReadyForAuditorReview, JsonSerializer.Serialize(report.Gaps));
        Assert.Equal("ReviewedExclusion", report.Controls.Single(item => item.ControlId == "164.312(a)(2)(iii)").Status);
    }

    [Fact]
    public void Hipaa_family_documents_must_map_every_safeguard_they_assess()
    {
        var scenario = Hipaa();
        var technical = scenario.Documents.Single(item => item.Type == "hipaa-technical-safeguards");
        scenario.Documents[scenario.Documents.IndexOf(technical)] = technical with
        { ControlIds = technical.ControlIds.Where(id => id != "164.312(d)").ToArray() };
        Assert.Contains(Inspect(scenario).Gaps, gap => gap.Code == "HipaaSafeguardMappingIncomplete");
    }

    [Fact]
    public void Hipaa_encryption_and_breach_window_declarations_are_validated_fully()
    {
        var scenario = Hipaa();
        scenario.Change("hipaa-technical-safeguards", body =>
            body["encryptionAtRest"] = new JsonObject { ["algorithms"] = new JsonArray(), ["coverage"] = "Nothing declared" });
        Assert.Contains(Inspect(scenario).Gaps, gap => gap.Code == "HipaaEncryptionEvidenceInvalid" && gap.ControlId == "164.312(a)(2)(iv)");

        scenario = Hipaa();
        scenario.Change("hipaa-breach-notification", body => body["notificationWindowDays"] = 90);
        var report = Inspect(scenario);
        Assert.False(report.ReadyForAuditorReview);
        Assert.Contains(report.Gaps, gap => gap.Code == "HipaaBreachWindowInvalid" && gap.ControlId == "164.404");

        scenario = Hipaa();
        scenario.Change("hipaa-risk-analysis", body => body["assessedAtUtc"] = End.AddDays(2).ToString("O"));
        Assert.Contains(Inspect(scenario).Gaps, gap => gap.Code == "HipaaRiskAnalysisTimingInvalid" && gap.ControlId == "164.308(a)(1)(ii)(A)");
    }

    internal static Scenario Hipaa()
    {
        var scenario = new Scenario(HipaaId);
        scenario.Change("hipaa-risk-analysis", body => body["assessedAtUtc"] = Start.ToString("O"));
        scenario.Change("hipaa-technical-safeguards", body =>
        {
            body["encryptionAtRest"] = new JsonObject { ["algorithms"] = new JsonArray("AES-256-GCM"), ["coverage"] = "Managed PostgreSQL storage volumes and backups" };
            body["encryptionInTransit"] = new JsonObject { ["algorithms"] = new JsonArray("TLS 1.3"), ["coverage"] = "All service edges including payment-provider calls" };
        });
        scenario.Change("hipaa-breach-notification", body => body["notificationWindowDays"] = 60);
        return scenario;
    }

    internal static Scenario Pci()
    {
        var scenario = new Scenario(PciId);
        var rationales = new JsonObject();
        foreach (var control in scenario.Template.Controls.Where(item => item.RequiredDocumentTypes.Contains("pci-saqa-eligibility")))
        { rationales[control.Id] = $"Outsourced to a PCI DSS validated provider; no cardholder data: {control.Id}"; }
        scenario.Change("pci-saqa-eligibility", body =>
        {
            body["saqType"] = "A";
            body["panStoragePosition"] = "none";
            body["tokenizationOnly"] = true;
            body["validatedProviders"] = new JsonArray("Validated Provider A", "Validated Provider B");
            body["eligibilityConfirmations"] = new JsonArray(
                "Cardholder-data functions fully outsourced to validated providers",
                "No electronic storage of cardholder data by the platform",
                "No electronic processing of cardholder data by the platform",
                "No electronic transmission of cardholder data by the platform",
                "No cardholder data on any platform system or network");
            body["requirementRationales"] = rationales;
            body["providerAttestationReferences"] = new JsonArray("AOC-2026-PROVIDER-A");
            body["reviewedAtUtc"] = Start.ToString("O");
        });
        return scenario;
    }

    private static CompliancePackageValidationReport Inspect(Scenario scenario) => new ComplianceEvidenceValidationEngine().Inspect(
        scenario.Template, scenario.Request, scenario.Documents, Datasets(scenario.Template), End.AddDays(1));

    private static IReadOnlyList<ComplianceEvidenceDataset> Datasets(ComplianceFrameworkTemplate template) => template.Controls.SelectMany(item => item.AutomaticEvidence)
        .Distinct().Select(kind => new ComplianceEvidenceDataset(kind, "Synthetic test observations", JsonSerializer.SerializeToUtf8Bytes(new[]
        { new { observedAtUtc = Start }, new { observedAtUtc = End } }), 2, Start, End, [DateOnly.FromDateTime(Start), DateOnly.FromDateTime(End)], [])).ToArray();
}
