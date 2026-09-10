using GameGuild.CQRS;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Finance.Economy.Commands;

public sealed record CreateMyHardCoinTopUpRequest(
    long HardCoinUnits,
    string IdempotencyKey);

public sealed record CreateMyHardCoinTopUpCommand(CreateMyHardCoinTopUpRequest Request)
    : ICommand<SelfServiceHardCoinTopUpReceipt>;

public sealed record GetMyHardCoinTopUpQuery(Guid TopUpId)
    : IQuery<EconomyTopUpStatusDto?>;

public sealed record ListMyHardCoinTopUpsQuery(int Take = 50)
    : IQuery<IReadOnlyList<EconomyTopUpStatusDto>>;

public sealed class CreateMyHardCoinTopUpCommandHandler(
    ISelfServiceHardCoinTopUpService topUps)
    : ICommandHandler<CreateMyHardCoinTopUpCommand, SelfServiceHardCoinTopUpReceipt>
{
    public Task<SelfServiceHardCoinTopUpReceipt> Handle(
        CreateMyHardCoinTopUpCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Request);
        return topUps.CreateAsync(
            new SelfServiceHardCoinTopUpRequest(
                request.Request.HardCoinUnits,
                request.Request.IdempotencyKey),
            cancellationToken);
    }
}

public sealed class GetMyHardCoinTopUpQueryHandler(
    IEconomyTopUpReader reader,
    IActorContextAccessor actorContextAccessor)
    : IQueryHandler<GetMyHardCoinTopUpQuery, EconomyTopUpStatusDto?>
{
    public async Task<EconomyTopUpStatusDto?> Handle(
        GetMyHardCoinTopUpQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TopUpId == Guid.Empty)
            throw new ArgumentException("Top-up ID is required.", nameof(request));
        var actor = EconomyTopUpActor.Require(actorContextAccessor);
        return await reader.GetAsync(
            actor.TenantId,
            actor.ActorId,
            request.TopUpId,
            cancellationToken);
    }
}

public sealed class ListMyHardCoinTopUpsQueryHandler(
    IEconomyTopUpReader reader,
    IActorContextAccessor actorContextAccessor)
    : IQueryHandler<ListMyHardCoinTopUpsQuery, IReadOnlyList<EconomyTopUpStatusDto>>
{
    public async Task<IReadOnlyList<EconomyTopUpStatusDto>> Handle(
        ListMyHardCoinTopUpsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Take is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(request));
        var actor = EconomyTopUpActor.Require(actorContextAccessor);
        return await reader.ListAsync(
            actor.TenantId,
            actor.ActorId,
            request.Take,
            cancellationToken);
    }
}

internal static class EconomyTopUpActor
{
    internal static (Guid TenantId, Guid ActorId) Require(IActorContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        var actor = accessor.ActorContext;
        if (!actor.IsAuthenticated || actor.TenantId is not { } tenantId ||
            actor.SubjectIdAsGuid is not { } actorId)
            throw new UnauthorizedAccessException(
                "Economy top-up access requires an authenticated tenant actor.");
        return (tenantId, actorId);
    }
}
