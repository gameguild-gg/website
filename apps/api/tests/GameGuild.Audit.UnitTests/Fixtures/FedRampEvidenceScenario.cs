using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using GameGuild.Compliance.Audit;

namespace GameGuild.Tests.Audit.Unit.Fixtures;

/// <summary>Synthetic evidence only; no assertion of an actual federal certification.</summary>
public sealed class FedRampEvidenceScenario
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public required ComplianceFrameworkTemplate Template { get; init; }
    public required CreateCompliancePackageRequest Request { get; init; }
    public List<ComplianceDocumentSnapshot> Documents { get; } = [];
    public List<ComplianceEvidenceDataset> Datasets { get; } = [];
    public DateTime CapturedAtUtc { get; private set; }

    public static FedRampEvidenceScenario Create() => Create("B", "Program");
    public static FedRampEvidenceScenario Create(string classId, string path) =>
        Create(classId, path, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc));
    public static FedRampEvidenceScenario Create(string classId, string path, DateTime startUtc, DateTime endUtc)
    {
        var start = startUtc;
        var end = endUtc;
        var template = new ComplianceFrameworkCatalog().Find($"fedramp-2026-rev5-{classId.ToLowerInvariant()}-{path.ToLowerInvariant()}-evidence-v1")!;
        var scenario = new FedRampEvidenceScenario { Template = template, CapturedAtUtc = end.AddSeconds(1), Request = new()
        { Name = "Synthetic Rev5 evidence", TemplateId = template.Id, PeriodStartUtc = start, PeriodEndUtc = end } };
        var assembly = typeof(ComplianceFrameworkCatalog).Assembly;
        using var resource = assembly.GetManifestResourceStream("GameGuild.Compliance.Audit.Resources.FedRamp2026.catalog.json")!;
        var catalog = JsonNode.Parse(resource)!;
        var rules = catalog["rules"]!.AsArray().Where(rule => rule!["classes"]!.AsArray().Any(item => item!.GetValue<string>() == classId) &&
            rule["paths"]!.AsArray().Any(item => item!.GetValue<string>() == path)).ToArray();
        var baseline = catalog["baselines"]![classId]!.AsObject();
        var all = template.Controls.Select(control => control.Id).ToArray();
        var reportId = Guid.NewGuid();
        scenario.Add("fedramp-supporting-evidence", new JsonObject { ["evidenceDescription"] = "Synthetic recognized assessor report, review and validation results." }, ["FRC-APP-FIA"], reportId);
        scenario.Add("fedramp-profile", new JsonObject
        {
            ["certificationProfile"] = new JsonObject { ["type"] = "Rev5", ["class"] = classId, ["path"] = path, ["rulesVersion"] = "2026.09.13.02" },
            ["packagePhase"] = "application", ["packageVerifiedAtUtc"] = end.ToString("O"), ["packageValidatedAtUtc"] = end.ToString("O"),
            ["independentAssessment"] = new JsonObject { ["completedAtUtc"] = end.AddDays(-1).ToString("O"), ["recognizedAssessor"] = true,
                ["assessor"] = "Synthetic recognized assessor", ["reportDocumentId"] = reportId.ToString("D") }
        }, all);
        var overview = new JsonObject
        {
            ["serviceIdentification"] = new JsonObject { ["providerName"] = "Synthetic provider", ["serviceName"] = "Evidence fixture", ["serviceAcronym"] = "EF",
                ["serviceDescription"] = "Synthetic cloud service", ["certificationType"] = "Rev5", ["fedRampPackageId"] = "Synthetic-EF",
                ["website"] = "https://example.test", ["logo"] = "https://example.test/logo.svg" },
            ["serviceProperties"] = new JsonObject { ["serviceType"] = new JsonArray("SaaS"), ["deploymentModel"] = "Public Cloud" },
            ["contactInformation"] = new JsonArray(Contact("Security"), Contact("Sales"))
        };
        scenario.Add("fedramp-overview", Payload("certification-package-overview", overview), rules.Where(rule => Schema(rule) == "certification-package-overview").Select(rule => rule!["id"]!.GetValue<string>()).ToArray());
        var decisions = new JsonObject();
        var requirements = new JsonArray();
        foreach (var rule in rules)
        {
            var id = rule!["id"]!.GetValue<string>();
            var applicable = Schema(rule) is "" or "certification-package-overview" or "security-decision-record" or "ongoing-certification-report";
            decisions[id] = Decision(applicable);
            requirements.Add(new JsonObject { ["frrID"] = id, ["frrImplementationStatus"] = applicable ? "Implemented" : "Not Implemented",
                ["frrImplementation"] = new JsonArray("Reviewed synthetic implementation or conditional absence"),
                ["frrValidation"] = new JsonArray("Synthetic internal validation"), ["frrAssessment"] = new JsonArray("Synthetic independent assessment") });
        }
        var controls = new JsonArray();
        foreach (var (id, parameters) in baseline)
        {
            decisions[id] = Decision(true);
            controls.Add(new JsonObject { ["controlId"] = id, ["controlImplementationStatus"] = "Implemented",
                ["controlImplementationDescription"] = "Synthetic implementation, independently reviewed", ["parameterValues"] = new JsonArray(parameters!.AsArray().Select(parameter =>
                    (JsonNode)new JsonObject { ["parameterId"] = parameter!.GetValue<string>(), ["parameterValue"] = "Synthetic organisation-defined value" }).ToArray()) });
        }
        var sdr = Payload("security-decision-record", new JsonObject { ["certificationPackageOverviewUri"] = OverviewUri,
            ["metadata"] = new JsonObject { ["version"] = "test-1", ["lastUpdated"] = end.ToString("O"), ["updateSource"] = "Synthetic reviewer" },
            ["fedRampRequirements"] = requirements, ["securityControls"] = controls });
        sdr["decisions"] = decisions;
        scenario.Add("fedramp-sdr", sdr, all);
        scenario.Add("fedramp-ocr", Payload("ongoing-certification-report", new JsonObject
        {
            ["certificationPackageOverviewUri"] = OverviewUri,
            ["reportPeriod"] = new JsonObject { ["from"] = start.ToString("yyyy-MM-dd"), ["to"] = end.ToString("yyyy-MM-dd") },
            ["certificationDataChanges"] = new JsonArray(), ["plannedCertificationDataChanges"] = new JsonObject
            { ["planningHorizonThrough"] = end.AddMonths(3).ToString("yyyy-MM-dd"), ["changes"] = new JsonArray() },
            ["acceptedVulnerabilities"] = "No accepted vulnerabilities in this synthetic period", ["transformativeChanges"] = new JsonArray(),
            ["updatedRecommendations"] = new JsonArray(), ["activeAgencies"] = new JsonArray(), ["reportableIncidents"] = new JsonObject { ["incidents"] = new JsonArray() }
        }), ["CCM-OCR-AVL"]);
        foreach (var kind in new[] { ComplianceEvidenceKind.Operations, ComplianceEvidenceKind.Authentication, ComplianceEvidenceKind.Authorization, ComplianceEvidenceKind.Integrity })
        {
            var dates = Enumerable.Range(0, (end.Date - start.Date).Days + 1).Select(offset => DateOnly.FromDateTime(start.AddDays(offset))).ToArray();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(dates.Select(date => new { observedAtUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) }), Json);
            scenario.Datasets.Add(new(kind, "synthetic", bytes, dates.Length, start, end.Date, dates, []));
        }
        scenario.Refresh();
        return scenario;
    }

    private const string OverviewUri = "https://evidence.example.test/overview.json";
    private static JsonObject Contact(string type) => new() { ["contactType"] = type, ["contactName"] = "Synthetic contact", ["contactEmail"] = "contact@example.test", ["contactPhone"] = "202-555-0101" };
    private static string Schema(JsonNode? rule) => rule?["schemaUri"]?.GetValue<string>()?.Replace("https://fedramp.gov/schemas/fedramp-", "", StringComparison.Ordinal).Replace("-schema-2026-06-24.json", "", StringComparison.Ordinal) ?? "";
    public static JsonObject Payload(string schema, JsonObject payload) => new() { ["schemaUri"] = $"https://fedramp.gov/schemas/fedramp-{schema}-schema-2026-06-24.json", ["payload"] = payload };
    private static JsonObject Decision(bool applicable) => new()
    {
        ["applicable"] = applicable, ["applicabilityRationale"] = applicable ? "Synthetic scoped requirement" : "No corresponding incident, change or vulnerability activity in this synthetic fixture",
        ["implementationStatus"] = applicable ? "Implemented" : "Not Applicable", ["implementation"] = "Synthetic reviewed implementation or reason",
        ["verification"] = "Synthetic internal design verification", ["validation"] = "Synthetic internal operation validation",
        ["independentVerification"] = "Synthetic independent design verification", ["independentValidation"] = "Synthetic independent operation validation",
        ["commentResponses"] = "No unresolved synthetic reviewer comments", ["customerRisk"] = "Conditional absence reviewed for synthetic fixture only",
        ["seniorOfficialAcceptance"] = "Synthetic senior official accepted the conditional decision"
    };

    public void Add(string type, JsonObject body, IReadOnlyList<string> controls) => Add(type, body, controls, Guid.NewGuid());
    public void Add(string type, JsonObject body, IReadOnlyList<string> controls, Guid id)
    {
        body["frameworkVersion"] = Template.Version; body["assessmentStatus"] = "satisfactory"; body["owner"] = "Synthetic evidence owner";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(body, Json);
        Documents.Add(new(id, type, type, "application/json", Hash(bytes), bytes,
            type == "fedramp-overview" ? OverviewUri : "https://evidence.example.test/" + type,
            Request.PeriodStartUtc.UtcDateTime.AddDays(-1), Request.PeriodEndUtc.UtcDateTime.AddDays(1), controls, new Dictionary<string, string>(),
            ComplianceDocumentReview.Approved, Guid.NewGuid(), Guid.NewGuid(), Request.PeriodEndUtc.UtcDateTime, 1, Template.Id));
        Refresh();
    }
    public void Change(string type, Action<JsonObject> change)
    {
        var index = Documents.FindIndex(document => document.Type == type);
        var root = JsonNode.Parse(Documents[index].Content)!.AsObject(); change(root);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(root, Json);
        Documents[index] = Documents[index] with { Content = bytes, ContentSha256 = Hash(bytes) };
    }
    public void Refresh() { Request.DocumentIds.Clear(); Request.DocumentIds.AddRange(Documents.Select(document => document.Id)); }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public CompliancePackageValidationReport Inspect() => new ComplianceEvidenceValidationEngine().Inspect(Template, Request, Documents, Datasets, CapturedAtUtc);
}
