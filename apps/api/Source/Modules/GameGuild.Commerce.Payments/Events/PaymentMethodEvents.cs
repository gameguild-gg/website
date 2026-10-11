using GameGuild.CQRS;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Domain event raised when a payment method is registered locally (issue #414).
/// </summary>
public class PaymentMethodRegisteredEvent(Guid paymentMethodId, Guid tenantId, Guid userId, string gatewayPaymentMethodId) : DomainEvent
{
    /// <summary>Local payment method ID.</summary>
    public Guid PaymentMethodId { get; } = paymentMethodId;

    /// <summary>Owning tenant.</summary>
    public Guid TenantId { get; } = tenantId;

    /// <summary>Owning user.</summary>
    public Guid UserId { get; } = userId;

    /// <summary>Gateway payment method reference (token only).</summary>
    public string GatewayPaymentMethodId { get; } = gatewayPaymentMethodId;
}

/// <summary>
///     Domain event raised when a payment method becomes its owner's default.
/// </summary>
public class PaymentMethodSetAsDefaultEvent(Guid paymentMethodId, Guid tenantId, Guid userId) : DomainEvent
{
    /// <summary>Local payment method ID.</summary>
    public Guid PaymentMethodId { get; } = paymentMethodId;

    /// <summary>Owning tenant.</summary>
    public Guid TenantId { get; } = tenantId;

    /// <summary>Owning user.</summary>
    public Guid UserId { get; } = userId;
}

/// <summary>
///     Domain event raised when a payment method's expiry month passes and it is auto-expired.
/// </summary>
public class PaymentMethodExpiredEvent(Guid paymentMethodId, Guid tenantId, Guid userId) : DomainEvent
{
    /// <summary>Local payment method ID.</summary>
    public Guid PaymentMethodId { get; } = paymentMethodId;

    /// <summary>Owning tenant.</summary>
    public Guid TenantId { get; } = tenantId;

    /// <summary>Owning user.</summary>
    public Guid UserId { get; } = userId;
}
