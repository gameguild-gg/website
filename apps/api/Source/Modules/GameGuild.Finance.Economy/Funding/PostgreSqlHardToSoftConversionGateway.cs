using System.Data.Common;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Funding;

public sealed record PersistedHardToSoftConversion(
    ConvertHardToSoftCommand Command,
    RegisteredPostingAuthority Authority,
    string? DispatchSnapshotHash = null);

public sealed record PersistedHardToSoftConversionReceipt(
    RegisteredPostingReceipt PrincipalPosting,
    RegisteredPostingReceipt? FeePosting);

public interface IHardToSoftConversionGateway
{
    PersistedHardToSoftConversionReceipt Convert(PersistedHardToSoftConversion request);
}

public sealed class PostgreSqlHardToSoftConversionGateway : IHardToSoftConversionGateway
{
    private readonly DbContext _db;

    public PostgreSqlHardToSoftConversionGateway(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Persistent Economy conversion requires the application's relational DbContext.");
    }

    public PersistedHardToSoftConversionReceipt Convert(PersistedHardToSoftConversion request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Command);
        ArgumentNullException.ThrowIfNull(request.Authority);
        if (request.DispatchSnapshotHash is { Length: > 128 })
            throw new ArgumentException("Dispatch snapshot hashes cannot exceed 128 characters.", nameof(request));

        var command = request.Command;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.PrincipalHardCoinUnits);
        ArgumentOutOfRangeException.ThrowIfNegative(command.FeeHardCoinUnits);
        if (command.FeeHardCoinUnits == 0 && command.FeePostingId.Value != Guid.Empty)
            throw new ArgumentException("A fee posting ID is only valid when a conversion fee is charged.", nameof(request));
        if (command.FeeHardCoinUnits > 0 && command.FeePostingId.Value == Guid.Empty)
            throw new ArgumentException("A fee posting ID is required when a conversion fee is charged.", nameof(request));

        var total = new CoinAmount(
            CurrencyCode.HardCoin,
            checked(command.PrincipalHardCoinUnits + command.FeeHardCoinUnits));
        command.Authorization.EnsureMatches(
            PostingTemplateKind.HardToSoftConversion,
            command.IdempotencyKey,
            total,
            command.ReserveVersion,
            command.RequestedAt);
        if (command.Authorization.SourceRoots.Count == 0)
            throw new RegisteredPostingRejectedException("A conversion requires an explicit source-root authorization.");

        Guid? feePostingId = command.FeeHardCoinUnits == 0
            ? null
            : command.FeePostingId.Value;

        try
        {
            var receipt = _db.Set<RegisteredPostingReceiptRow>()
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM economy_private.post_self_service_hard_to_soft_conversion_v1(
                        {request.Authority.ActorId},
                        {request.Authority.TenantId},
                        {command.PrincipalPostingId.Value},
                        {feePostingId},
                        {command.IdempotencyKey.Value},
                        {request.Authority.RiskDecisionId},
                        {command.WalletId.Value},
                        {command.OutputLotId.Value},
                        {command.Authorization.SourceRoots.Select(root => root.Value).ToArray()},
                        {command.PrincipalHardCoinUnits},
                        {command.FeeHardCoinUnits},
                        {command.RequestedAt},
                        {request.DispatchSnapshotHash})
                    """)
                .AsNoTracking()
                .Single();

            var principal = ToReceipt(receipt);
            var fee = command.FeeHardCoinUnits == 0
                ? null
                : ReadFeeReceipt(command.FeePostingId);
            return new PersistedHardToSoftConversionReceipt(principal, fee);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new RegisteredPostingRejectedException(
                "The persistent Economy conversion writer rejected the conversion.",
                exception);
        }
    }

    private RegisteredPostingReceipt ReadFeeReceipt(PostingId feePostingId)
    {
        var receipt = _db.Set<RegisteredPostingReceiptRow>()
            .FromSqlInterpolated($"""
                SELECT posting."Id" AS posting_id,
                       entry."Sequence" AS journal_sequence,
                       entry."Hash" AS journal_hash,
                       false AS duplicate
                FROM public.economy_posting_groups posting
                JOIN public.economy_journal_entries entry ON entry."PostingGroupId" = posting."Id"
                WHERE posting."Id" = {feePostingId.Value}
                """)
            .AsNoTracking()
            .SingleOrDefault()
            ?? throw new RegisteredPostingRejectedException("The conversion fee posting was not persisted.");
        return ToReceipt(receipt);
    }

    private static RegisteredPostingReceipt ToReceipt(RegisteredPostingReceiptRow receipt) =>
        new(new PostingId(receipt.PostingId), receipt.JournalSequence, receipt.JournalHash, receipt.Duplicate);

    private static bool IsDatabaseFailure(Exception exception) =>
        exception is DbException or DbUpdateException or InvalidOperationException ||
        exception.GetBaseException() is DbException;
}
