using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using GameGuild.CQRS.Models;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Represents a subscription linking a tenant to a subscription plan
/// </summary>
[Table("Subscriptions")]
[Index(nameof(TenantId), nameof(Status))]
[Index(nameof(PlanId))]
[Index(nameof(NextBillingDate))]
[Index(nameof(Status))]
[Index(nameof(ExternalId))]
[Index(nameof(ExternalCustomerId))]
[Index(nameof(LastPaymentAt))]
[Index(nameof(TrialEndDate))]
[Index(nameof(CancelledAt))]
public class Subscription : StatefulEntity<SubscriptionStatus>, ISubscription
{
    /// <summary>
    ///     Valid state transitions for subscriptions (monotonic state machine)
    /// </summary>
    protected override IReadOnlyDictionary<SubscriptionStatus, IReadOnlySet<SubscriptionStatus>> ValidTransitions { get; } =
        new Dictionary<SubscriptionStatus, IReadOnlySet<SubscriptionStatus>>
        {
            { SubscriptionStatus.PendingActivation, new HashSet<SubscriptionStatus> { SubscriptionStatus.Active, SubscriptionStatus.Trialing, SubscriptionStatus.Cancelled } },
            { SubscriptionStatus.Trialing, new HashSet<SubscriptionStatus> { SubscriptionStatus.Active, SubscriptionStatus.Cancelled, SubscriptionStatus.Expired } },
            { SubscriptionStatus.Active, new HashSet<SubscriptionStatus> { SubscriptionStatus.PastDue, SubscriptionStatus.Suspended, SubscriptionStatus.Cancelled } },
            { SubscriptionStatus.PastDue, new HashSet<SubscriptionStatus> { SubscriptionStatus.Active, SubscriptionStatus.Suspended, SubscriptionStatus.Cancelled } },
            { SubscriptionStatus.Suspended, new HashSet<SubscriptionStatus> { SubscriptionStatus.Active, SubscriptionStatus.Cancelled } },
            { SubscriptionStatus.Cancelled, new HashSet<SubscriptionStatus>() }, // Terminal state
            { SubscriptionStatus.Expired, new HashSet<SubscriptionStatus>() } // Terminal state
        };

    /// <summary>
    ///     Parameterless constructor for EF Core
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "EF Core constructor - cannot be tested directly")]
    private Subscription()
    {
        // EF Core will populate properties via reflection
    }

    /// <summary>
    ///     Creates a new subscription (TenantId required - fail-closed)
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when tenantId is empty</exception>
    // ReSharper disable once VirtualMemberCallInConstructor - Setting TenantId is safe as it's a simple property setter
    public Subscription(Guid tenantId, Guid planId, Guid createdByUserId, BillingCycle billingCycle, Money amount, DateTime startDate, DateTime? trialEndDate = null, Guid? lockedPriceVersionId = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required for financial entities (fail-closed)", nameof(tenantId));

        TenantId = new TenantId(tenantId);
        PlanId = planId;
        CreatedByUserId = createdByUserId;
        BillingCycle = billingCycle;
        Amount = amount;
        StartDate = startDate;
        TrialEndDate = trialEndDate;
        LockedPriceVersionId = lockedPriceVersionId;
        Status = trialEndDate.HasValue ? SubscriptionStatus.Trialing : SubscriptionStatus.PendingActivation;

        (var periodStart, var periodEnd, var nextBilling) = CalculateBillingDates(startDate, billingCycle);
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        NextBillingDate = nextBilling;
    }

    /// <summary>
    ///     Reference to the user who created/manages this subscription
    /// </summary>
    [Required]
    public Guid CreatedByUserId { get; private set; }

    // ═══════════════════════════════════════════════════════════════════════
    // ECONOMIC MODEL ALIGNMENT - Order linkage for audit trail
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     ID of the fulfilled order that created this subscription.
    ///     Required for audit trail - no subscription without payment proof.
    ///     Null only for legacy/migrated subscriptions.
    /// </summary>
    /// <remarks>
    ///     Economic invariant: New subscriptions MUST have a FulfilledOrderId.
    ///     This prevents subscription creation without prior payment.
    /// </remarks>
    public Guid? FulfilledOrderId { get; private set; }

    /// <summary>
    ///     ID of the most recent order that modified this subscription.
    ///     Updated on upgrades, downgrades, renewals.
    /// </summary>
    public Guid? LastModifyingOrderId { get; private set; }

    /// <summary>
    ///     Associates the originating fulfilled order with this subscription.
    ///     Should be called immediately after creation.
    /// </summary>
    /// <param name="orderId">The fulfilled order ID</param>
    /// <exception cref="InvalidOperationException">Thrown if already set to a different order</exception>
    public void SetFulfilledOrderId(Guid orderId)
    {
        if (FulfilledOrderId is Guid existingOrderId && existingOrderId != orderId)
            throw new InvalidOperationException($"Subscription {Id} already linked to order {FulfilledOrderId}");
        
        FulfilledOrderId = orderId;
        LastModifyingOrderId = orderId;
    }

    /// <summary>
    ///     Records that an order modified this subscription (upgrade/downgrade/renewal).
    /// </summary>
    /// <param name="orderId">The modifying order ID</param>
    public void RecordModifyingOrder(Guid orderId)
    {
        LastModifyingOrderId = orderId;
    }

    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Last processed renewal idempotency key (prevents duplicate charges)
    /// </summary>
    [MaxLength(100)]
    public string? LastRenewalIdempotencyKey { get; private set; }

    /// <summary>
    ///     Last payment idempotency key (prevents duplicate payment recording)
    /// </summary>
    [MaxLength(100)]
    public string? LastPaymentIdempotencyKey { get; private set; }

    /// <summary>
    ///     Locked price version ID (ensures subscription uses contracted rate, not current plan price).
    ///     If null, the subscription uses the current plan price on renewal.
    /// </summary>
    /// <remarks>
    ///     This prevents the "price change affecting existing subscriptions" attack scenario
    ///     where an admin price change would unexpectedly affect existing subscribers.
    /// </remarks>
    public Guid? LockedPriceVersionId { get; private set; }

    /// <summary>
    ///     Last processed billing cycle number (prevents out-of-order payment corruption).
    ///     Payments are only accepted if they advance or match this value.
    /// </summary>
    public int LastProcessedBillingCycle { get; private set; }

    /// <summary>
    ///     Trial end date (if applicable)
    /// </summary>
    public DateTime? TrialEndDate { get; private set; }

    /// <summary>
    ///     Reason for cancellation (if cancelled)
    /// </summary>
    public CancellationReason? CancellationReason { get; private set; }

    /// <summary>
    ///     Additional notes about cancellation
    /// </summary>
    [MaxLength(1000)]
    public string? CancellationNote { get; private set; }

    /// <summary>
    ///     When the subscription was cancelled
    /// </summary>
    public DateTime? CancelledAt { get; private set; }

    /// <summary>
    ///     External ID for payment provider integration (Stripe subscription ID, etc.)
    /// </summary>
    [MaxLength(100)]
    public string? ExternalId { get; private set; }

    /// <summary>
    ///     External customer ID for payment provider
    /// </summary>
    [MaxLength(100)]
    public string? ExternalCustomerId { get; private set; }

    /// <summary>
    ///     Local payment-method registry ID (GameGuild.Commerce.Payments, issue #414) used to
    ///     bill this subscription. Plain reference — no cross-module FK; guarded by the
    ///     Payments module's delete rule.
    /// </summary>
    public Guid? PaymentMethodId { get; private set; }

    /// <summary>
    ///     Whether this subscription auto-renews
    /// </summary>
    public bool AutoRenew { get; private set; } = true;

    /// <summary>
    ///     Current billing period start
    /// </summary>
    public DateTime CurrentPeriodStart { get; private set; }

    /// <summary>
    ///     Current billing period end
    /// </summary>
    public DateTime CurrentPeriodEnd { get; private set; }

    /// <summary>
    ///     Number of billing cycles processed
    /// </summary>
    public int BillingCycleCount { get; private set; }

    /// <summary>
    ///     Last successful payment date
    /// </summary>
    public DateTime? LastPaymentAt { get; private set; }

    /// <summary>
    ///     Additional metadata (JSON serialized)
    /// </summary>
    [MaxLength(2000)]
    public string? Metadata { get; private set; }

    /// <summary>
    ///     Row version for optimistic concurrency control.
    ///     Prevents payment processing race conditions (e.g., concurrent renewal and cancellation).
    /// </summary>
    /// <remarks>
    ///     Economic invariant: Concurrent modifications to subscription state
    ///     (payment recording, cancellation, suspension) are detected and handled.
    /// </remarks>
    public byte[]? RowVersion { get; set; }

    // Navigation properties
    public virtual SubscriptionPlan Plan { get; set; } = null!;

    /// <summary>
    ///     Explicit interface implementation to expose TenantId as Guid.
    ///     Throws InvalidOperationException if TenantId is null (fail-closed behavior).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when TenantId is null</exception>
    Guid ISubscription.TenantId { get => TenantId ?? throw new InvalidOperationException("TenantId is required for subscription entities but was null. This indicates a data integrity issue."); }

    /// <summary>
    ///     Current status of the subscription
    /// </summary>
    public override SubscriptionStatus Status { get; protected set; }

    /// <summary>
    ///     Reference to the subscription plan
    /// </summary>
    [Required]
    public Guid PlanId { get; private set; }

    /// <summary>
    ///     Billing cycle frequency
    /// </summary>
    public BillingCycle BillingCycle { get; private set; }

    /// <summary>
    ///     Current subscription amount
    /// </summary>
    // ReSharper disable once RedundantDefaultMemberInitializer - null! required for nullable reference types with private setter
    public Money Amount { get; private set; } = null!;

    /// <summary>
    ///     When the subscription started/became active
    /// </summary>
    public DateTime StartDate { get; }

    /// <summary>
    ///     When the subscription ends (null for active subscriptions)
    /// </summary>
    public DateTime? EndDate { get; private set; }

    /// <summary>
    ///     When the next billing cycle occurs
    /// </summary>
    public DateTime NextBillingDate { get; private set; }

    /// <summary>
    ///     Checks if the subscription is currently active
    /// </summary>
    public bool IsActive { get => Status == SubscriptionStatus.Active; }

    /// <summary>
    ///     Checks if the subscription is in trial
    /// </summary>
    public bool IsTrialing { get => Status == SubscriptionStatus.Trialing; }

    /// <summary>
    ///     Checks if the subscription is cancelled
    /// </summary>
    public bool IsCancelled { get => Status == SubscriptionStatus.Cancelled; }

    /// <summary>
    ///     Gets remaining trial days (if in trial)
    /// </summary>
    public int? GetRemainingTrialDays()
    {
        if (!IsTrialing || !TrialEndDate.HasValue) return null;

        var remaining = (TrialEndDate.Value - SystemClock.UtcNow).Days;

        return Math.Max(0, remaining);
    }

    /// <summary>
    ///     Gets days until next billing
    /// </summary>
    public int GetDaysUntilNextBilling()
    {
        if (!IsActive) return -1;

        return Math.Max(0, (NextBillingDate - SystemClock.UtcNow).Days);
    }

    /// <summary>
    ///     Activates the subscription
    /// </summary>
    public void Activate()
    {
        TransitionTo(SubscriptionStatus.Active);
        Raise(new SubscriptionActivatedEvent(Id, TenantId!.Value));
    }

    /// <summary>
    ///     Starts a trial period (with state machine validation)
    /// </summary>
    public void StartTrial(DateTime trialEndDate)
    {
        TransitionTo(SubscriptionStatus.Trialing);
        TrialEndDate = trialEndDate;

        Raise(new TrialStartedEvent(Id, TenantId!.Value, trialEndDate));
    }

    /// <summary>
    ///     Ends the trial period (with state machine validation)
    /// </summary>
    public void EndTrial(bool convertToPaid)
    {
        if (Status != SubscriptionStatus.Trialing)
            throw new InvalidOperationException("Can only end trial for trialing subscriptions");

        if (convertToPaid)
        {
            TransitionTo(SubscriptionStatus.Active);
            Raise(new SubscriptionActivatedEvent(Id, TenantId!.Value));
        }
        else
        {
            var reason = Subscriptions.CancellationReason.TrialEnded;
            Cancel(reason, "Trial period ended without conversion");
        }

        Raise(new TrialEndedEvent(Id, TenantId!.Value, convertToPaid));
    }

    /// <summary>
    ///     Cancels the subscription (with state machine validation)
    /// </summary>
    public void Cancel(CancellationReason reason, string? note = null, DateTime? effectiveDate = null)
    {
        if (Status == SubscriptionStatus.Cancelled) return; // Idempotent

        var oldStatus = Status;
        TransitionTo(SubscriptionStatus.Cancelled);
        CancellationReason = reason;
        CancellationNote = note;
        CancelledAt = SystemClock.UtcNow;
        EndDate = effectiveDate ?? SystemClock.UtcNow;
        AutoRenew = false;

        Raise(new SubscriptionCancelledEvent(Id, TenantId!.Value, reason, oldStatus));
    }

    /// <summary>
    ///     Suspends the subscription temporarily
    /// </summary>
    public void Suspend(string? reason = null)
    {
        TransitionTo(SubscriptionStatus.Suspended);
        AutoRenew = false;

        if (!string.IsNullOrEmpty(reason)) { Metadata = JsonSerializer.Serialize(new { suspensionReason = reason }); }

        Raise(new SubscriptionSuspendedEvent(Id, TenantId!.Value, reason));
    }

    /// <summary>
    ///     Reactivates a suspended subscription (with state machine validation)
    /// </summary>
    public void Reactivate()
    {
        TransitionTo(SubscriptionStatus.Active);
        AutoRenew = true;
        Metadata = null;

        Raise(new SubscriptionReactivatedEvent(Id, TenantId!.Value));
    }

    /// <summary>
    ///     Updates the subscription plan with proration calculation
    /// </summary>
    /// <param name="newPlanId">The new plan ID</param>
    /// <param name="newAmount">The new amount for the plan</param>
    /// <param name="effectiveDate">When the change takes effect (null = immediate)</param>
    /// <returns>Proration details for billing adjustment</returns>
    public PlanChangeProration ChangePlan(Guid newPlanId, Money newAmount, DateTime? effectiveDate = null)
    {
        if (Status != SubscriptionStatus.Active) throw new InvalidOperationException("Can only change plans for active subscriptions");

        var oldPlanId = PlanId;
        var oldAmount = Amount;

        // Calculate proration for the remaining period
        var proration = CalculateProration(oldAmount, newAmount, effectiveDate ?? SystemClock.UtcNow);

        PlanId = newPlanId;
        Amount = newAmount;

        Raise(new SubscriptionPlanChangedEvent(Id, TenantId!.Value, oldPlanId, newPlanId, oldAmount, newAmount));

        return proration;
    }

    /// <summary>
    ///     Calculates proration for plan changes
    /// </summary>
    private PlanChangeProration CalculateProration(Money oldAmount, Money newAmount, DateTime effectiveDate)
    {
        var totalDaysInPeriod = (CurrentPeriodEnd - CurrentPeriodStart).TotalDays;
        var remainingDays = Math.Max(0, (CurrentPeriodEnd - effectiveDate).TotalDays);

        if (totalDaysInPeriod <= 0 || remainingDays <= 0)
            return new PlanChangeProration(0, 0, 0, effectiveDate);

        var dailyRateOld = oldAmount.Amount / (decimal)totalDaysInPeriod;
        var dailyRateNew = newAmount.Amount / (decimal)totalDaysInPeriod;

        var creditForUnused = dailyRateOld * (decimal)remainingDays;
        var chargeForNew = dailyRateNew * (decimal)remainingDays;
        var netAdjustment = chargeForNew - creditForUnused;

        return new PlanChangeProration(creditForUnused, chargeForNew, netAdjustment, effectiveDate);
    }

    /// <summary>
    ///     Updates the billing cycle
    /// </summary>
    public void ChangeBillingCycle(BillingCycle newBillingCycle, Money newAmount)
    {
        if (Status != SubscriptionStatus.Active) throw new InvalidOperationException("Can only change billing cycle for active subscriptions");

        var oldCycle = BillingCycle;
        var oldAmount = Amount;

        BillingCycle = newBillingCycle;
        Amount = newAmount;

        (_, var periodEnd, var nextBilling) = CalculateBillingDates(CurrentPeriodStart, newBillingCycle);
        CurrentPeriodEnd = periodEnd;
        NextBillingDate = nextBilling;

        Raise(new SubscriptionBillingCycleChangedEvent(Id, TenantId ?? Guid.Empty, oldCycle, newBillingCycle, oldAmount, newAmount));
    }

    /// <summary>
    ///     Returns the amount due for a renewal without mutating paid subscription state.
    /// </summary>
    /// <param name="newAmount">The quoted amount for the new billing period</param>
    /// <param name="idempotencyKey">Unique key for this renewal (e.g., "{subscriptionId}:{billingCycle}:{periodStart}")</param>
    /// <returns>Result indicating success or failure with reason</returns>
    public SubscriptionRenewalResult ProcessRenewal(Money newAmount, string idempotencyKey)
    {
        return PrepareRenewal(idempotencyKey);
    }

    /// <summary>
    ///     Prepares the next renewal quote without advancing billing state or recognizing revenue.
    /// </summary>
    public SubscriptionRenewalResult PrepareRenewal(string idempotencyKey)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
            return SubscriptionRenewalResult.Failed(Id, "Idempotency key is required for renewal processing");

        if (Status != SubscriptionStatus.Active)
            return SubscriptionRenewalResult.Failed(Id, "Subscription is not active");

        if (!AutoRenew)
            return SubscriptionRenewalResult.Failed(Id, "Auto-renewal is disabled");

        return SubscriptionRenewalResult.RequiresPayment(
            Id,
            BillingCycleCount,
            LastProcessedBillingCycle + 1,
            Amount);
    }

    /// <summary>
    ///     Sets external IDs for payment provider integration
    /// </summary>
    public void SetExternalIds(string? subscriptionId, string? customerId)
    {
        ExternalId = subscriptionId;
        ExternalCustomerId = customerId;

        Raise(new SubscriptionExternalIdUpdatedEvent(Id, subscriptionId ?? string.Empty));
    }

    /// <summary>
    ///     Assigns the local payment method that bills this subscription (issue #414).
    /// </summary>
    public void SetPaymentMethod(Guid? paymentMethodId)
    {
        if (paymentMethodId == Guid.Empty)
            throw new ArgumentException("PaymentMethodId must be a non-empty GUID or null to clear.", nameof(paymentMethodId));

        if (PaymentMethodId == paymentMethodId)
        {
            return; // Idempotent
        }

        PaymentMethodId = paymentMethodId;
    }

    /// <summary>
    ///     Locks the subscription to a specific price version.
    ///     This ensures the subscription continues at the contracted rate
    ///     even if the plan price changes.
    /// </summary>
    /// <param name="priceVersionId">The price version ID to lock to</param>
    /// <exception cref="InvalidOperationException">Thrown if subscription is cancelled</exception>
    public void LockToPriceVersion(Guid priceVersionId)
    {
        if (Status == SubscriptionStatus.Cancelled)
            throw new InvalidOperationException("Cannot lock price version for cancelled subscriptions");

        LockedPriceVersionId = priceVersionId;

        Raise(new SubscriptionPriceVersionLockedEvent(Id, TenantId ?? Guid.Empty, priceVersionId));
    }

    /// <summary>
    ///     Unlocks the subscription from its current price version,
    ///     allowing it to use the current plan price on renewal.
    /// </summary>
    public void UnlockPriceVersion()
    {
        if (!LockedPriceVersionId.HasValue)
            return; // Already unlocked

        var oldVersionId = LockedPriceVersionId.Value;
        LockedPriceVersionId = null;

        Raise(new SubscriptionPriceVersionUnlockedEvent(Id, TenantId ?? Guid.Empty, oldVersionId));
    }

    /// <summary>
    ///     Records a successful payment with idempotency key and billing cycle tracking
    ///     (prevents duplicate recording and out-of-order payment corruption)
    /// </summary>
    /// <param name="amount">Payment amount</param>
    /// <param name="currency">Currency code</param>
    /// <param name="paymentDate">When payment was processed</param>
    /// <param name="idempotencyKey">Unique payment key (e.g., external payment ID from provider)</param>
    /// <param name="forBillingCycle">Specific billing cycle this payment is for</param>
    /// <returns>PaymentRecordResult indicating success, already processed, or rejected</returns>
    public PaymentRecordResult RecordPayment(decimal amount, string currency, DateTime paymentDate, string idempotencyKey, int? forBillingCycle = null)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
            throw new ArgumentException("Idempotency key is required for payment recording", nameof(idempotencyKey));

        // ═══════════════════════════════════════════════════════════════════════
        // ECONOMIC INVARIANT: Cannot record payments for cancelled/expired subscriptions
        // This prevents charging users for subscriptions they've already cancelled.
        // ═══════════════════════════════════════════════════════════════════════
        if (Status == SubscriptionStatus.Cancelled)
            return PaymentRecordResult.RejectedCancelled(
                $"Cannot record payment for cancelled subscription {Id}. Refund required.");
        
        if (Status == SubscriptionStatus.Expired)
            return PaymentRecordResult.RejectedCancelled(
                $"Cannot record payment for expired subscription {Id}. Renewal required.");

        if (!forBillingCycle.HasValue)
        {
            return PaymentRecordResult.RejectedOutOfOrder(
                -1,
                LastProcessedBillingCycle,
                $"A specific billing cycle is required; expected cycle {LastProcessedBillingCycle + 1}");
        }

        var requestedCycle = forBillingCycle.Value;
        if (amount != Amount.Amount)
        {
            return PaymentRecordResult.RejectedMoney(
                idempotencyKey,
                requestedCycle,
                LastProcessedBillingCycle,
                $"Payment amount {amount} does not match authoritative amount {Amount.Amount}");
        }

        if (!string.Equals(currency, Amount.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return PaymentRecordResult.RejectedMoney(
                idempotencyKey,
                requestedCycle,
                LastProcessedBillingCycle,
                $"Payment currency {currency} does not match authoritative currency {Amount.Currency}");
        }

        if (requestedCycle == LastProcessedBillingCycle)
        {
            if (LastPaymentIdempotencyKey == idempotencyKey)
                return PaymentRecordResult.AlreadyProcessed(idempotencyKey, LastProcessedBillingCycle);

            return PaymentRecordResult.RejectedOutOfOrder(
                requestedCycle,
                LastProcessedBillingCycle,
                $"Billing cycle {requestedCycle} was already confirmed by a different payment");
        }

        var expectedCycle = LastProcessedBillingCycle + 1;
        if (requestedCycle != expectedCycle)
        {
            return PaymentRecordResult.RejectedOutOfOrder(
                requestedCycle,
                LastProcessedBillingCycle,
                $"Payment for billing cycle {requestedCycle} rejected: expected cycle {expectedCycle}");
        }

        if (requestedCycle > 1)
        {
            (var periodStart, var periodEnd, var nextBilling) = CalculateBillingDates(NextBillingDate, BillingCycle);
            CurrentPeriodStart = periodStart;
            CurrentPeriodEnd = periodEnd;
            NextBillingDate = nextBilling;
            Raise(new SubscriptionRenewedEvent(Id, TenantId ?? Guid.Empty, requestedCycle, Amount));
        }

        LastPaymentAt = paymentDate;
        LastPaymentIdempotencyKey = idempotencyKey;
        LastProcessedBillingCycle = requestedCycle;
        BillingCycleCount = requestedCycle;

        Raise(new SubscriptionPaymentProcessedEvent(Id, TenantId ?? Guid.Empty, Amount.Amount, Amount.Currency, paymentDate));
        return PaymentRecordResult.Success(idempotencyKey, requestedCycle);
    }

    /// <summary>
    ///     Records a payment (backward-compatible overload returning bool)
    /// </summary>
    [Obsolete("Use RecordPayment with PaymentRecordResult return type for better error handling")]
    public bool RecordPaymentLegacy(decimal amount, string currency, DateTime paymentDate, string idempotencyKey)
    {
        var result = RecordPayment(amount, currency, paymentDate, idempotencyKey);
        return result.IsSuccess;
    }

    /// <summary>
    ///     Records a payment failure (uses state machine for transition)
    /// </summary>
    public void RecordPaymentFailure(string reason, DateTime failureDate)
    {
        if (Status == SubscriptionStatus.Active)
        {
            TransitionTo(SubscriptionStatus.PastDue);
        }

        Raise(new SubscriptionPaymentFailedEvent(Id, TenantId ?? Guid.Empty, reason, failureDate));
    }

    /// <summary>
    ///     Updates subscription metadata
    /// </summary>
    public void UpdateMetadata(string metadata)
    {
        if (string.IsNullOrEmpty(metadata)) throw new ArgumentNullException(nameof(metadata));

        if (metadata.Length > 2000) throw new ArgumentException("Metadata cannot exceed 2000 characters");

        Metadata = metadata;
    }

    /// <summary>
    ///     Sets auto-renewal preference
    /// </summary>
    public void SetAutoRenew(bool autoRenew)
    {
        if (Status == SubscriptionStatus.Cancelled) throw new InvalidOperationException("Cannot change auto-renewal for cancelled subscriptions");

        AutoRenew = autoRenew;
    }

    /// <summary>
    ///     Calculates billing dates based on start date and cycle
    /// </summary>
    private static (DateTime periodStart, DateTime periodEnd, DateTime nextBilling) CalculateBillingDates(DateTime startDate, BillingCycle cycle)
    {
        var periodStart = startDate;

        (var periodEnd, var nextBilling) = cycle switch
        {
            BillingCycle.Monthly => (startDate.AddMonths(1).AddDays(-1), startDate.AddMonths(1)),
            BillingCycle.Quarterly => (startDate.AddMonths(3).AddDays(-1), startDate.AddMonths(3)),
            BillingCycle.SemiAnnually => (startDate.AddMonths(6).AddDays(-1), startDate.AddMonths(6)),
            BillingCycle.Annually => (startDate.AddYears(1).AddDays(-1), startDate.AddYears(1)),
            BillingCycle.Biannually => (startDate.AddYears(2).AddDays(-1), startDate.AddYears(2)),
            _ => throw new ArgumentOutOfRangeException(nameof(cycle))
        };

        return (periodStart, periodEnd, nextBilling);
    }
}
