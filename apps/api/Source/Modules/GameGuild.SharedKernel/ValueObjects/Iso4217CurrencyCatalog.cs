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
        // ISO 4217 "List One: Currency and funds code list", published 2026-09-17:
        // https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/lists/list-one.xml
        // Retired codes are excluded from new currency configuration. Money preserves its
        // existing two-decimal fallback for historical values with retired/unknown codes.
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
        Add(seed, "HUF", "348", 2, "Ft", "Forint");
        Add(seed, "RON", "946", 2, "lei", "Romanian Leu");
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
        Add(seed, "MMK", "104", 2, "K", "Kyat");
        Add(seed, "KHR", "116", 2, "៛", "Riel");
        Add(seed, "LAK", "418", 2, "₭", "Lao Kip");
        Add(seed, "TWD", "901", 2, "NT$", "New Taiwan Dollar");
        Add(seed, "MOP", "446", 2, "MOP$", "Pataca");
        Add(seed, "BND", "096", 2, "B$", "Brunei Dollar");
        Add(seed, "FJD", "242", 2, "FJ$", "Fiji Dollar");
        Add(seed, "VUV", "548", 0, "VT", "Vatu");
        Add(seed, "XPF", "953", 0, "CFPF", "CFP Franc");

        // Remaining active ISO 4217 currencies (issue #412: full-catalog coverage for the
        // multi-currency foundation). Exponents follow the ISO 4217 "List One" table.
        // Africa
        Add(seed, "AOA", "973", 2, "Kz", "Kwanza");
        Add(seed, "BIF", "108", 0, "FBu", "Burundi Franc");
        Add(seed, "BWP", "072", 2, "P", "Pula");
        Add(seed, "CDF", "976", 2, "FC", "Congolese Franc");
        Add(seed, "CVE", "132", 2, "$", "Cabo Verde Escudo");
        Add(seed, "DJF", "262", 0, "Fdj", "Djibouti Franc");
        Add(seed, "ERN", "232", 2, "Nfk", "Nakfa");
        Add(seed, "ETB", "230", 2, "Br", "Ethiopian Birr");
        Add(seed, "GMD", "270", 2, "D", "Dalasi");
        Add(seed, "GNF", "324", 0, "FG", "Guinean Franc");
        Add(seed, "KMF", "174", 0, "CF", "Comorian Franc");
        Add(seed, "LRD", "430", 2, "L$", "Liberian Dollar");
        Add(seed, "LSL", "426", 2, "L", "Loti");
        Add(seed, "MGA", "969", 2, "Ar", "Malagasy Ariary");
        Add(seed, "MRU", "929", 2, "UM", "Ouguiya");
        Add(seed, "MUR", "480", 2, "₨", "Mauritius Rupee");
        Add(seed, "MVR", "462", 2, "Rf", "Rufiyaa");
        Add(seed, "MWK", "454", 2, "MK", "Malawi Kwacha");
        Add(seed, "MZN", "943", 2, "MT", "Mozambique Metical");
        Add(seed, "NAD", "516", 2, "N$", "Namibia Dollar");
        Add(seed, "RWF", "646", 0, "FRw", "Rwanda Franc");
        Add(seed, "SCR", "690", 2, "₨", "Seychelles Rupee");
        Add(seed, "SDG", "938", 2, "ج.س.", "Sudanese Pound");
        Add(seed, "SLE", "925", 2, "Le", "Leone");
        Add(seed, "SOS", "706", 2, "Sh", "Somali Shilling");
        Add(seed, "SSP", "728", 2, "£", "South Sudanese Pound");
        Add(seed, "STN", "930", 2, "Db", "Dobra");
        Add(seed, "SZL", "748", 2, "E", "Lilangeni");
        Add(seed, "ZMW", "967", 2, "ZK", "Zambian Kwacha");
        Add(seed, "ZWG", "924", 2, "ZiG$", "Zimbabwe Gold");

        // Americas
        Add(seed, "XCG", "532", 2, "XCG", "Caribbean Guilder");
        Add(seed, "BBD", "052", 2, "Bds$", "Barbados Dollar");
        Add(seed, "BSD", "044", 2, "B$", "Bahamian Dollar");
        Add(seed, "BZD", "084", 2, "BZ$", "Belize Dollar");
        Add(seed, "CUP", "192", 2, "$MN", "Cuban Peso");
        Add(seed, "GYD", "328", 2, "G$", "Guyana Dollar");
        Add(seed, "HTG", "332", 2, "G", "Gourde");
        Add(seed, "JMD", "388", 2, "J$", "Jamaican Dollar");
        Add(seed, "KYD", "136", 2, "$", "Cayman Islands Dollar");
        Add(seed, "PAB", "590", 2, "B/.", "Balboa");
        Add(seed, "SRD", "968", 2, "$", "Surinam Dollar");
        Add(seed, "SVC", "222", 2, "$", "El Salvador Colon");
        Add(seed, "TTD", "780", 2, "TT$", "Trinidad and Tobago Dollar");
        Add(seed, "XCD", "951", 2, "EC$", "East Caribbean Dollar");

        // Asia
        Add(seed, "AMD", "051", 2, "֏", "Armenian Dram");
        Add(seed, "AZN", "944", 2, "₼", "Azerbaijan Manat");
        Add(seed, "BTN", "064", 2, "Nu.", "Ngultrum");
        Add(seed, "IRR", "364", 2, "﷼", "Iranian Rial");
        Add(seed, "KGS", "417", 2, "с", "Som");
        Add(seed, "KPW", "408", 2, "₩", "North Korean Won");
        Add(seed, "KZT", "398", 2, "₸", "Tenge");
        Add(seed, "LBP", "422", 2, "ل.ل", "Lebanese Pound");
        Add(seed, "MNT", "496", 2, "₮", "Tugrik");
        Add(seed, "PGK", "598", 2, "K", "Kina");
        Add(seed, "SBD", "090", 2, "SI$", "Solomon Islands Dollar");
        Add(seed, "TJS", "972", 2, "SM", "Somoni");
        Add(seed, "TMT", "934", 2, "T", "Turkmenistan New Manat");
        Add(seed, "TOP", "776", 2, "T$", "Pa’anga");
        Add(seed, "UZS", "860", 2, "сўм", "Uzbekistan Sum");
        Add(seed, "WST", "882", 2, "WS$", "Tala");
        Add(seed, "YER", "886", 2, "﷼", "Yemeni Rial");

        // Europe and Central Asia
        Add(seed, "ALL", "008", 2, "L", "Lek");
        Add(seed, "BAM", "977", 2, "KM", "Convertible Mark");
        Add(seed, "BYN", "933", 2, "Br", "Belarusian Ruble");
        Add(seed, "GEL", "981", 2, "₾", "Lari");
        Add(seed, "GIP", "292", 2, "£", "Gibraltar Pound");
        Add(seed, "MDL", "498", 2, "L", "Moldovan Leu");
        Add(seed, "MKD", "807", 2, "ден", "Denar");
        Add(seed, "RSD", "941", 2, "дин", "Serbian Dinar");

        // Others
        Add(seed, "AWG", "533", 2, "Afl.", "Aruban Florin");
        Add(seed, "HNL", "340", 2, "L", "Lempira");
        Add(seed, "NIO", "558", 2, "C$", "Cordoba Oro");
        Add(seed, "SYP", "760", 2, "£", "Syrian Pound");
        Add(seed, "VES", "928", 2, "Bs.S", "Bolívar Soberano");

        // Other current currencies and funds with defined decimal precision. Use the
        // ISO code as an unambiguous display hint when no localized symbol is supplied.
        Add(seed, "AFN", "971", 2, "AFN", "Afghani");
        Add(seed, "BMD", "060", 2, "BMD", "Bermudian Dollar");
        Add(seed, "BOV", "984", 2, "BOV", "Mvdol");
        Add(seed, "CHE", "947", 2, "CHE", "WIR Euro");
        Add(seed, "CHW", "948", 2, "CHW", "WIR Franc");
        Add(seed, "COU", "970", 2, "COU", "Unidad de Valor Real");
        Add(seed, "FKP", "238", 2, "FKP", "Falkland Islands Pound");
        Add(seed, "MXV", "979", 2, "MXV", "Mexican Unidad de Inversion (UDI)");
        Add(seed, "SHP", "654", 2, "SHP", "Saint Helena Pound");
        Add(seed, "USN", "997", 2, "USN", "US Dollar (Next day)");
        Add(seed, "UYI", "940", 0, "UYI", "Uruguay Peso en Unidades Indexadas (UI)");
        Add(seed, "UYW", "927", 4, "UYW", "Unidad Previsional");
        Add(seed, "VED", "926", 2, "VED", "Bolívar Soberano");
        Add(seed, "XAD", "396", 2, "XAD", "Arab Accounting Dinar");

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
