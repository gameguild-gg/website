using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Command to soft-delete a pricing rule (issue #395). The soft delete touches the rule's
/// timestamps, which invalidates the pricing engine's cached rule sets for the affected product.
/// </summary>
/// <param name="RuleId">Rule ID to delete</param>
[AuthorizeRequest(MonetizationPermission.Keys.Monetize)]
public sealed record DeletePricingRuleCommand(Guid RuleId) : ICommand<Unit>;

/// <summary>
/// Handler for soft-deleting a pricing rule.
/// </summary>
public sealed class DeletePricingRuleCommandHandler(IPricingRuleRepository pricingRuleRepository)
    : ICommandHandler<DeletePricingRuleCommand, Unit>
{
    public async Task<Unit> Handle(DeletePricingRuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var deleted = await pricingRuleRepository.DeleteAsync(request.RuleId, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            throw new PricingRuleNotFoundException(request.RuleId);
        }

        return Unit.Value;
    }
}
