using GameGuild.Commerce;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Payment-method registry management (issue #414): tokenized registration, display
///     updates, single-default invariant with subscription re-pointing, and a fail-closed
///     delete guard while billable subscriptions still reference a method.
/// </summary>
public class PaymentMethodRegistryService(
    IPaymentMethodRepository repository,
    IStripeCustomerService stripeCustomerService,
    ISubscriptionPaymentMethodCoordination subscriptionCoordination,
    IApplicationDbContext context,
    ILogger<PaymentMethodRegistryService> logger) : IPaymentMethodService
{
    private const string GatewayProviderStripe = "stripe";

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PaymentMethodDto>>> ListAsync(
        PaymentMethodActor actor,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateActor(actor, out var actorError))
        {
            return Result.Failure<IReadOnlyList<PaymentMethodDto>>(actorError);
        }

        var scopeToOwner = !actor.IsSystemAdmin;
        var methods = await repository
            .ListAsync(tenantId, scopeToOwner ? actor.UserId : null, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<PaymentMethodDto>>(methods.Select(ToDto).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<PaymentMethodDto>> GetByIdAsync(
        PaymentMethodActor actor,
        Guid paymentMethodId,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateActor(actor, out var actorError))
        {
            return Result.Failure<PaymentMethodDto>(actorError);
        }

        var paymentMethod = await repository.GetByIdAsync(paymentMethodId, cancellationToken).ConfigureAwait(false);
        if (paymentMethod is null)
        {
            return NotFound(paymentMethodId);
        }

        if (!CanAccess(actor, paymentMethod, out var accessError))
        {
            return Result.Failure<PaymentMethodDto>(accessError);
        }

        return Result.Success(ToDto(paymentMethod));
    }

    /// <inheritdoc />
    public async Task<Result<PaymentMethodDto>> CreateAsync(
        PaymentMethodActor actor,
        CreatePaymentMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateActor(actor, out var actorError))
        {
            return Result.Failure<PaymentMethodDto>(actorError);
        }

        if (request.TenantId == Guid.Empty)
        {
            return Result.Failure<PaymentMethodDto>(Error.Validation("PaymentMethods.TenantRequired", "TenantId cannot be empty."));
        }

        if (!actor.IsSystemAdmin && actor.TenantId != request.TenantId)
        {
            return Result.Failure<PaymentMethodDto>(Error.Forbidden("PaymentMethods.CrossTenant", "Cross-tenant payment method registration is denied."));
        }

        if (!StripePaymentMethodIdentifier.IsValid(request.GatewayPaymentMethodId))
        {
            return Result.Failure<PaymentMethodDto>(Error.Validation("PaymentMethods.InvalidToken", StripePaymentMethodIdentifier.ValidationMessage));
        }

        var gatewayDetails = await ResolveGatewayDetailsAsync(request.GatewayPaymentMethodId, cancellationToken).ConfigureAwait(false);
        if (gatewayDetails is null)
        {
            return Result.Failure<PaymentMethodDto>(Error.Validation(
                "PaymentMethods.GatewayLookupFailed",
                "The payment method could not be verified at the gateway. Confirm the payment method token and retry."));
        }

        var ownerUserId = actor.IsSystemAdmin ? request.OwnerUserId ?? Guid.Empty : actor.UserId;
        if (ownerUserId == Guid.Empty)
        {
            return Result.Failure<PaymentMethodDto>(Error.Validation(
                "PaymentMethods.OwnerRequired",
                "OwnerUserId is required when a system administrator registers a payment method."));
        }

        return await UpsertCoreAsync(
            actor,
            request.TenantId,
            ownerUserId,
            request.GatewayPaymentMethodId,
            gatewayDetails,
            request.SetAsDefault,
            request.Nickname,
            request.BillingAddress,
            subscriptionId: null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<PaymentMethodDto>> UpsertFromCheckoutAsync(
        PaymentMethodActor actor,
        Guid tenantId,
        Guid subscriptionId,
        string gatewayPaymentMethodId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || subscriptionId == Guid.Empty)
        {
            return Result.Failure<PaymentMethodDto>(Error.Validation("PaymentMethods.InvalidCheckoutContext", "TenantId and SubscriptionId are required to register a checkout payment method."));
        }

        if (!StripePaymentMethodIdentifier.IsValid(gatewayPaymentMethodId))
        {
            return Result.Failure<PaymentMethodDto>(Error.Validation("PaymentMethods.InvalidToken", StripePaymentMethodIdentifier.ValidationMessage));
        }

        var ownerUserId = actor.UserId;
        if (ownerUserId == Guid.Empty)
        {
            // Service actors completing checkouts on behalf of a tenant: attribute the
            // method to the subscription's billing owner.
            ownerUserId = await subscriptionCoordination
                .GetSubscriptionBillingUserIdAsync(subscriptionId, cancellationToken)
                .ConfigureAwait(false)
                ?? Guid.Empty;
        }

        if (ownerUserId == Guid.Empty)
        {
            return Result.Failure<PaymentMethodDto>(Error.Problem(
                "PaymentMethods.OwnerUnresolved",
                "No billing owner could be resolved for the checkout payment method."));
        }

        var gatewayDetails = await ResolveGatewayDetailsAsync(gatewayPaymentMethodId, cancellationToken).ConfigureAwait(false);
        if (gatewayDetails is null)
        {
            // Checkout already succeeded at the gateway; a metadata lookup failure must not
            // fail the charge. Register the reference without display metadata.
            gatewayDetails = new GatewayPaymentMethodDetails("card", null, null, null, null, null, null, null, null);
            logger.LogWarning(
                "Gateway details unavailable for payment method during checkout upsert; registered without display metadata (Subscription {SubscriptionId}).",
                subscriptionId);
        }

        return await UpsertCoreAsync(
            actor,
            tenantId,
            ownerUserId,
            gatewayPaymentMethodId,
            gatewayDetails,
            setAsDefault: true,
            nickname: null,
            billingAddress: null,
            subscriptionId,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<PaymentMethodDto>> UpdateAsync(
        PaymentMethodActor actor,
        Guid paymentMethodId,
        UpdatePaymentMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateActor(actor, out var actorError))
        {
            return Result.Failure<PaymentMethodDto>(actorError);
        }

        var paymentMethod = await repository.GetByIdAsync(paymentMethodId, cancellationToken).ConfigureAwait(false);
        if (paymentMethod is null)
        {
            return NotFound(paymentMethodId);
        }

        if (!CanAccess(actor, paymentMethod, out var accessError))
        {
            return Result.Failure<PaymentMethodDto>(accessError);
        }

        paymentMethod.UpdateDisplay(request.Nickname, request.BillingAddress);
        await repository.UpdateAsync(paymentMethod, cancellationToken).ConfigureAwait(false);

        return Result.Success(ToDto(paymentMethod));
    }

    /// <inheritdoc />
    public async Task<Result<PaymentMethodDto>> SetDefaultAsync(
        PaymentMethodActor actor,
        Guid paymentMethodId,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateActor(actor, out var actorError))
        {
            return Result.Failure<PaymentMethodDto>(actorError);
        }

        var paymentMethod = await repository.GetByIdAsync(paymentMethodId, cancellationToken).ConfigureAwait(false);
        if (paymentMethod is null)
        {
            return NotFound(paymentMethodId);
        }

        if (!CanAccess(actor, paymentMethod, out var accessError))
        {
            return Result.Failure<PaymentMethodDto>(accessError);
        }

        if (paymentMethod.Status != PaymentMethodStatus.Active)
        {
            return Result.Failure<PaymentMethodDto>(Error.Conflict(
                "PaymentMethods.NotUsable",
                $"Payment method {paymentMethodId} is {paymentMethod.Status.ToString().ToLowerInvariant()} and cannot be the default."));
        }

        var tenantId = paymentMethod.TenantId ?? Guid.Empty;
        var previousDefault = paymentMethod.IsDefault
            ? null
            : await repository.GetDefaultAsync(tenantId, paymentMethod.UserId, cancellationToken).ConfigureAwait(false);

        await using var transaction = await context.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (previousDefault is not null)
            {
                previousDefault.ClearDefault();
                await repository.UpdateAsync(previousDefault, cancellationToken).ConfigureAwait(false);
            }

            paymentMethod.MarkAsDefault();
            await repository.UpdateAsync(paymentMethod, cancellationToken).ConfigureAwait(false);

            // Changing the default re-points the subscriptions billed with the old default.
            if (previousDefault is not null)
            {
                var rePointed = await subscriptionCoordination
                    .RepointSubscriptionsAsync(tenantId, previousDefault.Id, paymentMethod.Id, cancellationToken)
                    .ConfigureAwait(false);
                if (rePointed > 0)
                {
                    logger.LogInformation(
                        "Default payment method changed for tenant {TenantId}; {RePointed} subscription(s) re-pointed from {FromId} to {ToId}.",
                        tenantId, rePointed, previousDefault.Id, paymentMethod.Id);
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return Result.Success(ToDto(paymentMethod));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(
        PaymentMethodActor actor,
        Guid paymentMethodId,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateActor(actor, out var actorError))
        {
            return Result.Failure(actorError);
        }

        var paymentMethod = await repository.GetByIdAsync(paymentMethodId, cancellationToken).ConfigureAwait(false);
        if (paymentMethod is null)
        {
            return Result.Failure(NotFoundError(paymentMethodId));
        }

        if (!CanAccess(actor, paymentMethod, out var accessError))
        {
            return Result.Failure(accessError);
        }

        var tenantId = paymentMethod.TenantId ?? Guid.Empty;
        var billableSubscriptions = await subscriptionCoordination
            .CountBillableSubscriptionsOnPaymentMethodAsync(tenantId, paymentMethod.Id, cancellationToken)
            .ConfigureAwait(false);

        if (billableSubscriptions > 0)
        {
            // Fail closed: subscriptions must be migrated to another method first.
            return Result.Failure(Error.Conflict(
                "PaymentMethods.InUseBySubscriptions",
                $"Payment method {paymentMethodId} is the billing method for {billableSubscriptions} active subscription(s). " +
                "Set another payment method as the default (which re-points subscriptions) before removing this one."));
        }

        paymentMethod.SoftDelete();
        await repository.UpdateAsync(paymentMethod, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Payment method {PaymentMethodId} soft-deleted for tenant {TenantId}.",
            paymentMethodId, tenantId);

        return Result.Success();
    }

    private async Task<Result<PaymentMethodDto>> UpsertCoreAsync(
        PaymentMethodActor actor,
        Guid tenantId,
        Guid ownerUserId,
        string gatewayPaymentMethodId,
        GatewayPaymentMethodDetails gatewayDetails,
        bool setAsDefault,
        string? nickname,
        string? billingAddress,
        Guid? subscriptionId,
        CancellationToken cancellationToken)
    {
        var type = MapGatewayType(gatewayDetails.Type);

        var existing = await repository
            .GetByGatewayIdAsync(tenantId, GatewayProviderStripe, gatewayPaymentMethodId, cancellationToken)
            .ConfigureAwait(false);

        PaymentMethod? deleted = null;
        if (existing is null)
        {
            deleted = await repository
                .GetDeletedByGatewayIdAsync(tenantId, GatewayProviderStripe, gatewayPaymentMethodId, cancellationToken)
                .ConfigureAwait(false);
        }

        var firstForOwner = existing is null
                            && deleted is null
                            && await repository.GetDefaultAsync(tenantId, ownerUserId, cancellationToken).ConfigureAwait(false) is null;

        var shouldBeDefault = setAsDefault || firstForOwner;

        await using var transaction = await context.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PaymentMethod paymentMethod;
            if (existing is not null)
            {
                paymentMethod = existing;
                paymentMethod.UpdateGatewayMetadata(type, gatewayDetails.CardBrand, gatewayDetails.CardLast4, gatewayDetails.CardExpMonth, gatewayDetails.CardExpYear, gatewayDetails.CardFingerprint);
            }
            else if (deleted is not null)
            {
                // Re-registering a previously removed token restores its row (history preserved).
                paymentMethod = deleted;
                paymentMethod.Restore();
                paymentMethod.UpdateGatewayMetadata(type, gatewayDetails.CardBrand, gatewayDetails.CardLast4, gatewayDetails.CardExpMonth, gatewayDetails.CardExpYear, gatewayDetails.CardFingerprint);
            }
            else
            {
                paymentMethod = new PaymentMethod(tenantId, ownerUserId, GatewayProviderStripe, gatewayPaymentMethodId, type, isDefault: false);
                paymentMethod.UpdateGatewayMetadata(type, gatewayDetails.CardBrand, gatewayDetails.CardLast4, gatewayDetails.CardExpMonth, gatewayDetails.CardExpYear, gatewayDetails.CardFingerprint);
                paymentMethod.UpdateDisplay(nickname, billingAddress);
                await repository.AddAsync(paymentMethod, cancellationToken).ConfigureAwait(false);
            }

            if (shouldBeDefault && !paymentMethod.IsDefault)
            {
                var previousDefault = await repository
                    .GetDefaultAsync(tenantId, ownerUserId, cancellationToken)
                    .ConfigureAwait(false);

                if (previousDefault is not null && previousDefault.Id != paymentMethod.Id)
                {
                    previousDefault.ClearDefault();
                    await repository.UpdateAsync(previousDefault, cancellationToken).ConfigureAwait(false);

                    var rePointed = await subscriptionCoordination
                        .RepointSubscriptionsAsync(tenantId, previousDefault.Id, paymentMethod.Id, cancellationToken)
                        .ConfigureAwait(false);
                    if (rePointed > 0)
                    {
                        logger.LogInformation(
                            "Default payment method registered for tenant {TenantId}; {RePointed} subscription(s) re-pointed.",
                            tenantId, rePointed);
                    }
                }

                paymentMethod.MarkAsDefault();
            }

            if (subscriptionId.HasValue)
            {
                paymentMethod.MarkUsed(SystemClock.UtcNow);
                await subscriptionCoordination
                    .AssignSubscriptionPaymentMethodAsync(subscriptionId.Value, paymentMethod.Id, cancellationToken)
                    .ConfigureAwait(false);
            }

            await repository.UpdateAsync(paymentMethod, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success(ToDto(paymentMethod));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<GatewayPaymentMethodDetails?> ResolveGatewayDetailsAsync(string gatewayPaymentMethodId, CancellationToken cancellationToken)
    {
        var result = await stripeCustomerService
            .GetPaymentMethodAsync(gatewayPaymentMethodId, cancellationToken)
            .ConfigureAwait(false);

        if (result is null || !result.Success || result.Details is null)
        {
            logger.LogWarning(
                "Gateway payment method lookup failed for {PaymentMethodId}: {ErrorCode}",
                LogRedaction.RedactId(gatewayPaymentMethodId, "pm"),
                result?.ErrorCode);
            return null;
        }

        return result.Details;
    }

    private static PaymentMethodType MapGatewayType(string gatewayType) => gatewayType?.Trim().ToLowerInvariant() switch
    {
        "card" => PaymentMethodType.Card,
        "us_bank_account" or "bank_account" or "sepa_debit" => PaymentMethodType.BankAccount,
        "paypal" or "apple_pay" or "google_pay" or "link" => PaymentMethodType.Wallet,
        _ => PaymentMethodType.Card
    };

    private static bool TryValidateActor(PaymentMethodActor actor, out Error error)
    {
        if (actor.IsSystemAdmin || actor.TenantId.HasValue)
        {
            error = Error.None;
            return true;
        }

        error = Error.Forbidden(
            "PaymentMethods.NoTenantContext",
            "The actor is not associated with any tenant and cannot manage payment methods.");
        return false;
    }

    private static bool CanAccess(PaymentMethodActor actor, PaymentMethod paymentMethod, out Error error)
    {
        // Fail closed: foreign-tenant methods look like 404s (no existence leak); same-tenant
        // non-owner access is an explicit 403.
        if (!actor.IsSystemAdmin && actor.TenantId != paymentMethod.TenantId)
        {
            error = NotFoundError(paymentMethod.Id);
            return false;
        }

        if (!actor.IsSystemAdmin && actor.UserId != paymentMethod.UserId)
        {
            error = Error.Forbidden(
                "PaymentMethods.NotOwner",
                "Only the payment method owner or a billing administrator can manage this payment method.");
            return false;
        }

        error = Error.None;
        return true;
    }

    private static Result<PaymentMethodDto> NotFound(Guid paymentMethodId) =>
        Result.Failure<PaymentMethodDto>(NotFoundError(paymentMethodId));

    private static Error NotFoundError(Guid paymentMethodId) =>
        Error.NotFound("PaymentMethods.NotFound", $"Payment method {paymentMethodId} was not found.");

    private static PaymentMethodDto ToDto(PaymentMethod paymentMethod) => new(
        paymentMethod.Id,
        paymentMethod.TenantId ?? Guid.Empty,
        paymentMethod.UserId,
        paymentMethod.Type.ToString(),
        paymentMethod.GatewayProvider,
        paymentMethod.GatewayPaymentMethodId,
        paymentMethod.IsDefault,
        paymentMethod.Status.ToString(),
        paymentMethod.CardBrand,
        paymentMethod.CardLast4,
        paymentMethod.CardExpMonth,
        paymentMethod.CardExpYear,
        paymentMethod.Nickname,
        paymentMethod.BillingAddress,
        paymentMethod.LastUsedAt,
        paymentMethod.ExpiresAt,
        paymentMethod.GetDaysUntilExpiry(SystemClock.UtcNow),
        paymentMethod.CreatedAt,
        paymentMethod.UpdatedAt);
}
