using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Config-driven, fail-closed gateway selection (issue #413). Eligibility is the
///     conjunction of the adapter being registered, the adapter reporting
///     <c>IsEnabled</c>, and — when a <c>Payments:GatewayRouting</c> entry exists for the
///     provider — that entry being enabled and its currency/country/payment-method/amount
///     filters matching the routing context. Results are ordered by ascending priority;
///     ties keep the registration order. No gateway is ever selected implicitly: an empty
///     selection is surfaced through <see cref="GatewaySelectionFailedException" /> by
///     <see cref="SelectPrimary" />.
/// </summary>
public sealed class ConfigPaymentGatewaySelector(
    IGatewayRegistry registry,
    IOptions<GatewayRoutingOptions> routingOptions,
    ILogger<ConfigPaymentGatewaySelector> logger) : IPaymentGatewaySelector
{
    private readonly GatewayRoutingOptions _options = routingOptions.Value;

    /// <inheritdoc />
    public IReadOnlyList<GatewayCandidate> Select(GatewayRouteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (candidates, rejections) = Evaluate(context);

        if (candidates.Count == 0)
        {
            // Fail closed: record why every registered gateway was rejected; the caller
            // turns this into an explicit failure, never a silent default.
            logger.LogWarning(
                "Gateway selection failed closed for currency {Currency}: {Rejections}",
                context.Currency,
                string.Join("; ", rejections.Select(rejection => $"{rejection.ProviderId}={rejection.Reason}")));
        }

        return candidates;
    }

    /// <inheritdoc />
    public GatewayCandidate SelectPrimary(GatewayRouteContext? context = null)
    {
        var effectiveContext = context ?? new GatewayRouteContext(string.Empty);
        var (candidates, rejections) = Evaluate(effectiveContext);

        return candidates.Count > 0
            ? candidates[0]
            : throw GatewaySelectionFailedException.Create(effectiveContext, rejections);
    }

    private (List<GatewayCandidate> Candidates, List<(string ProviderId, string Reason)> Rejections) Evaluate(
        GatewayRouteContext context)
    {
        var candidates = new List<GatewayCandidate>();
        var rejections = new List<(string ProviderId, string Reason)>();

        foreach (var gateway in registry.Gateways)
        {
            var entry = _options.FindEntry(gateway.ProviderId);

            if (!gateway.IsEnabled)
            {
                rejections.Add((gateway.ProviderId, "adapter disabled (PaymentGateways configuration)"));
                continue;
            }

            if (entry is not null && !entry.Enabled)
            {
                rejections.Add((gateway.ProviderId, "routing entry disabled (Payments:GatewayRouting)"));
                continue;
            }

            if (!MatchesFilters(entry, context, out var filterReason))
            {
                rejections.Add((gateway.ProviderId, filterReason));
                continue;
            }

            candidates.Add(
                new GatewayCandidate(
                    gateway,
                    GatewayRoutingOptions.ResolvePriority(gateway.ProviderId, entry),
                    entry));
        }

        // OrderBy is stable, so equal priorities keep the adapter registration order.
        var orderedCandidates = candidates.OrderBy(candidate => candidate.Priority).ToList();

        return (orderedCandidates, rejections);
    }

    /// <summary>
    ///     Applies the entry's opt-in filters. An entry without a filter dimension (empty
    ///     list or null bound) does not restrict that dimension, so an unconfigured gateway
    ///     stays eligible for every request.
    /// </summary>
    private static bool MatchesFilters(GatewayRouteEntry? entry, GatewayRouteContext context, out string reason)
    {
        reason = string.Empty;

        if (entry is null)
        {
            return true;
        }

        if (entry.SupportedCurrencies.Count > 0 &&
            !entry.SupportedCurrencies.Contains(context.Currency, StringComparer.OrdinalIgnoreCase))
        {
            reason = $"currency {context.Currency} not supported";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(context.Country) &&
            entry.SupportedCountries.Count > 0 &&
            !entry.SupportedCountries.Contains(context.Country, StringComparer.OrdinalIgnoreCase))
        {
            reason = $"country {context.Country} not supported";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(context.PaymentMethodType) &&
            entry.SupportedPaymentMethods.Count > 0 &&
            !entry.SupportedPaymentMethods.Contains(context.PaymentMethodType, StringComparer.OrdinalIgnoreCase))
        {
            reason = $"payment method {context.PaymentMethodType} not supported";
            return false;
        }

        if (context.Amount.HasValue)
        {
            if (entry.MinAmount.HasValue && context.Amount.Value < entry.MinAmount.Value)
            {
                reason = $"amount {context.Amount.Value} below the gateway minimum {entry.MinAmount.Value}";
                return false;
            }

            if (entry.MaxAmount.HasValue && context.Amount.Value > entry.MaxAmount.Value)
            {
                reason = $"amount {context.Amount.Value} above the gateway maximum {entry.MaxAmount.Value}";
                return false;
            }
        }

        return true;
    }
}
