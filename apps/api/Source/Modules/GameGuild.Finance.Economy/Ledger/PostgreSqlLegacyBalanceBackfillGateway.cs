using System.Data.Common;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Ledger;

public sealed record LegacyBalanceBackfillPostingRequest(
    RegisteredPostingAuthority Authority,
    CapabilityAuthorizationReceipt CapabilityReceipt,
    Guid LegacyWalletId,
    WalletId WalletId,
    SourceStampId SourceStampId,
    PostingId PostingId,
    CreditLotId CreditLotId,
    IdempotencyKey IdempotencyKey,
    long HardUnits,
    string SnapshotHash,
    string ProviderHash,
    string DestinationHash,
    string SourceRootHash,
    DateTimeOffset PostedAt);

public interface ILegacyBalanceBackfillGateway
{
    RegisteredPostingReceipt Post(LegacyBalanceBackfillPostingRequest request);
}

public sealed class PostgreSqlLegacyBalanceBackfillGateway : ILegacyBalanceBackfillGateway
{
    private readonly DbContext _db;

    public PostgreSqlLegacyBalanceBackfillGateway(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext ?? throw new InvalidOperationException(
            "Legacy Economy backfill requires the application's relational DbContext.");
    }

    public RegisteredPostingReceipt Post(LegacyBalanceBackfillPostingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Authority);
        ArgumentNullException.ThrowIfNull(request.CapabilityReceipt);
        if (request.LegacyWalletId == Guid.Empty)
            throw new ArgumentException("Legacy wallet ID is required.", nameof(request));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.HardUnits);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SnapshotHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProviderHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceRootHash);
        if (request.CapabilityReceipt.Capability != EconomyValueMovementCapability.LegacyBalanceBackfill)
            throw new ArgumentException("A LegacyBalanceBackfill capability receipt is required.", nameof(request));

        try
        {
            var row = _db.Set<RegisteredPostingReceiptRow>()
                .FromSqlInterpolated($"""
                    SELECT * FROM economy_private.post_legacy_balance_backfill_v1(
                        {request.Authority.CapabilityId},
                        {request.Authority.ActorId},
                        {request.Authority.TenantId},
                        {request.PostingId.Value},
                        {request.IdempotencyKey.Value},
                        {request.CapabilityReceipt.PolicyVersion},
                        {request.CapabilityReceipt.ReserveVersion},
                        {request.Authority.RiskDecisionId},
                        {request.Authority.RiskOperationFingerprint},
                        {request.Authority.ExpectedCounterVersion},
                        {request.LegacyWalletId},
                        {request.WalletId.Value},
                        {request.SourceStampId.Value},
                        {request.CreditLotId.Value},
                        {request.HardUnits},
                        {request.SnapshotHash.Trim()},
                        {request.CapabilityReceipt.Id},
                        {request.CapabilityReceipt.ReceiptHash},
                        {request.CapabilityReceipt.KillSwitchEpoch},
                        {request.CapabilityReceipt.JurisdictionCode},
                        {request.ProviderHash.Trim()},
                        {request.DestinationHash.Trim()},
                        {request.SourceRootHash.Trim()},
                        {request.PostedAt})
                    """)
                .AsNoTracking()
                .Single();
            return new RegisteredPostingReceipt(
                new PostingId(row.PostingId),
                row.JournalSequence,
                row.JournalHash,
                row.Duplicate);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new RegisteredPostingRejectedException(
                "The protected legacy-balance writer rejected the posting.", exception);
        }
    }

    private static bool IsDatabaseFailure(Exception exception) =>
        exception is DbException or DbUpdateException or InvalidOperationException ||
        exception.GetBaseException() is DbException;
}
