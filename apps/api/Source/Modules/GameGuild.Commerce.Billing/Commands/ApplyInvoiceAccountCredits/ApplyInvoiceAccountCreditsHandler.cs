using FluentValidation;
using GameGuild.Commerce.Payments;
using GameGuild.Commerce.Subscriptions;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Applies account credits to an invoice (issue #410 v1). Fails closed on: missing
///     idempotency key, invoice not found in the actor's tenant, invoice not owned by the
///     calling user (via the subscription's owner), invoice already paid/void, and
///     currency mismatch between invoice and credits. Idempotent per (tenant,
///     Idempotency-Key): a replay returns the recorded settlement unchanged.
/// </summary>
public sealed class ApplyInvoiceAccountCreditsHandler(
    IApplicationDbContext context,
    IAccountCreditService creditService,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<ApplyInvoiceAccountCreditsCommand, Result<InvoiceAccountCreditSettlementResult>>
{
    private const string SubscriptionInvoiceKind = "subscription";

    /// <inheritdoc />
    public async Task<Result<InvoiceAccountCreditSettlementResult>> Handle(ApplyInvoiceAccountCreditsCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Result.Failure<InvoiceAccountCreditSettlementResult>(
                Error.Validation("InvoiceCredits.IdempotencyKeyRequired", "An Idempotency-Key header is required."));
        }

        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.SubjectIdAsGuid.HasValue || !actor.TenantId.HasValue)
        {
            return Result.Failure<InvoiceAccountCreditSettlementResult>(
                Error.Unauthorized("InvoiceCredits.ActorRequired", "An authenticated user and tenant context is required."));
        }

        var userId = actor.SubjectIdAsGuid.Value;
        var tenantId = actor.TenantId.Value;

        var existingSettlement = await context.Set<AccountCreditInvoiceSettlement>()
            .AsNoTracking()
            .FirstOrDefaultAsync(settlement => settlement.TenantId == tenantId
                                               && settlement.IdempotencyKey == request.IdempotencyKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (existingSettlement is not null)
        {
            var replayed = await context.Set<Invoice>()
                .AsNoTracking()
                .FirstOrDefaultAsync(invoice => invoice.Id == existingSettlement.InvoiceId, cancellationToken)
                .ConfigureAwait(false);

            return Result.Success(new InvoiceAccountCreditSettlementResult(
                existingSettlement.InvoiceId,
                replayed?.InvoiceNumber ?? string.Empty,
                replayed?.Status ?? InvoiceStatus.Open,
                existingSettlement.Amount,
                existingSettlement.Currency,
                replayed is null ? 0 : replayed.Total - replayed.AmountPaid,
                [],
                IdempotentReplay: true));
        }

        var invoice = await context.Set<Invoice>()
            .FirstOrDefaultAsync(inv => inv.Id == request.InvoiceId && inv.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (invoice is null)
        {
            return Result.Failure<InvoiceAccountCreditSettlementResult>(
                Error.NotFound("InvoiceCredits.InvoiceNotFound", "Invoice was not found in your tenant."));
        }

        if (invoice.Status is InvoiceStatus.Paid)
        {
            return Result.Failure<InvoiceAccountCreditSettlementResult>(
                Error.Conflict("InvoiceCredits.AlreadyPaid", "Invoice is already paid."));
        }

        if (invoice.Status is not (InvoiceStatus.Open or InvoiceStatus.PastDue))
        {
            return Result.Failure<InvoiceAccountCreditSettlementResult>(
                Error.Conflict("InvoiceCredits.NotPayable", $"Invoice cannot be settled while in status {invoice.Status}."));
        }

        // Invoice ownership: the invoice's subscription must belong to the calling user.
        var subscription = await context.Set<Subscription>()
            .AsNoTracking()
            .FirstOrDefaultAsync(sub => sub.Id == invoice.SubscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null || subscription.CreatedByUserId != userId)
        {
            return Result.Failure<InvoiceAccountCreditSettlementResult>(
                Error.Forbidden("InvoiceCredits.NotOwned", "Invoice does not belong to the calling user."));
        }

        var outstanding = invoice.Total - invoice.AmountPaid;
        if (outstanding <= 0)
        {
            return Result.Failure<InvoiceAccountCreditSettlementResult>(
                Error.Conflict("InvoiceCredits.NothingOutstanding", "Invoice has no outstanding amount."));
        }

        var target = request.MaxAmount.HasValue ? Math.Min(request.MaxAmount.Value, outstanding) : outstanding;
        if (target <= 0)
        {
            return Result.Failure<InvoiceAccountCreditSettlementResult>(
                Error.Validation("InvoiceCredits.InvalidMaxAmount", "MaxAmount must be positive."));
        }

        // FIFO consumption enqueues its grant mutations and Consume ledger rows without
        // saving; the settlement row and invoice update join the same SaveChanges batch.
        var consumption = await creditService.ConsumeAsync(
            tenantId,
            userId,
            invoice.Currency,
            target,
            invoice.Id,
            SubscriptionInvoiceKind,
            request.IdempotencyKey,
            cancellationToken)
            .ConfigureAwait(false);

        if (consumption.IsFailure)
        {
            return Result.Failure<InvoiceAccountCreditSettlementResult>(consumption.Error);
        }

        var application = consumption.Value;
        try
        {
            invoice.ApplyAccountCredit(application.AmountApplied, SystemClock.UtcNow);
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<InvoiceAccountCreditSettlementResult>(
                Error.Conflict("InvoiceCredits.NotPayable", exception.Message));
        }

        context.Set<AccountCreditInvoiceSettlement>().Add(new AccountCreditInvoiceSettlement(
            tenantId,
            userId,
            invoice.Id,
            application.AmountApplied,
            application.Currency,
            request.IdempotencyKey));

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Lost the idempotency race: return the winner's settlement.
            var winner = await context.Set<AccountCreditInvoiceSettlement>()
                .AsNoTracking()
                .FirstOrDefaultAsync(settlement => settlement.TenantId == tenantId
                                                   && settlement.IdempotencyKey == request.IdempotencyKey,
                    cancellationToken)
                .ConfigureAwait(false);

            if (winner is not null)
            {
                var replayed = await context.Set<Invoice>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(inv => inv.Id == winner.InvoiceId, cancellationToken)
                    .ConfigureAwait(false);

                return Result.Success(new InvoiceAccountCreditSettlementResult(
                    winner.InvoiceId,
                    replayed?.InvoiceNumber ?? string.Empty,
                    replayed?.Status ?? InvoiceStatus.Open,
                    winner.Amount,
                    winner.Currency,
                    replayed is null ? 0 : replayed.Total - replayed.AmountPaid,
                    [],
                    IdempotentReplay: true));
            }

            throw;
        }

        return Result.Success(new InvoiceAccountCreditSettlementResult(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.Status,
            application.AmountApplied,
            application.Currency,
            invoice.Total - invoice.AmountPaid,
            application.ConsumedGrants,
            IdempotentReplay: false));
    }
}

/// <summary>Fail-closed validation of the apply request.</summary>
public sealed class ApplyInvoiceAccountCreditsValidator : AbstractValidator<ApplyInvoiceAccountCreditsCommand>
{
    public ApplyInvoiceAccountCreditsValidator()
    {
        RuleFor(command => command.InvoiceId)
            .NotEqual(Guid.Empty);
        RuleFor(command => command.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(command => command.MaxAmount)
            .Must(amount => amount is null || amount.Value > 0m)
            .WithMessage("MaxAmount must be positive when provided.");
    }
}
