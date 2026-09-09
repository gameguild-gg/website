using FluentAssertions;
using GameGuild.API.Controllers;
using GameGuild.Finance.Economy.AdRewards;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyAdRewardQueryAdministrationControllerTests
{
    [Fact]
    public async Task QueriesBindActorTenantAndReturnNotFoundWithoutCrossTenantLeakage()
    {
        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var reader = new Mock<IAdRewardOperationalQueryReader>(MockBehavior.Strict);
        reader.Setup(item => item.ListSessionsAsync(
                tenantId, DurableAdRewardSessionState.Deferred, "google", 25, "sessions", default))
            .ReturnsAsync(new EconomyOperationalPage<AdRewardSessionOperationalSummary>([], null));
        reader.Setup(item => item.FindSessionAsync(tenantId, sessionId, default))
            .ReturnsAsync((AdRewardSessionOperationalDetails?)null);
        reader.Setup(item => item.ListPendingClaimsAsync(tenantId, false, 30, "claims", default))
            .ReturnsAsync(new EconomyOperationalPage<AdRewardPendingClaimOperationalStatus>([], null));
        reader.Setup(item => item.ListReconciliationsAsync(tenantId, "google", 35, "reconciliations", default))
            .ReturnsAsync(new EconomyOperationalPage<AdRewardReconciliationOperationalStatus>([], null));
        var controller = CreateController(reader.Object, tenantId, authorized: true);

        (await controller.ListSessions(
            DurableAdRewardSessionState.Deferred, "google", 25, "sessions", default))
            .Should().BeOfType<OkObjectResult>();
        (await controller.GetSession(sessionId, default)).Should().BeOfType<NotFoundResult>();
        (await controller.ListPendingClaims(false, 30, "claims", default)).Should().BeOfType<OkObjectResult>();
        (await controller.ListReconciliations("google", 35, "reconciliations", default))
            .Should().BeOfType<OkObjectResult>();
        reader.VerifyAll();
    }

    [Fact]
    public async Task EveryQueryRequiresTheAdRewardsPermission()
    {
        var reader = new Mock<IAdRewardOperationalQueryReader>(MockBehavior.Strict);
        var controller = CreateController(reader.Object, Guid.NewGuid(), authorized: false);

        (await controller.ListSessions(null, null, 20, null, default)).Should().BeOfType<ForbidResult>();
        (await controller.GetSession(Guid.NewGuid(), default)).Should().BeOfType<ForbidResult>();
        (await controller.ListPendingClaims(null, 20, null, default)).Should().BeOfType<ForbidResult>();
        (await controller.ListReconciliations(null, 20, null, default)).Should().BeOfType<ForbidResult>();
        reader.VerifyNoOtherCalls();
    }

    private static EconomyAdRewardQueryAdministrationController CreateController(
        IAdRewardOperationalQueryReader reader,
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
                ? new HashSet<string> { EconomyPermission.Keys.OperateAdRewards }
                : new HashSet<string>(),
            Roles = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            IsAuthenticated = true
        });
        return new EconomyAdRewardQueryAdministrationController(reader, accessor);
    }
}
