using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Account credit ledger implementation (issue #410 v1). Grants are idempotent per
///     (tenant, idempotency key); consumption drains Active grants FIFO by expiry —
///     earliest <c>ExpiresAt</c> first, non-expiring grants last, then earliest issued —
///     appending one Consume ledger row per touched grant, all inside the single
///     SaveChanges batch shared with the caller's invoice settlement.
/// </summary>
public sealed class AccountCreditService(IApplicationDbContext context) : IAccountCreditService
{
    private const string SubscriptionInvoiceKind = "subscription";
    private const string AddonInvoiceKind = "addon";

    /// <inheritdoc />
    public async Task<Result<AccountCreditGrant>> GrantAsync(AccountCreditGrantRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currency = NormalizeCurrency(request.Currency);
        if (currency is null)
        {
            return Result.Failure<AccountCreditGrant>(
                Error.Validation("AccountCredits.InvalidCurrency", "Currency must be a known ISO 4217 code."));
        }

        if (request.IdempotencyKey is not null)
        {
            var existing = await FindByIdempotencyKeyAsync(request.TenantId, request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                return Result.Success(existing);
            }
        }

        AccountCreditGrant grant;
        try
        {
            grant = new AccountCreditGrant(
                request.TenantId,
                request.UserId,
                request.Amount,
                currency,
                request.Source,
                request.CreditType,
                request.Description,
                request.SourceReferenceId,
                request.ExpiresAt,
                request.IdempotencyKey);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<AccountCreditGrant>(
                Error.Validation("AccountCredits.InvalidGrant", exception.Message));
        }

        context.Set<AccountCreditGrant>().Add(grant);
        context.Set<AccountCreditTransaction>().Add(new AccountCreditTransaction(
            request.TenantId,
            request.UserId,
            grant.Id,
            AccountCreditTransactionType.Grant,
            grant.Amount,
            grant.RemainingAmount,
            grant.Currency,
            grant.Description,
            request.SourceReferenceId,
            request.IdempotencyKey));

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Lost the unique-idempotency race: return the winner.
            if (request.IdempotencyKey is not null)
            {
                var winner = await FindByIdempotencyKeyAsync(request.TenantId, request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
                if (winner is not null)
                {
                    return Result.Success(winner);
                }
            }

            throw;
        }

        AccountCreditMetrics.RecordGrantIssued(currency, request.Source.ToString());
        return Result.Success(grant);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountCreditBalance>> GetBalancesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;
        var balances = await context.Set<AccountCreditGrant>()
            .AsNoTracking()
            .Where(grant => grant.TenantId == tenantId
                            && grant.UserId == userId
                            && grant.Status == AccountCreditGrantStatus.Active
                            && grant.RemainingAmount > 0
                            && (grant.ExpiresAt == null || grant.ExpiresAt > now))
            .GroupBy(grant => grant.Currency)
            .Select(grouping => new AccountCreditBalance(
                grouping.Key,
                grouping.Sum(grant => grant.RemainingAmount),
                grouping.Count(),
                grouping.Min(grant => grant.ExpiresAt)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return balances;
    }

    /// <inheritdoc />
    public async Task<PagedResult<AccountCreditTransaction>> GetHistoryAsync(Guid tenantId, Guid userId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = context.Set<AccountCreditTransaction>()
            .AsNoTracking()
            .Where(transaction => transaction.TenantId == tenantId && transaction.UserId == userId);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<AccountCreditTransaction>(items, total, skip, take);
    }

    /// <inheritdoc />
    public async Task<Result<AccountCreditApplicationResult>> ConsumeAsync(
        Guid tenantId,
        Guid userId,
        string currency,
        decimal amount,
        Guid invoiceId,
        string invoiceKind,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedCurrency = NormalizeCurrency(currency);
        if (normalizedCurrency is null)
        {
            return Result.Failure<AccountCreditApplicationResult>(
                Error.Validation("AccountCredits.InvalidCurrency", "Currency must be a known ISO 4217 code."));
        }

        if (amount <= 0)
        {
            return Result.Failure<AccountCreditApplicationResult>(
                Error.Validation("AccountCredits.InvalidAmount", "Consumption amount must be positive."));
        }

        if (invoiceId == Guid.Empty)
        {
            return Result.Failure<AccountCreditApplicationResult>(
                Error.Validation("AccountCredits.InvalidInvoice", "An invoice reference is required."));
        }

        var now = SystemClock.UtcNow;

        // FIFO by expiry: earliest deadline first, non-expiring last, then earliest issued.
        // (Expression-tree safe: coalesce and member access only, no method calls.)
        var farFuture = DateTime.MaxValue;
        var grants = await context.Set<AccountCreditGrant>()
            .Where(grant => grant.TenantId == tenantId
                            && grant.UserId == userId
                            && grant.Status == AccountCreditGrantStatus.Active
                            && grant.RemainingAmount > 0
                            && grant.Currency == normalizedCurrency
                            && (grant.ExpiresAt == null || grant.ExpiresAt > now))
            .OrderBy(grant => grant.ExpiresAt ?? farFuture)
            .ThenBy(grant => grant.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var eligible = grants.Where(grant => IsEligibleForInvoiceKind(grant.CreditType, invoiceKind)).ToList();

        var available = eligible.Sum(grant => grant.RemainingAmount);
        if (available <= 0)
        {
            return Result.Failure<AccountCreditApplicationResult>(
                Error.NotFound("AccountCredits.NoEligibleCredit", $"No eligible account credit available in {normalizedCurrency}."));
        }

        var applied = Math.Min(amount, available);
        var consumed = new List<AccountCreditConsumption>(eligible.Count);
        var remainingToApply = applied;

        foreach (var grant in eligible)
        {
            if (remainingToApply <= 0)
            {
                break;
            }

            var taken = grant.Consume(remainingToApply, now);
            if (taken <= 0)
            {
                continue;
            }

            remainingToApply -= taken;
            consumed.Add(new AccountCreditConsumption(grant.Id, taken, grant.RemainingAmount));
            context.Set<AccountCreditTransaction>().Add(new AccountCreditTransaction(
                tenantId,
                userId,
                grant.Id,
                AccountCreditTransactionType.Consume,
                taken,
                grant.RemainingAmount,
                grant.Currency,
                $"Applied to invoice {invoiceId}",
                invoiceId,
                idempotencyKey));
        }

        AccountCreditMetrics.RecordApplied(normalizedCurrency, applied);
        return Result.Success(new AccountCreditApplicationResult(
            invoiceId,
            applied,
            normalizedCurrency,
            amount - applied,
            consumed));
    }

    /// <inheritdoc />
    public async Task<Result> RevokeAsync(Guid tenantId, Guid grantId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation("AccountCredits.ReasonRequired", "A revocation reason is required."));
        }

        var grant = await context.Set<AccountCreditGrant>()
            .FirstOrDefaultAsync(g => g.TenantId == tenantId && g.Id == grantId, cancellationToken)
            .ConfigureAwait(false);

        if (grant is null)
        {
            return Result.Failure(Error.NotFound("AccountCredits.GrantNotFound", "Account credit grant was not found."));
        }

        try
        {
            grant.Revoke(reason, SystemClock.UtcNow);
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure(Error.Conflict("AccountCredits.NotRevocable", exception.Message));
        }

        context.Set<AccountCreditTransaction>().Add(new AccountCreditTransaction(
            grant.TenantId,
            grant.UserId,
            grant.Id,
            AccountCreditTransactionType.Revoke,
            grant.Amount,
            0m,
            grant.Currency,
            $"Revoked: {reason}"));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<AccountCreditExpiryResult> ExpireDueAsync(DateTime nowUtc, int batchSize, CancellationToken cancellationToken = default)
    {
        var size = Math.Clamp(batchSize, 1, 1000);
        var expiredGrants = 0;
        var expiredAmount = 0m;

        var dueGrants = await context.Set<AccountCreditGrant>()
            .Where(grant => grant.Status == AccountCreditGrantStatus.Active
                            && grant.ExpiresAt != null
                            && grant.ExpiresAt <= nowUtc)
            .OrderBy(grant => grant.ExpiresAt)
            .Take(size)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var grant in dueGrants)
        {
            var wasted = grant.RemainingAmount;
            grant.MarkExpired(nowUtc);
            expiredGrants++;
            expiredAmount += wasted;
            if (wasted > 0)
            {
                context.Set<AccountCreditTransaction>().Add(new AccountCreditTransaction(
                    grant.TenantId,
                    grant.UserId,
                    grant.Id,
                    AccountCreditTransactionType.Expire,
                    wasted,
                    0m,
                    grant.Currency,
                    "Expired unused"));
            }
        }

        var expiredGifts = 0;
        var dueGifts = await context.Set<GiftSubscription>()
            .Where(gift => (gift.Status == GiftStatus.Purchased || gift.Status == GiftStatus.Delivered)
                           && gift.ExpiresAt <= nowUtc)
            .OrderBy(gift => gift.ExpiresAt)
            .Take(size)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var gift in dueGifts)
        {
            gift.MarkExpired(nowUtc);
            expiredGifts++;
        }

        if (dueGrants.Count > 0 || dueGifts.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        AccountCreditMetrics.RecordExpiredGrants(expiredGrants, expiredAmount);
        AccountCreditMetrics.RecordExpiredGifts(expiredGifts);
        return new AccountCreditExpiryResult(expiredGrants, expiredAmount, expiredGifts);
    }

    /// <summary>Idempotency replay lookup per (tenant, key).</summary>
    private async Task<AccountCreditGrant?> FindByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken cancellationToken)
        => await context.Set<AccountCreditGrant>()
            .FirstOrDefaultAsync(grant => grant.TenantId == tenantId && grant.IdempotencyKey == idempotencyKey, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Credit-type eligibility for the invoice kind being settled.</summary>
    private static bool IsEligibleForInvoiceKind(AccountCreditType creditType, string invoiceKind)
        => creditType switch
        {
            AccountCreditType.General => true,
            AccountCreditType.Subscription => string.Equals(invoiceKind, SubscriptionInvoiceKind, StringComparison.OrdinalIgnoreCase),
            AccountCreditType.Addon => string.Equals(invoiceKind, AddonInvoiceKind, StringComparison.OrdinalIgnoreCase),
            _ => false // fail closed on unknown credit types
        };

    /// <summary>Normalizes an ISO 4217 code; null when unknown.</summary>
    private static string? NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            return null;
        }

        var upper = currency.ToUpperInvariant();
        return Iso4217CurrencyCatalog.Exists(upper) ? upper : null;
    }
}
