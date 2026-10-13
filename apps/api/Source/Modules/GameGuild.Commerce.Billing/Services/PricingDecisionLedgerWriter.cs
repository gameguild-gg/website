using GameGuild.Commerce;
using GameGuild.Compliance.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Billing;

/// <summary>
/// Append-only writer behind <see cref="IPricingDecisionLedgerWriter"/> (issue #400).
/// Validates the decision, persists the row inside its own transaction (idempotent on
/// the calculation key via the unique index) and publishes the matching Compliance.Audit
/// event so pricing decisions join the central audit trail alongside promo mutations.
/// Audit publishing is best-effort like <c>PromoCodeAuditLogger</c>; the ledger insert
/// itself is fail-closed — an invalid or duplicate-keyed decision never silently skips.
/// </summary>
public sealed class PricingDecisionLedgerWriter(
    IApplicationDbContext dbContext,
    IAuditService auditService,
    ILogger<PricingDecisionLedgerWriter> logger) : IPricingDecisionLedgerWriter
{
    private const string AuditActionType = "PricingDecision.Recorded";

    /// <inheritdoc />
    public async Task<(PricingDecisionRecordingStatus Status, PricingDecisionLedgerEntry? Entry)> RecordAsync(
        PricingDecisionRecord decision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);

        if (!PricingDecisionType.All.Contains(decision.DecisionType))
        {
            logger.LogWarning(
                "Rejected pricing decision with unknown decision type {DecisionType}",
                decision.DecisionType);
            return (PricingDecisionRecordingStatus.Rejected, null);
        }

        if (decision.Breakdown is null || decision.Breakdown.Quantity < 1)
        {
            logger.LogWarning("Rejected pricing decision {DecisionType}: invalid breakdown quantity", decision.DecisionType);
            return (PricingDecisionRecordingStatus.Rejected, null);
        }

        if (decision.Breakdown.FinalAmount < 0)
        {
            logger.LogWarning(
                "Rejected pricing decision {DecisionType}: negative final amount {FinalAmount}",
                decision.DecisionType,
                decision.Breakdown.FinalAmount);
            return (PricingDecisionRecordingStatus.Rejected, null);
        }

        if (string.IsNullOrWhiteSpace(decision.IdempotencyKey) || decision.IdempotencyKey.Trim().Length > 200)
        {
            logger.LogWarning("Rejected pricing decision {DecisionType}: idempotency key missing or too long", decision.DecisionType);
            return (PricingDecisionRecordingStatus.Rejected, null);
        }

        var idempotencyKey = decision.IdempotencyKey.Trim();

        // Idempotent replay: a retried calculation returns the original row.
        var existing = await dbContext.Set<PricingDecisionLedgerEntry>()
            .FirstOrDefaultAsync(entry => entry.IdempotencyKey == idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return (PricingDecisionRecordingStatus.Replay, existing);
        }

        var breakdown = decision.Breakdown;
        var appliedCodes = breakdown.PromoApplications?
            .Select(application => application.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var entry = new PricingDecisionLedgerEntry
        {
            DecisionType = decision.DecisionType,
            TenantId = decision.TenantId,
            OrderId = decision.Order,
            OrderLineItemId = decision.OrderLineItem,
            PaymentId = decision.PaymentId,
            SubscriptionId = decision.Subscription,
            BillingCycleNumber = decision.BillingCycleNumber,
            ProductId = decision.Product,
            Quantity = breakdown.Quantity,
            Currency = string.IsNullOrWhiteSpace(breakdown.Currency) ? "USD" : breakdown.Currency.ToUpperInvariant(),
            BaseAmount = breakdown.BaseAmount,
            RuleAdjustmentTotal = breakdown.RuleAdjustmentTotal,
            PromoDiscountTotal = breakdown.PromoDiscountTotal,
            FinalAmount = breakdown.FinalAmount,
            BreakdownJson = breakdown.ToJson(),
            // Stored with leading/trailing delimiters so exact-code filtering is a plain
            // Contains(",CODE,") without substring false positives (WELCOME vs WELCOME10).
            AppliedPromoCodes = appliedCodes is { Count: > 0 } ? $",{string.Join(",", appliedCodes)}," : null,
            AppliedPricingRuleId = decision.AppliedPricingRuleId,
            AppliedStackingRuleName = decision.AppliedStackingRuleName,
            PricingEngineVersion = PricingDecisionLedgerDefaults.EngineVersion,
            ActorUserId = decision.ActorUserId,
            ActorIpAddress = decision.ActorIpAddress,
            IdempotencyKey = idempotencyKey,
            DecidedAtUtc = SystemClock.UtcNow
        };

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            dbContext.Set<PricingDecisionLedgerEntry>().Add(entry);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);

            // A concurrent request with the same key may have committed first (unique index).
            var winner = await dbContext.Set<PricingDecisionLedgerEntry>()
                .FirstOrDefaultAsync(row => row.IdempotencyKey == idempotencyKey, CancellationToken.None)
                .ConfigureAwait(false);
            if (winner is not null)
            {
                return (PricingDecisionRecordingStatus.Replay, winner);
            }

            throw;
        }

        await PublishAuditAsync(entry, appliedCodes, cancellationToken).ConfigureAwait(false);

        return (PricingDecisionRecordingStatus.Recorded, entry);
    }

    private async Task PublishAuditAsync(
        PricingDecisionLedgerEntry entry,
        IReadOnlyList<string>? appliedCodes,
        CancellationToken cancellationToken)
    {
        try
        {
            var metadata = new Dictionary<string, object?>
            {
                ["ledgerEntryId"] = entry.Id,
                ["decisionType"] = entry.DecisionType,
                ["tenantId"] = entry.TenantId,
                ["orderId"] = entry.OrderId,
                ["orderLineItemId"] = entry.OrderLineItemId,
                ["subscriptionId"] = entry.SubscriptionId,
                ["billingCycleNumber"] = entry.BillingCycleNumber,
                ["productId"] = entry.ProductId,
                ["quantity"] = entry.Quantity,
                ["currency"] = entry.Currency,
                ["baseAmount"] = entry.BaseAmount,
                ["ruleAdjustmentTotal"] = entry.RuleAdjustmentTotal,
                ["promoDiscountTotal"] = entry.PromoDiscountTotal,
                ["finalAmount"] = entry.FinalAmount,
                ["appliedPromoCodes"] = appliedCodes,
                ["appliedPricingRuleId"] = entry.AppliedPricingRuleId,
                ["appliedStackingRuleName"] = entry.AppliedStackingRuleName,
                ["pricingEngineVersion"] = entry.PricingEngineVersion,
                ["actorIpAddress"] = entry.ActorIpAddress,
                ["idempotencyKey"] = entry.IdempotencyKey
            };

            await auditService.LogAdminActionAsync(
                entry.ActorUserId ?? Guid.Empty,
                AuditActionType,
                $"Pricing decision '{entry.DecisionType}' recorded: final {entry.FinalAmount} {entry.Currency} " +
                $"(base {entry.BaseAmount}, rules -{entry.RuleAdjustmentTotal}, promos -{entry.PromoDiscountTotal})",
                metadata).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Audit publishing is best-effort: the durable ledger row is the source of truth.
            logger.LogWarning(
                exception,
                "Failed to publish pricing decision audit event for ledger entry {LedgerEntryId}",
                entry.Id);
        }
    }
}
