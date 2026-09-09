namespace GameGuild.Finance.Economy.Risk;

public static class EconomyJurisdictionCode
{
    public static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().ToUpperInvariant();
        return normalized.Length == 3 && normalized.All(character => character is >= 'A' and <= 'Z')
            ? normalized
            : null;
    }

    public static string Require(string value, string parameterName)
    {
        return NormalizeOptional(value)
            ?? throw new ArgumentException(
                "Jurisdiction must be an ISO 3166-1 alpha-3 code.",
                parameterName);
    }
}
