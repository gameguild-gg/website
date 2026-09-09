using FluentAssertions;
using GameGuild.API.Controllers;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyReserveQueryAdministrationControllerTests
{
    [Fact]
    public async Task QueriesUseActorTenantAndReturnNotFoundForMissingDetails()
    {
        var tenantId = Guid.NewGuid();
        var observationId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var reader = new Mock<IEconomyReserveQueryReader>(MockBehavior.Strict);
        reader.Setup(item => item.ListCustodyAsync(tenantId, 25, "custody", default))
            .ReturnsAsync(new EconomyOperationalPage<EconomyCustodyObservationOperationalStatus>([], null));
        reader.Setup(item => item.FindCustodyAsync(tenantId, observationId, default))
            .ReturnsAsync((EconomyCustodyObservationOperationalStatus?)null);
        reader.Setup(item => item.ListProposalsAsync(tenantId, 30, "reserve", default))
            .ReturnsAsync(new EconomyOperationalPage<EconomyReserveProposalOperationalStatus>([], null));
        reader.Setup(item => item.FindProposalAsync(tenantId, proposalId, default))
            .ReturnsAsync((EconomyReserveProposalOperationalStatus?)null);
        reader.Setup(item => item.ReadActiveHeadAsync(tenantId, default))
            .ReturnsAsync((EconomyActiveReserveOperationalDetails?)null);
        var controller = CreateController(reader.Object, tenantId, authorized: true);

        (await controller.ListCustody(25, "custody", default)).Should().BeOfType<OkObjectResult>();
        (await controller.GetCustody(observationId, default)).Should().BeOfType<NotFoundResult>();
        (await controller.ListProposals(30, "reserve", default)).Should().BeOfType<OkObjectResult>();
        (await controller.GetProposal(proposalId, default)).Should().BeOfType<NotFoundResult>();
        (await controller.GetActiveHead(default)).Should().BeOfType<NotFoundResult>();
        reader.VerifyAll();
    }

    [Fact]
    public async Task EveryQueryRequiresTheReservePermission()
    {
        var reader = new Mock<IEconomyReserveQueryReader>(MockBehavior.Strict);
        var controller = CreateController(reader.Object, Guid.NewGuid(), authorized: false);

        (await controller.ListCustody(20, null, default)).Should().BeOfType<ForbidResult>();
        (await controller.GetCustody(Guid.NewGuid(), default)).Should().BeOfType<ForbidResult>();
        (await controller.ListProposals(20, null, default)).Should().BeOfType<ForbidResult>();
        (await controller.GetProposal(Guid.NewGuid(), default)).Should().BeOfType<ForbidResult>();
        (await controller.GetActiveHead(default)).Should().BeOfType<ForbidResult>();
        reader.VerifyNoOtherCalls();
    }

    private static EconomyReserveQueryAdministrationController CreateController(
        IEconomyReserveQueryReader reader,
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
                ? new HashSet<string> { EconomyPermission.Keys.ManageReserves }
                : new HashSet<string>(),
            Roles = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            IsAuthenticated = true
        });
        return new EconomyReserveQueryAdministrationController(reader, accessor);
    }
}
