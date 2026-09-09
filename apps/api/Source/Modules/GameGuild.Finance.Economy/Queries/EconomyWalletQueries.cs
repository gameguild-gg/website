using GameGuild.CQRS;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Queries;

public sealed record GetMyEconomyWalletQuery : IQuery<EconomyWalletSummaryDto?>;

public sealed record ListMyEconomyWalletTransactionsQuery(int Take = 50)
    : IQuery<IReadOnlyList<EconomyWalletTransactionDto>>;

public sealed class GetMyEconomyWalletQueryHandler(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<GetMyEconomyWalletQuery, EconomyWalletSummaryDto?>
{
    public async Task<EconomyWalletSummaryDto?> Handle(GetMyEconomyWalletQuery request, CancellationToken cancellationToken)
    {
        var actor = EconomyWalletActor.Require(actorContextAccessor);
        var wallet = await context.Set<EconomyWalletRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.OwnerId == actor.UserId && row.TenantId == actor.TenantId,
                cancellationToken)
            .ConfigureAwait(false);

        if (wallet is null)
            return null;

        var balance = await context.Set<EconomyWalletBalanceProjectionRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.WalletId == wallet.Id, cancellationToken)
            .ConfigureAwait(false);
        var debt = await context.Set<EconomyWalletDebtRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.WalletId == wallet.Id, cancellationToken)
            .ConfigureAwait(false);

        return new EconomyWalletSummaryDto(
            wallet.Id,
            wallet.State,
            wallet.CreatedAt,
            balance?.PendingHard ?? 0,
            balance?.PendingSoft ?? 0,
            balance?.PurchasedHard ?? 0,
            balance?.EarnedHard ?? 0,
            balance?.RestrictedHard ?? 0,
            balance?.Soft ?? 0,
            balance?.HeldHard ?? 0,
            balance?.HeldSoft ?? 0,
            balance?.AvailableHardToSpend ?? 0,
            balance?.AvailableSoftToSpend ?? 0,
            balance?.WithdrawableHard ?? 0,
            debt?.OutstandingHardUnits ?? 0,
            balance?.RebuiltAt ?? wallet.CreatedAt,
            balance?.SourceJournalSequence ?? 0);
    }
}

public sealed class ListMyEconomyWalletTransactionsQueryHandler(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor)
    : IQueryHandler<ListMyEconomyWalletTransactionsQuery, IReadOnlyList<EconomyWalletTransactionDto>>
{
    public async Task<IReadOnlyList<EconomyWalletTransactionDto>> Handle(
        ListMyEconomyWalletTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var actor = EconomyWalletActor.Require(actorContextAccessor);
        var take = Math.Clamp(request.Take, 1, 100);
        var walletId = await context.Set<EconomyWalletRow>()
            .AsNoTracking()
            .Where(row => row.OwnerId == actor.UserId && row.TenantId == actor.TenantId)
            .Select(row => (Guid?)row.Id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (walletId is null)
            return [];

        return await (
                from line in context.Set<EconomyJournalLineRow>().AsNoTracking()
                join entry in context.Set<EconomyJournalEntryRow>().AsNoTracking()
                    on line.JournalEntryId equals entry.Id
                join postingGroup in context.Set<EconomyPostingGroupRow>().AsNoTracking()
                    on entry.PostingGroupId equals postingGroup.Id
                where line.WalletId == walletId.Value
                orderby entry.Sequence descending, line.Sequence ascending
                select new EconomyWalletTransactionDto(
                    postingGroup.Id,
                    entry.Id,
                    entry.Sequence,
                    postingGroup.TemplateKind,
                    postingGroup.Status,
                    entry.RecordedAt,
                    line.Side,
                    line.Currency,
                    line.AmountUnits,
                    line.Provenance))
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

internal static class EconomyWalletActor
{
    internal static (Guid UserId, Guid TenantId) Require(IActorContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        var actor = accessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid is not { } userId || actor.TenantId is not { } tenantId)
            throw new UnauthorizedAccessException("Economy wallet access requires an authenticated user and tenant context.");
        return (userId, tenantId);
    }
}
