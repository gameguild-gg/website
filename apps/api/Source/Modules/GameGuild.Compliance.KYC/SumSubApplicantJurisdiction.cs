using System.Text.Json;

namespace GameGuild.Compliance.KYC;

public static class SumSubApplicantJurisdiction
{
    public static string? Resolve(JsonElement applicant)
    {
        return Normalize(NestedCountry(applicant, "info"))
            ?? Normalize(DirectCountry(applicant))
            ?? Normalize(NestedCountry(applicant, "fixedInfo"));
    }

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().ToUpperInvariant();
        return normalized.Length == 3 && normalized.All(character => character is >= 'A' and <= 'Z')
            ? normalized : null;
    }

    private static string? DirectCountry(JsonElement applicant) =>
        StringProperty(applicant, "country");

    private static string? NestedCountry(JsonElement applicant, string propertyName) =>
        applicant.TryGetProperty(propertyName, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? StringProperty(nested, "country")
            : null;

    private static string? StringProperty(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
