using FluentAssertions;
using GameGuild.API.Controllers;
using GameGuild.Commerce.Orders;
using GameGuild.Economy.Marketplace;
using GameGuild.Economy.Risk;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyMarketplaceControllerContractTests
{
    [Fact]
    public void SelfServiceBodiesExposeOnlyBusinessIntent()
    {
        typeof(SettleMyMarketplaceOrderRequest).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["CurrencyChoice", "IdempotencyKey"]);
        typeof(RefundMarketplaceSettlementRequest).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["Quantity", "ReasonCode", "IdempotencyKey"]);
        typeof(CompleteOrderMarketplaceSettlement).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["CurrencyChoice", "IdempotencyKey"]);
        typeof(SettleAuthoritativeMarketplaceOrderRequest).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["OrderId", "CurrencyChoice", "IdempotencyKey", "SettledAt"]);
        typeof(RefundAuthoritativeMarketplaceOrderRequest).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo([
                "Authority", "SettlementId", "Quantity", "ReasonCode", "IdempotencyKey", "RefundedAt"
            ]);
    }

    [Theory]
    [InlineData(EconomyProtectedOperationState.Denied, 403)]
    [InlineData(EconomyProtectedOperationState.ReviewRequired, 409)]
    [InlineData(EconomyProtectedOperationState.Hold, 409)]
    [InlineData(EconomyProtectedOperationState.ComplianceUnavailable, 503)]
    public async Task ProtectedOperationStatesAreReturnedAsStructuredResponses(
        EconomyProtectedOperationState state,
        int expectedStatus)
    {
        var actor = new ActorContextAccessor();
        actor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });
        Guid? reviewId = state == EconomyProtectedOperationState.ReviewRequired ? Guid.NewGuid() : null;
        var settlements = new Mock<IDurableMarketplaceSettlementService>(MockBehavior.Strict);
        settlements.Setup(service => service.SettleAsync(
                It.IsAny<SettleAuthoritativeMarketplaceOrderRequest>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromException<DurableMarketplaceSettlementResult>(
                new EconomyProtectedOperationException(state, reviewId, ["not-ready"])));
        var controller = new EconomyMarketplaceController(
            EconomyHandlerSenders.Public(marketplaceSettlements: settlements.Object),
            actor,
            TimeProvider.System);

        var result = await controller.Settle(
            Guid.NewGuid(),
            new SettleMyMarketplaceOrderRequest(MarketplaceCurrencyChoice.Hard, "settlement-1"),
            CancellationToken.None);

        var response = result.Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(expectedStatus);
        response.Value.Should().BeEquivalentTo(new
        {
            State = state,
            ReviewId = reviewId,
            Diagnostics = new[] { "not-ready" }
        });
    }
}
