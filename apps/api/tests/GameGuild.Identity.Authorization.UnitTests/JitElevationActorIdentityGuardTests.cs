using FluentAssertions;
using GameGuild.Identity.Authorization.Commands;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests;

/// <summary>
///     Guards for the JIT elevation command surface (issue #327, adversarial
///     finding F-3): acting identities come from the authenticated actor — never
///     from the request body — and approval additionally requires system or
///     same-tenant administrator authority. These are the unit-level twins of the
///     HTTP-level scenarios in GameGuild.API.SecurityTests/ElevationSeamAdversarialTests.
/// </summary>
public class JitElevationActorIdentityGuardTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();

    private static ActorContext ActorFor(Guid subjectId, Guid? tenantId, params string[] roles) =>
        ActorContextBuilder.Create()
            .WithActorKind(ActorKind.User)
            .WithSubjectId(subjectId.ToString())
            .WithTenantId(tenantId)
            .WithRoles(roles)
            .AsAuthenticated()
            .Build();

    private static ActorContext Unauthenticated() =>
        ActorContextBuilder.Create()
            .WithActorKind(ActorKind.User)
            .Build();

    private (Mock<IJitElevationService> Service,
        Mock<IJitElevationRequestRepository> Repository,
        Mock<IActorContextAccessor> Accessor) CreateDeps(ActorContext actor, JitElevationRequest? stored = null)
    {
        var service = new Mock<IJitElevationService>();
        var repository = new Mock<IJitElevationRequestRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(access => access.ActorContext).Returns(actor);
        return (service, repository, accessor);
    }

    private JitElevationRequest PendingElevation(Guid requesterId, Guid? tenantId) => new()
    {
        Id = Guid.NewGuid(),
        RequesterId = requesterId,
        TenantId = tenantId is null ? null : new GameGuild.CQRS.Models.TenantId(tenantId.Value),
        Permission = "features:read",
        Justification = "unit test",
        DurationMinutes = 30,
        Status = ElevationRequestStatus.Pending,
    };

    // ---------------------------------------------------------------------
    // Request: requester identity must be the actor
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Request_ForgedRequesterId_IsRejected()
    {
        var actorId = Guid.NewGuid();
        var (service, _, accessor) = CreateDeps(ActorFor(actorId, _tenantId));
        var handler = new RequestJitElevationHandler(
            service.Object, accessor.Object, NullLogger<RequestJitElevationHandler>.Instance);

        var act = () => handler.Handle(
            new RequestJitElevationCommand(Guid.NewGuid(), _tenantId, "features:read", "why", 30),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        service.Verify(
            svc => svc.RequestElevationAsync(
                It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Request_UnauthenticatedActor_IsRejected()
    {
        var (service, _, accessor) = CreateDeps(Unauthenticated());
        var handler = new RequestJitElevationHandler(
            service.Object, accessor.Object, NullLogger<RequestJitElevationHandler>.Instance);

        var act = () => handler.Handle(
            new RequestJitElevationCommand(Guid.NewGuid(), _tenantId, "features:read", "why", 30),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Request_OwnIdentity_IsForwarded()
    {
        var actorId = Guid.NewGuid();
        var (service, _, accessor) = CreateDeps(ActorFor(actorId, _tenantId));
        var handler = new RequestJitElevationHandler(
            service.Object, accessor.Object, NullLogger<RequestJitElevationHandler>.Instance);

        await handler.Handle(
            new RequestJitElevationCommand(actorId, _tenantId, "features:read", "why", 30),
            CancellationToken.None);

        service.Verify(
            svc => svc.RequestElevationAsync(
                actorId, _tenantId, "features:read", "why", 30,
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------------
    // Approve: reviewer identity must be the actor AND approval is admin-gated
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Approve_SpoofedReviewerId_IsRejected()
    {
        var adminId = Guid.NewGuid();
        var member = Guid.NewGuid();
        var (service, repository, accessor) = CreateDeps(
            ActorFor(adminId, _tenantId, "TenantAdmin"), PendingElevation(member, _tenantId));
        var handler = new ApproveJitElevationHandler(
            service.Object, repository.Object, accessor.Object, NullLogger<ApproveJitElevationHandler>.Instance);

        // The body names a different reviewer than the authenticated admin.
        var act = () => handler.Handle(
            new ApproveJitElevationCommand(Guid.NewGuid(), Guid.NewGuid(), "spoofed"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        service.Verify(
            svc => svc.ApproveRequestAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Approve_MemberSelfApprovalWithSpoofedReviewer_IsRejected()
    {
        // The exact escalation from the adversarial suite: the requesting member
        // approves their own request naming an arbitrary third reviewer id.
        var memberId = Guid.NewGuid();
        var (service, repository, accessor) = CreateDeps(
            ActorFor(memberId, _tenantId), PendingElevation(memberId, _tenantId));
        var handler = new ApproveJitElevationHandler(
            service.Object, repository.Object, accessor.Object, NullLogger<ApproveJitElevationHandler>.Instance);

        var act = () => handler.Handle(
            new ApproveJitElevationCommand(Guid.NewGuid(), Guid.NewGuid(), "self-approval"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        service.Verify(
            svc => svc.ApproveRequestAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Approve_MemberAsThemselves_IsStillDeniedWithoutAdminRole()
    {
        var memberId = Guid.NewGuid();
        var (service, repository, accessor) = CreateDeps(
            ActorFor(memberId, _tenantId), PendingElevation(memberId, _tenantId));
        var handler = new ApproveJitElevationHandler(
            service.Object, repository.Object, accessor.Object, NullLogger<ApproveJitElevationHandler>.Instance);

        // Even with an honest reviewer id, a plain member must not approve.
        var act = () => handler.Handle(
            new ApproveJitElevationCommand(Guid.NewGuid(), memberId, "honest but unauthorized"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Approve_TenantAdminOfAnotherTenant_IsDenied()
    {
        var adminId = Guid.NewGuid();
        var member = Guid.NewGuid();
        var (service, repository, accessor) = CreateDeps(
            ActorFor(adminId, _otherTenantId, "TenantAdmin"), PendingElevation(member, _tenantId));
        var handler = new ApproveJitElevationHandler(
            service.Object, repository.Object, accessor.Object, NullLogger<ApproveJitElevationHandler>.Instance);

        var act = () => handler.Handle(
            new ApproveJitElevationCommand(Guid.NewGuid(), adminId, "cross-tenant"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Approve_GlobalElevation_ByTenantAdmin_IsDenied()
    {
        var adminId = Guid.NewGuid();
        var member = Guid.NewGuid();
        var (service, repository, accessor) = CreateDeps(
            ActorFor(adminId, _tenantId, "TenantAdmin"), PendingElevation(member, tenantId: null));
        var handler = new ApproveJitElevationHandler(
            service.Object, repository.Object, accessor.Object, NullLogger<ApproveJitElevationHandler>.Instance);

        var act = () => handler.Handle(
            new ApproveJitElevationCommand(Guid.NewGuid(), adminId, "global"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Approve_SameTenantAdmin_IsForwardedWithActorIdentity()
    {
        var adminId = Guid.NewGuid();
        var member = Guid.NewGuid();
        var (service, repository, accessor) = CreateDeps(
            ActorFor(adminId, _tenantId, "TenantAdmin"), PendingElevation(member, _tenantId));
        var handler = new ApproveJitElevationHandler(
            service.Object, repository.Object, accessor.Object, NullLogger<ApproveJitElevationHandler>.Instance);

        var requestId = Guid.NewGuid();
        await handler.Handle(new ApproveJitElevationCommand(requestId, adminId, "ok"), CancellationToken.None);

        service.Verify(
            svc => svc.ApproveRequestAsync(requestId, adminId, "ok", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Approve_SystemAdmin_CanApproveGlobalElevation()
    {
        var adminId = Guid.NewGuid();
        var member = Guid.NewGuid();
        var (service, repository, accessor) = CreateDeps(
            ActorFor(adminId, _otherTenantId, "SystemAdmin"), PendingElevation(member, tenantId: null));
        var handler = new ApproveJitElevationHandler(
            service.Object, repository.Object, accessor.Object, NullLogger<ApproveJitElevationHandler>.Instance);

        await handler.Handle(new ApproveJitElevationCommand(Guid.NewGuid(), adminId, "global"), CancellationToken.None);

        service.Verify(
            svc => svc.ApproveRequestAsync(It.IsAny<Guid>(), adminId, "global", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------------
    // Deny / Revoke: identity from the actor (attribution cannot be spoofed)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Deny_SpoofedReviewerId_IsRejected()
    {
        var actorId = Guid.NewGuid();
        var (service, _, accessor) = CreateDeps(ActorFor(actorId, _tenantId));
        var handler = new DenyJitElevationHandler(
            service.Object, accessor.Object, NullLogger<DenyJitElevationHandler>.Instance);

        var act = () => handler.Handle(
            new DenyJitElevationCommand(Guid.NewGuid(), Guid.NewGuid(), "spoofed"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        service.Verify(
            svc => svc.DenyRequestAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Revoke_SpoofedRevokerId_IsRejected()
    {
        var actorId = Guid.NewGuid();
        var (service, _, accessor) = CreateDeps(ActorFor(actorId, _tenantId));
        var handler = new RevokeJitElevationHandler(
            service.Object, accessor.Object, NullLogger<RevokeJitElevationHandler>.Instance);

        var act = () => handler.Handle(
            new RevokeJitElevationCommand(Guid.NewGuid(), Guid.NewGuid(), "spoofed"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        service.Verify(
            svc => svc.RevokeElevationAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Revoke_OwnElevation_IsForwardedWithActorIdentity()
    {
        var actorId = Guid.NewGuid();
        var (service, _, accessor) = CreateDeps(ActorFor(actorId, _tenantId));
        var handler = new RevokeJitElevationHandler(
            service.Object, accessor.Object, NullLogger<RevokeJitElevationHandler>.Instance);

        var requestId = Guid.NewGuid();
        await handler.Handle(new RevokeJitElevationCommand(requestId, actorId, "done"), CancellationToken.None);

        service.Verify(
            svc => svc.RevokeElevationAsync(requestId, actorId, "done", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
