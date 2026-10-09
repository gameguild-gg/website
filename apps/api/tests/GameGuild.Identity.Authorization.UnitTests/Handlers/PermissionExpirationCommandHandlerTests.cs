using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Handlers;

public class PermissionExpirationCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly Mock<IPermissionExpirationService> _serviceMock = new();
    private readonly Mock<IActorContextAccessor> _actorAccessorMock = new();

    private SetTenantPermissionExpirationCommandHandler CreateSetHandler() => new(
        _serviceMock.Object,
        _actorAccessorMock.Object,
        NullLogger<SetTenantPermissionExpirationCommandHandler>.Instance);

    private ExtendTenantPermissionExpirationCommandHandler CreateExtendHandler() => new(
        _serviceMock.Object,
        _actorAccessorMock.Object,
        NullLogger<ExtendTenantPermissionExpirationCommandHandler>.Instance);

    private ProcessExpiredPermissionsCommandHandler CreateProcessHandler() => new(
        _serviceMock.Object,
        _actorAccessorMock.Object,
        NullLogger<ProcessExpiredPermissionsCommandHandler>.Instance);

    private SendExpirationRemindersCommandHandler CreateRemindersHandler() => new(
        _serviceMock.Object,
        _actorAccessorMock.Object,
        NullLogger<SendExpirationRemindersCommandHandler>.Instance);

    private void SetActor(
        bool authenticated,
        Guid? tenantId = null,
        string[]? roles = null,
        string[]? permissions = null)
    {
        var actor = new ActorContext
        {
            ActorKind = authenticated ? ActorKind.User : ActorKind.Anonymous,
            SubjectId = authenticated ? Guid.NewGuid().ToString() : null,
            TenantId = tenantId,
            Roles = new HashSet<string>(roles ?? Array.Empty<string>(), StringComparer.Ordinal),
            Permissions = new HashSet<string>(permissions ?? Array.Empty<string>(), StringComparer.Ordinal),
            IsAuthenticated = authenticated
        };

        _actorAccessorMock.SetupGet(x => x.ActorContext).Returns(actor);
    }

    // ── SetTenantPermissionExpirationCommand ───────────────────

    [Fact]
    public async Task SetExpiration_SameTenantAdminIsAllowed()
    {
        SetActor(true, TenantId, roles: new[] { "TenantAdmin" });

        _serviceMock
            .Setup(x => x.SetExpirationAsync(TenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission>());

        var result = await CreateSetHandler().Handle(
            new SetTenantPermissionExpirationCommand
            {
                TenantId = new TenantId(TenantId),
                PermissionIds = new[] { Guid.NewGuid() },
                ExpiresAt = SystemClock.UtcNow.AddDays(3)
            },
            CancellationToken.None);

        result.Should().Be(0);
    }

    [Fact]
    public async Task SetExpiration_CrossTenantAdminIsRejected()
    {
        SetActor(true, Guid.NewGuid(), roles: new[] { "TenantAdmin" });

        var act = () => CreateSetHandler().Handle(
            new SetTenantPermissionExpirationCommand
            {
                TenantId = new TenantId(TenantId),
                PermissionIds = new[] { Guid.NewGuid() },
                ExpiresAt = SystemClock.UtcNow.AddDays(3)
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _serviceMock.Verify(x => x.SetExpirationAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetExpiration_AnonymousActorIsRejected()
    {
        SetActor(false);

        var act = () => CreateSetHandler().Handle(
            new SetTenantPermissionExpirationCommand
            {
                TenantId = new TenantId(TenantId),
                PermissionIds = new[] { Guid.NewGuid() },
                ExpiresAt = SystemClock.UtcNow.AddDays(3)
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SetExpiration_GlobalDefaultRequiresManageGlobalDefaults()
    {
        SetActor(true, TenantId, roles: new[] { "TenantAdmin" });

        var act = () => CreateSetHandler().Handle(
            new SetTenantPermissionExpirationCommand
            {
                TenantId = new TenantId(Guid.Empty),
                PermissionIds = new[] { Guid.NewGuid() },
                ExpiresAt = SystemClock.UtcNow.AddDays(3)
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SetExpiration_SystemAdminIsAllowedForAnyTenant()
    {
        SetActor(true, Guid.NewGuid(), roles: new[] { "SystemAdmin" });

        _serviceMock
            .Setup(x => x.SetExpirationAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission>());

        var result = await CreateSetHandler().Handle(
            new SetTenantPermissionExpirationCommand
            {
                TenantId = new TenantId(TenantId),
                PermissionIds = new[] { Guid.NewGuid() },
                ExpiresAt = SystemClock.UtcNow.AddDays(3)
            },
            CancellationToken.None);

        result.Should().Be(0);
    }

    // ── ExtendTenantPermissionExpirationCommand ────────────────

    [Fact]
    public async Task ExtendExpiration_SameTenantAdminIsAllowed()
    {
        SetActor(true, TenantId, roles: new[] { "Owner" });

        _serviceMock
            .Setup(x => x.ExtendExpirationAsync(TenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<TimeSpan>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission>());

        var result = await CreateExtendHandler().Handle(
            new ExtendTenantPermissionExpirationCommand
            {
                TenantId = new TenantId(TenantId),
                PermissionIds = new[] { Guid.NewGuid() },
                Extension = TimeSpan.FromDays(7)
            },
            CancellationToken.None);

        result.Should().Be(0);
    }

    [Fact]
    public async Task ExtendExpiration_PlainUserIsRejected()
    {
        SetActor(true, TenantId);

        var act = () => CreateExtendHandler().Handle(
            new ExtendTenantPermissionExpirationCommand
            {
                TenantId = new TenantId(TenantId),
                PermissionIds = new[] { Guid.NewGuid() },
                Extension = TimeSpan.FromDays(7)
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── ProcessExpiredPermissionsCommand ───────────────────────

    [Fact]
    public async Task ProcessExpired_RequiresSystemAdmin()
    {
        SetActor(true, TenantId, roles: new[] { "TenantAdmin" });

        var act = () => CreateProcessHandler().Handle(new ProcessExpiredPermissionsCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ProcessExpired_SystemAdminTriggersProcessing()
    {
        SetActor(true, roles: new[] { "SystemAdmin" });

        _serviceMock
            .Setup(x => x.ProcessExpiredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var result = await CreateProcessHandler().Handle(new ProcessExpiredPermissionsCommand(), CancellationToken.None);

        result.Should().Be(4);
    }

    // ── SendExpirationRemindersCommand ─────────────────────────

    [Fact]
    public async Task SendReminders_RequiresSystemAdmin()
    {
        SetActor(false);

        var act = () => CreateRemindersHandler().Handle(new SendExpirationRemindersCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SendReminders_SystemAdminPublishesReminders()
    {
        SetActor(true, roles: new[] { "SystemAdmin" });

        _serviceMock
            .Setup(x => x.SendUpcomingExpirationRemindersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await CreateRemindersHandler().Handle(new SendExpirationRemindersCommand(), CancellationToken.None);

        result.Should().Be(2);
    }
}
