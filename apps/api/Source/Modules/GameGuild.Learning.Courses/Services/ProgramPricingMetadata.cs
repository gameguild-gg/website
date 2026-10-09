using System.Text.Json;

namespace GameGuild.Learning.Courses;

internal static class ProgramPricingMetadata
{
  private const string MetadataKey = "pricing";
  private const string DefaultCurrency = "USD";

  public static PricingDto Read(Program program)
  {
    if (string.IsNullOrWhiteSpace(program.Metadata))
        {
            return Disabled();
        }

        try
    {
      using var document = JsonDocument.Parse(program.Metadata);
      if (!document.RootElement.TryGetProperty(MetadataKey, out var pricing) || pricing.ValueKind != JsonValueKind.Object)
      {
        return Disabled();
      }

      var price = TryGetDecimal(pricing, "price") ?? 0m;
      var currency = TryGetString(pricing, "currency") ?? DefaultCurrency;
      var isSubscription = TryGetBoolean(pricing, "isSubscription") ?? false;
      var subscriptionDurationDays = TryGetInt(pricing, "subscriptionDurationDays");
      var isEnabled = TryGetBoolean(pricing, "isMonetizationEnabled") ?? false;

      return new PricingDto(price, currency, isSubscription, isSubscription ? subscriptionDurationDays : null, isEnabled);
    }
    catch (JsonException)
    {
      return Disabled();
    }
  }

  public static PricingDto Enable(Program program, MonetizationDto monetization)
  {
    return Write(
      program,
      monetization.Price,
      monetization.Currency,
      monetization.IsSubscription,
      monetization.SubscriptionDurationDays,
      true);
  }

  public static PricingDto Disable(Program program)
  {
    var current = Read(program);
    return Write(program, current.Price, current.Currency, current.IsSubscription, current.SubscriptionDurationDays, false);
  }

  public static PricingDto Update(Program program, UpdatePricingDto update)
  {
    var current = Read(program);
    var isSubscription = update.IsSubscription ?? current.IsSubscription;
    var subscriptionDurationDays = isSubscription ? update.SubscriptionDurationDays ?? current.SubscriptionDurationDays : null;

    return Write(
      program,
      update.Price ?? current.Price,
      update.Currency ?? current.Currency,
      isSubscription,
      subscriptionDurationDays,
      current.IsMonetizationEnabled);
  }

  private static PricingDto Write(Program program, decimal price, string? currency, bool isSubscription, int? subscriptionDurationDays, bool isEnabled)
  {
    var normalizedPrice = Math.Max(0m, price);
    var normalizedCurrency = string.IsNullOrWhiteSpace(currency) ? DefaultCurrency : currency.Trim().ToUpperInvariant();
    var normalizedDuration = isSubscription ? subscriptionDurationDays : null;

    program.SetMetadata(MetadataKey, new
    {
      price = normalizedPrice,
      currency = normalizedCurrency,
      isSubscription,
      subscriptionDurationDays = normalizedDuration,
      isMonetizationEnabled = isEnabled,
    });

    return new PricingDto(normalizedPrice, normalizedCurrency, isSubscription, normalizedDuration, isEnabled);
  }

  private static PricingDto Disabled() => new(0m, DefaultCurrency, false, null, false);

  private static decimal? TryGetDecimal(JsonElement element, string propertyName)
  {
    return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var value)
      ? value
      : null;
  }

  private static string? TryGetString(JsonElement element, string propertyName)
  {
    return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
      ? property.GetString()
      : null;
  }

  private static bool? TryGetBoolean(JsonElement element, string propertyName)
  {
    return element.TryGetProperty(propertyName, out var property) && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
      ? property.GetBoolean()
      : null;
  }

  private static int? TryGetInt(JsonElement element, string propertyName)
  {
    return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
      ? value
      : null;
  }
}
