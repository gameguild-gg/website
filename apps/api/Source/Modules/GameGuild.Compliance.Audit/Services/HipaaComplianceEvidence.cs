using System.Text.Json;
using static GameGuild.Compliance.Audit.ComplianceNativeEvidence;

namespace GameGuild.Compliance.Audit;

/// <summary>Structured quality checks for HIPAA evidence. Collecting declarations is not a compliance determination.</summary>
internal static class HipaaComplianceEvidence
{
    private const string EncryptionAtRestControl = "164.312(a)(2)(iv)";
    private const string EncryptionInTransitControl = "164.312(e)(2)(ii)";

    internal static void ValidateDocument(ComplianceDocumentSnapshot document, JsonElement root,
        DateTime capturedAtUtc, List<ComplianceEvidenceGap> gaps)
    {
        switch (document.Type)
        {
            case "hipaa-risk-analysis": ValidateRiskAnalysis(document.Id, root, capturedAtUtc, gaps); break;
            case "hipaa-technical-safeguards": ValidateTechnicalSafeguards(document.Id, root, gaps); break;
            case "hipaa-breach-notification": ValidateBreachNotification(document.Id, root, gaps); break;
            default: break; // The remaining catalog documents use the common content, review, field and control-assessment checks.
        }
    }

    private static void ValidateRiskAnalysis(Guid id, JsonElement root, DateTime capturedAtUtc, List<ComplianceEvidenceGap> gaps)
    {
        if (!Utc(root, "assessedAtUtc", out var assessed) || assessed > capturedAtUtc)
        {
            gaps.Add(new("164.308(a)(1)(ii)(A)", "HipaaRiskAnalysisTimingInvalid",
                "The risk analysis must record a UTC assessment date no later than capture.", id));
        }
    }

    private static void ValidateTechnicalSafeguards(Guid id, JsonElement root, List<ComplianceEvidenceGap> gaps)
    {
        ValidateEncryption(id, root, "encryptionAtRest", EncryptionAtRestControl, "at rest", gaps);
        ValidateEncryption(id, root, "encryptionInTransit", EncryptionInTransitControl, "in transit", gaps);
    }

    private static void ValidateEncryption(Guid id, JsonElement root, string field, string controlId, string phase, List<ComplianceEvidenceGap> gaps)
    {
        var declaration = Property(root, field);
        if (declaration.ValueKind != JsonValueKind.Object || !ValidStrings(declaration, "algorithms", 10) ||
            !HasText(declaration, "coverage"))
        {
            gaps.Add(new(controlId, "HipaaEncryptionEvidenceInvalid",
                $"The reviewed encryption-{phase} declaration must name its algorithms and what it covers.", id));
        }
    }

    private static void ValidateBreachNotification(Guid id, JsonElement root, List<ComplianceEvidenceGap> gaps)
    {
        var window = Property(root, "notificationWindowDays");
        if (window.ValueKind != JsonValueKind.Number || !window.TryGetInt32(out var days) || days is < 1 or > 60)
        {
            gaps.Add(new("164.404", "HipaaBreachWindowInvalid",
                "The reviewed breach process must declare an individual-notification window of at most 60 days.", id));
        }
    }

    internal static IReadOnlyList<ComplianceEvidenceGap> InspectScope(ComplianceFrameworkTemplate template,
        CreateCompliancePackageRequest request, IReadOnlyList<ComplianceDocumentSnapshot> documents,
        IReadOnlyDictionary<Guid, JsonElement> roots, DateTime capturedAtUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var gaps = new List<ComplianceEvidenceGap>();
        void AllGap(string code, string detail, Guid? documentId)
        { gaps.AddRange(template.Controls.Select(item => new ComplianceEvidenceGap(item.Id, code, detail, documentId))); }
        // Fail closed: each family assessment must map exactly the safeguards that require it, so an
        // unmapped safeguard is reported instead of silently accepted. Per-control document presence,
        // approval and period coverage are enforced separately by the common engine checks.
        foreach (var type in new[] { "hipaa-risk-analysis", "hipaa-security-management", "hipaa-physical-safeguards",
            "hipaa-technical-safeguards", "hipaa-breach-notification" })
        {
            var document = documents.SingleOrDefault(item => item.Type == type);
            if (document is null) { continue; } // The generic engine already reports the missing document per control.
            var expected = template.Controls.Where(item => item.RequiredDocumentTypes.Contains(type, StringComparer.Ordinal))
                .Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            if (!expected.SetEquals(document.ControlIds))
            { AllGap("HipaaSafeguardMappingIncomplete", $"The {type} assessment must map exactly the safeguards that require it.", document.Id); }
        }
        return gaps;
    }

    private static bool Utc(JsonElement root, string name, out DateTime value)
    {
        var field = Property(root, name);
        value = default;
        return field.ValueKind == JsonValueKind.String && field.TryGetDateTime(out value) && value.Kind == DateTimeKind.Utc;
    }
}
