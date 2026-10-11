using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Commerce.Subscriptions;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Materializes the local <see cref="Invoice"/> for a subscription billing cycle on confirmed payment.
///     This is the production invoice writer behind both payment-success paths:
///     first-party charges (<c>PaymentSubscriptionSyncService</c>, through the
///     <see cref="ISubscriptionInvoiceMaterializer"/> port) and provider-billed cycles
///     (<c>StripeBillingWebhookService</c> <c>invoice.payment_succeeded</c>).
/// </summary>
/// <remarks>
///     <para>
///         Idempotency: the cycle identity is <c>subscription:{{subscriptionId}}:cycle:{{n}}:invoice</c>.
///         Flow is check-then-create with the unique <c>IX_invoices_IdempotencyKey</c> index as the
///         concurrency backstop (a racing insert loses, re-fetches the winner and returns it).
///     </para>
///     <para>
///         Because the payment is already confirmed when this runs, the invoice is issued
///         (<see cref="Invoice.Issue"/>) and the payment recorded (<see cref="Invoice.RecordPayment"/>)
///         in the same flow. The subscription invoices read model is a SQL projection of the
///         invoices table, so persisting the invoice row IS the read-model projection.
///     </para>
/// </remarks>
public sealed class InvoiceGenerationService(
    IApplicationDbContext context,
    ISubscriptionRepository subscriptionRepository,
    IUserRepository userRepository,
    INotificationService notificationService,
    ILogger<InvoiceGenerationService> logger) : IInvoiceGenerationService
{
    private static readonly JsonSerializerOptions MetadataOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    ///     Builds the idempotency key that identifies the invoice of one subscription billing cycle.
    /// </summary>
    public static string BuildIdempotencyKey(Guid subscriptionId, int billingCycleNumber)
        => $"subscription:{subscriptionId}:cycle:{billingCycleNumber}:invoice";

    /// <summary>
    ///     Derives the stable payment identifier for a provider-billed cycle. Provider-billed cycles have no
    ///     local Payment row (the provider invoice is the payment receipt), so the invoices.PaymentId slot
    ///     stores a deterministic id derived from the provider invoice id: stable across replays, unique per
    ///     provider invoice, and never colliding with local payment ids (which are random Guids).
    /// </summary>
    public static Guid DeriveProviderPaymentId(string providerInvoiceId)
    {
        if (string.IsNullOrWhiteSpace(providerInvoiceId))
        {
            throw new ArgumentException("Provider invoice id is required to derive a payment id", nameof(providerInvoiceId));
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"gameguild:provider-invoice:{providerInvoiceId}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    /// <inheritdoc />
    public async Task<InvoiceGenerationResult> MaterializeForConfirmedCycleAsync(
        ConfirmedCycleInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.BillingCycleNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Billing cycle number must be 1-based");
        }

        var subscription = await subscriptionRepository
            .GetByIdAsync(request.SubscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            throw new InvalidOperationException(
                $"Cannot materialize invoice for subscription {request.SubscriptionId}: subscription not found");
        }

        var tenantId = ((ISubscription)subscription).TenantId;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Cannot materialize invoice for subscription {request.SubscriptionId}: tenant is missing");
        }

        var idempotencyKey = BuildIdempotencyKey(request.SubscriptionId, request.BillingCycleNumber);

        var existing = await FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            var enriched = await EnrichExistingAsync(existing, request, cancellationToken).ConfigureAwait(false);
            if (enriched)
            {
                logger.LogInformation(
                    "Invoice {InvoiceId} for subscription {SubscriptionId} cycle {Cycle} enriched on idempotent replay",
                    existing.Id,
                    request.SubscriptionId,
                    request.BillingCycleNumber);
            }

            return new InvoiceGenerationResult(existing, Created: false);
        }

        var invoice = new Invoice(
            tenantId,
            request.SubscriptionId,
            request.Amount,
            request.Currency,
            idempotencyKey);

        invoice.SetBillingPeriod(subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd);
        invoice.SetDescription($"Subscription billing cycle {request.BillingCycleNumber}");
        invoice.SetMetadata(JsonSerializer.Serialize(new
        {
            subscriptionId = request.SubscriptionId,
            billingCycleNumber = request.BillingCycleNumber,
            generatedAtUtc = SystemClock.UtcNow,
            source = request.PaymentId.HasValue ? "first-party-payment" : "provider-billing"
        }, MetadataOptions));

        if (!string.IsNullOrWhiteSpace(request.ProviderInvoiceId))
        {
            invoice.SetExternalId(request.ProviderInvoiceId);
        }

        // Payment is already confirmed: issue immediately with the confirmation date as the due date,
        // then record the payment so the invoice lands in Paid status.
        invoice.Issue(request.ProcessedAtUtc);

        var paymentId = ResolvePaymentId(request);
        if (paymentId.HasValue)
        {
            invoice.RecordPayment(paymentId.Value, request.Amount, request.ProcessedAtUtc);
        }

        context.Set<Invoice>().Add(invoice);
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            // Concurrent materialization of the same cycle lost the race: detach and return the winner.
            context.Set<Invoice>().Remove(invoice);
            var winner = await FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken).ConfigureAwait(false);
            if (winner is null)
            {
                throw;
            }

            await EnrichExistingAsync(winner, request, cancellationToken).ConfigureAwait(false);
            return new InvoiceGenerationResult(winner, Created: false);
        }

        logger.LogInformation(
            "Invoice {InvoiceNumber} materialized for subscription {SubscriptionId} cycle {Cycle} ({Amount} {Currency})",
            invoice.InvoiceNumber,
            request.SubscriptionId,
            request.BillingCycleNumber,
            request.Amount,
            request.Currency);

        await DispatchInvoiceIssuedEmailAsync(invoice, subscription, request, cancellationToken).ConfigureAwait(false);

        return new InvoiceGenerationResult(invoice, Created: true);
    }

    private Task<Invoice?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
        => context.Set<Invoice>()
            .AsNoTracking()
            .FirstOrDefaultAsync(invoice => invoice.IdempotencyKey == idempotencyKey, cancellationToken);

    private static Guid? ResolvePaymentId(ConfirmedCycleInvoiceRequest request)
        => request.PaymentId
           ?? (string.IsNullOrWhiteSpace(request.ProviderInvoiceId)
               ? null
               : DeriveProviderPaymentId(request.ProviderInvoiceId));

    /// <summary>
    ///     Late enrichment of an already-materialized invoice on idempotent replay: stamp a provider
    ///     external id that arrived after creation, and record the payment when the first attempt
    ///     could not (no local payment id and no provider invoice id yet).
    /// </summary>
    private async Task<bool> EnrichExistingAsync(
        Invoice existing,
        ConfirmedCycleInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var changed = false;

        if (!string.IsNullOrWhiteSpace(request.ProviderInvoiceId) &&
            string.IsNullOrEmpty(existing.ExternalId))
        {
            existing.SetExternalId(request.ProviderInvoiceId);
            changed = true;
        }

        var paymentId = ResolvePaymentId(request);
        if (paymentId.HasValue &&
            existing.Status == InvoiceStatus.Open &&
            existing.PaymentId is null)
        {
            existing.RecordPayment(paymentId.Value, request.Amount, request.ProcessedAtUtc);
            changed = true;
        }

        if (changed)
        {
            context.Set<Invoice>().Update(existing);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return changed;
    }

    /// <summary>
    ///     Queues the invoice-issued email through the Notifications module (the same durable
    ///     queue + renderer pipeline the monthly statement email uses). Best effort: the invoice
    ///     row is already persisted, so a dispatch failure is logged, never propagated.
    /// </summary>
    private async Task DispatchInvoiceIssuedEmailAsync(
        Invoice invoice,
        Subscription subscription,
        ConfirmedCycleInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var recipient = await userRepository
                .GetByIdAsync(subscription.CreatedByUserId, cancellationToken)
                .ConfigureAwait(false);

            if (recipient is null || !recipient.IsActive || recipient.IsSuspended || string.IsNullOrWhiteSpace(recipient.Email))
            {
                logger.LogInformation(
                    "Invoice {InvoiceId} email skipped: recipient user {UserId} is unavailable or cannot receive mail",
                    invoice.Id,
                    subscription.CreatedByUserId);
                return;
            }

            var metadata = JsonSerializer.Serialize(new InvoiceIssuedEmailMetadata(
                TenantId: ((ISubscription)subscription).TenantId,
                SubscriptionId: subscription.Id,
                UserId: recipient.Id,
                InvoiceId: invoice.Id,
                InvoiceNumber: invoice.InvoiceNumber,
                BillingCycleNumber: request.BillingCycleNumber,
                Amount: request.Amount,
                Currency: request.Currency,
                PeriodStart: invoice.PeriodStart,
                PeriodEnd: invoice.PeriodEnd,
                RecipientEmail: recipient.Email,
                RecipientName: recipient.Name), MetadataOptions);

            var result = await notificationService
                .SendAsync(
                    recipientId: recipient.Id,
                    type: NotificationType.InvoiceIssued,
                    title: $"Invoice {invoice.InvoiceNumber} issued",
                    message:
                        $"Invoice {invoice.InvoiceNumber} for subscription billing cycle {request.BillingCycleNumber} " +
                        $"({request.Amount} {request.Currency}) has been issued and paid. A PDF copy is attached to this email.",
                    channel: NotificationChannel.Email,
                    tenantId: ((ISubscription)subscription).TenantId,
                    priority: GameGuild.Notifications.NotificationPriority.Normal,
                    referenceEntityId: invoice.Id,
                    referenceEntityType: nameof(Invoice),
                    metadata: metadata,
                    recipientEmail: recipient.Email,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (result.IsSuccess)
            {
                logger.LogInformation(
                    "Invoice {InvoiceId} issued email queued for user {UserId}",
                    invoice.Id,
                    recipient.Id);
            }
            else
            {
                logger.LogInformation(
                    "Invoice {InvoiceId} issued email not queued: {Reason}",
                    invoice.Id,
                    result.Error?.Description ?? "preference decision");
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Invoice {InvoiceId} issued email dispatch failed; the invoice itself is persisted",
                invoice.Id);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is DbException { SqlState: "23505" })
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
///     Metadata contract carried on the InvoiceIssued notification row. The dispatcher writes this JSON
///     into <see cref="Notification.Metadata"/>; <c>InvoiceIssuedEmailRenderer</c> reads it back at send time.
/// </summary>
public sealed record InvoiceIssuedEmailMetadata(
    Guid TenantId,
    Guid SubscriptionId,
    Guid UserId,
    Guid InvoiceId,
    string InvoiceNumber,
    int BillingCycleNumber,
    decimal Amount,
    string Currency,
    DateTime? PeriodStart,
    DateTime? PeriodEnd,
    string RecipientEmail,
    string? RecipientName);
