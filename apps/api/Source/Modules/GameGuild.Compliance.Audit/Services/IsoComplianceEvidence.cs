using System.Text.Json;
using static GameGuild.Compliance.Audit.ComplianceNativeEvidence;

namespace GameGuild.Compliance.Audit;

internal static class IsoComplianceEvidence
{
    internal static void ValidateDocument(ComplianceFrameworkTemplate template, ComplianceDocumentSnapshot document,
        JsonElement root, List<ComplianceEvidenceGap> gaps)
    {
        if (document.Type == "iso-context")
        {
            var climate = Property(root, "climateRelevanceAssessment");
            if (Property(climate, "relevant").ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                gaps.Add(new("ISMS.4", "ClimateRelevanceMissing", "Record an explicit climate relevance decision for the organisation context.", document.Id));
            }
            RequireFields(climate, ["rationale", "interestedPartyRequirements"], "ISMS.4", document.Id, gaps);
        }
        if (document.Type != "iso-soa") { return; }
        var annex = template.Controls.Where(item => item.Id.StartsWith("A.", StringComparison.Ordinal)).Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var necessary = Array(root, "necessaryControls", 500);
        ValidateNecessaryControls(document.Id, necessary, annex, gaps);
        var decisions = Property(root, "annexADecisions");
        if (decisions.ValueKind == JsonValueKind.Object && decisions.EnumerateObject().Any(item => !annex.Contains(item.Name)))
        {
            gaps.Add(new("ISMS.6", "SoAUnknownDecision", "Annex A decisions must reference identifiers from the selected framework version.", document.Id));
        }
        var necessaryIds = necessary.Select(item => Text(item, "id")).ToHashSet(StringComparer.Ordinal);
        foreach (var id in annex) { ValidateDecision(document.Id, id, Property(decisions, id), necessaryIds, gaps); }
    }

    private static void ValidateNecessaryControls(Guid documentId, JsonElement[] controls, HashSet<string> annex, List<ComplianceEvidenceGap> gaps)
    {
        var ids = controls.Select(item => Text(item, "id")).ToArray();
        if (controls.Length == 0 || ids.Any(id => id.Length is 0 or > 100 || string.IsNullOrWhiteSpace(id)) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
        {
            gaps.Add(new("ISMS.6", "NecessaryControlsInvalid", "Use one to 500 necessary controls with unique bounded identifiers, including custom controls.", documentId));
        }
        foreach (var control in controls)
        {
            RequireFields(control, ["description", "inclusionJustification"], "ISMS.6", documentId, gaps);
            var status = Text(control, "implementationStatus");
            if (status is not ("implemented" or "planned" or "not-implemented"))
            {
                gaps.Add(new("ISMS.6", "ControlImplementationInvalid", "Necessary control implementation status must be implemented, planned or not-implemented.", documentId));
            }
            if (status == "implemented") { RequireFields(control, ["effectivenessEvidence"], "ISMS.6", documentId, gaps); }
            if (!ValidStrings(control, "annexAReferences", 93, true) || Strings(control, "annexAReferences", 93).Any(id => !annex.Contains(id)))
            {
                gaps.Add(new("ISMS.6", "NecessaryControlReferencesInvalid", "Use known Annex A references, or an empty array for a custom control.", documentId));
            }
        }
    }

    private static void ValidateDecision(Guid documentId, string id, JsonElement decision,
        HashSet<string> necessaryIds, List<ComplianceEvidenceGap> gaps)
    {
        var applicable = Property(decision, "applicable");
        if (decision.ValueKind != JsonValueKind.Object || applicable.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            gaps.Add(new(id, "SoADecisionMissing", $"Record a structured applicability decision for {id}.", documentId));
            return;
        }
        RequireFields(decision, applicable.ValueKind == JsonValueKind.True ? ["inclusionJustification"] : ["exclusionJustification"], id, documentId, gaps);
        if (!ValidStrings(decision, "necessaryControlIds", 500, applicable.ValueKind == JsonValueKind.False) ||
            Strings(decision, "necessaryControlIds", 500).Any(reference => !necessaryIds.Contains(reference)))
        {
            gaps.Add(new(id, "SoAControlReferenceInvalid", $"The decision for {id} must resolve to documented necessary controls.", documentId));
        }
    }

    internal static IReadOnlyList<ComplianceEvidenceGap> InspectScope(ComplianceFrameworkTemplate template, CreateCompliancePackageRequest request,
        IReadOnlyList<ComplianceDocumentSnapshot> documents, IReadOnlyDictionary<Guid, JsonElement> roots, CancellationToken cancellationToken)
    {
        var gaps = new List<ComplianceEvidenceGap>();
        var annex = template.Controls.Where(item => item.Id.StartsWith("A.", StringComparison.Ordinal)).ToArray();
        foreach (var document in documents.Where(item => item.Type == "iso-soa"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var root = roots[document.Id];
            var decisions = Property(root, "annexADecisions");
            foreach (var necessary in Array(root, "necessaryControls", 500).Where(item => Text(item, "implementationStatus") != "implemented"))
            {
                var id = Text(necessary, "id");
                var detail = $"Necessary control {id} is not implemented; reviewed evidence remains incomplete.";
                gaps.Add(new("ISMS.6", "ControlImplementationPending", detail, document.Id));
                foreach (var control in annex.Where(control => Strings(Property(decisions, control.Id), "necessaryControlIds", 500).Contains(id, StringComparer.Ordinal)))
                {
                    gaps.Add(new(control.Id, "ControlImplementationPending", detail, document.Id));
                }
            }
            foreach (var control in annex)
            {
                var decision = Property(decisions, control.Id);
                var applicable = Property(decision, "applicable");
                var exclusion = request.Exclusions.SingleOrDefault(item => item.ControlId == control.Id);
                var excluded = applicable.ValueKind == JsonValueKind.False;
                if ((excluded && (exclusion is null || exclusion.ApplicabilityDocumentId != document.Id ||
                    exclusion.Rationale.Trim() != Text(decision, "exclusionJustification").Trim())) ||
                    (applicable.ValueKind == JsonValueKind.True && exclusion is not null))
                {
                    gaps.Add(new(control.Id, "SoAScopeMismatch", "The requested exclusions and their reasons must agree with the reviewed Statement of Applicability.", document.Id));
                }
            }
        }
        return gaps;
    }
}
