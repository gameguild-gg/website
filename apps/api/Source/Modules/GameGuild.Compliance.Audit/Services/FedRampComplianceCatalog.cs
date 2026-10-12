using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Json.Schema;

namespace GameGuild.Compliance.Audit;

/// <summary>Pinned 2026 Rev5 provider profiles, tailored by class and certification path.</summary>
internal static class FedRampComplianceCatalog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly CatalogData Data = JsonSerializer.Deserialize<CatalogData>(ReadResource("catalog.json"), Json)!;
    private static readonly Profile[] Profiles = (from classId in new[] { "B", "C", "D" }
        from path in new[] { "Program", "Agency" }
        select new Profile(classId, path, Data.Baselines[classId],
            Data.Rules.Where(rule => rule.Classes.Contains(classId) && rule.Paths.Contains(path)).ToArray())).ToArray();
    private static readonly Lazy<IReadOnlyDictionary<string, JsonSchema>> CompiledSchemas = new(BuildSchemas);

    internal sealed record Rule(string Id, string Name, string[] Classes, string[] Paths, string Subset,
        Dictionary<string, string> Force, string SourceUri, JsonElement Effective, string? SchemaUri);
    internal sealed record SchemaSource(string Uri, string Filename, string Version, string Sha256);
    private sealed record CatalogData(string RulesVersion, Dictionary<string, Dictionary<string, string[]>> Baselines,
        Rule[] Rules, SchemaSource[] Schemas);
    internal sealed record Profile(string Class, string Path, IReadOnlyDictionary<string, string[]> Baseline, IReadOnlyList<Rule> Rules)
    {
        internal string Id => $"fedramp-2026-rev5-{Class.ToLowerInvariant()}-{Path.ToLowerInvariant()}-evidence-v1";
        internal string Version => $"FedRAMP-2026/Rev5/{Class}/{Path}@{Data.RulesVersion}";
    }

    internal static Profile? Find(string id) => Profiles.SingleOrDefault(profile => profile.Id == id);
    internal static IReadOnlyList<ComplianceFrameworkTemplate> Templates() => Profiles.Select(Build).ToArray();
    internal static string PinnedRulesVersion => Data.RulesVersion;
    internal static IReadOnlyList<SchemaSource> SchemaSources => Data.Schemas;

    private static ComplianceFrameworkTemplate Build(Profile profile)
    {
        const string rules = "https://github.com/FedRAMP/rules/blob/58487bda77d76d9ce334304ec2e779ece7cc7d54/fedramp-consolidated-rules.json";
        const string nist = "https://github.com/FedRAMP/2026/blob/f3819f13210fe2a5ccb51bfb2df0833608b09079/tools/data/NIST_SP-800-53_rev5_catalog.xml";
        var controls = profile.Rules.Select(rule => new ComplianceControlTemplate(rule.Id, rule.SourceUri, [],
            rule.Id == "FRC-CSO-PKG" ? ["fedramp-profile", "fedramp-sdr", "fedramp-overview"] :
            rule.Id == "CCM-OCR-AVL" ? ["fedramp-profile", "fedramp-sdr", "fedramp-ocr"] : ["fedramp-profile", "fedramp-sdr"])).ToList();
        controls.AddRange(profile.Baseline.Keys.Select(id => new ComplianceControlTemplate(id, rules,
            id.StartsWith("AU-", StringComparison.Ordinal) ? [ComplianceEvidenceKind.Operations, ComplianceEvidenceKind.Integrity] :
            id.StartsWith("IA-", StringComparison.Ordinal) ? [ComplianceEvidenceKind.Authentication] :
            id.StartsWith("AC-", StringComparison.Ordinal) ? [ComplianceEvidenceKind.Authorization] : [], ["fedramp-profile", "fedramp-sdr"])));
        return new(profile.Id, ComplianceFramework.FedRAMP, profile.Version, ComplianceEvidencePeriodMode.Period,
            [rules, nist, "https://www.fedramp.gov/2026/providers/rev5/rules/fedramp-certification/"], controls,
            [Requirement("fedramp-profile", "certificationProfile", "packagePhase", "packageVerifiedAtUtc", "packageValidatedAtUtc", "independentAssessment"),
             Requirement("fedramp-overview", "schemaUri", "payload"), Requirement("fedramp-sdr", "schemaUri", "payload", "decisions"),
             Requirement("fedramp-ocr", "schemaUri", "payload"), Requirement("fedramp-artifact", "schemaUri", "payload"),
             Requirement("fedramp-supporting-evidence", "evidenceDescription")]);
    }

    private static ComplianceDocumentRequirement Requirement(string type, params string[] fields) =>
        new(type, ["frameworkVersion", "assessmentStatus", "owner", .. fields], true);

    internal static string DocumentSchemaUri(string type) => "https://fedramp.gov/schemas/" + (type switch
    {
        "fedramp-overview" => "fedramp-certification-package-overview-schema-2026-06-24.json",
        "fedramp-sdr" => "fedramp-security-decision-record-schema-2026-06-24.json",
        "fedramp-ocr" => "fedramp-ongoing-certification-report-schema-2026-06-24.json",
        _ => string.Empty
    });

    internal static bool IsSchemaValid(string uri, JsonElement payload) =>
        CompiledSchemas.Value.TryGetValue(uri, out var schema) && payload.ValueKind == JsonValueKind.Object &&
        schema.Evaluate(payload, new EvaluationOptions { RequireFormatValidation = true, OutputFormat = OutputFormat.Flag }).IsValid;

    private static IReadOnlyDictionary<string, JsonSchema> BuildSchemas()
    {
        var options = new BuildOptions { Dialect = Dialect.Draft202012, SchemaRegistry = new SchemaRegistry() };
        // Only bundled, checksum-pinned government schemas are resolved. No HTTP or user schemas.
        var texts = Data.Schemas.ToDictionary(item => item.Uri, item =>
        {
            var bytes = ReadResource(item.Filename);
            if (Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != item.Sha256)
            { throw new InvalidOperationException("The bundled FedRAMP schema checksum changed."); }
            return Encoding.UTF8.GetString(bytes);
        }, StringComparer.Ordinal);
        options.SchemaRegistry.Fetch = (_, _) => throw new InvalidOperationException("Unbundled FedRAMP schema reference.");
        // The nine pinned schemas reference only this shared document and their own local definitions.
        // Pre-register it to resolve pointers without re-registering an already compiled schema.
        const string common = "https://fedramp.gov/schemas/fedramp-common-definitions-schema-2026-06-24.json";
        return texts.OrderBy(item => item.Key == common ? 0 : 1)
            .ToDictionary(item => item.Key, item => JsonSchema.FromText(item.Value, options), StringComparer.Ordinal);
    }

    private static byte[] ReadResource(string filename)
    {
        var assembly = typeof(FedRampComplianceCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream($"GameGuild.Compliance.Audit.Resources.FedRamp2026.{filename}")
            ?? throw new InvalidOperationException("The pinned FedRAMP resource is missing.");
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
