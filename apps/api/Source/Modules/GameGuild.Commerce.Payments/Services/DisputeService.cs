using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
/// Dispute service implementation
/// </summary>
public class DisputeService(
    IApplicationDbContext context,
    ILogger<DisputeService> logger,
    IRevenueAuditService? revenueAuditService = null,
    IDisputeNotificationService? disputeNotificationService = null) : IDisputeService
{
    private DbSet<PaymentDispute> PaymentDisputes { get => context.Set<PaymentDispute>(); }

    private DbSet<DisputeEvidence> DisputeEvidences { get => context.Set<DisputeEvidence>(); }

    public async Task<PaymentDispute> CreateDisputeAsync(Guid paymentId, Guid userId, DisputeType type, decimal amount, string reason, string? description = null, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Creating dispute for payment {PaymentId} by user {UserId}", paymentId, userId);

        var dueDate = SystemClock.UtcNow.AddDays(14); // Default 14 days to respond

        var dispute = new PaymentDispute { PaymentId = paymentId, UserId = userId, Type = type, Amount = amount, Reason = reason, Description = description, Status = DisputeStatus.Submitted, DueDate = dueDate };

        PaymentDisputes.Add(dispute);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Dispute created with ID {DisputeId}", dispute.Id);

        // Team alert (issue #419): locally created disputes notify the configured recipients too.
        if (disputeNotificationService is not null)
        {
            var payment = await context.Set<Payment>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken)
                .ConfigureAwait(false);
            await disputeNotificationService
                .SendDisputeCreatedAlertAsync(dispute, payment, cancellationToken)
                .ConfigureAwait(false);
        }

        return dispute;
    }

    public async Task<PaymentDispute?> GetDisputeByIdAsync(Guid disputeId, CancellationToken cancellationToken = default)
    {
        return await PaymentDisputes.Include(d => d.Evidence).FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken);
    }

    public async Task<List<PaymentDispute>> GetDisputesByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return await PaymentDisputes.Include(d => d.Evidence).Where(d => d.PaymentId == paymentId).OrderByDescending(d => d.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<List<PaymentDispute>> GetDisputesByUserIdAsync(Guid userId, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        return await PaymentDisputes.Include(d => d.Evidence).Where(d => d.UserId == userId).OrderByDescending(d => d.CreatedAt).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public async Task<List<PaymentDispute>> GetDisputesByStatusAsync(DisputeStatus status, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        return await PaymentDisputes.Include(d => d.Evidence).Where(d => d.Status == status).OrderByDescending(d => d.CreatedAt).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public async Task UpdateDisputeStatusAsync(Guid disputeId, DisputeStatus newStatus, DateTime? dueDate = null, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Updating dispute {DisputeId} status to {Status}", disputeId, newStatus);

        var dispute = await GetDisputeByIdAsync(disputeId, cancellationToken).ConfigureAwait(false);

        if (dispute == null) throw new InvalidOperationException($"Dispute {disputeId} not found");

        switch (newStatus)
        {
            case DisputeStatus.UnderReview : dispute.MoveToReview(); break;
            case DisputeStatus.PendingCustomerResponse : dispute.RequestCustomerResponse(dueDate ?? SystemClock.UtcNow.AddDays(7)); break;
            case DisputeStatus.PendingMerchantResponse : dispute.RequestMerchantResponse(dueDate ?? SystemClock.UtcNow.AddDays(7)); break;
            default :
                dispute.Status = newStatus;
                if (dueDate.HasValue) dispute.DueDate = dueDate.Value;

                break;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Dispute {DisputeId} status updated to {Status}", disputeId, newStatus);
    }

    public async Task ResolveDisputeAsync(Guid disputeId, DisputeResolution resolution, string? notes = null, Guid? resolvedBy = null, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Resolving dispute {DisputeId} with resolution {Resolution}", disputeId, resolution);

        var dispute = await GetDisputeByIdAsync(disputeId, cancellationToken).ConfigureAwait(false);

        if (dispute == null) throw new InvalidOperationException($"Dispute {disputeId} not found");

        if (resolution == DisputeResolution.Won)
            dispute.MarkAsWon(notes ?? string.Empty, resolvedBy ?? Guid.Empty);
        else if (resolution == DisputeResolution.Lost)
            dispute.MarkAsLost(notes ?? string.Empty, resolvedBy ?? Guid.Empty);
        else
            dispute.Resolve(resolution, notes ?? string.Empty, resolvedBy ?? Guid.Empty);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Dispute-fee accounting (issue #419 "record dispute fees / update accounting"): the
        // gateway's chargeback fee is expensed once, at resolution time. Best-effort — the
        // ledger write must never roll back the recorded resolution.
        if (dispute.DisputeFee > 0 && revenueAuditService is not null)
        {
            try
            {
                await revenueAuditService.CreateLedgerEntryAsync(
                    entryType: LedgerEntryType.Fee,
                    debitAccount: LedgerAccount.RefundsAndChargebacks.ToAccountCode(),
                    creditAccount: LedgerAccount.Cash.ToAccountCode(),
                    amount: dispute.DisputeFee,
                    currency: "USD",
                    description: $"Dispute fee for dispute {dispute.Id} (payment {dispute.PaymentId}, outcome {resolution})",
                    referenceNumber: dispute.GatewayDisputeId,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Dispute-fee ledger entry for dispute {DisputeId} failed; the resolution itself stays applied",
                    dispute.Id);
            }
        }

        logger.LogInformation("Dispute {DisputeId} resolved", disputeId);
    }

    public async Task CancelDisputeAsync(Guid disputeId, string reason, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Cancelling dispute {DisputeId}", disputeId);

        var dispute = await GetDisputeByIdAsync(disputeId, cancellationToken).ConfigureAwait(false);

        if (dispute == null) throw new InvalidOperationException($"Dispute {disputeId} not found");

        dispute.Cancel(reason);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Dispute {DisputeId} cancelled", disputeId);
    }

    public async Task<DisputeEvidence> AddEvidenceAsync(
        Guid disputeId,
        EvidenceType evidenceType,
        string title,
        string description,
        Guid submittedBy,
        bool isFromMerchant = false,
        string? fileUrl = null,
        string? fileName = null,
        long? fileSize = null,
        string? mimeType = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Adding evidence to dispute {DisputeId}", disputeId);

        var dispute = await GetDisputeByIdAsync(disputeId, cancellationToken).ConfigureAwait(false);

        if (dispute == null) throw new InvalidOperationException($"Dispute {disputeId} not found");

        var evidence = new DisputeEvidence
        {
            DisputeId = disputeId,
            EvidenceType = evidenceType,
            Title = title,
            Description = description,
            SubmittedBy = submittedBy,
            IsFromMerchant = isFromMerchant,
            FileUrl = fileUrl,
            FileName = fileName,
            FileSize = fileSize,
            MimeType = mimeType,
            SubmittedAt = SystemClock.UtcNow
        };

        DisputeEvidences.Add(evidence);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Evidence {EvidenceId} added to dispute {DisputeId}", evidence.Id, disputeId);

        return evidence;
    }

    public async Task<List<DisputeEvidence>> GetDisputeEvidenceAsync(Guid disputeId, CancellationToken cancellationToken = default)
    {
        return await context.Set<DisputeEvidence>().Where(e => e.DisputeId == disputeId).OrderByDescending(e => e.SubmittedAt).ToListAsync(cancellationToken);
    }
}
