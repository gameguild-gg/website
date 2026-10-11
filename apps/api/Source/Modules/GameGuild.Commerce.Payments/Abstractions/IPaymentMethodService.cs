namespace GameGuild.Commerce.Payments;

/// <summary>
///     Self-service management of the tenant-scoped payment-method registry (issue #414).
///     All operations are scoped by the acting identity resolved from the request context.
/// </summary>
public interface IPaymentMethodService
{
    /// <summary>
    ///     Lists live payment methods. Non-admin actors see their own; system admins see the
    ///     whole tenant.
    /// </summary>
    Task<Result<IReadOnlyList<PaymentMethodDto>>> ListAsync(
        PaymentMethodActor actor,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads a single payment method (tenant-scoped; owners and system admins only).
    /// </summary>
    Task<Result<PaymentMethodDto>> GetByIdAsync(
        PaymentMethodActor actor,
        Guid paymentMethodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Registers a payment method from a tokenized gateway result (Stripe <c>pm_</c> id
    ///     from Stripe.js/Elements or a confirmed SetupIntent). Raw card data is never accepted.
    /// </summary>
    Task<Result<PaymentMethodDto>> CreateAsync(
        PaymentMethodActor actor,
        CreatePaymentMethodRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates display-only fields (nickname, billing address).
    /// </summary>
    Task<Result<PaymentMethodDto>> UpdateAsync(
        PaymentMethodActor actor,
        Guid paymentMethodId,
        UpdatePaymentMethodRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Makes this method the actor's default inside the tenant (single-default invariant;
    ///     subscriptions billed with the previous default are re-pointed).
    /// </summary>
    Task<Result<PaymentMethodDto>> SetDefaultAsync(
        PaymentMethodActor actor,
        Guid paymentMethodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Soft-deletes a payment method. Fails closed with a conflict while any billable
    ///     subscription still points at it.
    /// </summary>
    Task<Result> DeleteAsync(
        PaymentMethodActor actor,
        Guid paymentMethodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Upserts the local registry row for a gateway payment method confirmed during a
    ///     subscription checkout (integration path; keeps Stripe behavior unchanged).
    /// </summary>
    Task<Result<PaymentMethodDto>> UpsertFromCheckoutAsync(
        PaymentMethodActor actor,
        Guid tenantId,
        Guid subscriptionId,
        string gatewayPaymentMethodId,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Acting identity for payment-method self-service (resolved from the request context,
///     never from the request body). <see cref="TenantId"/> is null for system admins, who
///     operate across tenants.
/// </summary>
public sealed record PaymentMethodActor(Guid? TenantId, Guid UserId, bool IsSystemAdmin)
{
    /// <summary>
    ///     Actor used by trusted server-side flows (checkout completion) before a concrete
    ///     owner is resolved.
    /// </summary>
    public static PaymentMethodActor System(Guid userId) => new(TenantId: null, userId, IsSystemAdmin: true);
}

/// <summary>
///     Request to register a payment method from a gateway token.
/// </summary>
public sealed record CreatePaymentMethodRequest(
    Guid TenantId,
    string GatewayPaymentMethodId,
    bool SetAsDefault = false,
    string? Nickname = null,
    string? BillingAddress = null,
    Guid? OwnerUserId = null);

/// <summary>
///     Request to update display-only payment-method fields.
/// </summary>
public sealed record UpdatePaymentMethodRequest(string? Nickname = null, string? BillingAddress = null);

/// <summary>
///     Display-safe projection of a registered payment method.
/// </summary>
public sealed record PaymentMethodDto(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    string Type,
    string GatewayProvider,
    string GatewayPaymentMethodId,
    bool IsDefault,
    string Status,
    string? CardBrand,
    string? CardLast4,
    int? CardExpMonth,
    int? CardExpYear,
    string? Nickname,
    string? BillingAddress,
    DateTime? LastUsedAt,
    DateTime? ExpiresAt,
    int? ExpiresInDays,
    DateTime CreatedAt,
    DateTime UpdatedAt);
