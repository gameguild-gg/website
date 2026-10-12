using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using GameGuild.Tests.Audit.Unit.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

public sealed partial class CompliancePackagingPostgreSqlHttpTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 15)]
    [InlineData(2, 0)]
    [InlineData(2, 15)]
    public async Task Soc2_profiles_capture_review_seal_and_download_actual_evidence_in_PostgreSql(int type, int mask)
    {
        using var factory = SignedFactory();
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid();
        using var admin = Admin(factory, tenant, user);
        var start = DateTime.UtcNow.Date;
        if (type == 2)
        {
            await using var seed = factory.Services.CreateAsyncScope();
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Set<AuditLog>().AddRange(new[] { AuditCategory.Authentication, AuditCategory.Authorization }.Select(category =>
                new AuditLog { TenantId = tenant, ActionType = "Soc2Seed", ResourceType = "Test", Category = category, CreatedAt = start }));
            await db.SaveChangesAsync();
            Assert.True((await seed.ServiceProvider.GetRequiredService<ITamperEvidentAuditService>().CreateAuditLogAsync(
                tenant, user, "Soc2Seed", "Test", null, null, null, "{}", "High", "192.0.2.1", "test")).IsSuccess);
        }
        var end = type == 1 ? start : new DateTime(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc);
        var scenario = Soc2EvidenceScenario.Create(type, mask, start, end);
        var templates = (await admin.GetFromJsonAsync<List<ComplianceFrameworkTemplate>>(Route + "/templates", JsonOptions))!;
        Assert.Contains(templates, template => template.Id == scenario.Template.Id && template.Version == scenario.Template.Version);
        var actualIds = new List<Guid>();
        Guid? actualSupportId = null;
        var syntheticSupportId = scenario.Documents[0].Id.ToString("D");
        foreach (var captured in scenario.Documents)
        {
            var content = captured.Content;
            if (captured.Type == "soc2-control-matrix")
            {
                content = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(content)
                    .Replace(syntheticSupportId, actualSupportId!.Value.ToString("D"), StringComparison.Ordinal));
            }
            var response = await admin.PostAsJsonAsync(Route + "/documents", new UploadComplianceDocumentRequest
            {
                TemplateId = scenario.Template.Id, Type = captured.Type, Name = captured.Name, MediaType = "application/json",
                ContentBase64 = Convert.ToBase64String(content), SourceUri = captured.SourceUri,
                ValidFromUtc = captured.ValidFromUtc, ValidUntilUtc = captured.ValidUntilUtc, ControlIds = captured.ControlIds.ToList()
            });
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            var stored = (await response.Content.ReadFromJsonAsync<ComplianceDocumentResponse>(JsonOptions))!;
            if (captured.Type == "soc2-supporting-evidence") { actualSupportId = stored.Id; }
            var review = await admin.PostAsJsonAsync($"{Route}/documents/{stored.Id}/review", new ReviewComplianceDocumentRequest
            {
                ExpectedRevision = 1, Decision = ComplianceDocumentReview.Approved,
                Notes = "Reviewed synthetic management/control evidence, not an actual service auditor opinion."
            });
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
        Assert.NotNull(zip.GetEntry($"documents/{actualSupportId:D}/content.json"));
        Assert.Equal(9, zip.Entries.Count(entry => entry.FullName.StartsWith("review/soc2/", StringComparison.Ordinal)));
        using var reader = new StreamReader(zip.GetEntry("review/soc2/control-matrix.json")!.Open());
        using var matrix = JsonDocument.Parse(await reader.ReadToEndAsync());
        Assert.Equal(mask == 0 ? 33 : 61, matrix.RootElement.GetProperty("controls").GetArrayLength());
        Assert.True((await admin.GetFromJsonAsync<ComplianceArtifactVerification>($"{Route}/{package.Summary.Id}/verification", JsonOptions))!.IsValid);
        using var other = Admin(factory, Guid.NewGuid(), user);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Route}/{package.Summary.Id}/download")).StatusCode);
        var missingSupport = request with { DocumentIds = actualIds.Where(id => id != actualSupportId).ToList() };
        var deficient = await admin.PostAsJsonAsync(Route, missingSupport);
        Assert.True(deficient.StatusCode == HttpStatusCode.OK, await deficient.Content.ReadAsStringAsync());
        var deficientPackage = (await deficient.Content.ReadFromJsonAsync<CompliancePackageResponse>(JsonOptions))!;
        Assert.Contains(deficientPackage.Manifest.Validation.Gaps, gap => gap.Code == "Soc2DesignEvidenceMissing");
        Assert.False(deficientPackage.Summary.ReadyForAuditorReview);
    }
}
