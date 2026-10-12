using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using GameGuild.Tests.Audit.Unit.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

public sealed partial class CompliancePackagingPostgreSqlHttpTests
{
    [Theory]
    [InlineData("B", "Program")]
    [InlineData("B", "Agency")]
    [InlineData("C", "Program")]
    [InlineData("C", "Agency")]
    [InlineData("D", "Program")]
    [InlineData("D", "Agency")]
    public async Task FedRamp_provider_profiles_capture_review_seal_and_download_native_payloads_in_PostgreSql(string classId, string path)
    {
        using var factory = SignedFactory();
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid();
        using var admin = Admin(factory, tenant, user);
        var start = DateTime.UtcNow.Date;
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Set<AuditLog>().AddRange(new[] { AuditCategory.Authentication, AuditCategory.Authorization }.Select(category =>
                new AuditLog { TenantId = tenant, ActionType = "FedRampSeed", ResourceType = "Test", Category = category, CreatedAt = start }));
            await db.SaveChangesAsync();
            Assert.True((await seed.ServiceProvider.GetRequiredService<ITamperEvidentAuditService>().CreateAuditLogAsync(
                tenant, user, "FedRampSeed", "Test", null, null, null, "{}", "High", "192.0.2.1", "test")).IsSuccess);
        }
        var scenario = FedRampEvidenceScenario.Create(classId, path, start, DateTime.UtcNow);
        var templates = (await admin.GetFromJsonAsync<List<ComplianceFrameworkTemplate>>(Route + "/templates", JsonOptions))!;
        Assert.Contains(templates, template => template.Id == scenario.Template.Id && template.Version == scenario.Template.Version);
        var actualIds = new List<Guid>();
        Guid? actualReportId = null;
        foreach (var captured in scenario.Documents)
        {
            var content = captured.Content;
            if (captured.Type == "fedramp-profile")
            {
                var root = JsonNode.Parse(content)!;
                root["independentAssessment"]!["reportDocumentId"] = actualReportId!.Value.ToString("D");
                content = JsonSerializer.SerializeToUtf8Bytes(root);
            }
            var upload = new UploadComplianceDocumentRequest
            { TemplateId = scenario.Template.Id, Type = captured.Type, Name = captured.Name, MediaType = "application/json", ContentBase64 = Convert.ToBase64String(content),
                SourceUri = captured.SourceUri, ValidFromUtc = captured.ValidFromUtc, ValidUntilUtc = captured.ValidUntilUtc, ControlIds = captured.ControlIds.ToList() };
            var response = await admin.PostAsJsonAsync(Route + "/documents", upload);
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            var stored = (await response.Content.ReadFromJsonAsync<ComplianceDocumentResponse>(JsonOptions))!;
            if (captured.Type == "fedramp-supporting-evidence") { actualReportId = stored.Id; }
            var review = await admin.PostAsJsonAsync($"{Route}/documents/{stored.Id}/review", new ReviewComplianceDocumentRequest
            { ExpectedRevision = 1, Decision = ComplianceDocumentReview.Approved, Notes = "Reviewed synthetic official-schema evidence; no claim of actual certification." });
            Assert.True(review.StatusCode == HttpStatusCode.OK, await review.Content.ReadAsStringAsync());
            actualIds.Add(stored.Id);
        }
        var request = scenario.Request with { DocumentIds = actualIds };
        var prepared = await admin.PostAsJsonAsync(Route, request);
        Assert.True(prepared.StatusCode == HttpStatusCode.OK, await prepared.Content.ReadAsStringAsync());
        var package = (await prepared.Content.ReadFromJsonAsync<CompliancePackageResponse>(JsonOptions))!;
        Assert.True(package.Summary.ReadyForAuditorReview, JsonSerializer.Serialize(package.Manifest.Validation.Gaps));
        var bytes = await admin.GetByteArrayAsync($"{Route}/{package.Summary.Id}/download");
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.NotNull(zip.GetEntry("review/fedramp/overview.txt"));
        using var reader = new StreamReader(zip.GetEntry("review/fedramp/security-decision-record.json")!.Open());
        using var sdr = JsonDocument.Parse(await reader.ReadToEndAsync());
        Assert.Equal(scenario.Template.Controls.Count - (path == "Program" ? 158 : 162), sdr.RootElement.GetProperty("securityControls").GetArrayLength());
        Assert.True((await admin.GetFromJsonAsync<ComplianceArtifactVerification>($"{Route}/{package.Summary.Id}/verification", JsonOptions))!.IsValid);
        using var other = Admin(factory, Guid.NewGuid(), user);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Route}/{package.Summary.Id}/download")).StatusCode);
        var missingReport = request with { DocumentIds = actualIds.Where(id => id != actualReportId).ToList() };
        var deficient = await admin.PostAsJsonAsync(Route, missingReport);
        Assert.Equal(HttpStatusCode.OK, deficient.StatusCode);
        var deficientPackage = (await deficient.Content.ReadFromJsonAsync<CompliancePackageResponse>(JsonOptions))!;
        Assert.Contains(deficientPackage.Manifest.Validation.Gaps, gap => gap.Code == "FedRampAssessmentReportMissing");
        Assert.False(deficientPackage.Summary.ReadyForAuditorReview);
    }
}
