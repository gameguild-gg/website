namespace GameGuild;

/// <summary>
///     Represents an address value object
/// </summary>
public record Address
{
    public Address(string street, string city, string state, string postalCode, string country, string? unit = null)
    {
        if (string.IsNullOrWhiteSpace(street))
        {
            throw new ArgumentException("Street cannot be null or empty.", nameof(street));
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new ArgumentException("City cannot be null or empty.", nameof(city));
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentException("State cannot be null or empty.", nameof(state));
        }

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            throw new ArgumentException("Postal code cannot be null or empty.", nameof(postalCode));
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            throw new ArgumentException("Country cannot be null or empty.", nameof(country));
        }

        Street = street.Trim();
        City = city.Trim();
        State = state.Trim();
        PostalCode = postalCode.Trim();
        Country = country.Trim();
        Unit = unit?.Trim();
    }

    public string Street { get; }

    public string City { get; }

    public string State { get; }

    public string PostalCode { get; }

    public string Country { get; }

    public string? Unit { get; }

    public string GetFullAddress()
    {
        var parts = new List<string> { Street };

        if (!string.IsNullOrEmpty(Unit))
        {
            parts.Add($"Unit {Unit}");
        }

        parts.Add($"{City}, {State} {PostalCode}");
        parts.Add(Country);

        return string.Join(Environment.NewLine, parts);
    }

    public string GetOneLine()
    {
        var parts = new List<string> { Street };

        if (!string.IsNullOrEmpty(Unit))
        {
            parts.Add($"Unit {Unit}");
        }

        parts.Add(City);
        parts.Add(State);
        parts.Add(PostalCode);
        parts.Add(Country);

        return string.Join(", ", parts);
    }

    public override string ToString() { return GetOneLine(); }
}
