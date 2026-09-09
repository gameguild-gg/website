using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

public sealed record CheckMultipleEntitlementsCommand(
    Guid UserId,
    IEnumerable<Guid> ProductIds) : ICommand<IDictionary<Guid, bool>>;
public sealed record GrantEntitlementCommand(
    Guid UserId,
    Guid ProductId,
    ProductAcquisitionType AcquisitionType,
    decimal PricePaid,
    string Currency,
    DateTime? ExpiresAt) : ICommand<GrantEntitlementCommandResult>;
public sealed record GrantEntitlementCommandResult(EntitlementResult Result, EntitlementInfo? Entitlement);
public sealed record RevokeEntitlementCommand(
    Guid EntitlementId,
    Guid UserId,
    Guid ProductId,
    string? Reason) : ICommand<bool>;

public sealed class EntitlementMutationCommandHandler(IEntitlementService entitlementService) :
    ICommandHandler<CheckMultipleEntitlementsCommand, IDictionary<Guid, bool>>,
    ICommandHandler<GrantEntitlementCommand, GrantEntitlementCommandResult>,
    ICommandHandler<RevokeEntitlementCommand, bool>
{
    public Task<IDictionary<Guid, bool>> Handle(
        CheckMultipleEntitlementsCommand command,
        CancellationToken cancellationToken) =>
        entitlementService.HasAccessAsync(command.UserId, command.ProductIds, cancellationToken);

    public async Task<GrantEntitlementCommandResult> Handle(
        GrantEntitlementCommand command,
        CancellationToken cancellationToken)
    {
        var result = await entitlementService.GrantEntitlementAsync(
            command.UserId,
            command.ProductId,
            command.AcquisitionType,
            command.PricePaid,
            command.Currency,
            command.ExpiresAt,
            orderId: null,
            cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            return new(result, null);

        var entitlements = await entitlementService.GetUserEntitlementsAsync(
            command.UserId,
            cancellationToken).ConfigureAwait(false);
        return new(result, entitlements.FirstOrDefault(entitlement => entitlement.ProductId == command.ProductId));
    }

    public Task<bool> Handle(RevokeEntitlementCommand command, CancellationToken cancellationToken) =>
        entitlementService.RevokeEntitlementAsync(
            command.UserId,
            command.ProductId,
            command.Reason,
            cancellationToken);
}
