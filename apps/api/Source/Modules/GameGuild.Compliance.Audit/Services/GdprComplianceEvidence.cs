using System.Text.Json;
using static GameGuild.Compliance.Audit.ComplianceNativeEvidence;

namespace GameGuild.Compliance.Audit;

internal static class GdprComplianceEvidence
{
    internal static void ValidateDocument(ComplianceDocumentSnapshot document, JsonElement root, DateTime capturedAtUtc, List<ComplianceEvidenceGap> gaps)
    {
        switch (document.Type)
        {
            case "gdpr-accountability": ValidateProcessingScope(document.Id, Property(root, "processingScope"), gaps); break;
            case "gdpr-processing-records": ValidateRecords(document.Id, root, gaps); break;
            case "gdpr-dpia-screening": ValidateScreenings(document.Id, root, gaps); break;
            case "gdpr-dpia": ValidateDpia(document.Id, root, capturedAtUtc, gaps); break;
            case "gdpr-prior-consultation": ValidateConsultation(document.Id, root, capturedAtUtc, gaps); break;
            default: break; // Other catalog documents use the common content, review and required-field checks.
        }
    }

    private static void ValidateProcessingScope(Guid id, JsonElement scope, List<ComplianceEvidenceGap> gaps)
    {
        RequireFields(scope, ["rationale"], "GDPR.Art.3", id, gaps);
        if (!ValidStrings(scope, "activityIds", 200) || !ValidStrings(scope, "roles", 2) || Strings(scope, "roles", 2).Any(role => role is not ("controller" or "processor")))
        {
            gaps.Add(new("GDPR.Art.3", "ProcessingScopeInvalid", "Declare one to 200 unique processing activities and the controller/processor roles in the reviewed scope.", id));
        }
    }

    private static void ValidateRecords(Guid id, JsonElement root, List<ComplianceEvidenceGap> gaps)
    {
        var records = Array(root, "processingActivities", 400);
        ValidateUniqueRows(id, records, "GDPR.Art.30", "processing records", item => Text(item, "activityId") + ":" + Text(item, "role"), gaps);
        foreach (var record in records)
        {
            RequireFields(record, ["activityId", "role", "transfers", "securityMeasures"], "GDPR.Art.30", id, gaps);
            switch (Text(record, "role"))
            {
                case "controller": RequireFields(record, ["controllerContact", "purposes", "subjectCategories", "dataCategories", "recipientCategories", "retention"], "GDPR.Art.30", id, gaps); break;
                case "processor": RequireFields(record, ["processorContact", "controllers", "processingCategories"], "GDPR.Art.30", id, gaps); break;
                default: gaps.Add(new("GDPR.Art.30", "ProcessingRoleInvalid", "Record an explicit controller or processor role for each processing activity.", id)); break;
            }
        }
    }

    private static void ValidateScreenings(Guid id, JsonElement root, List<ComplianceEvidenceGap> gaps)
    {
        var screenings = Array(root, "screenings");
        ValidateUniqueRows(id, screenings, "GDPR.Art.35", "DPIA screenings", item => Text(item, "activityId"), gaps);
        foreach (var screening in screenings)
        {
            RequireFields(screening, ["activityId", "rationale", "supervisoryAuthorityListsReview", "reviewTriggers"], "GDPR.Art.35", id, gaps);
            if (Property(screening, "dpiaRequired").ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                gaps.Add(new("GDPR.Art.35", "DPIAScreeningInvalid", "The reviewed screening must explicitly determine whether a DPIA is required.", id));
            }
        }
    }

    private static void ValidateDpia(Guid id, JsonElement root, DateTime capturedAtUtc, List<ComplianceEvidenceGap> gaps)
    {
        RequireFields(root, ["activityId", "processingDescription", "processingPurposes", "necessityProportionality", "individualRightsRisks", "mitigations", "reviewTriggers"], "GDPR.Art.35", id, gaps);
        var residual = Property(root, "residualRiskDecision");
        RequireFields(residual, ["rationale"], "GDPR.Art.35", id, gaps);
        if (Text(residual, "level") is not ("low" or "medium" or "high"))
        {
            gaps.Add(new("GDPR.Art.35", "ResidualRiskInvalid", "Record a reviewed residual risk level and rationale.", id));
        }
        RequireFields(Property(root, "consultation"), ["dpoAdvice", "dataSubjectViews"], "GDPR.Art.35", id, gaps);
        var timing = Property(root, "assessmentTiming");
        var basis = Text(timing, "basis");
        var valid = Utc(timing, "assessedAtUtc", out var assessed);
        valid = Utc(timing, "effectiveAtUtc", out var effective) && valid;
        // Preserve the initial assessment date on subsequent reviews; a later review cannot erase a historical gap.
        if (basis == "periodic-review")
        {
            valid = valid && Utc(timing, "originalAssessmentAtUtc", out var original) && original <= effective && HasText(timing, "originalAssessmentReference");
        }
        else { valid = valid && (basis is "initial-processing" or "material-change") && assessed <= effective; }
        if (!valid || assessed > capturedAtUtc)
        {
            gaps.Add(new("GDPR.Art.35", "DPIATimingGap", "Record UTC assessment and processing/change dates with evidence of an assessment before the declared processing began.", id));
        }
    }

    private static bool Utc(JsonElement root, string name, out DateTime value)
    {
        var field = Property(root, name);
        value = default;
        return field.ValueKind == JsonValueKind.String && field.TryGetDateTime(out value) && value.Kind == DateTimeKind.Utc;
    }

    private static void ValidateConsultation(Guid id, JsonElement root, DateTime capturedAtUtc, List<ComplianceEvidenceGap> gaps)
    {
        RequireFields(root, ["activityId", "authority", "submissionReference"], "GDPR.Art.36", id, gaps);
        var status = Text(root, "consultationStatus");
        if (status is not ("pending" or "completed"))
        {
            gaps.Add(new("GDPR.Art.36", "PriorConsultationInvalid", "Record whether the supervisory authority consultation is pending or completed.", id));
        }
        if (status == "completed") { RequireFields(root, ["outcomeReference"], "GDPR.Art.36", id, gaps); }
        var submittedValid = Utc(root, "submittedAtUtc", out var submitted);
        var completedValid = Utc(root, "completedAtUtc", out var completed);
        if (!submittedValid || submitted > capturedAtUtc || (status == "completed" && (!completedValid || completed < submitted || completed > capturedAtUtc)))
        {
            gaps.Add(new("GDPR.Art.36", "PriorConsultationTimingGap", "Record UTC submission and completion dates with the reviewed outcome; completion cannot precede submission or capture.", id));
        }
    }

    private static void ValidateUniqueRows(Guid id, JsonElement[] rows, string control, string description,
        Func<JsonElement, string> key, List<ComplianceEvidenceGap> gaps)
    {
        var keys = rows.Select(key).ToArray();
        if (rows.Length == 0 || keys.Any(value => value.Length is 0 or > 210 || string.IsNullOrWhiteSpace(value)) || keys.Distinct(StringComparer.Ordinal).Count() != keys.Length)
        {
            gaps.Add(new(control, "NativeEvidenceRowsInvalid", $"Use bounded, unique {description} with explicit activity identifiers.", id));
        }
    }

    internal static IReadOnlyList<ComplianceEvidenceGap> InspectScope(CreateCompliancePackageRequest request,
        IReadOnlyList<ComplianceDocumentSnapshot> documents, IReadOnlyDictionary<Guid, JsonElement> roots, CancellationToken cancellationToken)
    {
        var gaps = new List<ComplianceEvidenceGap>();
        var scoped = documents.Where(item => item.Type == "gdpr-accountability" && IsUsable(item, request, "GDPR.Art.3"))
            .SelectMany(item => Strings(Property(roots[item.Id], "processingScope"), "activityIds")).ToHashSet(StringComparer.Ordinal);
        var screenings = Rows("gdpr-dpia-screening", "screenings", "GDPR.Art.35", request, documents, roots);
        var records = Rows("gdpr-processing-records", "processingActivities", "GDPR.Art.30", request, documents, roots, 400);
        var roles = documents.Where(item => item.Type == "gdpr-accountability" && IsUsable(item, request, "GDPR.Art.3"))
            .SelectMany(item => Strings(Property(roots[item.Id], "processingScope"), "roles", 2)).ToHashSet(StringComparer.Ordinal);
        foreach (var record in records.Where(item => !roles.Contains(Text(item.Row, "role"))))
        {
            gaps.Add(new("GDPR.Art.30", "ProcessingRoleScopeMismatch", "The processing record role must match a role in the reviewed scope.", record.DocumentId));
        }
        var recordsExcluded = request.Exclusions.Any(item => item.ControlId == "GDPR.Art.30");
        foreach (var activity in scoped)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var matches = screenings.Where(item => Text(item.Row, "activityId") == activity).ToArray();
            if (matches.Length != 1)
            {
                gaps.Add(new("GDPR.Art.35", "DPIAScreeningMissing", $"Activity {activity} requires exactly one reviewed screening covering the period.", null));
                continue;
            }
            if (!recordsExcluded && !records.Any(item => Text(item.Row, "activityId") == activity))
            {
                gaps.Add(new("GDPR.Art.30", "ProcessingRecordMissing", $"Activity {activity} has no reviewed processing record covering the period.", null));
            }
            var dpias = documents.Where(item => item.Type == "gdpr-dpia" && IsUsable(item, request, "GDPR.Art.35") && Text(roots[item.Id], "activityId") == activity).ToArray();
            if (Property(matches[0].Row, "dpiaRequired").ValueKind == JsonValueKind.True && dpias.Length != 1)
            {
                gaps.Add(new("GDPR.Art.35", "MissingDPIA", $"Activity {activity} requires exactly one reviewed DPIA covering the period.", matches[0].DocumentId));
            }
            if (dpias.Length > 1)
            {
                gaps.Add(new("GDPR.Art.35", "DPIARevisionAmbiguous", $"Select one current DPIA revision for activity {activity}; reference its history inside that assessment.", null));
            }
            foreach (var dpia in dpias) { CheckConsultation(activity, dpia, roots, request, documents, gaps); }
        }
        foreach (var row in screenings.Concat(records).Where(item => !scoped.Contains(Text(item.Row, "activityId"))))
        {
            gaps.Add(new("GDPR.Art.3", "ProcessingScopeMismatch", "A screening or processing record refers to an activity outside the reviewed scope.", row.DocumentId));
        }
        foreach (var document in documents.Where(item => (item.Type is "gdpr-dpia" or "gdpr-prior-consultation") && !scoped.Contains(Text(roots[item.Id], "activityId"))))
        {
            gaps.Add(new("GDPR.Art.3", "ProcessingScopeMismatch", "A DPIA or consultation refers to an activity outside the reviewed scope.", document.Id));
        }
        return gaps;
    }

    private static (Guid DocumentId, JsonElement Row)[] Rows(string type, string field, string control, CreateCompliancePackageRequest request,
        IReadOnlyList<ComplianceDocumentSnapshot> documents, IReadOnlyDictionary<Guid, JsonElement> roots, int maximum = 200) =>
        documents.Where(item => item.Type == type && IsUsable(item, request, control)).SelectMany(item => Array(roots[item.Id], field, maximum).Select(row => (item.Id, row))).ToArray();

    private static void CheckConsultation(string activity, ComplianceDocumentSnapshot dpia, IReadOnlyDictionary<Guid, JsonElement> roots,
        CreateCompliancePackageRequest request, IReadOnlyList<ComplianceDocumentSnapshot> documents, List<ComplianceEvidenceGap> gaps)
    {
        if (Text(Property(roots[dpia.Id], "residualRiskDecision"), "level") != "high") { return; }
        var consultation = documents.Where(item => item.Type == "gdpr-prior-consultation" && IsUsable(item, request, "GDPR.Art.36") && Text(roots[item.Id], "activityId") == activity).ToArray();
        if (consultation.Length != 1 || Text(roots[consultation[0].Id], "consultationStatus") != "completed")
        {
            gaps.Add(new("GDPR.Art.36", "PriorConsultationMissing", $"Activity {activity} has residual high risk and requires reviewed, completed prior consultation evidence.", dpia.Id));
            return;
        }
        if (!Utc(roots[consultation[0].Id], "completedAtUtc", out var completed) ||
            !Utc(Property(roots[dpia.Id], "assessmentTiming"), "effectiveAtUtc", out var effective) || completed > effective)
        {
            gaps.Add(new("GDPR.Art.36", "PriorConsultationTimingGap", "Prior consultation evidence must predate the declared processing or material change.", consultation[0].Id));
        }
    }
}
