using System.Text.Json;

namespace GameGuild.Compliance.Audit;

/// <summary>Structured evidence quality and scope checks for the versioned native profiles.</summary>
internal static class ComplianceNativeEvidence
{
    internal static bool IsNative(ComplianceFrameworkTemplate template) =>
        template.Id is ComplianceFrameworkCatalog.IsoIsmsId or ComplianceFrameworkCatalog.GdprId ||
        FedRampComplianceCatalog.Find(template.Id) is not null || Soc2ComplianceCatalog.Find(template.Id) is not null;

    internal static void ValidateDocument(ComplianceFrameworkTemplate template, ComplianceDocumentSnapshot document,
        JsonElement root, DateTime capturedAtUtc, List<ComplianceEvidenceGap> gaps)
    {
        if (!IsNative(template) || document.Type is "control-assessment" or "applicability" or "fedramp-supporting-evidence" or "soc2-supporting-evidence") { return; }
        if (root.ValueKind != JsonValueKind.Object)
        {
            gaps.Add(new(string.Empty, "StructuredDocumentRequired", "Native framework evidence requires actual structured JSON contents.", document.Id));
            return;
        }
        if (template.Id == ComplianceFrameworkCatalog.IsoIsmsId)
        {
            IsoComplianceEvidence.ValidateDocument(template, document, root, gaps);
        }
        else if (template.Id == ComplianceFrameworkCatalog.GdprId) { GdprComplianceEvidence.ValidateDocument(document, root, capturedAtUtc, gaps); }
        else if (Soc2ComplianceCatalog.Find(template.Id) is not null) { Soc2ComplianceEvidence.ValidateDocument(template, document, root, capturedAtUtc, gaps); }
        else { FedRampComplianceEvidence.ValidateDocument(template, document, root, capturedAtUtc, gaps); }
    }

    internal static IReadOnlyList<ComplianceEvidenceGap> InspectScope(ComplianceFrameworkTemplate template,
        CreateCompliancePackageRequest request, IReadOnlyList<ComplianceDocumentSnapshot> documents,
        IReadOnlyDictionary<Guid, JsonElement> roots, DateTime capturedAtUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return template.Id switch
        {
            ComplianceFrameworkCatalog.IsoIsmsId => IsoComplianceEvidence.InspectScope(template, request, documents, roots, cancellationToken),
            ComplianceFrameworkCatalog.GdprId => GdprComplianceEvidence.InspectScope(request, documents, roots, cancellationToken),
            _ => Soc2ComplianceCatalog.Find(template.Id) is not null
                ? Soc2ComplianceEvidence.InspectScope(template, request, documents, roots, capturedAtUtc, cancellationToken)
                : FedRampComplianceCatalog.Find(template.Id) is not null
                ? FedRampComplianceEvidence.InspectScope(template, request, documents, roots, capturedAtUtc, cancellationToken) : []
        };
    }

    internal static JsonElement Property(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) ? value : default;

    internal static string Text(JsonElement root, string name)
    {
        var value = Property(root, name);
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    }

    internal static bool HasText(JsonElement root, string name) => Text(root, name) is { Length: > 0 and <= 4000 } text && !string.IsNullOrWhiteSpace(text);

    internal static JsonElement[] Array(JsonElement root, string name, int maximum = 200)
    {
        var value = Property(root, name);
        return value.ValueKind == JsonValueKind.Array && value.GetArrayLength() <= maximum ? value.EnumerateArray().ToArray() : [];
    }

    internal static string[] Strings(JsonElement root, string name, int maximum = 200) => Array(root, name, maximum)
        .Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty).ToArray();

    internal static bool ValidStrings(JsonElement root, string name, int maximum, bool allowEmpty = false)
    {
        var field = Property(root, name);
        if (field.ValueKind != JsonValueKind.Array || field.GetArrayLength() > maximum || (!allowEmpty && field.GetArrayLength() == 0)) { return false; }
        var values = Strings(root, name, maximum);
        return values.Length == field.GetArrayLength() && values.All(value => value.Length is > 0 and <= 100 && !string.IsNullOrWhiteSpace(value)) &&
            values.Distinct(StringComparer.Ordinal).Count() == values.Length;
    }

    internal static void RequireFields(JsonElement root, IEnumerable<string> fields, string controlId,
        Guid documentId, List<ComplianceEvidenceGap> gaps)
    {
        foreach (var field in fields.Where(field => !HasText(root, field)))
        {
            gaps.Add(new(controlId, "NativeEvidenceFieldInvalid", $"A bounded, nonempty {field} declaration is required.", documentId));
        }
    }

    internal static bool IsUsable(ComplianceDocumentSnapshot document, CreateCompliancePackageRequest request, string controlId) =>
        document.Review == ComplianceDocumentReview.Approved && document.ReviewedByUserId is not null && document.ReviewedAtUtc is not null &&
        document.ControlIds.Contains(controlId, StringComparer.Ordinal) &&
        document.ValidFromUtc <= request.PeriodStartUtc.UtcDateTime && document.ValidUntilUtc >= request.PeriodEndUtc.UtcDateTime;
}
