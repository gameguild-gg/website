using System.Data.Common;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Funding;

public sealed record PersistedAdRewardIssuanceRequest(
    RegisteredPostingAuthority Authority,
    PostingId PostingId,
    IdempotencyKey IdempotencyKey,
    SourceStampId SourceStampId,
    CreditLotId OutputLotId,
    WalletId WalletId,
    long SoftUnits,
    PolicyVersion PolicyVersion,
    ReserveVersion ReserveVersion,
    string Network,
    string ProviderEventReference,
    string EvidenceHash,
    DateTimeOffset IssuedAt,
    string CapabilityReceiptHash);

public interface IAdRewardIssuanceGateway
{
    RegisteredPostingReceipt Issue(PersistedAdRewardIssuanceRequest request);
}

public sealed class PostgreSqlAdRewardIssuanceGateway : IAdRewardIssuanceGateway
{
    private readonly DbContext _db;

    public PostgreSqlAdRewardIssuanceGateway(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Persistent ad reward issuance requires the application's relational DbContext.");
    }

    public RegisteredPostingReceipt Issue(PersistedAdRewardIssuanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Authority);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.SoftUnits);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Network);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProviderEventReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.EvidenceHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CapabilityReceiptHash);
        if (request.CapabilityReceiptHash.Length > 128)
            throw new ArgumentException("Capability receipt hashes cannot exceed 128 characters.", nameof(request));

        try
        {
            var row = _db.Set<RegisteredPostingReceiptRow>()
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM economy_private.post_ad_reward_issuance_v1(
                        {request.Authority.CapabilityId},
                        {request.Authority.ActorId},
                        {request.Authority.TenantId},
                        {request.PostingId.Value},
                        {request.IdempotencyKey.Value},
                        {request.PolicyVersion.Value},
                        {request.ReserveVersion.Value},
                        {request.Authority.RiskDecisionId},
                        {request.Authority.RiskOperationFingerprint},
                        {request.Authority.ExpectedCounterVersion},
                        {request.SourceStampId.Value},
                        {request.OutputLotId.Value},
                        {request.WalletId.Value},
                        {request.SoftUnits},
                        {request.Network.Trim()},
                        {request.ProviderEventReference.Trim()},
                        {request.EvidenceHash.Trim()},
                        {request.IssuedAt},
                        {request.CapabilityReceiptHash.Trim()})
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
                "The persistent ad reward writer rejected the issuance.",
                exception);
        }
    }

    private static bool IsDatabaseFailure(Exception exception) =>
        exception is DbException or DbUpdateException or InvalidOperationException ||
        exception.GetBaseException() is DbException;
}
