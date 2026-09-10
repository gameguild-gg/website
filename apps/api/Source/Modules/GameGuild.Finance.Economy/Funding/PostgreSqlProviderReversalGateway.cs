using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Funding;


public sealed record PersistedProviderReversal(
    ReverseTopUpCommand Command,
    RegisteredPostingAuthority Authority,
    string? DispatchSnapshotHash = null);

public sealed record PersistedProviderReversalReceipt(
    PostingId OperationId,
    long RecoveredHardUnits,
    long RecoveredConvertedSoftUnits,
    long ResponsibleDebtHardUnits,
    long PlatformLossHardUnits,
    bool IsDuplicate);

public interface IProviderReversalGateway
{
    PersistedProviderReversalReceipt Reverse(PersistedProviderReversal request);
}

public sealed class PostgreSqlProviderReversalGateway : IProviderReversalGateway
{
    private readonly DbContext _db;

    public PostgreSqlProviderReversalGateway(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Persistent provider reversals require the application's relational DbContext.");
    }

    public PersistedProviderReversalReceipt Reverse(PersistedProviderReversal request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Command);
        ArgumentNullException.ThrowIfNull(request.Authority);
        if (request.DispatchSnapshotHash is { Length: > 128 })
            throw new ArgumentException("Dispatch snapshot hashes cannot exceed 128 characters.", nameof(request));

        var command = request.Command;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.CumulativeProviderHardUnits);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Evidence);
        if (!Enum.IsDefined(command.IrrecoverableDisposition))
            throw new ArgumentOutOfRangeException(nameof(request));

        var evidenceHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(command.Evidence.Trim()))).ToLowerInvariant();

        try
        {
            var receipt = _db.Set<ProviderReversalReceiptRow>()
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM economy_private.post_provider_reversal_v2(
                        {request.Authority.CapabilityId},
                        {request.Authority.ActorId},
                        {request.Authority.TenantId},
                        {command.PostingIdSeed.Value},
                        {command.IdempotencyKey.Value},
                        {command.SourceId.Value},
                        {command.CumulativeProviderHardUnits},
                        {(int)command.IrrecoverableDisposition},
                        {evidenceHash},
                        {command.PolicyVersion.Value},
                        {command.ReserveVersion.Value},
                        {request.Authority.RiskDecisionId},
                        {request.Authority.RiskOperationFingerprint},
                        {request.Authority.ExpectedCounterVersion},
                        {command.OccurredAt},
                        {request.DispatchSnapshotHash})
                    """)
                .AsNoTracking()
                .Single();

            return new PersistedProviderReversalReceipt(
                new PostingId(receipt.OperationId),
                receipt.RecoveredHardUnits,
                receipt.RecoveredConvertedSoftUnits,
                receipt.ResponsibleDebtHardUnits,
                receipt.PlatformLossHardUnits,
                receipt.Duplicate);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new RegisteredPostingRejectedException(
                "The persistent provider reversal writer rejected the reversal.",
                exception);
        }
    }

    private static bool IsDatabaseFailure(Exception exception) =>
        exception is DbException or DbUpdateException or InvalidOperationException ||
        exception.GetBaseException() is DbException;
}
