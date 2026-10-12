using System.Text.Json;
using static GameGuild.Compliance.Audit.ComplianceNativeEvidence;

namespace GameGuild.Compliance.Audit;

/// <summary>Checks captured management and control-test evidence, without issuing an audit opinion.</summary>
internal static class Soc2ComplianceEvidence
{
    internal static void ValidateDocument(ComplianceFrameworkTemplate template, ComplianceDocumentSnapshot document,
        JsonElement root, DateTime capturedAtUtc, List<ComplianceEvidenceGap> gaps)
    {
        var profile = Soc2ComplianceCatalog.Find(template.Id)!;
        void Gap(string code, string detail) => gaps.Add(new(string.Empty, code, detail, document.Id));
        if (document.Type == "soc2-system-description")
        {
            if (!Integer(root, "reportType", out var type) || type != profile.ReportType ||
                !ValidStrings(root, "categories", 5) || !Strings(root, "categories", 5).ToHashSet(StringComparer.Ordinal).SetEquals(profile.Categories))
            { Gap("Soc2ScopeMismatch", "Report type and selected categories must match the versioned template exactly."); }
            RequireFields(root, ["systemName", "boundaries", "services", "serviceCommitments", "systemRequirements",
                "systemIncidents", "systemChanges", "guidanceReview"], string.Empty, document.Id, gaps);
            RequireFields(Property(root, "components"), ["infrastructure", "software", "people", "procedures", "data"],
                string.Empty, document.Id, gaps);
            if (!ValidStrings(root, "complementaryUserEntityControls", 100, true) ||
                Property(root, "subserviceOrganizations").ValueKind != JsonValueKind.Array ||
                Property(root, "subserviceOrganizations").GetArrayLength() > 50)
            { Gap("Soc2SystemDescriptionIncomplete", "Declare bounded user-entity responsibilities and the subservice organization register."); }
        }
        else if (document.Type == "soc2-management-assertion")
        {
            if (!Integer(root, "reportType", out var type) || type != profile.ReportType ||
                Property(root, "fairPresentation").ValueKind != JsonValueKind.True ||
                Property(root, "suitableDesign").ValueKind != JsonValueKind.True ||
                (profile.ReportType == 2 && Property(root, "operatingEffectiveness").ValueKind != JsonValueKind.True))
            { Gap("Soc2AssertionInvalid", "Capture actual affirmative management assertions appropriate to the report type."); }
            RequireFields(root, ["signatory"], string.Empty, document.Id, gaps);
            if (!Utc(root, "signedAtUtc", out var signed) || signed > capturedAtUtc)
            { Gap("Soc2AssertionDateInvalid", "Management assertion signing time must be UTC and no later than capture."); }
        }
        else if (document.Type == "soc2-control-matrix")
        {
            RequireFields(root, ["guidanceReview"], string.Empty, document.Id, gaps);
            var mappings = Property(root, "criteriaMappings");
            if (mappings.ValueKind != JsonValueKind.Object || mappings.EnumerateObject().Count() > 61 ||
                Array(root, "controls", 200).Length == 0)
            { Gap("Soc2ControlMatrixInvalid", "Capture bounded criterion mappings and actual control assessment records."); }
        }
    }

    internal static IReadOnlyList<ComplianceEvidenceGap> InspectScope(ComplianceFrameworkTemplate template,
        CreateCompliancePackageRequest request, IReadOnlyList<ComplianceDocumentSnapshot> documents,
        IReadOnlyDictionary<Guid, JsonElement> roots, DateTime capturedAtUtc, CancellationToken cancellationToken)
    {
        var gaps = new List<ComplianceEvidenceGap>();
        var profile = Soc2ComplianceCatalog.Find(template.Id)!;
        void AllGap(string code, string detail, Guid? documentId)
        { gaps.AddRange(template.Controls.Select(item => new ComplianceEvidenceGap(item.Id, code, detail, documentId))); }
        var expectedIds = template.Controls.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var type in new[] { "soc2-system-description", "soc2-management-assertion", "soc2-control-matrix" })
        {
            var document = documents.SingleOrDefault(item => item.Type == type);
            if (document is null) { AllGap("Soc2NativeDocumentMissing", $"Capture the reviewed {type} document.", null); continue; }
            if (!expectedIds.SetEquals(document.ControlIds))
            { AllGap("Soc2ScopeMappingIncomplete", "Native scope, assertion and matrix documents must map every selected criterion.", document.Id); }
            if (type == "soc2-control-matrix") { continue; }
            var root = roots[document.Id];
            if (!Utc(root, "periodStartUtc", out var start) || !Utc(root, "periodEndUtc", out var end) ||
                start != request.PeriodStartUtc.UtcDateTime || end != request.PeriodEndUtc.UtcDateTime ||
                (profile.ReportType == 2 && start >= end))
            { AllGap("Soc2PeriodMismatch", "Scope and assertion dates must match the requested instant or nonzero audit period.", document.Id); }
            if (type == "soc2-management-assertion" &&
                (!Utc(root, "signedAtUtc", out var signed) || signed < request.PeriodEndUtc.UtcDateTime))
            { AllGap("Soc2AssertionDateInvalid", "The captured management assertion must be signed at or after the assessment endpoint.", document.Id); }
        }
        var matrix = documents.SingleOrDefault(item => item.Type == "soc2-control-matrix");
        if (matrix is null) { return gaps; }
        var matrixRoot = roots[matrix.Id];
        var mappings = Property(matrixRoot, "criteriaMappings");
        if (mappings.ValueKind != JsonValueKind.Object || !expectedIds.SetEquals(mappings.EnumerateObject().Select(item => item.Name)))
        { AllGap("Soc2CriteriaMappingMismatch", "Map exactly the selected known criteria; additional or missing identifiers remain gaps.", matrix.Id); }
        var rows = Array(matrixRoot, "controls", 200);
        var identifiers = rows.Select(row => Text(row, "controlId")).ToArray();
        if (identifiers.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 100) ||
            identifiers.Distinct(StringComparer.Ordinal).Count() != identifiers.Length)
        { AllGap("Soc2ControlIdentifierInvalid", "Control identifiers must be bounded, nonempty and unique.", matrix.Id); return gaps; }
        var controls = rows.ToDictionary(row => Text(row, "controlId"), StringComparer.Ordinal);
        var linked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var criterion in template.Controls)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mapping = Property(mappings, criterion.Id);
            var ids = Strings(mapping, "controlIds", 20);
            if (!ValidStrings(mapping, "controlIds", 20) || !HasText(mapping, "mappingRationale") ||
                !HasText(mapping, "pointOfFocusReview") || Text(mapping, "assessmentStatus") != "satisfactory")
            { gaps.Add(new(criterion.Id, "Soc2CriterionAssessmentIncomplete", "Each criterion needs reviewed control mappings, rationale and guidance review.", matrix.Id)); }
            foreach (var id in ids)
            {
                linked.Add(id);
                if (!controls.TryGetValue(id, out var control))
                { gaps.Add(new(criterion.Id, "Soc2MappedControlMissing", "A mapped control has no captured assessment record.", matrix.Id)); continue; }
                ValidateControl(profile.ReportType, criterion.Id, control, matrix, request, documents, capturedAtUtc, gaps);
            }
        }
        if (!linked.SetEquals(controls.Keys))
        { AllGap("Soc2UnmappedControl", "Every captured control assessment must have an in-scope criterion mapping.", matrix.Id); }
        var scope = documents.SingleOrDefault(item => item.Type == "soc2-system-description");
        if (scope is not null)
        {
            foreach (var service in Array(roots[scope.Id], "subserviceOrganizations", 50))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var method = Text(service, "method");
                if (!HasText(service, "name") || !HasText(service, "serviceDescription") || !HasText(service, "boundary") ||
                    method is not ("inclusive" or "carve-out") ||
                    (method == "inclusive" && (!ValidStrings(service, "includedControlIds", 200) || Strings(service, "includedControlIds", 200).Any(id => !controls.ContainsKey(id)))) ||
                    (method == "carve-out" && (!HasText(service, "complementarySubserviceControls") ||
                        !EvidenceLinks(service, "monitoringEvidenceDocumentIds", "CC9.2", request, documents))))
                { AllGap("Soc2SubserviceEvidenceIncomplete", "Resolve each subservice boundary, inclusion method and captured controls or monitoring evidence.", scope.Id); }
            }
        }
        return gaps;
    }

    private static void ValidateControl(int reportType, string criterionId, JsonElement control,
        ComplianceDocumentSnapshot matrix, CreateCompliancePackageRequest request,
        IReadOnlyList<ComplianceDocumentSnapshot> documents, DateTime capturedAtUtc, List<ComplianceEvidenceGap> gaps)
    {
        void Gap(string code, string detail) => gaps.Add(new(criterionId, code, detail, matrix.Id));
        if (!HasText(control, "description") || !HasText(control, "owner") || !HasText(control, "designBasis") ||
            Text(control, "designConclusion") != "suitable")
        { Gap("Soc2DesignDeficiency", "Capture the control owner, description and satisfactory design assessment."); }
        if (!EvidenceLinks(control, "designEvidenceDocumentIds", criterionId, request, documents))
        { Gap("Soc2DesignEvidenceMissing", "Design assessment must reference actual captured, reviewed and mapped supporting evidence."); }
        if (reportType == 1) { return; }
        if (!HasText(control, "testProcedures") || Text(control, "operatingConclusion") != "effective" ||
            !Integer(control, "deviationCount", out var deviations) || deviations != 0)
        { Gap("Soc2OperatingDeficiency", "Capture operating tests and conclusions; unresolved or declared deviations remain gaps."); }
        var samples = Array(control, "samples", 200);
        if (!Integer(control, "populationCount", out var population) || population < 1 ||
            !Integer(control, "sampleCount", out var sampleCount) || sampleCount < 1 || sampleCount > population ||
            samples.Length != sampleCount || samples.Select(row => Text(row, "sampleId")).Distinct(StringComparer.Ordinal).Count() != sampleCount ||
            samples.Any(row => !HasText(row, "sampleId") || Text(row, "sampleId").Length > 100 || Text(row, "result") != "pass" ||
                !Utc(row, "observedAtUtc", out var time) || time < request.PeriodStartUtc.UtcDateTime || time > request.PeriodEndUtc.UtcDateTime ||
                !EvidenceLink(Text(row, "evidenceDocumentId"), criterionId, request, documents)))
        { Gap("Soc2SampleEvidenceInvalid", "A bounded, unique and consistent sample register must link captured evidence and observations inside the audit period."); }
        if (!Utc(control, "testedFromUtc", out var start) || !Utc(control, "testedUntilUtc", out var end) ||
            start > request.PeriodStartUtc.UtcDateTime || end < request.PeriodEndUtc.UtcDateTime || end > capturedAtUtc)
        { Gap("Soc2OperatingPeriodGap", "Operating assessment must cover the complete requested audit period."); }
    }

    private static bool EvidenceLinks(JsonElement root, string field, string criterionId,
        CreateCompliancePackageRequest request, IReadOnlyList<ComplianceDocumentSnapshot> documents) =>
        ValidStrings(root, field, 20) && Strings(root, field, 20).All(id => EvidenceLink(id, criterionId, request, documents));

    private static bool EvidenceLink(string id, string criterionId, CreateCompliancePackageRequest request,
        IReadOnlyList<ComplianceDocumentSnapshot> documents) =>
        Guid.TryParseExact(id, "D", out var parsed) && documents.Any(document => document.Id == parsed &&
            document.Type == "soc2-supporting-evidence" && IsUsable(document, request, criterionId));

    private static bool Integer(JsonElement root, string field, out int value)
    { var item = Property(root, field); value = 0; return item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out value); }
    private static bool Utc(JsonElement root, string field, out DateTime value)
    { var item = Property(root, field); value = default; return item.ValueKind == JsonValueKind.String && item.TryGetDateTime(out value) && value.Kind == DateTimeKind.Utc; }
}
