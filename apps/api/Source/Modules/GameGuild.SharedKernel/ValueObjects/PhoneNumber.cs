namespace GameGuild;

/// <summary>
///     Represents a phone number value object with validation
/// </summary>
public record PhoneNumber
{
    public PhoneNumber(string phoneNumber, string? countryCode = null)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new ArgumentException("Phone number cannot be null or empty.", nameof(phoneNumber));
        }

        var cleanNumber = CleanPhoneNumber(phoneNumber);

        if (cleanNumber.StartsWith("+"))
        {
            // International format
            if (cleanNumber.Length < 8 || cleanNumber.Length > 15)
            {
                throw new ArgumentException("Invalid phone number format.", nameof(phoneNumber));
            }

            CountryCode = ExtractCountryCode(cleanNumber);
            NationalNumber = cleanNumber[CountryCode.Length..];
        }
        else
        {
            // National format — country code is required
            if (string.IsNullOrWhiteSpace(countryCode))
            {
                throw new ArgumentException("Country code is required for national phone number format.", nameof(countryCode));
            }

            CountryCode = countryCode;
            NationalNumber = cleanNumber;

            if (NationalNumber.Length < 7 || NationalNumber.Length > 12)
            {
                throw new ArgumentException("Invalid phone number format.", nameof(phoneNumber));
            }
        }

        Value = CountryCode + NationalNumber;
    }

    public string Value { get; }

    public string CountryCode { get; }

    public string NationalNumber { get; }

    public static implicit operator string(PhoneNumber phone) { return phone.Value; }

    private static string CleanPhoneNumber(string phoneNumber)
    {
        // Remove all non-digit characters except + at the beginning
        var cleaned = phoneNumber.AsSpan().Trim();
        var buffer = new System.Text.StringBuilder(cleaned.Length);

        for (var i = 0; i < cleaned.Length; i++)
        {
            var c = cleaned[i];

            if (char.IsDigit(c) || (i == 0 && c == '+')) { buffer.Append(c); }
        }

        return buffer.ToString();
    }

    private static string ExtractCountryCode(string internationalNumber)
    {
        // ITU-T E.164 country calling codes, ordered by specificity (longer prefixes first)
        // to ensure correct matching (e.g., +1868 Trinidad before +1 NANP)
        ReadOnlySpan<string> codes =
        [
            // 4-digit codes (selected Caribbean/Pacific nations under NANP)
            "+1684", "+1264", "+1268", "+1242", "+1246", "+1441", "+1284",
            "+1345", "+1767", "+1809", "+1829", "+1849", "+1473", "+1876",
            "+1664", "+1721", "+1758", "+1784", "+1868", "+1869", "+1649",
            // 3-digit codes
            "+93",  "+355", "+213", "+376", "+244", "+54",  "+374", "+297",
            "+61",  "+43",  "+994", "+973", "+880", "+375", "+32",  "+501",
            "+229", "+975", "+591", "+387", "+267", "+55",  "+673", "+359",
            "+226", "+257", "+855", "+237", "+238", "+236", "+235", "+56",
            "+86",  "+57",  "+269", "+242", "+243", "+506", "+385", "+53",
            "+357", "+420", "+45",  "+253", "+593", "+20",  "+503", "+240",
            "+291", "+372", "+251", "+679", "+358", "+33",  "+241", "+220",
            "+995", "+49",  "+233", "+30",  "+299", "+502", "+224", "+245",
            "+592", "+509", "+504", "+36",  "+354", "+91",  "+62",  "+98",
            "+964", "+353", "+972", "+39",  "+225", "+81",  "+962", "+254",
            "+996", "+856", "+371", "+961", "+266", "+231", "+218", "+423",
            "+370", "+352", "+261", "+265", "+60",  "+960", "+223", "+356",
            "+222", "+230", "+52",  "+373", "+377", "+976", "+382", "+212",
            "+258", "+264", "+977", "+31",  "+64",  "+505", "+227", "+234",
            "+850", "+47",  "+968", "+92",  "+507", "+675", "+595", "+51",
            "+63",  "+48",  "+351", "+974", "+40",  "+250", "+966", "+221",
            "+381", "+232", "+65",  "+421", "+386", "+252", "+27",  "+82",
            "+211", "+34",  "+94",  "+249", "+597", "+268", "+46",  "+41",
            "+963", "+886", "+992", "+255", "+66",  "+228", "+676", "+216",
            "+90",  "+993", "+256", "+380", "+971", "+44",  "+598", "+998",
            "+678", "+58",  "+84",  "+967", "+260", "+263",
            // 1-digit code (NANP: US, Canada, Caribbean)
            "+7",   // Russia/Kazakhstan
            "+1"    // North American Numbering Plan
        ];

        foreach (var code in codes)
        {
            if (internationalNumber.StartsWith(code, StringComparison.Ordinal))
            {
                return code;
            }
        }

        // Fallback: assume first 2–3 digits are the country code
        return internationalNumber[..3];
    }

    public string GetDisplayFormat()
    {
        return CountryCode switch
        {
            "+1" when NationalNumber.Length == 10 => $"({NationalNumber[..3]}) {NationalNumber.Substring(3, 3)}-{NationalNumber[6..]}",
            _ => $"{CountryCode} {NationalNumber}"
        };
    }

    public override string ToString() { return GetDisplayFormat(); }
}
