using System.Globalization;
using System.Text.Json;
using static GameGuild.Compliance.Audit.ComplianceNativeEvidence;

namespace GameGuild.Compliance.Audit;

/// <summary>Schema checks and reviewed provider declarations. Readiness is not federal certification.</summary>
internal static class FedRampComplianceEvidence
{
    internal static void ValidateDocument(ComplianceFrameworkTemplate template, ComplianceDocumentSnapshot document,
        JsonElement root, DateTime capturedAtUtc, List<ComplianceEvidenceGap> gaps)
    {
        var profile = FedRampComplianceCatalog.Find(template.Id)!;
        void Gap(string code, string detail) => gaps.Add(new(string.Empty, code, detail, document.Id));
        if (!WithinBounds(root))
        { Gap("FedRampPayloadLimit", "Structured evidence must have bounded strings, depth, collections and node count."); return; }
        if (document.Type == "fedramp-profile")
        {
            var target = Property(root, "certificationProfile");
            if (Text(target, "type") != "Rev5" || Text(target, "class") != profile.Class || Text(target, "path") != profile.Path ||
                Text(target, "rulesVersion") != FedRampComplianceCatalog.PinnedRulesVersion)
            { Gap("FedRampProfileMismatch", "The type, class, path and pinned rules version must match the selected profile."); }
            var phase = Text(root, "packagePhase");
            if (phase is not ("application" or "ongoing")) { Gap("FedRampPhaseInvalid", "Declare application or ongoing package phase."); }
            foreach (var field in new[] { "packageVerifiedAtUtc", "packageValidatedAtUtc" })
            {
                if (!Utc(root, field, out var date) || date > capturedAtUtc ||
                    (phase == "application" && date < capturedAtUtc.AddDays(-7)))
                { Gap("FedRampPackageStale", "Application packages require provider verification and validation within the previous seven days."); }
            }
            var assessment = Property(root, "independentAssessment");
            if (phase == "application" && (!Utc(assessment, "completedAtUtc", out var completion) || completion > capturedAtUtc || completion < capturedAtUtc.AddMonths(-3) ||
                Property(assessment, "recognizedAssessor").ValueKind != JsonValueKind.True || !HasText(assessment, "assessor") ||
                !Guid.TryParse(Text(assessment, "reportDocumentId"), out _)))
            { Gap("FedRampIndependentAssessmentStale", "The application requires a captured report from a recognized independent assessor completed within three months."); }
            return;
        }
        var uri = Text(root, "schemaUri");
        var expected = FedRampComplianceCatalog.DocumentSchemaUri(document.Type);
        if ((document.Type != "fedramp-artifact" && uri != expected) ||
            (document.Type == "fedramp-artifact" && document.ControlIds.Any(id => !profile.Rules.Any(rule => rule.Id == id && rule.SchemaUri == uri))) ||
            !FedRampComplianceCatalog.IsSchemaValid(uri, Property(root, "payload")))
        { Gap("FedRampSchemaInvalid", "The actual payload must validate against the checksum-pinned official schema, including references and formats."); }
        if (document.Type == "fedramp-artifact")
        {
            foreach (var id in document.ControlIds)
            {
                var reportType = id switch { "IEC-CSO-IIR" => "Initial", "IEC-CSO-OIR" => "Ongoing", "IEC-CSO-FIR" => "Final", _ => null };
                if (reportType is not null && Text(Property(root, "payload"), "reportType") != reportType)
                { Gap("FedRampReportTypeMismatch", "The actual incident report type must match its mapped initial, ongoing or final rule."); }
            }
        }
        if (document.Type == "fedramp-overview" && Text(Property(Property(root, "payload"), "serviceIdentification"), "certificationType") != "Rev5")
        { Gap("FedRampProfileMismatch", "The overview certification type must match the Rev5 profile."); }
        if (document.Type == "fedramp-sdr")
        {
            var metadata = Property(Property(root, "payload"), "metadata");
            if (!HasText(metadata, "version") || !HasText(metadata, "updateSource") || !Utc(metadata, "lastUpdated", out var updated) || updated > capturedAtUtc)
            { Gap("FedRampSdrMetadataInvalid", "The SDR requires actual version, UTC update time and update source metadata."); }
            if (Property(root, "decisions").ValueKind != JsonValueKind.Object)
            { Gap("FedRampDecisionsMissing", "Every scoped rule and Rev5 control requires a structured reviewed decision."); }
        }
    }

    internal static IReadOnlyList<ComplianceEvidenceGap> InspectScope(ComplianceFrameworkTemplate template,
        CreateCompliancePackageRequest request, IReadOnlyList<ComplianceDocumentSnapshot> documents,
        IReadOnlyDictionary<Guid, JsonElement> roots, DateTime capturedAtUtc, CancellationToken cancellationToken)
    {
        var profile = FedRampComplianceCatalog.Find(template.Id)!;
        var gaps = new List<ComplianceEvidenceGap>();
        var sdr = documents.SingleOrDefault(item => item.Type == "fedramp-sdr");
        if (sdr is null) { return gaps; }
        var body = roots[sdr.Id];
        var payload = Property(body, "payload");
        var decisions = Property(body, "decisions");
        var ids = template.Controls.Select(control => control.Id).ToHashSet(StringComparer.Ordinal);
        void Gap(string id, string code, string detail) => gaps.Add(new(id, code, detail, sdr.Id));
        if (decisions.ValueKind == JsonValueKind.Object && decisions.EnumerateObject().Any(item => !ids.Contains(item.Name)))
        { Gap("SDR-CSO-FRR", "FedRampUnknownDecision", "SDR decisions contain identifiers outside the pinned class/path profile."); }
        var ruleRows = Array(payload, "fedRampRequirements", 1500);
        var controlRows = Array(payload, "securityControls", 1500);
        if (ruleRows.Any(row => !profile.Rules.Any(rule => rule.Id == Text(row, "frrID"))) ||
            controlRows.Any(row => !profile.Baseline.ContainsKey(Text(row, "controlId"))) ||
            ruleRows.Select(row => Text(row, "frrID")).Distinct(StringComparer.Ordinal).Count() != ruleRows.Length ||
            controlRows.Select(row => Text(row, "controlId")).Distinct(StringComparer.Ordinal).Count() != controlRows.Length)
        { Gap("SDR-CSO-FRR", "FedRampSdrIdentifiersInvalid", "Official SDR records must use unique identifiers from the pinned profile."); }
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var decision = Property(decisions, id);
            var applicable = Property(decision, "applicable");
            if (applicable.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            { Gap(id, "FedRampDecisionMissing", "A reviewed boolean applicability decision is required."); continue; }
            if (!sdr.ControlIds.Contains(id, StringComparer.Ordinal))
            { Gap(id, "FedRampDecisionMappingMissing", "The captured SDR revision must be mapped to this rule or control."); }
            RequireFields(decision, ["applicabilityRationale", "implementation", "verification", "validation", "independentVerification", "independentValidation", "commentResponses"], id, sdr.Id, gaps);
            if (applicable.ValueKind == JsonValueKind.False)
            {
                RequireFields(decision, ["customerRisk", "seniorOfficialAcceptance"], id, sdr.Id, gaps);
                if (Text(decision, "implementationStatus") != "Not Applicable")
                { Gap(id, "FedRampDecisionStatusInvalid", "Non-applicability requires an explicit reviewed Not Applicable decision."); }
            }
            else if (Text(decision, "implementationStatus") != "Implemented")
            { Gap(id, "FedRampImplementationPending", "Planned, partial and unimplemented decisions remain evidence deficiencies."); }
            var rule = profile.Rules.SingleOrDefault(item => item.Id == id);
            var rows = rule is null ? controlRows.Where(row => Text(row, "controlId") == id).ToArray() : ruleRows.Where(row => Text(row, "frrID") == id).ToArray();
            if (rows.Length != 1)
            { Gap(id, "FedRampSdrRecordMissing", "The official SDR payload must contain exactly one record for this identifier."); continue; }
            var row = rows[0];
            if (applicable.ValueKind == JsonValueKind.True && Text(row, rule is null ? "controlImplementationStatus" : "frrImplementationStatus") != "Implemented")
            { Gap(id, "FedRampImplementationPending", "The official SDR record does not declare an implemented control or rule."); }
            if (rule is null)
            {
                if (!HasText(row, "controlImplementationDescription")) { Gap(id, "FedRampImplementationMissing", "The actual SDR requires an implementation description."); }
                var parameters = Array(row, "parameterValues", 1500);
                var parameterIds = parameters.Select(item => Text(item, "parameterId")).ToArray();
                if (!profile.Baseline[id].ToHashSet(StringComparer.Ordinal).SetEquals(parameterIds) ||
                    parameterIds.Distinct(StringComparer.Ordinal).Count() != parameterIds.Length || parameters.Any(item => !HasText(item, "parameterValue")))
                { Gap(id, "FedRampParametersMissing", "Document all organisation-defined parameter values from the pinned NIST control catalog, without duplicate or unknown parameters."); }
            }
            else
            {
                foreach (var field in new[] { "frrImplementation", "frrValidation", "frrAssessment" })
                {
                    var statements = Array(row, field, 100);
                    if (statements.Length == 0 || statements.Any(item => item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString())))
                    { Gap(id, "FedRampSdrStatementMissing", "The official rule record requires actual implementation, validation and independent assessment statements."); }
                }
                if (applicable.ValueKind == JsonValueKind.True && rule.SchemaUri is not null && !documents.Any(document =>
                    document.ControlIds.Contains(id, StringComparer.Ordinal) && Text(roots[document.Id], "schemaUri") == rule.SchemaUri && IsUsable(document, request, id)))
                { Gap(id, "FedRampRuleArtifactMissing", "The applicable rule requires its captured, mapped, approved official JSON artifact."); }
            }
        }
        var scope = documents.SingleOrDefault(item => item.Type == "fedramp-profile");
        if (scope is not null)
        {
            var scopeRoot = roots[scope.Id];
            if (Text(scopeRoot, "packagePhase") == "application")
            {
                var assessment = Property(scopeRoot, "independentAssessment");
                if (!Guid.TryParse(Text(assessment, "reportDocumentId"), out var reportId) || !documents.Any(item =>
                    item.Id == reportId && item.Type == "fedramp-supporting-evidence" && IsUsable(item, request, "FRC-APP-FIA")))
                { gaps.Add(new("FRC-APP-FIA", "FedRampAssessmentReportMissing", "Capture the actual reviewed independent assessment report in this package.", scope.Id)); }
                foreach (var document in documents.Where(item => item.Type is "fedramp-overview" or "fedramp-sdr" or "fedramp-ocr"))
                {
                    if (document.ReviewedAtUtc is null || document.ReviewedAtUtc < capturedAtUtc.AddDays(-7))
                    { gaps.Add(new("FRC-APP-FCP", "FedRampPackageStale", "Application package evidence must have a current review within seven days.", document.Id)); }
                }
                if (!Utc(Property(payload, "metadata"), "lastUpdated", out var updated) || updated < capturedAtUtc.AddDays(-7))
                { Gap("FRC-APP-FCP", "FedRampPackageStale", "The actual SDR update must be within seven days for an application."); }
            }
        }
        ValidateOcr(request, documents, roots, gaps);
        var overview = documents.SingleOrDefault(item => item.Type == "fedramp-overview");
        if (overview is not null)
        {
            foreach (var document in documents.Where(item => item.Type is "fedramp-sdr" or "fedramp-ocr" or "fedramp-artifact"))
            {
                var overviewUri = Text(Property(roots[document.Id], "payload"), "certificationPackageOverviewUri");
                if (overviewUri != overview.SourceUri)
                { gaps.Add(new("FRC-CSO-PKG", "FedRampOverviewReferenceMismatch", "Official records must reference the captured overview source URI.", document.Id)); }
            }
        }
        return gaps;
    }

    private static void ValidateOcr(CreateCompliancePackageRequest request, IReadOnlyList<ComplianceDocumentSnapshot> documents,
        IReadOnlyDictionary<Guid, JsonElement> roots, List<ComplianceEvidenceGap> gaps)
    {
        var document = documents.SingleOrDefault(item => item.Type == "fedramp-ocr");
        if (document is null) { return; }
        var payload = Property(roots[document.Id], "payload");
        var period = Property(payload, "reportPeriod");
        bool Date(JsonElement root, string field, out DateTime date) => DateTime.TryParseExact(Text(root, field), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        if (!Date(period, "from", out var from) || !Date(period, "to", out var to) || from > to || to > from.AddMonths(3) ||
            from.Date > request.PeriodStartUtc.UtcDateTime.Date || to.Date < request.PeriodEndUtc.UtcDateTime.Date ||
            !Date(Property(payload, "plannedCertificationDataChanges"), "planningHorizonThrough", out var horizon) || horizon < to.AddMonths(3))
        { gaps.Add(new("CCM-OCR-AVL", "FedRampReportTimelineGap", "The OCR must cover the requested period, at most three months, and planning must extend at least three months past report end.", document.Id)); }
    }

    private static bool Utc(JsonElement root, string field, out DateTime value) =>
        DateTime.TryParse(Text(root, field), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value) && value.Kind == DateTimeKind.Utc;

    private static bool WithinBounds(JsonElement root)
    {
        var stack = new Stack<(JsonElement Node, int Depth)>();
        stack.Push((root, 0));
        var count = 0;
        while (stack.TryPop(out var item))
        {
            if (++count > 60000 || item.Depth > 32) { return false; }
            if (item.Node.ValueKind == JsonValueKind.String && item.Node.GetString()!.Length > 4000) { return false; }
            if (item.Node.ValueKind == JsonValueKind.Array)
            {
                if (item.Node.GetArrayLength() > 1500) { return false; }
                foreach (var child in item.Node.EnumerateArray()) { stack.Push((child, item.Depth + 1)); }
            }
            if (item.Node.ValueKind == JsonValueKind.Object)
            {
                foreach (var child in item.Node.EnumerateObject())
                { if (child.Name.Length > 200) { return false; } stack.Push((child.Value, item.Depth + 1)); }
            }
        }
        return true;
    }
}
