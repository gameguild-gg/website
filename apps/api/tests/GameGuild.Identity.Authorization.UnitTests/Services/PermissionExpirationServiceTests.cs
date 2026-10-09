using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

public class PermissionExpirationServiceTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private readonly Mock<ITenantPermissionRepository> _repoMock = new();
    private readonly Mock<IPermissionAuditService> _auditMock = new();
    private readonly Mock<ITenantSecurityVersionStore> _versionStoreMock = new();
    private readonly Mock<IPublisher> _publisherMock = new();
    private readonly Mock<IActorContextAccessor> _actorAccessorMock = new();
    private readonly PermissionExpirationOptions _options = new();
    private readonly PermissionExpirationService _sut;

    public PermissionExpirationServiceTests()
    {
        _versionStoreMock
            .Setup(x => x.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);

        _auditMock
            .Setup(x => x.LogPermissionChangeAsync(
                It.IsAny<PermissionOperationType>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionAuditLog());

        _publisherMock
            .Setup(x => x.Publish(It.IsAny<PermissionExpirationNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _actorAccessorMock.SetupGet(x => x.ActorContext).Returns(ActorContext.Anonymous);

        _sut = CreateService();
    }

    private PermissionExpirationService CreateService() => new(
        _repoMock.Object,
        _auditMock.Object,
        _versionStoreMock.Object,
        Options.Create(_options),
        NullLogger<PermissionExpirationService>.Instance,
        _publisherMock.Object,
        _actorAccessorMock.Object);

    private static TenantPermission CreateGrant(
        Guid tenantId,
        DateTime? expiresAt,
        bool isActive = true,
        Dictionary<string, object>? metadata = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TenantId = tenantId,
            Permissions = new[] { "courses:read" },
            ExpiresAt = expiresAt,
            IsActive = isActive,
            Metadata = metadata
        };

    // ── ProcessExpiredAsync ────────────────────────────────────

    [Fact]
    public async Task ProcessExpiredAsync_DeactivatesExpiredGrantAuditsAndBumpsTenantVersion()
    {
        var grant = CreateGrant(TenantA, SystemClock.UtcNow.AddHours(-1));

        _repoMock
            .Setup(x => x.GetExpiredPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission> { grant });
        _repoMock
            .Setup(x => x.UpdateAsync(It.IsAny<TenantPermission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission p, CancellationToken _) => p);

        var processed = await _sut.ProcessExpiredAsync();

        processed.Should().Be(1);
        grant.IsActive.Should().BeFalse();
        grant.ExpiresAt.Should().NotBeNull().And.BeOnOrBefore(SystemClock.UtcNow);

        _auditMock.Verify(
            x => x.LogPermissionChangeAsync(
                PermissionOperationType.Expire,
                grant.UserId,
                Guid.Empty,
                TenantA,
                It.IsAny<string?>(), grant.Id, It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _versionStoreMock.Verify(
            x => x.IncrementVersionAsync(TenantA.ToString(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessExpiredAsync_PublishesExpiredNotification()
    {
        var expiresAt = SystemClock.UtcNow.AddMinutes(-5);
        var grant = CreateGrant(TenantA, expiresAt);

        _repoMock
            .Setup(x => x.GetExpiredPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission> { grant });
        _repoMock
            .Setup(x => x.UpdateAsync(It.IsAny<TenantPermission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission p, CancellationToken _) => p);

        await _sut.ProcessExpiredAsync();

        _publisherMock.Verify(
            x => x.Publish(
                It.Is<PermissionExpirationNotification>(n =>
                    n.PermissionId == grant.Id &&
                    n.Kind == PermissionExpirationKind.Expired &&
                    n.ExpiresAt == expiresAt &&
                    n.TenantId == TenantA),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessExpiredAsync_SkipsGrantsAlreadyProcessedByAPreviousCycle()
    {
        var grant = CreateGrant(
            TenantA,
            SystemClock.UtcNow.AddHours(-1),
            metadata: new Dictionary<string, object>
            {
                ["expirationProcessedAt"] = SystemClock.UtcNow.AddMinutes(-10).ToString("O")
            });

        _repoMock
            .Setup(x => x.GetExpiredPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission> { grant });

        var processed = await _sut.ProcessExpiredAsync();

        processed.Should().Be(0);
        _repoMock.Verify(x => x.UpdateAsync(It.IsAny<TenantPermission>(), It.IsAny<CancellationToken>()), Times.Never);
        _versionStoreMock.Verify(x => x.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessExpiredAsync_ContinuesWhenASingleGrantFails()
    {
        var failing = CreateGrant(TenantA, SystemClock.UtcNow.AddHours(-1));
        var succeeding = CreateGrant(TenantB, SystemClock.UtcNow.AddHours(-1));

        _repoMock
            .Setup(x => x.GetExpiredPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission> { failing, succeeding });
        _repoMock
            .Setup(x => x.UpdateAsync(It.IsAny<TenantPermission>(), It.IsAny<CancellationToken>()))
            .Returns((TenantPermission p, CancellationToken _) => ReferenceEquals(p, failing)
                ? Task.FromException<TenantPermission>(new InvalidOperationException("boom"))
                : Task.FromResult(p));

        var processed = await _sut.ProcessExpiredAsync();

        processed.Should().Be(1);
        succeeding.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessExpiredAsync_BumpsVersionOncePerTenant()
    {
        var first = CreateGrant(TenantA, SystemClock.UtcNow.AddHours(-1));
        var second = CreateGrant(TenantA, SystemClock.UtcNow.AddMinutes(-30));

        _repoMock
            .Setup(x => x.GetExpiredPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission> { first, second });
        _repoMock
            .Setup(x => x.UpdateAsync(It.IsAny<TenantPermission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission p, CancellationToken _) => p);

        await _sut.ProcessExpiredAsync();

        _versionStoreMock.Verify(
            x => x.IncrementVersionAsync(TenantA.ToString(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── SendUpcomingExpirationRemindersAsync ───────────────────

    [Fact]
    public async Task SendUpcomingExpirationRemindersAsync_PublishesReminderAndStampsGrant()
    {
        var grant = CreateGrant(TenantA, SystemClock.UtcNow.AddDays(2));

        _repoMock
            .Setup(x => x.GetExpiringBeforeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission> { grant });
        _repoMock
            .Setup(x => x.UpdateAsync(It.IsAny<TenantPermission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission p, CancellationToken _) => p);

        var published = await _sut.SendUpcomingExpirationRemindersAsync();

        published.Should().Be(1);
        grant.Metadata.Should().ContainKey("expirationReminderAt");

        _publisherMock.Verify(
            x => x.Publish(
                It.Is<PermissionExpirationNotification>(n =>
                    n.PermissionId == grant.Id &&
                    n.Kind == PermissionExpirationKind.Upcoming &&
                    n.ExpiresAt == grant.ExpiresAt),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Reminders are informational only: no authorization mutation, so no version bump.
        _versionStoreMock.Verify(x => x.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendUpcomingExpirationRemindersAsync_SkipsRecentlyRemindedGrants()
    {
        var grant = CreateGrant(
            TenantA,
            SystemClock.UtcNow.AddDays(2),
            metadata: new Dictionary<string, object>
            {
                ["expirationReminderAt"] = SystemClock.UtcNow.AddHours(-1).ToString("O")
            });

        _repoMock
            .Setup(x => x.GetExpiringBeforeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission> { grant });

        var published = await _sut.SendUpcomingExpirationRemindersAsync();

        published.Should().Be(0);
        _publisherMock.Verify(
            x => x.Publish(It.IsAny<PermissionExpirationNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendUpcomingExpirationRemindersAsync_RemindsAgainAfterMinimumInterval()
    {
        var grant = CreateGrant(
            TenantA,
            SystemClock.UtcNow.AddDays(2),
            metadata: new Dictionary<string, object>
            {
                ["expirationReminderAt"] = SystemClock.UtcNow.AddDays(-2).ToString("O")
            });

        _repoMock
            .Setup(x => x.GetExpiringBeforeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission> { grant });
        _repoMock
            .Setup(x => x.UpdateAsync(It.IsAny<TenantPermission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission p, CancellationToken _) => p);

        var published = await _sut.SendUpcomingExpirationRemindersAsync();

        published.Should().Be(1);
    }

    // ── SetExpirationAsync ─────────────────────────────────────

    [Fact]
    public async Task SetExpirationAsync_SetsFutureExpirationAndBumpsVersionAndAudits()
    {
        var grant = CreateGrant(TenantA, null);
        var newExpiry = SystemClock.UtcNow.AddDays(7);

        _repoMock
            .Setup(x => x.GetByIdsInTenantAsync(TenantA, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission> { grant });
        _repoMock
            .Setup(x => x.UpdateAsync(It.IsAny<TenantPermission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission p, CancellationToken _) => p);

        var updated = await _sut.SetExpirationAsync(TenantA, new[] { grant.Id }, newExpiry, "rotation");

        updated.Should().HaveCount(1);
        grant.ExpiresAt.Should().Be(newExpiry);
        grant.IsActive.Should().BeTrue();

        _versionStoreMock.Verify(
            x => x.IncrementVersionAsync(TenantA.ToString(), It.IsAny<CancellationToken>()),
            Times.Once);
        _auditMock.Verify(
            x => x.LogPermissionChangeAsync(
                PermissionOperationType.Update,
                It.IsAny<Guid?>(),
                It.IsAny<Guid>(),
                TenantA,
                It.IsAny<string?>(), grant.Id, It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetExpirationAsync_RejectsPastExpiration()
    {
        var act = () => _sut.SetExpirationAsync(TenantA, new[] { Guid.NewGuid() }, SystemClock.UtcNow.AddHours(-1));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetExpirationAsync_NullExpirationMakesGrantPermanent()
    {
        var grant = CreateGrant(TenantA, SystemClock.UtcNow.AddDays(1));

        _repoMock
            .Setup(x => x.GetByIdsInTenantAsync(TenantA, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission> { grant });
        _repoMock
            .Setup(x => x.UpdateAsync(It.IsAny<TenantPermission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission p, CancellationToken _) => p);

        await _sut.SetExpirationAsync(TenantA, new[] { grant.Id }, null);

        grant.ExpiresAt.Should().BeNull();
        grant.IsActive.Should().BeTrue();
    }

    // ── ExtendExpirationAsync ──────────────────────────────────

    [Fact]
    public async Task ExtendExpirationAsync_AddsExtensionToExistingFutureExpiry()
    {
        var current = SystemClock.UtcNow.AddDays(1);
        var grant = CreateGrant(TenantA, current);

        _repoMock
            .Setup(x => x.GetByIdsInTenantAsync(TenantA, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission> { grant });
        _repoMock
            .Setup(x => x.UpdateAsync(It.IsAny<TenantPermission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission p, CancellationToken _) => p);

        await _sut.ExtendExpirationAsync(TenantA, new[] { grant.Id }, TimeSpan.FromDays(6));

        grant.ExpiresAt.Should().BeCloseTo(current.AddDays(6), TimeSpan.FromSeconds(5));
        grant.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ExtendExpirationAsync_RevivesExpiredGrant()
    {
        var grant = CreateGrant(TenantA, SystemClock.UtcNow.AddDays(-2), isActive: false);

        _repoMock
            .Setup(x => x.GetByIdsInTenantAsync(TenantA, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission> { grant });
        _repoMock
            .Setup(x => x.UpdateAsync(It.IsAny<TenantPermission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission p, CancellationToken _) => p);

        await _sut.ExtendExpirationAsync(TenantA, new[] { grant.Id }, TimeSpan.FromDays(3));

        grant.IsActive.Should().BeTrue();
        grant.ExpiresAt.Should().BeCloseTo(SystemClock.UtcNow.AddDays(3), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ExtendExpirationAsync_RejectsNonPositiveExtension()
    {
        var act = () => _sut.ExtendExpirationAsync(TenantA, new[] { Guid.NewGuid() }, TimeSpan.Zero);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExtendExpirationAsync_NoMatchingGrantsDoesNotBumpVersion()
    {
        _repoMock
            .Setup(x => x.GetByIdsInTenantAsync(TenantA, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission>());

        var updated = await _sut.ExtendExpirationAsync(TenantA, new[] { Guid.NewGuid() }, TimeSpan.FromDays(1));

        updated.Should().BeEmpty();
        _versionStoreMock.Verify(x => x.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── GetExpiringAsync ───────────────────────────────────────

    [Fact]
    public async Task GetExpiringAsync_FiltersToRequestedTenant()
    {
        var inTenant = CreateGrant(TenantA, SystemClock.UtcNow.AddDays(1));
        var otherTenant = CreateGrant(TenantB, SystemClock.UtcNow.AddDays(1));

        _repoMock
            .Setup(x => x.GetExpiringBeforeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission> { inTenant, otherTenant });

        var result = await _sut.GetExpiringAsync(TenantA);

        result.Should().ContainSingle().Which.Should().BeSameAs(inTenant);
    }
}
