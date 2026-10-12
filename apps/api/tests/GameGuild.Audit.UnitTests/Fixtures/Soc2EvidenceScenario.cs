using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GameGuild.Compliance.Audit;

namespace GameGuild.Tests.Audit.Unit.Fixtures;

/// <summary>Synthetic management and control evidence; never an actual auditor opinion.</summary>
public sealed class Soc2EvidenceScenario
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public required ComplianceFrameworkTemplate Template { get; init; }
    public required CreateCompliancePackageRequest Request { get; init; }
    public required DateTime CapturedAtUtc { get; init; }
    public List<ComplianceDocumentSnapshot> Documents { get; } = [];
    public List<ComplianceEvidenceDataset> Datasets { get; } = [];

    public static Soc2EvidenceScenario Create(int type, int mask)
    {
        var start = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        return Create(type, mask, start, type == 1 ? start : start.AddDays(1));
    }

    public static Soc2EvidenceScenario Create(int type, int mask, DateTime start, DateTime end)
    {
        var categories = new List<string> { "Security" };
        var slug = new StringBuilder("security");
        var names = new[] { "Availability", "ProcessingIntegrity", "Confidentiality", "Privacy" };
        var slugs = new[] { "availability", "processing-integrity", "confidentiality", "privacy" };
        for (var index = 0; index < 4; index++)
        { if ((mask & (1 << index)) != 0) { categories.Add(names[index]); slug.Append('-').Append(slugs[index]); } }
        var template = new ComplianceFrameworkCatalog().Find($"soc2-tsc2017-type{type}-{slug}-evidence-v1")!;
        var scenario = new Soc2EvidenceScenario
        {
            Template = template, CapturedAtUtc = end.AddSeconds(1),
            Request = new() { Name = "Synthetic SOC2 evidence", TemplateId = template.Id, PeriodStartUtc = start, PeriodEndUtc = end }
        };
        var all = template.Controls.Select(item => item.Id).ToArray();
        var supportId = Guid.NewGuid();
        scenario.Add("soc2-supporting-evidence", new() { ["evidenceDescription"] = "Synthetic approved design reports and operating test observations." }, all, supportId);
        scenario.Add("soc2-system-description", new()
        {
            ["reportType"] = type, ["categories"] = new JsonArray(categories.Select(item => (JsonNode)JsonValue.Create(item)!).ToArray()),
            ["periodStartUtc"] = start.ToString("O"), ["periodEndUtc"] = end.ToString("O"), ["systemName"] = "Synthetic system",
            ["boundaries"] = "Synthetic service boundary", ["services"] = "Synthetic hosted service",
            ["serviceCommitments"] = "Synthetic commitments", ["systemRequirements"] = "Synthetic requirements",
            ["components"] = new JsonObject { ["infrastructure"] = "Synthetic hosting", ["software"] = "Synthetic software",
                ["people"] = "Synthetic assigned staff", ["procedures"] = "Synthetic reviewed procedures", ["data"] = "Synthetic records" },
            ["systemIncidents"] = "No material incidents in the synthetic fixture", ["systemChanges"] = "No material changes in the synthetic fixture",
            ["complementaryUserEntityControls"] = new JsonArray(), ["subserviceOrganizations"] = new JsonArray(),
            ["guidanceReview"] = "Synthetic review of the applicable current AICPA description guidance"
        }, all);
        scenario.Add("soc2-management-assertion", new()
        {
            ["reportType"] = type, ["periodStartUtc"] = start.ToString("O"), ["periodEndUtc"] = end.ToString("O"),
            ["fairPresentation"] = true, ["suitableDesign"] = true, ["operatingEffectiveness"] = type == 2,
            ["signatory"] = "Synthetic management signatory", ["signedAtUtc"] = end.ToString("O")
        }, all);
        var mappings = new JsonObject();
        var controls = new JsonArray();
        foreach (var criterion in all)
        {
            var id = "control-" + criterion;
            mappings[criterion] = new JsonObject { ["controlIds"] = new JsonArray(id), ["mappingRationale"] = "Synthetic criterion-specific assessment",
                ["pointOfFocusReview"] = "Synthetic assessment of relevant current points of focus", ["assessmentStatus"] = "satisfactory" };
            var control = new JsonObject
            {
                ["controlId"] = id, ["description"] = "Synthetic control description", ["owner"] = "Synthetic assigned owner",
                ["designBasis"] = "Synthetic suitable-design review", ["designConclusion"] = "suitable",
                ["designEvidenceDocumentIds"] = new JsonArray(supportId.ToString("D"))
            };
            if (type == 2)
            {
                control["testProcedures"] = "Synthetic test of the selected population";
                control["populationCount"] = 5; control["sampleCount"] = 1; control["deviationCount"] = 0;
                control["operatingConclusion"] = "effective";
                control["testedFromUtc"] = start.ToString("O"); control["testedUntilUtc"] = end.ToString("O");
                control["samples"] = new JsonArray(new JsonObject { ["sampleId"] = id + "-sample", ["observedAtUtc"] = start.ToString("O"),
                    ["result"] = "pass", ["evidenceDocumentId"] = supportId.ToString("D") });
            }
            controls.Add(control);
        }
        scenario.Add("soc2-control-matrix", new() { ["criteriaMappings"] = mappings, ["controls"] = controls,
            ["guidanceReview"] = "Synthetic review of relevant 2022 points of focus and assessment judgment" }, all);
        foreach (var kind in template.Controls.SelectMany(item => item.AutomaticEvidence).Distinct())
        {
            var dates = Enumerable.Range(0, (end.Date - start.Date).Days + 1).Select(offset => DateOnly.FromDateTime(start.AddDays(offset))).ToArray();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(dates.Select(date => new { observedAtUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) }), Json);
            scenario.Datasets.Add(new(kind, "synthetic", bytes, dates.Length, start, end.Date, dates, []));
        }
        return scenario;
    }

    public void Add(string type, JsonObject body, IReadOnlyList<string> controls) => Add(type, body, controls, Guid.NewGuid());
    public void Add(string type, JsonObject body, IReadOnlyList<string> controls, Guid id)
    {
        body["frameworkVersion"] = Template.Version; body["assessmentStatus"] = "satisfactory"; body["owner"] = "Synthetic evidence owner";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(body, Json);
        Documents.Add(new(id, type, type, "application/json", Hash(bytes), bytes, "https://evidence.example.test/" + type,
            Request.PeriodStartUtc.UtcDateTime.AddDays(-1), Request.PeriodEndUtc.UtcDateTime.AddDays(1), controls, new Dictionary<string, string>(),
            ComplianceDocumentReview.Approved, Guid.NewGuid(), Guid.NewGuid(), Request.PeriodEndUtc.UtcDateTime, 1, Template.Id));
        Request.DocumentIds.Add(id);
    }

    public void Change(string type, Action<JsonObject> change)
    {
        var index = Documents.FindIndex(document => document.Type == type);
        var root = JsonNode.Parse(Documents[index].Content)!.AsObject(); change(root);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(root, Json);
        Documents[index] = Documents[index] with { Content = bytes, ContentSha256 = Hash(bytes) };
    }

    public CompliancePackageValidationReport Inspect() => new ComplianceEvidenceValidationEngine().Inspect(Template, Request, Documents, Datasets, CapturedAtUtc);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
