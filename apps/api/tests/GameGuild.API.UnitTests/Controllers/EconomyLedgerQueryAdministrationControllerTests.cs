using FluentAssertions;
using GameGuild.API.Controllers;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyLedgerQueryAdministrationControllerTests
{
    [Fact]
    public async Task QueriesUseActorTenantAndReturnSafeMissingResults()
    {
        var tenantId = Guid.NewGuid();
        var verificationId = Guid.NewGuid();
        var anchorId = Guid.NewGuid();
        const long generation = 5;
        var reader = new Mock<IEconomyLedgerQueryReader>(MockBehavior.Strict);
        reader.Setup(item => item.ListVerificationsAsync(tenantId, 10, "verification", default))
            .ReturnsAsync(new EconomyOperationalPage<EconomyJournalVerificationRunDetails>([], null));
        reader.Setup(item => item.FindVerificationAsync(tenantId, verificationId, default))
            .ReturnsAsync((EconomyJournalVerificationRunDetails?)null);
        reader.Setup(item => item.ListAnchorsAsync(tenantId, 20, "anchor", default))
            .ReturnsAsync(new EconomyOperationalPage<EconomyAnchorOperationalDetails>([], null));
        reader.Setup(item => item.FindAnchorAsync(tenantId, anchorId, default))
            .ReturnsAsync((EconomyAnchorOperationalDetails?)null);
        reader.Setup(item => item.ReadAnchorVerificationsAsync(tenantId, anchorId, default))
            .ReturnsAsync(Array.Empty<EconomyAnchorVerificationOperationalStatus>());
        reader.Setup(item => item.ListProjectionsAsync(tenantId, 30, "projection", default))
            .ReturnsAsync(new EconomyOperationalPage<EconomyProjectionGenerationOperationalDetails>([], null));
        reader.Setup(item => item.FindProjectionAsync(tenantId, generation, default))
            .ReturnsAsync((EconomyProjectionGenerationOperationalDetails?)null);
        reader.Setup(item => item.ReadProjectionAuditAsync(tenantId, generation, default))
            .ReturnsAsync(Array.Empty<EconomyProjectionApprovalAuditEntry>());
        var controller = CreateController(reader.Object, tenantId, authorized: true);

        (await controller.ListVerifications(10, "verification", default)).Should().BeOfType<OkObjectResult>();
        (await controller.GetVerification(verificationId, default)).Should().BeOfType<NotFoundResult>();
        (await controller.ListAnchors(20, "anchor", default)).Should().BeOfType<OkObjectResult>();
        (await controller.GetAnchor(anchorId, default)).Should().BeOfType<NotFoundResult>();
        (await controller.ListAnchorVerifications(anchorId, default)).Should().BeOfType<OkObjectResult>();
        (await controller.ListProjections(30, "projection", default)).Should().BeOfType<OkObjectResult>();
        (await controller.GetProjection(generation, default)).Should().BeOfType<NotFoundResult>();
        (await controller.GetProjectionAudit(generation, default)).Should().BeOfType<OkObjectResult>();
        reader.VerifyAll();
    }

    [Fact]
    public async Task EveryQueryRequiresTheLedgerPermission()
    {
        var reader = new Mock<IEconomyLedgerQueryReader>(MockBehavior.Strict);
        var controller = CreateController(reader.Object, Guid.NewGuid(), authorized: false);

        (await controller.ListVerifications(20, null, default)).Should().BeOfType<ForbidResult>();
        (await controller.GetVerification(Guid.NewGuid(), default)).Should().BeOfType<ForbidResult>();
        (await controller.ListAnchors(20, null, default)).Should().BeOfType<ForbidResult>();
        (await controller.GetAnchor(Guid.NewGuid(), default)).Should().BeOfType<ForbidResult>();
        (await controller.ListAnchorVerifications(Guid.NewGuid(), default)).Should().BeOfType<ForbidResult>();
        (await controller.ListProjections(20, null, default)).Should().BeOfType<ForbidResult>();
        (await controller.GetProjection(1, default)).Should().BeOfType<ForbidResult>();
        (await controller.GetProjectionAudit(1, default)).Should().BeOfType<ForbidResult>();
        reader.VerifyNoOtherCalls();
    }

    private static EconomyLedgerQueryAdministrationController CreateController(
        IEconomyLedgerQueryReader reader,
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
                ? new HashSet<string> { EconomyPermission.Keys.OperateLedger }
                : new HashSet<string>(),
            Roles = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            IsAuthenticated = true
        });
        return new EconomyLedgerQueryAdministrationController(reader, accessor);
    }
}
