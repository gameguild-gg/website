using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests;

public class PermissionExpirationOptionsTests
{
    // ── Validation ─────────────────────────────────────────────

    [Fact]
    public void Validate_DefaultOptionsAreValid()
    {
        new PermissionExpirationOptions().Validate().Should().BeEmpty();
    }

    [Fact]
    public void Validate_RejectsInvalidWorkerSettings()
    {
        var options = new PermissionExpirationOptions
        {
            InitialDelay = TimeSpan.FromSeconds(-1),
            ScanInterval = TimeSpan.Zero,
            ExecutionTimeout = TimeSpan.Zero,
            BatchSize = 0
        };

        var errors = options.Validate();

        errors.Should().Contain(e => e.Contains("InitialDelay"));
        errors.Should().Contain(e => e.Contains("ScanInterval"));
        errors.Should().Contain(e => e.Contains("ExecutionTimeout"));
        errors.Should().Contain(e => e.Contains("BatchSize"));
    }

    [Fact]
    public void Validate_RejectsInvalidWindowsAndPeriods()
    {
        var options = new PermissionExpirationOptions
        {
            UpcomingNotificationWindow = TimeSpan.FromDays(366),
            MinimumReminderInterval = TimeSpan.FromHours(-1),
            DefaultGrantExpiration = TimeSpan.Zero,
            DefaultExpirationByPermission = new Dictionary<string, TimeSpan>
            {
                ["courses:create"] = TimeSpan.Zero
            }
        };

        var errors = options.Validate();

        errors.Should().Contain(e => e.Contains("UpcomingNotificationWindow"));
        errors.Should().Contain(e => e.Contains("MinimumReminderInterval"));
        errors.Should().Contain(e => e.Contains("DefaultGrantExpiration"));
        errors.Should().Contain(e => e.Contains("courses:create"));
    }

    // ── Default resolution precedence ─────────────────────────

    [Fact]
    public void ResolveDefaultExpiration_ExactPermissionWins()
    {
        var options = new PermissionExpirationOptions
        {
            DefaultGrantExpiration = TimeSpan.FromDays(99),
            DefaultExpirationByPermission = new Dictionary<string, TimeSpan>
            {
                ["courses:create"] = TimeSpan.FromDays(1),
                ["courses:*"] = TimeSpan.FromDays(7)
            }
        };

        var resolved = options.ResolveDefaultExpiration(new[] { "courses:create" });

        resolved.Should().Be(TimeSpan.FromDays(1));
    }

    [Fact]
    public void ResolveDefaultExpiration_LongestWildcardPrefixWins()
    {
        var options = new PermissionExpirationOptions
        {
            DefaultExpirationByPermission = new Dictionary<string, TimeSpan>
            {
                ["courses:*"] = TimeSpan.FromDays(7),
                ["courses:reports:*"] = TimeSpan.FromDays(2)
            }
        };

        var resolved = options.ResolveDefaultExpiration(new[] { "courses:reports:export" });

        resolved.Should().Be(TimeSpan.FromDays(2));
    }

    [Fact]
    public void ResolveDefaultExpiration_FallsBackToGlobalDefaultThenNull()
    {
        var options = new PermissionExpirationOptions
        {
            DefaultGrantExpiration = TimeSpan.FromDays(30)
        };

        options.ResolveDefaultExpiration(new[] { "unknown:permission" }).Should().Be(TimeSpan.FromDays(30));
        options.ResolveDefaultExpiration(null).Should().Be(TimeSpan.FromDays(30));

        options.DefaultGrantExpiration = null;
        options.ResolveDefaultExpiration(new[] { "unknown:permission" }).Should().BeNull();
    }

    // ── Grant-time default application (issue #331) ───────────

    [Fact]
    public async Task GrantTenantPermissionAsync_AppliesConfiguredDefaultExpiration()
    {
        var repoMock = new Mock<ITenantPermissionRepository>();
        repoMock
            .Setup(x => x.CreateAsync(It.IsAny<TenantPermission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission p, CancellationToken _) => p);

        var options = new PermissionExpirationOptions
        {
            ApplyDefaultsOnGrant = true,
            DefaultExpirationByPermission = new Dictionary<string, TimeSpan>
            {
                ["courses:create"] = TimeSpan.FromHours(2)
            }
        };

        var sut = new PermissionGrantService(
            repoMock.Object,
            AuditMock(),
            VersionStoreMock(),
            AnonymousActor(),
            NullLogger<PermissionGrantService>.Instance,
            changeNotifiers: null,
            expirationOptions: Options.Create(options));

        var result = await sut.GrantTenantPermissionAsync(
            Guid.NewGuid(), Guid.NewGuid(), new[] { "courses:create" });

        result.ExpiresAt.Should().NotBeNull();
        result.ExpiresAt!.Value.Should().BeCloseTo(SystemClock.UtcNow.AddHours(2), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GrantTenantPermissionAsync_ExplicitExpirationWinsOverDefaults()
    {
        var repoMock = new Mock<ITenantPermissionRepository>();
        repoMock
            .Setup(x => x.CreateAsync(It.IsAny<TenantPermission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission p, CancellationToken _) => p);

        var options = new PermissionExpirationOptions
        {
            ApplyDefaultsOnGrant = true,
            DefaultGrantExpiration = TimeSpan.FromDays(30)
        };
        var explicitExpiry = SystemClock.UtcNow.AddDays(5);

        var sut = new PermissionGrantService(
            repoMock.Object,
            AuditMock(),
            VersionStoreMock(),
            AnonymousActor(),
            NullLogger<PermissionGrantService>.Instance,
            changeNotifiers: null,
            expirationOptions: Options.Create(options));

        var result = await sut.GrantTenantPermissionAsync(
            Guid.NewGuid(), Guid.NewGuid(), new[] { "courses:create" }, expiresAt: explicitExpiry);

        result.ExpiresAt.Should().Be(explicitExpiry);
    }

    [Fact]
    public async Task GrantTenantPermissionAsync_WithoutOptInKeepsGrantsPermanent()
    {
        var repoMock = new Mock<ITenantPermissionRepository>();
        repoMock
            .Setup(x => x.CreateAsync(It.IsAny<TenantPermission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission p, CancellationToken _) => p);

        var options = new PermissionExpirationOptions
        {
            ApplyDefaultsOnGrant = false,
            DefaultGrantExpiration = TimeSpan.FromDays(30)
        };

        var sut = new PermissionGrantService(
            repoMock.Object,
            AuditMock(),
            VersionStoreMock(),
            AnonymousActor(),
            NullLogger<PermissionGrantService>.Instance,
            changeNotifiers: null,
            expirationOptions: Options.Create(options));

        var result = await sut.GrantTenantPermissionAsync(
            Guid.NewGuid(), Guid.NewGuid(), new[] { "courses:create" });

        result.ExpiresAt.Should().BeNull();
    }

    private static IPermissionAuditService AuditMock()
    {
        var auditMock = new Mock<IPermissionAuditService>();
        auditMock
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
        return auditMock.Object;
    }

    private static ITenantSecurityVersionStore VersionStoreMock()
    {
        var versionStoreMock = new Mock<ITenantSecurityVersionStore>();
        versionStoreMock
            .Setup(x => x.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);
        return versionStoreMock.Object;
    }

    private static IActorContextAccessor AnonymousActor()
    {
        var accessorMock = new Mock<IActorContextAccessor>();
        accessorMock.SetupGet(x => x.ActorContext).Returns(ActorContext.Anonymous);
        return accessorMock.Object;
    }
}
