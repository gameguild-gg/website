using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.Compliance.Audit;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class ComplianceFrameworkCatalogTests
{
    [Fact]
    public void Iso_ISMS_profile_includes_management_clauses_and_the_Annex_A_reference_set()
    {
        var template = new ComplianceFrameworkCatalog().Find("iso27001-2022-isms-evidence-v2");
        Assert.NotNull(template);
        Assert.Equal("ISO/IEC27001:2022+Amd1:2024", template.Version);
        Assert.Equal(100, template.Controls.Count);
        Assert.All(Enumerable.Range(4, 7), number => Assert.Contains(template.Controls, item => item.Id == $"ISMS.{number}"));
        Assert.Contains(template.Documents, item => item.Type == "iso-soa");
        Assert.Contains(template.Documents, item => item.Type == "iso-context" && item.RequiredFields.Contains("climateRelevanceAssessment"));
    }

    [Fact]
    public void Gdpr_profile_maps_processing_obligations_and_requires_DPIA_screening()
    {
        var template = new ComplianceFrameworkCatalog().Find("gdpr-2016-679-evidence-v1");
        Assert.NotNull(template);
        Assert.Equal(ComplianceFramework.GDPR, template.Framework);
        Assert.Equal(43, template.Controls.Count);
        Assert.All(new[] { 3, 5, 6, 12, 22, 28, 30, 32, 33, 34, 35, 36, 39, 44, 49, 89 }, article =>
            Assert.Contains(template.Controls, item => item.Id == $"GDPR.Art.{article}"));
        Assert.Contains(template.Documents, item => item.Type == "gdpr-dpia");
        Assert.Contains(template.Documents, item => item.Type == "gdpr-dpia-screening");
    }

    [Fact]
    public void Catalog_returns_independent_control_document_and_source_collections()
    {
        var catalog = new ComplianceFrameworkCatalog();
        var first = catalog.GetTemplates();
        var template = first.Single(item => item.Id == "gdpr-2016-679-evidence-v1");
        ((string[])template.Sources)[0] = "https://example.test/changed";
        ((string[])template.Documents[0].RequiredFields)[0] = "changed";
        ((string[])template.Controls[0].RequiredDocumentTypes)[0] = "changed";
        var actual = catalog.Find(template.Id)!;
        Assert.DoesNotContain(actual.Sources, item => item.Contains("changed", StringComparison.Ordinal));
        Assert.Equal("frameworkVersion", actual.Documents[0].RequiredFields[0]);
        Assert.Equal("gdpr-accountability", actual.Controls[0].RequiredDocumentTypes[0]);
    }

    [Fact]
    public void Legacy_template_and_previously_signed_archive_keep_their_original_contract()
    {
        var assembly = typeof(ComplianceFrameworkCatalogTests).Assembly;
        using var fixtureStream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(name => name.EndsWith("Fixtures.ComplianceEvidence.LegacyV1.json", StringComparison.Ordinal)))!;
        using var fixture = JsonDocument.Parse(fixtureStream);
        var options = new AuditSigningOptions { ActiveKeyId = "legacy-fixture" };
        options.Keys["legacy-fixture"] = new AuditSigningKeyOptions { PublicKeyPem = fixture.RootElement.GetProperty("publicKeyPem").GetString() };
        var builder = new ComplianceArtifactBuilder(new ComplianceEvidenceValidationEngine(),
            new EcdsaCryptographicSigningService(Options.Create(options)));
        var template = new ComplianceFrameworkCatalog().Find("iso27001-2022-evidence-v1")!;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(template, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("d9038990bc1bf89f3fa50995866d5fe7962bb0e54e51337b9b09706e1d5cd641", Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        var zip = Convert.FromBase64String(fixture.RootElement.GetProperty("zipBase64").GetString()!);
        Assert.True(builder.Verify(zip, Guid.Parse("22222222-2222-4222-8222-222222222222"),
            Guid.Parse("11111111-1111-4111-8111-111111111111")).IsValid);
    }
}
