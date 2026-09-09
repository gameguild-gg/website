using System.Data.Common;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Ledger;

public sealed record PersistedFifoTransferRequest(
    TransferFragmentsCommand Command,
    RegisteredPostingAuthority Authority,
    string? DispatchSnapshotHash = null);

public interface IFifoTransferGateway
{
    RegisteredPostingReceipt Transfer(PersistedFifoTransferRequest request);
}

public sealed class PostgreSqlFifoTransferGateway : IFifoTransferGateway
{
    private readonly DbContext _db;

    public PostgreSqlFifoTransferGateway(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Persistent Economy FIFO transfers require the application's relational DbContext.");
    }

    public RegisteredPostingReceipt Transfer(PersistedFifoTransferRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Command);
        ArgumentNullException.ThrowIfNull(request.Authority);

        var command = request.Command;
        if (command.SourceWalletId == command.DestinationWalletId)
            throw new ArgumentException("Source and destination wallets must differ.", nameof(request));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.Amount.Units);
        if (!Enum.IsDefined(command.Provenance))
            throw new ArgumentOutOfRangeException(nameof(request));
        if (request.DispatchSnapshotHash is { Length: > 128 })
            throw new ArgumentException("Dispatch snapshot hashes cannot exceed 128 characters.", nameof(request));

        try
        {
            var receipt = _db.Set<RegisteredPostingReceiptRow>()
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM economy_private.post_fifo_transfer_v1(
                        {request.Authority.CapabilityId},
                        {request.Authority.ActorId},
                        {request.Authority.TenantId},
                        {command.PostingId.Value},
                        {command.IdempotencyKey.Value},
                        {command.PolicyVersion.Value},
                        {command.ReserveVersion.Value},
                        {request.Authority.RiskDecisionId},
                        {request.Authority.RiskOperationFingerprint},
                        {request.Authority.ExpectedCounterVersion},
                        {command.SourceWalletId.Value},
                        {command.DestinationWalletId.Value},
                        {(int)command.Amount.Currency},
                        {(int)command.Provenance},
                        {command.Amount.Units},
                        {command.RequestedAt},
                        {request.DispatchSnapshotHash})
                    """)
                .AsNoTracking()
                .Single();

            return new RegisteredPostingReceipt(
                new PostingId(receipt.PostingId),
                receipt.JournalSequence,
                receipt.JournalHash,
                receipt.Duplicate);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new RegisteredPostingRejectedException(
                "The persistent Economy FIFO transfer writer rejected the request.",
                exception);
        }
    }

    private static bool IsDatabaseFailure(Exception exception) =>
        exception is DbException or DbUpdateException or InvalidOperationException ||
        exception.GetBaseException() is DbException;
}
