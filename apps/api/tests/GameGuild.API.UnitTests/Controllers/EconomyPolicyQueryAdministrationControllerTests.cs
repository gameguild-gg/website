using FluentAssertions;
using GameGuild.API.Controllers;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyPolicyQueryAdministrationControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task QueriesUseActorTenantAndNeverAcceptTenantAuthorityFromTheRequest()
    {
        var tenantId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var reader = new Mock<IEconomyPolicyQueryReader>(MockBehavior.Strict);
        reader.Setup(item => item.ListAsync(
                tenantId, EconomyValueMovementCapability.PayoutExecution, 25, "cursor", Now, default))
            .ReturnsAsync(new EconomyOperationalPage<EconomyCapabilityPolicyOperationalStatus>([], null));
        reader.Setup(item => item.FindAsync(tenantId, policyId, Now, default))
            .ReturnsAsync((EconomyPolicyOperationalDetails?)null);
        reader.Setup(item => item.ReadAuditAsync(tenantId, policyId, default))
            .ReturnsAsync(Array.Empty<EconomyPolicyAuditEntry>());
        var controller = CreateController(reader.Object, tenantId, authorized: true);

        (await controller.List(EconomyValueMovementCapability.PayoutExecution, 25, "cursor", default))
            .Should().BeOfType<OkObjectResult>();
        (await controller.Get(policyId, default)).Should().BeOfType<NotFoundResult>();
        (await controller.Audit(policyId, default)).Should().BeOfType<OkObjectResult>();
        reader.VerifyAll();
    }

    [Fact]
    public async Task EveryQueryRequiresThePolicyPermission()
    {
        var reader = new Mock<IEconomyPolicyQueryReader>(MockBehavior.Strict);
        var controller = CreateController(reader.Object, Guid.NewGuid(), authorized: false);

        (await controller.List(null, 20, null, default)).Should().BeOfType<ForbidResult>();
        (await controller.Get(Guid.NewGuid(), default)).Should().BeOfType<ForbidResult>();
        (await controller.Audit(Guid.NewGuid(), default)).Should().BeOfType<ForbidResult>();
        reader.VerifyNoOtherCalls();
    }

    private static EconomyPolicyQueryAdministrationController CreateController(
        IEconomyPolicyQueryReader reader,
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
                ? new HashSet<string> { EconomyPermission.Keys.ManagePolicies }
                : new HashSet<string>(),
            Roles = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            IsAuthenticated = true
        });
        return new EconomyPolicyQueryAdministrationController(
            reader, accessor, new FixedTimeProvider());
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
