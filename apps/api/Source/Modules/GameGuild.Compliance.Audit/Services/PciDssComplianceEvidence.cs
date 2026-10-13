using System.Text.Json;
using static GameGuild.Compliance.Audit.ComplianceNativeEvidence;

namespace GameGuild.Compliance.Audit;

/// <summary>Structured SAQ A posture checks. A declared posture is not a QSA attestation of compliance.</summary>
internal static class PciDssComplianceEvidence
{
    internal static void ValidateDocument(ComplianceDocumentSnapshot document, JsonElement root,
        DateTime capturedAtUtc, List<ComplianceEvidenceGap> gaps)
    {
        if (document.Type != "pci-saqa-eligibility") { return; }
        void Gap(string code, string detail) => gaps.Add(new(string.Empty, code, detail, document.Id));
        if (Text(root, "saqType") != "A")
        { Gap("PciSaqaPostureInvalid", "The profile organizes SAQ A evidence; a different SAQ type must use its own review path."); }
        if (Property(root, "tokenizationOnly").ValueKind != JsonValueKind.True)
        { Gap("PciSaqaPostureInvalid", "SAQ A evidence for this platform requires the reviewed tokenization-only declaration."); }
        if (Text(root, "panStoragePosition") != "none")
        { Gap("PciSaqaPostureInvalid", "SAQ A requires the reviewed declaration that no cardholder data is stored by the platform."); }
        if (!ValidStrings(root, "validatedProviders", 10))
        { Gap("PciSaqaPostureInvalid", "Name the PCI DSS validated payment providers handling every cardholder-data function."); }
        if (!ValidStrings(root, "eligibilityConfirmations", 10) ||
            Array(root, "eligibilityConfirmations", 10).Length < 5)
        { Gap("PciSaqaPostureInvalid", "Affirm each reviewed SAQ A eligibility criterion explicitly."); }
        if (!Utc(root, "reviewedAtUtc", out var reviewed) || reviewed > capturedAtUtc)
        { Gap("PciSaqaPostureInvalid", "The eligibility declaration must record a UTC review date no later than capture."); }
    }

    internal static IReadOnlyList<ComplianceEvidenceGap> InspectScope(ComplianceFrameworkTemplate template,
        CreateCompliancePackageRequest request, IReadOnlyList<ComplianceDocumentSnapshot> documents,
        IReadOnlyDictionary<Guid, JsonElement> roots, DateTime capturedAtUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var gaps = new List<ComplianceEvidenceGap>();
        // Fail closed: each document type must map exactly the requirements that require it.
        foreach (var type in new[] { "pci-saqa-eligibility", "pci-tokenized-storage", "pci-vulnerability-management", "pci-policy" })
        {
            var document = documents.SingleOrDefault(item => item.Type == type);
            if (document is null) { continue; } // The generic engine reports the missing document per requirement.
            var expected = template.Controls.Where(item => item.RequiredDocumentTypes.Contains(type, StringComparer.Ordinal))
                .Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            if (!expected.SetEquals(document.ControlIds))
            { gaps.AddRange(template.Controls.Select(item => new ComplianceEvidenceGap(item.Id, "PciRequirementMappingIncomplete",
                $"The {type} assessment must map exactly the requirements that require it.", document.Id))); }
        }
        var eligibility = documents.SingleOrDefault(item => item.Type == "pci-saqa-eligibility");
        if (eligibility is null) { return gaps; } // The generic engine reports the missing declaration per requirement.
        var root = roots[eligibility.Id];
        var postureValid = Text(root, "saqType") == "A" && Property(root, "tokenizationOnly").ValueKind == JsonValueKind.True &&
            Text(root, "panStoragePosition") == "none" && ValidStrings(root, "validatedProviders", 10);
        var rationales = Property(root, "requirementRationales");
        foreach (var control in template.Controls)
        {
            // Every requirement resolved by the SAQ A declaration needs its own reviewed rationale;
            // an unjustified not-applicable requirement stays a gap instead of passing silently.
            if (!control.RequiredDocumentTypes.Contains("pci-saqa-eligibility", StringComparer.Ordinal)) { continue; }
            if (!postureValid)
            {
                gaps.Add(new(control.Id, "PciSaqaPostureMismatch",
                    "The SAQ A declaration does not support the token-only outsourced posture this profile requires.", eligibility.Id));
            }
            var rationale = Property(rationales, control.Id);
            if (rationale.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(rationale.GetString()) ||
                rationale.GetString()!.Length > 2000)
            {
                gaps.Add(new(control.Id, "PciRequirementRationaleMissing",
                    $"Requirement {control.Id} requires a reviewed SAQ A rationale in the eligibility declaration.", eligibility.Id));
            }
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
