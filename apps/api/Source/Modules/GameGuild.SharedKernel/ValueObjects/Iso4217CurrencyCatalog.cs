namespace GameGuild;

/// <summary>
///     Reference data for a single ISO 4217 currency.
/// </summary>
/// <param name="Code">Three-letter alphabetic currency code (e.g. "USD"). Always upper-case.</param>
/// <param name="NumericCode">Three-digit ISO 4217 numeric code (e.g. "840"). Kept as a string to preserve leading zeros.</param>
/// <param name="MinorUnitExponent">
///     Number of minor-unit digits the currency carries: 0 for JPY-style integer currencies,
///     2 for most, 3 for KWD/BHD-style, 4 for funds codes such as CLF.
/// </param>
/// <param name="Symbol">Commonly used currency symbol (e.g. "$"). Display hint only — never persisted with amounts.</param>
/// <param name="EnglishName">Official English name of the currency.</param>
public sealed record CurrencyInfo(
    string Code,
    string NumericCode,
    int MinorUnitExponent,
    string Symbol,
    string EnglishName);

/// <summary>
///     Static catalog of ISO 4217 currency reference data (code, numeric code, minor-unit
///     exponent, symbol, English name).
///     This is pure platform reference data: no FX rates, no domain-module vocabulary.
///     Codes outside the catalog fall back to <see cref="DefaultMinorUnitExponent"/> minor
///     units rather than being rejected, so legacy persisted values keep working while the
///     platform migrates toward catalog-backed validation.
/// </summary>
public static class Iso4217CurrencyCatalog
{
    /// <summary>
    ///     Minor-unit exponent assumed for currencies that are not in the catalog.
    ///     Matches the historical hard-coded two-decimal behavior of <see cref="Money"/>.
    /// </summary>
    public const int DefaultMinorUnitExponent = 2;

    private static readonly IReadOnlyDictionary<string, CurrencyInfo> CurrenciesByCode =
        BuildCatalog(new Dictionary<string, CurrencyInfo>(StringComparer.Ordinal));

    /// <summary>Number of currencies described by the catalog.</summary>
    public static int Count { get => CurrenciesByCode.Count; }

    /// <summary>All catalog entries (unordered). Materialized once; the underlying
    /// <see cref="Dictionary{TKey,TValue}.ValueCollection"/> does not implement
    /// <see cref="IReadOnlyCollection{T}"/> on its own.</summary>
    public static IReadOnlyCollection<CurrencyInfo> All { get; } = CurrenciesByCode.Values.ToArray();

    /// <summary>
    ///     Checks whether <paramref name="code"/> is a known ISO 4217 currency code.
    ///     Case-insensitive.
    /// </summary>
    public static bool Exists(string? code) { return TryGet(code, out _); }

    /// <summary>
    ///     Looks up a currency by its three-letter code, case-insensitively.
    /// </summary>
    /// <param name="code">Currency code to look up.</param>
    /// <param name="currency">The matching catalog entry, normalized to its canonical upper-case code.</param>
    /// <returns>True when the code is known to the catalog.</returns>
    public static bool TryGet(string? code, out CurrencyInfo? currency)
    {
        currency = null;
        if (string.IsNullOrWhiteSpace(code) || code.Length != 3)
        {
            return false;
        }

        return CurrenciesByCode.TryGetValue(code.ToUpperInvariant(), out currency);
    }

    /// <summary>
    ///     Gets a currency by its three-letter code, case-insensitively.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the code is not present in the catalog.</exception>
    public static CurrencyInfo Get(string code)
    {
        if (!TryGet(code, out var currency) || currency is null)
        {
            throw new ArgumentException(
                $"Currency code '{code}' is not a known ISO 4217 currency in the platform catalog.", nameof(code));
        }

        return currency;
    }

    /// <summary>
    ///     Gets the minor-unit exponent for a currency code (0 for JPY, 2 for USD, 3 for KWD, 4 for CLF).
    ///     Unknown codes return <see cref="DefaultMinorUnitExponent"/> so callers never have to branch
    ///     on catalog membership to round safely.
    /// </summary>
    public static int GetMinorUnitExponent(string? code)
    {
        return TryGet(code, out var currency) && currency is not null
            ? currency.MinorUnitExponent
            : DefaultMinorUnitExponent;
    }

    private static IReadOnlyDictionary<string, CurrencyInfo> BuildCatalog(Dictionary<string, CurrencyInfo> seed)
    {
        // Whitelisted billing currencies first (CurrencyCodes.Supported in Commerce.Billing),
        // then the broader active-currency set the platform is growing into. Exponents follow
        // ISO 4217 table "List one: Currency and funds code list".
        Add(seed, "USD", "840", 2, "$", "US Dollar");
        Add(seed, "EUR", "978", 2, "€", "Euro");
        Add(seed, "GBP", "826", 2, "£", "Pound Sterling");
        Add(seed, "JPY", "392", 0, "¥", "Yen");
        Add(seed, "CAD", "124", 2, "C$", "Canadian Dollar");
        Add(seed, "AUD", "036", 2, "A$", "Australian Dollar");

        // Americas
        Add(seed, "BRL", "986", 2, "R$", "Brazilian Real");
        Add(seed, "MXN", "484", 2, "MX$", "Mexican Peso");
        Add(seed, "CLP", "152", 0, "CLP$", "Chilean Peso");
        Add(seed, "COP", "170", 2, "CO$", "Colombian Peso");
        Add(seed, "ARS", "032", 2, "AR$", "Argentine Peso");
        Add(seed, "PEN", "604", 2, "S/", "Sol");
        Add(seed, "UYU", "858", 2, "$U", "Peso Uruguayo");
        Add(seed, "BOB", "068", 2, "Bs", "Boliviano");
        Add(seed, "PYG", "600", 0, "₲", "Guarani");
        Add(seed, "CRC", "188", 2, "₡", "Costa Rican Colon");
        Add(seed, "DOP", "214", 2, "RD$", "Dominican Peso");
        Add(seed, "GTQ", "320", 2, "Q", "Quetzal");
        Add(seed, "CLF", "990", 4, "CLF", "Unidad de Fomento (funds code)");

        // Europe
        Add(seed, "CHF", "756", 2, "CHF", "Swiss Franc");
        Add(seed, "SEK", "752", 2, "kr", "Swedish Krona");
        Add(seed, "NOK", "578", 2, "kr", "Norwegian Krone");
        Add(seed, "DKK", "208", 2, "kr", "Danish Krone");
        Add(seed, "ISK", "352", 0, "kr", "Iceland Krona");
        Add(seed, "PLN", "985", 2, "zł", "Zloty");
        Add(seed, "CZK", "203", 2, "Kč", "Czech Koruna");
        Add(seed, "HUF", "348", 0, "Ft", "Forint");
        Add(seed, "RON", "946", 2, "lei", "Romanian Leu");
        Add(seed, "BGN", "975", 2, "лв", "Bulgarian Lev");
        Add(seed, "UAH", "980", 2, "₴", "Hryvnia");
        Add(seed, "RUB", "643", 2, "₽", "Russian Ruble");
        Add(seed, "TRY", "949", 2, "₺", "Turkish Lira");

        // Middle East and Africa
        Add(seed, "ILS", "376", 2, "₪", "New Israeli Sheqel");
        Add(seed, "AED", "784", 2, "د.إ", "UAE Dirham");
        Add(seed, "SAR", "682", 2, "﷼", "Saudi Riyal");
        Add(seed, "QAR", "634", 2, "ر.ق", "Qatari Riyal");
        Add(seed, "KWD", "414", 3, "د.ك", "Kuwaiti Dinar");
        Add(seed, "BHD", "048", 3, ".د.ب", "Bahraini Dinar");
        Add(seed, "OMR", "512", 3, "﷼", "Rial Omani");
        Add(seed, "JOD", "400", 3, "د.ا", "Jordanian Dinar");
        Add(seed, "IQD", "368", 3, "ع.د", "Iraqi Dinar");
        Add(seed, "LYD", "434", 3, "ل.د", "Libyan Dinar");
        Add(seed, "TND", "788", 3, "د.ت", "Tunisian Dinar");
        Add(seed, "EGP", "818", 2, "E£", "Egyptian Pound");
        Add(seed, "ZAR", "710", 2, "R", "Rand");
        Add(seed, "NGN", "566", 2, "₦", "Naira");
        Add(seed, "KES", "404", 2, "KSh", "Kenyan Shilling");
        Add(seed, "GHS", "936", 2, "GH₵", "Ghana Cedi");
        Add(seed, "TZS", "834", 2, "TSh", "Tanzanian Shilling");
        Add(seed, "UGX", "800", 0, "USh", "Uganda Shilling");
        Add(seed, "MAD", "504", 2, "د.م.", "Moroccan Dirham");
        Add(seed, "DZD", "012", 2, "د.ج", "Algerian Dinar");
        Add(seed, "XOF", "952", 0, "CFA", "CFA Franc BCEAO");
        Add(seed, "XAF", "950", 0, "FCFA", "CFA Franc BEAC");

        // Asia-Pacific
        Add(seed, "NZD", "554", 2, "NZ$", "New Zealand Dollar");
        Add(seed, "CNY", "156", 2, "CN¥", "Yuan Renminbi");
        Add(seed, "HKD", "344", 2, "HK$", "Hong Kong Dollar");
        Add(seed, "SGD", "702", 2, "S$", "Singapore Dollar");
        Add(seed, "KRW", "410", 0, "₩", "Won");
        Add(seed, "INR", "356", 2, "₹", "Indian Rupee");
        Add(seed, "IDR", "360", 2, "Rp", "Rupiah");
        Add(seed, "MYR", "458", 2, "RM", "Malaysian Ringgit");
        Add(seed, "PHP", "608", 2, "₱", "Philippine Peso");
        Add(seed, "THB", "764", 2, "฿", "Baht");
        Add(seed, "VND", "704", 0, "₫", "Dong");
        Add(seed, "PKR", "586", 2, "Rs", "Pakistan Rupee");
        Add(seed, "BDT", "050", 2, "৳", "Taka");
        Add(seed, "LKR", "144", 2, "Rs", "Sri Lanka Rupee");
        Add(seed, "NPR", "524", 2, "₨", "Nepalese Rupee");
        Add(seed, "MMK", "104", 0, "K", "Kyat");
        Add(seed, "KHR", "116", 0, "៛", "Riel");
        Add(seed, "LAK", "418", 0, "₭", "Lao Kip");
        Add(seed, "TWD", "901", 2, "NT$", "New Taiwan Dollar");
        Add(seed, "MOP", "446", 2, "MOP$", "Pataca");
        Add(seed, "BND", "096", 2, "B$", "Brunei Dollar");
        Add(seed, "FJD", "242", 2, "FJ$", "Fiji Dollar");
        Add(seed, "XPF", "953", 0, "CFPF", "CFP Franc");

        return seed;
    }

    private static void Add(
        Dictionary<string, CurrencyInfo> seed,
        string code,
        string numericCode,
        int minorUnitExponent,
        string symbol,
        string englishName)
    {
        var canonicalCode = code.ToUpperInvariant();
        var entry = new CurrencyInfo(canonicalCode, numericCode, minorUnitExponent, symbol, englishName);

        // Duplicate codes or numeric codes would mean the reference data itself is corrupt;
        // fail fast at first use instead of serving ambiguous lookups.
        if (seed.ContainsKey(canonicalCode))
        {
            throw new InvalidOperationException($"Duplicate ISO 4217 currency code '{canonicalCode}' in catalog seed data.");
        }

        if (seed.Values.Any(existing => existing.NumericCode == numericCode))
        {
            throw new InvalidOperationException($"Duplicate ISO 4217 numeric code '{numericCode}' in catalog seed data.");
        }

        seed.Add(canonicalCode, entry);
    }
}
