using FluentAssertions;
using GameGuild.API.Controllers;
using GameGuild.Finance.Economy.Marketplace;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyMarketplaceQueryAdministrationControllerTests
{
    [Fact]
    public async Task QueriesBindActorTenantAndReturnNotFoundWithoutCrossTenantLeakage()
    {
        var tenantId = Guid.NewGuid();
        var settlementId = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        var reader = new Mock<IMarketplaceOperationalQueryReader>(MockBehavior.Strict);
        reader.Setup(item => item.ListSettlementsAsync(
                tenantId, MarketplaceSettlementStatus.Settled, 25, "settlements", default))
            .ReturnsAsync(new EconomyOperationalPage<MarketplaceSettlementOperationalSummary>([], null));
        reader.Setup(item => item.FindSettlementAsync(tenantId, settlementId, default))
            .ReturnsAsync((MarketplaceSettlementOperationalDetails?)null);
        reader.Setup(item => item.ListRefundsAsync(tenantId, 30, "refunds", default))
            .ReturnsAsync(new EconomyOperationalPage<MarketplaceRefundOperationalStatus>([], null));
        reader.Setup(item => item.FindRefundAsync(tenantId, refundId, default))
            .ReturnsAsync((MarketplaceRefundOperationalStatus?)null);
        reader.Setup(item => item.ListOutboxAsync(tenantId, false, 35, "outbox", default))
            .ReturnsAsync(new EconomyOperationalPage<MarketplaceOutboxOperationalStatus>([], null));
        var controller = CreateController(reader.Object, tenantId, authorized: true);

        (await controller.ListSettlements(
            MarketplaceSettlementStatus.Settled, 25, "settlements", default)).Should().BeOfType<OkObjectResult>();
        (await controller.GetSettlement(settlementId, default)).Should().BeOfType<NotFoundResult>();
        (await controller.ListRefunds(30, "refunds", default)).Should().BeOfType<OkObjectResult>();
        (await controller.GetRefund(refundId, default)).Should().BeOfType<NotFoundResult>();
        (await controller.ListOutbox(false, 35, "outbox", default)).Should().BeOfType<OkObjectResult>();
        reader.VerifyAll();
    }

    [Fact]
    public async Task EveryQueryRequiresTheMarketplacePermission()
    {
        var reader = new Mock<IMarketplaceOperationalQueryReader>(MockBehavior.Strict);
        var controller = CreateController(reader.Object, Guid.NewGuid(), authorized: false);

        (await controller.ListSettlements(null, 20, null, default)).Should().BeOfType<ForbidResult>();
        (await controller.GetSettlement(Guid.NewGuid(), default)).Should().BeOfType<ForbidResult>();
        (await controller.ListRefunds(20, null, default)).Should().BeOfType<ForbidResult>();
        (await controller.GetRefund(Guid.NewGuid(), default)).Should().BeOfType<ForbidResult>();
        (await controller.ListOutbox(null, 20, null, default)).Should().BeOfType<ForbidResult>();
        reader.VerifyNoOtherCalls();
    }

    private static EconomyMarketplaceQueryAdministrationController CreateController(
        IMarketplaceOperationalQueryReader reader,
        Guid tenantId,
        bool authorized)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Permissions = authorized
                ? new HashSet<string> { EconomyPermission.Keys.OperateMarketplace }
                : new HashSet<string>(),
            Roles = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            IsAuthenticated = true
        });
        return new EconomyMarketplaceQueryAdministrationController(reader, accessor);
    }
}
