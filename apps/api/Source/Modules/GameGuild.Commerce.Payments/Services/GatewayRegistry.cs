namespace GameGuild.Commerce.Payments;

/// <summary>
///     Registry of the payment gateway adapters wired in DI (issue #413). Every processing
///     adapter registered by <c>PaymentsModule</c> is addressable here by provider id;
///     unknown provider ids resolve to null so callers fail closed instead of guessing.
/// </summary>
public interface IGatewayRegistry
{
    /// <summary>All registered gateway adapters, in registration order.</summary>
    IReadOnlyList<IPaymentGateway> Gateways { get; }

    /// <summary>
    ///     Resolves the adapter for a provider id (case-insensitive). Returns null for
    ///     unknown provider ids — callers must treat that as a failure, never a default.
    /// </summary>
    IPaymentGateway? Find(string providerId);
}

/// <summary>
///     Default <see cref="IGatewayRegistry" />: holds the concrete adapter instances the
///     module registers (Stripe, PayPal). Adding a third gateway means adding it to this
///     constructor and to the module registration — the registry is intentionally explicit
///     so no adapter is ever routed to by accident.
/// </summary>
public sealed class GatewayRegistry(
    StripePaymentGateway stripe,
    PayPalPaymentGateway payPal) : IGatewayRegistry
{
    private readonly IReadOnlyList<IPaymentGateway> _gateways = new IPaymentGateway[] { stripe, payPal };

    /// <inheritdoc />
    public IReadOnlyList<IPaymentGateway> Gateways => _gateways;

    /// <inheritdoc />
    public IPaymentGateway? Find(string providerId) =>
        string.IsNullOrWhiteSpace(providerId)
            ? null
            : _gateways.FirstOrDefault(gateway =>
                string.Equals(gateway.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
}
