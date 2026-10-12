using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

public sealed partial class CompliancePackagingPostgreSqlHttpTests
{
    [Theory]
    [InlineData("iso27001-2022-isms-evidence-v2", 100, "review/iso27001/statement-of-applicability.csv")]
    [InlineData("gdpr-2016-679-evidence-v1", 43, "review/gdpr/dpia-index.csv")]
    public async Task Native_framework_documents_are_reviewed_collected_sealed_and_verified_through_HTTP_PostgreSql(
        string templateId, int controls, string nativePath)
    {
        using var factory = SignedFactory();
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        using var admin = Admin(factory, tenant, user);
        var template = (await admin.GetFromJsonAsync<List<ComplianceFrameworkTemplate>>(Route + "/templates", JsonOptions))!.Single(item => item.Id == templateId);
        Assert.Equal(controls, template.Controls.Count);
        var start = DateTime.UtcNow.Date;
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Set<AuditLog>().AddRange(new[] { AuditCategory.Authentication, AuditCategory.Authorization }.Select(category =>
                new AuditLog { TenantId = tenant, ActionType = "NativeProfileSeed", ResourceType = "Test", Category = category, CreatedAt = start }));
            await db.SaveChangesAsync();
            var signed = await seed.ServiceProvider.GetRequiredService<ITamperEvidentAuditService>().CreateAuditLogAsync(
                tenant, user, "NativeProfileSeed", "Test", null, null, null, "{}", "High", "192.0.2.1", "test");
            Assert.True(signed.IsSuccess);
        }
        var ids = new List<Guid>();
        foreach (var requirement in template.Documents.Where(item => item.Type != "gdpr-prior-consultation"))
        {
            var upload = NativeUpload(template, requirement, start);
            var response = await admin.PostAsJsonAsync(Route + "/documents", upload);
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            var document = (await response.Content.ReadFromJsonAsync<ComplianceDocumentResponse>(JsonOptions))!;
            var reviewed = await admin.PostAsJsonAsync($"{Route}/documents/{document.Id}/review", new ReviewComplianceDocumentRequest
            { ExpectedRevision = 1, Decision = ComplianceDocumentReview.Approved, Notes = "Review of synthetic native profile evidence." });
            Assert.True(reviewed.StatusCode == HttpStatusCode.OK, await reviewed.Content.ReadAsStringAsync());
            ids.Add(document.Id);
        }
        var request = new CreateCompliancePackageRequest
        { TemplateId = templateId, Name = "Native framework PostgreSQL proof", PeriodStartUtc = start, PeriodEndUtc = DateTime.UtcNow, DocumentIds = ids };
        var prepared = await admin.PostAsJsonAsync(Route, request);
        Assert.True(prepared.StatusCode == HttpStatusCode.OK, await prepared.Content.ReadAsStringAsync());
        var package = (await prepared.Content.ReadFromJsonAsync<CompliancePackageResponse>(JsonOptions))!;
        Assert.True(package.Summary.ReadyForAuditorReview, JsonSerializer.Serialize(package.Manifest.Validation.Gaps));
        var content = await admin.GetByteArrayAsync($"{Route}/{package.Summary.Id}/download");
        using (var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read))
        {
            using var reader = new StreamReader(zip.GetEntry(nativePath)!.Open());
            Assert.Contains(template.Framework == ComplianceFramework.ISO27001 ? "CUSTOM.1" : "ACT-1", await reader.ReadToEndAsync());
        }
        Assert.True((await admin.GetFromJsonAsync<ComplianceArtifactVerification>($"{Route}/{package.Summary.Id}/verification", JsonOptions))!.IsValid);
        using var other = Admin(factory, Guid.NewGuid(), user);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Route}/{package.Summary.Id}/download")).StatusCode);
        var excluded = request with { Exclusions = [new() { ControlId = template.Framework == ComplianceFramework.ISO27001 ? "ISMS.4" : "GDPR.Art.35", Rationale = "Bypass mandatory scope review", ApplicabilityDocumentId = ids[0] }] };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Route, excluded)).StatusCode);
        if (template.Framework == ComplianceFramework.GDPR)
        {
            await AssertMissingDpia(admin, request, template);
        }
    }

    private static async Task AssertMissingDpia(HttpClient admin, CreateCompliancePackageRequest request, ComplianceFrameworkTemplate template)
    {
        var list = (await admin.GetFromJsonAsync<List<ComplianceDocumentResponse>>(Route + "/documents", JsonOptions))!;
        var dpia = list.Single(item => item.Type == "gdpr-dpia" && item.TemplateId == template.Id);
        var reduced = request with { DocumentIds = request.DocumentIds.Where(id => id != dpia.Id).ToList() };
        var response = await admin.PostAsJsonAsync(Route, reduced);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var package = (await response.Content.ReadFromJsonAsync<CompliancePackageResponse>(JsonOptions))!;
        Assert.False(package.Summary.ReadyForAuditorReview);
        Assert.Contains(package.Manifest.Validation.Gaps, gap => gap.Code == "MissingDPIA");
        Assert.True((await admin.GetFromJsonAsync<ComplianceArtifactVerification>($"{Route}/{package.Summary.Id}/verification", JsonOptions))!.IsValid);
    }

    private static UploadComplianceDocumentRequest NativeUpload(ComplianceFrameworkTemplate template, ComplianceDocumentRequirement requirement, DateTime start)
    {
        var body = new JsonObject();
        foreach (var field in requirement.RequiredFields) { body[field] = "Reviewed synthetic declaration"; }
        body["frameworkVersion"] = template.Version;
        body["assessmentStatus"] = "satisfactory";
        if (requirement.RequiresControlAssessments)
        {
            body["controlAssessments"] = JsonSerializer.SerializeToNode(template.Controls.ToDictionary(item => item.Id, item => new
            { owner = "Test assessor", assessmentStatus = "satisfactory", controlImplementation = "Synthetic implementation", effectivenessEvidence = "Synthetic test evidence" }));
        }
        FillNativeBody(template, requirement.Type, start, body);
        var mapped = template.Controls.Where(item => item.RequiredDocumentTypes.Contains(requirement.Type)).Select(item => item.Id).ToList();
        if (requirement.Type == "gdpr-dpia") { mapped = ["GDPR.Art.35", "GDPR.Art.36"]; }
        return new() { TemplateId = template.Id, Name = "Synthetic " + requirement.Type, Type = requirement.Type,
            MediaType = "application/json", SourceUri = "https://example.test/native-evidence", ValidFromUtc = start.AddDays(-2), ValidUntilUtc = start.AddDays(2),
            ControlIds = mapped, ContentBase64 = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(body)) };
    }

    private static void FillNativeBody(ComplianceFrameworkTemplate template, string type, DateTime start, JsonObject body)
    {
        switch (type)
        {
            case "iso-context": body["climateRelevanceAssessment"] = JsonSerializer.SerializeToNode(new { relevant = false, rationale = "Reviewed context", interestedPartyRequirements = "Reviewed requirements" }); break;
            case "iso-soa": FillSoa(template, body); break;
            case "gdpr-accountability": body["processingScope"] = JsonSerializer.SerializeToNode(new { activityIds = new[] { "ACT-1" }, roles = new[] { "controller" }, rationale = "Reviewed scope" }); break;
            case "gdpr-processing-records": body["processingActivities"] = JsonSerializer.SerializeToNode(new[] { new
                { activityId = "ACT-1", role = "controller", controllerContact = "Test controller", purposes = "Service", subjectCategories = "Users", dataCategories = "Contact", recipientCategories = "Processors", transfers = "None", retention = "Reviewed schedule", securityMeasures = "Reviewed safeguards" } }); break;
            case "gdpr-dpia-screening": body["screenings"] = JsonSerializer.SerializeToNode(new[] { new
                { activityId = "ACT-1", dpiaRequired = true, rationale = "Reviewed risk", supervisoryAuthorityListsReview = "Authority lists reviewed", reviewTriggers = "Processing changes" } }); break;
            case "gdpr-dpia":
                body["activityId"] = "ACT-1";
                body["residualRiskDecision"] = JsonSerializer.SerializeToNode(new { level = "low", rationale = "Mitigations reviewed" });
                body["consultation"] = JsonSerializer.SerializeToNode(new { dpoAdvice = "Reviewed advice", dataSubjectViews = "Reviewed views or applicability rationale" });
                body["assessmentTiming"] = JsonSerializer.SerializeToNode(new { assessedAtUtc = start.AddDays(-1), effectiveAtUtc = start, basis = "initial-processing" });
                break;
            default: break; // The remaining document types use the common synthetic required-field declarations.
        }
    }

    private static void FillSoa(ComplianceFrameworkTemplate template, JsonObject body)
    {
        var annex = template.Controls.Where(item => item.Id.StartsWith("A.", StringComparison.Ordinal)).Select(item => item.Id).ToArray();
        body["annexADecisions"] = JsonSerializer.SerializeToNode(annex.ToDictionary(id => id, id => new
            { applicable = true, inclusionJustification = "Risk treatment", necessaryControlIds = new[] { "NC-" + id } }));
        body["necessaryControls"] = JsonSerializer.SerializeToNode(annex.Select(id => new
            { id = "NC-" + id, description = "Synthetic control", inclusionJustification = "Reviewed risk", implementationStatus = "implemented", effectivenessEvidence = "Synthetic review", annexAReferences = new[] { id } })
            .Append(new { id = "CUSTOM.1", description = "Custom control", inclusionJustification = "Reviewed custom risk", implementationStatus = "implemented", effectivenessEvidence = "Synthetic review", annexAReferences = Array.Empty<string>() }));
    }
}
