using System.Text;
using System.Text.Json;
using static GameGuild.Compliance.Audit.ComplianceNativeEvidence;

namespace GameGuild.Compliance.Audit;

/// <summary>Human-readable indexes of the captured declarations, including gaps and source revision paths.</summary>
internal static class ComplianceReviewerFormats
{
    internal static IReadOnlyList<string> RequiredPaths(string templateId) => templateId switch
    {
        ComplianceFrameworkCatalog.IsoIsmsId => ["review/iso27001/statement-of-applicability.csv", "review/iso27001/management-evidence.csv"],
        ComplianceFrameworkCatalog.GdprId => ["review/gdpr/dpia-index.csv", "review/gdpr/processing-records.csv"],
        _ => Soc2ComplianceCatalog.Find(templateId) is not null ? Soc2ReviewerFormats.Paths :
            FedRampComplianceCatalog.Find(templateId) is not null ? FedRampPaths : []
    };

    private static readonly string[] FedRampPaths = ["review/fedramp/overview.json", "review/fedramp/overview.txt",
        "review/fedramp/security-decision-record.json", "review/fedramp/security-decision-record.txt",
        "review/fedramp/ongoing-certification-report.json", "review/fedramp/ongoing-certification-report.txt",
        "review/fedramp/decisions.csv", "review/fedramp/profile.json", "review/fedramp/schema-provenance.json"];

    internal static IReadOnlyDictionary<string, byte[]> Build(ComplianceFrameworkTemplate template,
        IReadOnlyList<ComplianceDocumentSnapshot> documents, CompliancePackageValidationReport report)
    {
        if (!IsNative(template)) { return new Dictionary<string, byte[]>(); }
        var roots = documents.ToDictionary(item => item.Id, Read);
        var files = new Dictionary<string, byte[]>();
        var paths = RequiredPaths(template.Id);
        if (template.Id == ComplianceFrameworkCatalog.IsoIsmsId)
        {
            files.Add(paths[0], Encoding.UTF8.GetBytes(SoaCsv(template, documents, roots, report)));
            files.Add(paths[1], Encoding.UTF8.GetBytes(ManagementCsv(report)));
        }
        else if (template.Id == ComplianceFrameworkCatalog.GdprId)
        {
            files.Add(paths[0], Encoding.UTF8.GetBytes(DpiaCsv(documents, roots, report)));
            files.Add(paths[1], Encoding.UTF8.GetBytes(RecordsCsv(documents, roots)));
        }
        else if (Soc2ComplianceCatalog.Find(template.Id) is not null) { Soc2ReviewerFormats.Build(template, documents, roots, report, files); }
        else { BuildFedRamp(template, documents, roots, report, files); }
        return files;
    }

    private static void BuildFedRamp(ComplianceFrameworkTemplate template, IReadOnlyList<ComplianceDocumentSnapshot> documents,
        IReadOnlyDictionary<Guid, JsonElement> roots, CompliancePackageValidationReport report, Dictionary<string, byte[]> files)
    {
        var formatting = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string type, string path)
        {
            var document = documents.SingleOrDefault(item => item.Type == type);
            var payload = document is null ? default : Property(roots[document.Id], "payload");
            var content = payload.ValueKind == JsonValueKind.Object ? JsonSerializer.Serialize(payload, formatting) : "{\"missingEvidence\":true}";
            files.Add(path + ".json", Encoding.UTF8.GetBytes(content));
            files.Add(path + ".txt", Encoding.UTF8.GetBytes($"{type}\nSource: {(document is null ? "missing" : Metadata(document))}\nReady for auditor review: {report.ReadyForAuditorReview}\n\n{content}\n"));
        }
        Export("fedramp-overview", "review/fedramp/overview");
        Export("fedramp-sdr", "review/fedramp/security-decision-record");
        Export("fedramp-ocr", "review/fedramp/ongoing-certification-report");
        var scope = documents.SingleOrDefault(item => item.Type == "fedramp-profile");
        files.Add("review/fedramp/profile.json", scope is null ? Encoding.UTF8.GetBytes("{\"missingEvidence\":true}") : JsonSerializer.SerializeToUtf8Bytes(roots[scope.Id], formatting));
        files.Add("review/fedramp/schema-provenance.json", JsonSerializer.SerializeToUtf8Bytes(FedRampComplianceCatalog.SchemaSources, formatting));
        var sdr = documents.SingleOrDefault(item => item.Type == "fedramp-sdr");
        var decisions = sdr is null ? default : Property(roots[sdr.Id], "decisions");
        var output = new StringBuilder("identifier,applicable,implementationStatus,applicabilityRationale,verification,validation,independentVerification,independentValidation,customerRisk,seniorOfficialAcceptance,sourceMetadata,gapCodes\r\n");
        foreach (var control in template.Controls)
        {
            var decision = Property(decisions, control.Id);
            Row(output, control.Id, Property(decision, "applicable").ToString(), Text(decision, "implementationStatus"), Text(decision, "applicabilityRationale"),
                Text(decision, "verification"), Text(decision, "validation"), Text(decision, "independentVerification"), Text(decision, "independentValidation"),
                Text(decision, "customerRisk"), Text(decision, "seniorOfficialAcceptance"), sdr is null ? "missing" : Metadata(sdr), Codes(report, control.Id));
        }
        files.Add("review/fedramp/decisions.csv", Encoding.UTF8.GetBytes(output.ToString()));
        foreach (var document in documents.Where(item => item.Type == "fedramp-artifact"))
        {
            var payload = Property(roots[document.Id], "payload");
            files.Add($"review/fedramp/artifacts/{document.Id:D}.json", payload.ValueKind == JsonValueKind.Object
                ? JsonSerializer.SerializeToUtf8Bytes(payload, formatting) : Encoding.UTF8.GetBytes("{\"missingEvidence\":true}"));
        }
    }

    private static JsonElement Read(ComplianceDocumentSnapshot document)
    {
        if (document.MediaType != "application/json") { return default; }
        try
        {
            using var json = CompliancePackagingEncoding.Parse(document.Content);
            return json.RootElement.ValueKind == JsonValueKind.Object ? json.RootElement.Clone() : default;
        }
        catch (JsonException) { return default; }
    }

    private static string SoaCsv(ComplianceFrameworkTemplate template, IReadOnlyList<ComplianceDocumentSnapshot> documents,
        IReadOnlyDictionary<Guid, JsonElement> roots, CompliancePackageValidationReport report)
    {
        var output = new StringBuilder("entryType,identifier,applicable,justification,necessaryControlIds,implementationStatus,effectivenessEvidence,sourceMetadata,gapCodes\r\n");
        foreach (var document in documents.Where(item => item.Type == "iso-soa"))
        {
            var root = roots[document.Id];
            foreach (var control in template.Controls.Where(item => item.Id.StartsWith("A.", StringComparison.Ordinal)))
            {
                var decision = Property(Property(root, "annexADecisions"), control.Id);
                var applicable = Property(decision, "applicable");
                var declared = applicable.ValueKind == JsonValueKind.True ? "true" : applicable.ValueKind == JsonValueKind.False ? "false" : "unknown";
                Row(output, "annex-decision", control.Id, declared, Text(decision, declared == "true" ? "inclusionJustification" : "exclusionJustification"),
                    string.Join(';', Strings(decision, "necessaryControlIds", 500)), string.Empty, string.Empty, Metadata(document), Codes(report, control.Id));
            }
            foreach (var control in Array(root, "necessaryControls", 500))
            {
                Row(output, "necessary-control", Text(control, "id"), string.Empty, Text(control, "inclusionJustification"),
                    string.Join(';', Strings(control, "annexAReferences", 93)), Text(control, "implementationStatus"), Text(control, "effectivenessEvidence"), Metadata(document), Codes(report, "ISMS.6"));
            }
        }
        return output.ToString();
    }

    private static string ManagementCsv(CompliancePackageValidationReport report)
    {
        var output = new StringBuilder("clauseGroup,status,evidencePaths,documentIds,gapCodes\r\n");
        foreach (var control in report.Controls.Where(item => item.ControlId.StartsWith("ISMS.", StringComparison.Ordinal)))
        {
            Row(output, control.ControlId, control.Status, string.Join(';', control.EvidencePaths), string.Join(';', control.DocumentIds), Codes(report, control.ControlId));
        }
        return output.ToString();
    }

    private static string DpiaCsv(IReadOnlyList<ComplianceDocumentSnapshot> documents, IReadOnlyDictionary<Guid, JsonElement> roots,
        CompliancePackageValidationReport report)
    {
        var output = new StringBuilder("activityId,dpiaRequired,screeningMetadata,dpiaMetadata,residualRisk,assessmentDate,processingDate,consultationMetadata,consultationStatus,gapCodes\r\n");
        var activities = documents.Where(item => item.Type == "gdpr-accountability").SelectMany(item => Strings(Property(roots[item.Id], "processingScope"), "activityIds"))
            .Concat(documents.Where(item => item.Type == "gdpr-dpia").Select(item => Text(roots[item.Id], "activityId"))).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        foreach (var activity in activities)
        {
            var screenings = documents.Where(item => item.Type == "gdpr-dpia-screening").SelectMany(item => Array(roots[item.Id], "screenings")
                .Where(row => Text(row, "activityId") == activity).Select(row => (Document: item, Row: row))).ToArray();
            var dpias = documents.Where(item => item.Type == "gdpr-dpia" && Text(roots[item.Id], "activityId") == activity).ToArray();
            var consultations = documents.Where(item => item.Type == "gdpr-prior-consultation" && Text(roots[item.Id], "activityId") == activity).ToArray();
            Row(output, activity, string.Join(';', screenings.Select(item => Property(item.Row, "dpiaRequired").ToString())),
                string.Join(';', screenings.Select(item => Metadata(item.Document))), string.Join(';', dpias.Select(Metadata)),
                string.Join(';', dpias.Select(item => Text(Property(roots[item.Id], "residualRiskDecision"), "level"))),
                string.Join(';', dpias.Select(item => Text(Property(roots[item.Id], "assessmentTiming"), "assessedAtUtc"))),
                string.Join(';', dpias.Select(item => Text(Property(roots[item.Id], "assessmentTiming"), "effectiveAtUtc"))),
                string.Join(';', consultations.Select(Metadata)), string.Join(';', consultations.Select(item => Text(roots[item.Id], "consultationStatus"))),
                string.Join(';', report.Gaps.Where(item => item.ControlId is "GDPR.Art.3" or "GDPR.Art.30" or "GDPR.Art.35" or "GDPR.Art.36").Select(item => item.Code).Distinct()));
        }
        return output.ToString();
    }

    private static string RecordsCsv(IReadOnlyList<ComplianceDocumentSnapshot> documents, IReadOnlyDictionary<Guid, JsonElement> roots)
    {
        var output = new StringBuilder("activityId,role,controllerContact,processorContact,purposes,processingCategories,transfers,retention,securityMeasures,sourceMetadata\r\n");
        foreach (var document in documents.Where(item => item.Type == "gdpr-processing-records"))
        {
            foreach (var row in Array(roots[document.Id], "processingActivities", 400))
            {
                Row(output, Text(row, "activityId"), Text(row, "role"), Text(row, "controllerContact"), Text(row, "processorContact"), Text(row, "purposes"),
                    Text(row, "processingCategories"), Text(row, "transfers"), Text(row, "retention"), Text(row, "securityMeasures"), Metadata(document));
            }
        }
        return output.ToString();
    }

    private static string Metadata(ComplianceDocumentSnapshot document) => $"documents/{document.Id:D}/metadata.json";
    private static string Codes(CompliancePackageValidationReport report, string id) => string.Join(';', report.Gaps.Where(item => item.ControlId == id).Select(item => item.Code).Distinct());
    private static void Row(StringBuilder output, params string[] cells) => output.AppendJoin(',', cells.Select(ComplianceArtifactBuilder.CsvCell)).Append("\r\n");
}
