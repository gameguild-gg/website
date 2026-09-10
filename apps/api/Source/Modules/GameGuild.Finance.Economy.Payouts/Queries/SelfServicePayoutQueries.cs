using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Finance.Economy.Payouts.Queries;

public sealed record EconomyPayoutOperationDto(
    Guid Id,
    long HardCoinUnits,
    PayoutOperationState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EconomyPayoutRequestDto(
    Guid Id,
    long HardCoinUnits,
    PayoutRequestState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static EconomyPayoutRequestDto From(PayoutRequest request) => new(
        request.Id,
        request.Amount.Units,
        request.State,
        request.CreatedAt,
        request.UpdatedAt);
}

public sealed record EconomyPayoutRequestReviewDto(
    Guid Id,
    Guid PayeeId,
    Guid WalletId,
    long HardCoinUnits,
    PayoutRequestState State,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static EconomyPayoutRequestReviewDto From(PayoutRequest request) => new(
        request.Id,
        request.PayeeId,
        request.WalletId.Value,
        request.Amount.Units,
        request.State,
        request.Version,
        request.CreatedAt,
        request.UpdatedAt);
}

public sealed record EconomyPayoutRequestReviewAuditDto(
    Guid Id,
    Guid ActorId,
    PayoutRequestState Outcome,
    string Reason,
    DateTimeOffset OccurredAt)
{
    public static EconomyPayoutRequestReviewAuditDto From(PayoutRequestReviewAuditEvent auditEvent) => new(
        auditEvent.Id,
        auditEvent.ActorId,
        auditEvent.Outcome,
        auditEvent.Reason,
        auditEvent.OccurredAt);
}

public sealed record ListMyPayoutOperationsQuery(Guid TenantId, Guid PayeeId, int Take)
    : IQuery<IReadOnlyList<EconomyPayoutOperationDto>>;

public sealed record GetMyPayoutOperationQuery(Guid TenantId, Guid PayeeId, Guid OperationId)
    : IQuery<EconomyPayoutOperationDto?>;

public sealed record ListMyPayoutRequestsQuery(Guid TenantId, Guid PayeeId, int Take)
    : IQuery<IReadOnlyList<EconomyPayoutRequestDto>>;

[AuthorizeRequest(EconomyPermission.Keys.ReviewPayouts)]
public sealed record ListPayoutRequestsForReviewQuery(int Take)
    : IQuery<IReadOnlyList<EconomyPayoutRequestReviewDto>>;

[AuthorizeRequest(EconomyPermission.Keys.ReviewPayouts)]
public sealed record ListPayoutRequestReviewAuditQuery(Guid RequestId)
    : IQuery<IReadOnlyList<EconomyPayoutRequestReviewAuditDto>>;

public sealed class ListMyPayoutOperationsQueryHandler(IPayoutOperationStore operations)
    : IQueryHandler<ListMyPayoutOperationsQuery, IReadOnlyList<EconomyPayoutOperationDto>>
{
    public Task<IReadOnlyList<EconomyPayoutOperationDto>> Handle(
        ListMyPayoutOperationsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<EconomyPayoutOperationDto> result = operations
            .ListForPayee(request.TenantId, request.PayeeId, request.Take)
            .Select(ToDto)
            .ToArray();
        return Task.FromResult(result);
    }

    private static EconomyPayoutOperationDto ToDto(PayoutOperation operation) => new(
        operation.Id,
        operation.Amount.Units,
        operation.State,
        operation.CreatedAt,
        operation.UpdatedAt);
}

public sealed class GetMyPayoutOperationQueryHandler(IPayoutOperationStore operations)
    : IQueryHandler<GetMyPayoutOperationQuery, EconomyPayoutOperationDto?>
{
    public Task<EconomyPayoutOperationDto?> Handle(
        GetMyPayoutOperationQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var operation = operations.GetForTenant(request.TenantId, request.OperationId);
            if (operation.PayeeId != request.PayeeId)
                return Task.FromResult<EconomyPayoutOperationDto?>(null);

            return Task.FromResult<EconomyPayoutOperationDto?>(new EconomyPayoutOperationDto(
                operation.Id,
                operation.Amount.Units,
                operation.State,
                operation.CreatedAt,
                operation.UpdatedAt));
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult<EconomyPayoutOperationDto?>(null);
        }
    }
}

public sealed class ListMyPayoutRequestsQueryHandler(IPayoutRequestStore requests)
    : IQueryHandler<ListMyPayoutRequestsQuery, IReadOnlyList<EconomyPayoutRequestDto>>
{
    public Task<IReadOnlyList<EconomyPayoutRequestDto>> Handle(
        ListMyPayoutRequestsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<EconomyPayoutRequestDto> result = requests
            .ListForPayee(request.TenantId, request.PayeeId, request.Take)
            .Select(EconomyPayoutRequestDto.From)
            .ToArray();
        return Task.FromResult(result);
    }
}

public sealed class ListPayoutRequestsForReviewQueryHandler(
    IActorContextAccessor actorContextAccessor,
    IPayoutRequestStore requests)
    : IQueryHandler<ListPayoutRequestsForReviewQuery, IReadOnlyList<EconomyPayoutRequestReviewDto>>
{
    public Task<IReadOnlyList<EconomyPayoutRequestReviewDto>> Handle(
        ListPayoutRequestsForReviewQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var tenantId = RequireWalletAdministratorTenant(actorContextAccessor);

        IReadOnlyList<EconomyPayoutRequestReviewDto> result = requests
            .ListForReview(tenantId, request.Take)
            .Select(EconomyPayoutRequestReviewDto.From)
            .ToArray();
        return Task.FromResult(result);
    }

    private static Guid RequireWalletAdministratorTenant(IActorContextAccessor actorContextAccessor)
    {
        var actor = actorContextAccessor.ActorContext;
        return actor.IsAuthenticated && actor.SubjectIdAsGuid.HasValue && actor.TenantId is { } tenantId &&
               actor.HasPermission(EconomyPermission.Keys.ReviewPayouts)
            ? tenantId
            : throw new UnauthorizedAccessException("The Economy payout-review permission is required.");
    }
}

public sealed class ListPayoutRequestReviewAuditQueryHandler(
    IActorContextAccessor actorContextAccessor,
    IPayoutRequestStore requests)
    : IQueryHandler<ListPayoutRequestReviewAuditQuery, IReadOnlyList<EconomyPayoutRequestReviewAuditDto>>
{
    public Task<IReadOnlyList<EconomyPayoutRequestReviewAuditDto>> Handle(
        ListPayoutRequestReviewAuditQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var tenantId = RequireWalletAdministratorTenant(actorContextAccessor);

        IReadOnlyList<EconomyPayoutRequestReviewAuditDto> result = requests
            .ListReviewAudit(request.RequestId, tenantId)
            .Select(EconomyPayoutRequestReviewAuditDto.From)
            .ToArray();
        return Task.FromResult(result);
    }

    private static Guid RequireWalletAdministratorTenant(IActorContextAccessor actorContextAccessor)
    {
        var actor = actorContextAccessor.ActorContext;
        return actor.IsAuthenticated && actor.SubjectIdAsGuid.HasValue && actor.TenantId is { } tenantId &&
               actor.HasPermission(EconomyPermission.Keys.ReviewPayouts)
            ? tenantId
            : throw new UnauthorizedAccessException("The Economy payout-review permission is required.");
    }
}
