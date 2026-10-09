using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

using GameGuild;

namespace GameGuild.Identity.Authorization.UnitTests;

/// <summary>
///     Issue #358 gap 5 (plugin architecture for custom evaluation logic) and gap 6
///     (evaluation-layer rate limiting / enumeration protection) tests.
/// </summary>
public class PermissionEvaluationExtensionTests
{
    private readonly Mock<ITenantPermissionRepository> _repository = new();
    private readonly Mock<IRbacPermissionResolver> _rbacResolver = new();
    private readonly Mock<IAuthorizationRolePermissionProvider> _roleProvider = new();
    private readonly Mock<IResourcePermissionService> _resourceService = new();

    private static IOptions<GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions> AuthOptions()
        => Options.Create(new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions());

    private EffectivePermissionResolverService CreateSut(
        IEnumerable<IPermissionEvaluationExtension>? extensions = null,
        IEvaluationDenialThrottleService? throttle = null)
    {
        _repository
            .Setup(repo => repo.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission?)null);
        _rbacResolver
            .Setup(resolver => resolver.ResolvePermissionsAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RbacResolutionResult(new HashSet<string>(), new HashSet<string>(), []));
        _roleProvider
            .Setup(provider => provider.GetPermissionsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string>)[]);

        return new EffectivePermissionResolverService(
            _repository.Object,
            _rbacResolver.Object,
            [_roleProvider.Object],
            _resourceService.Object,
            AuthOptions(),
            NullLogger<EffectivePermissionResolverService>.Instance,
            jitElevationRepository: null,
            evaluationExtensions: extensions,
            denialThrottle: throttle);
    }

    private sealed class RecordingExtension(
        string name,
        int order,
        IReadOnlyCollection<string> allows,
        IReadOnlyCollection<string> denies,
        IList<string> callLog,
        bool throwInstead = false) : IPermissionEvaluationExtension
    {
        public string Name => name;
        public int Order => order;

        public Task<PermissionEvaluationExtensionResult> EvaluateAsync(
            PermissionEvaluationExtensionContext context,
            CancellationToken cancellationToken = default)
        {
            callLog.Add(name);
            if (throwInstead)
            {
                throw new InvalidOperationException("extension exploded");
            }

            return Task.FromResult(new PermissionEvaluationExtensionResult(allows, denies));
        }
    }

    [Fact]
    public async Task ResolveAsync_InvokesExtensionsInOrderThenRegistrationOrder()
    {
        var calls = new List<string>();
        var late = new RecordingExtension("late", 10, [], [], calls);
        var early = new RecordingExtension("early", 1, [], [], calls);
        var secondEarly = new RecordingExtension("second-early", 1, [], [], calls);

        var sut = CreateSut(new[] { late, early, secondEarly });

        await sut.ResolveAsync(EffectivePermissionContext.ForTenant(Guid.NewGuid(), Guid.NewGuid()));

        calls.Should().Equal("early", "second-early", "late");
    }

    [Fact]
    public async Task ResolveAsync_ExtensionAllows_AreGrantedWithExtensionSource()
    {
        var sut = CreateSut(new[]
        {
            new RecordingExtension("ext", 1, new[] { "workspace:export" }, Array.Empty<string>(), new List<string>())
        });

        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var effective = await sut.ResolveAsync(EffectivePermissionContext.ForTenant(user, tenant));

        effective.Permissions.Should().Contain("workspace:export");
        effective.Sources["workspace:export"].Should().Be(PermissionSource.Extension);
    }

    [Fact]
    public async Task ResolveAsync_ExtensionDenies_SubjectToDenyWins()
    {
        // The direct-grant layer allows the permission, the extension denies it:
        // DENY-WINS removes it from the effective set.
        _repository
            .Setup(repo => repo.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission
            {
                UserId = Guid.NewGuid(),
                Permissions = new[] { "workspace:export" }
            });

        var sut = CreateSut(new[]
        {
            new RecordingExtension("ext", 1, Array.Empty<string>(), new[] { "workspace:export" }, new List<string>())
        });

        var effective = await sut.ResolveAsync(EffectivePermissionContext.ForTenant(Guid.NewGuid(), Guid.NewGuid()));

        effective.Permissions.Should().NotContain("workspace:export");
    }

    [Fact]
    public async Task ResolveAsync_LaterExtensionObservesEarlierExtensionAllows()
    {
        string? observed = null;
        var first = new RecordingExtension("first", 1, new[] { "workspace:export" }, Array.Empty<string>(), new List<string>());
        var observer = new CallbackExtension("observer", 2, context =>
        {
            observed = context.CurrentAllows.Contains("workspace:export") ? "workspace:export" : null;
            return PermissionEvaluationExtensionResult.None;
        });

        var sut = CreateSut(new IPermissionEvaluationExtension[] { first, observer });

        await sut.ResolveAsync(EffectivePermissionContext.ForTenant(Guid.NewGuid(), Guid.NewGuid()));

        observed.Should().Be("workspace:export");
    }

    [Fact]
    public async Task ResolveAsync_ExtensionCannotGrantAdminWildcard()
    {
        var sut = CreateSut(new[]
        {
            new RecordingExtension("ext", 1, new[] { "admin:*", "tenant:read" }, Array.Empty<string>(), new List<string>())
        });

        var effective = await sut.ResolveAsync(EffectivePermissionContext.ForTenant(Guid.NewGuid(), Guid.NewGuid()));

        effective.Permissions.Should().NotContain("admin:*");
        effective.Permissions.Should().Contain("tenant:read");
    }

    [Fact]
    public async Task ResolveAsync_ThrowingExtension_IsSkippedAndResolutionContinues()
    {
        var calls = new List<string>();
        var broken = new RecordingExtension("broken", 1, new[] { "should:not-appear" }, Array.Empty<string>(), calls, throwInstead: true);
        var healthy = new RecordingExtension("healthy", 2, new[] { "tenant:read" }, Array.Empty<string>(), calls);

        var sut = CreateSut(new[] { broken, healthy });

        var effective = await sut.ResolveAsync(EffectivePermissionContext.ForTenant(Guid.NewGuid(), Guid.NewGuid()));

        calls.Should().Equal("broken", "healthy");
        effective.Permissions.Should().NotContain("should:not-appear");
        effective.Permissions.Should().Contain("tenant:read");
    }

    private sealed class CallbackExtension(
        string name,
        int order,
        Func<PermissionEvaluationExtensionContext, PermissionEvaluationExtensionResult> callback)
        : IPermissionEvaluationExtension
    {
        public string Name => name;
        public int Order => order;

        public Task<PermissionEvaluationExtensionResult> EvaluateAsync(
            PermissionEvaluationExtensionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(callback(context));
    }
}

/// <summary>
///     Gap 6: evaluation-layer denial throttle (enumeration protection).
/// </summary>
public class EvaluationDenialThrottleTests
{
    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }

    private static EvaluationThrottleOptions ThrottleOptions(int maxDenials = 3, int window = 60, int throttle = 120) => new()
    {
        Enabled = true,
        MaxDeniedEvaluationsPerWindow = maxDenials,
        WindowSeconds = window,
        ThrottleDurationSeconds = throttle
    };

    private static (EvaluationDenialThrottleService Service, DateTime Now) CreateService(
        EvaluationThrottleOptions options,
        DateTime now)
    {
        var engineOptions = new PermissionEngineOptions { EvaluationThrottle = options };
        SystemClock.SetProvider(new FixedTimeProvider(now));
        var service = new EvaluationDenialThrottleService(
            Options.Create(engineOptions),
            NullLogger<EvaluationDenialThrottleService>.Instance);
        return (service, now);
    }

    private void Cleanup() => SystemClock.Reset();

    [Fact]
    public void RecordsDenialsUntilLimit_ThenThrottles()
    {
        var now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        var (service, _) = CreateService(ThrottleOptions(maxDenials: 3), now);
        try
        {
            var user = Guid.NewGuid();
            var tenant = Guid.NewGuid();

            service.IsThrottled(user, tenant).Should().BeFalse();
            service.RecordDenial(user, tenant, now);
            service.RecordDenial(user, tenant, now);
            service.RecordDenial(user, tenant, now);
            service.IsThrottled(user, tenant).Should().BeFalse("the limit itself is still allowed");

            service.RecordDenial(user, tenant, now.AddSeconds(1));
            service.IsThrottled(user, tenant).Should().BeTrue("one deny beyond the limit trips the throttle");
        }
        finally
        {
            Cleanup();
        }
    }

    [Fact]
    public void ThrottleExpiresAfterDuration()
    {
        var now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        var (service, _) = CreateService(ThrottleOptions(maxDenials: 1, throttle: 120), now);
        try
        {
            var user = Guid.NewGuid();
            var tenant = Guid.NewGuid();

            service.RecordDenial(user, tenant, now);
            service.IsThrottled(user, tenant).Should().BeFalse();
            service.RecordDenial(user, tenant, now.AddSeconds(1));
            service.IsThrottled(user, tenant).Should().BeTrue();

            // Just before expiry: still throttled (IsThrottled advances via the clock provider).
            SystemClock.SetProvider(new FixedTimeProvider(now.AddSeconds(1).AddSeconds(119)));
            service.IsThrottled(user, tenant).Should().BeTrue();

            SystemClock.SetProvider(new FixedTimeProvider(now.AddSeconds(1).AddSeconds(121)));
            service.IsThrottled(user, tenant).Should().BeFalse("the throttle window elapsed");
        }
        finally
        {
            Cleanup();
        }
    }

    [Fact]
    public void DenialsOutsideTheSlidingWindow_DoNotAccumulate()
    {
        var now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        var (service, _) = CreateService(ThrottleOptions(maxDenials: 2, window: 60), now);
        try
        {
            var user = Guid.NewGuid();
            var tenant = Guid.NewGuid();

            // Old denials fall out of the window before the new ones arrive.
            service.RecordDenial(user, tenant, now.AddMinutes(-10));
            service.RecordDenial(user, tenant, now.AddMinutes(-9));
            service.RecordDenial(user, tenant, now);
            service.RecordDenial(user, tenant, now);

            service.IsThrottled(user, tenant).Should().BeFalse("only in-window denials count");
        }
        finally
        {
            Cleanup();
        }
    }

    [Fact]
    public void DisabledThrottle_NeverTrips()
    {
        var now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        var (service, _) = CreateService(new EvaluationThrottleOptions { Enabled = false }, now);
        try
        {
            var user = Guid.NewGuid();
            var tenant = Guid.NewGuid();

            for (var i = 0; i < 100; i++)
            {
                service.RecordDenial(user, tenant, now);
            }

            service.IsThrottled(user, tenant).Should().BeFalse();
        }
        finally
        {
            Cleanup();
        }
    }

    [Fact]
    public void PairsAreIsolated_PerUserAndTenant()
    {
        var now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        var (service, _) = CreateService(ThrottleOptions(maxDenials: 1), now);
        try
        {
            var userA = Guid.NewGuid();
            var userB = Guid.NewGuid();
            var tenant = Guid.NewGuid();

            service.RecordDenial(userA, tenant, now);
            service.RecordDenial(userA, tenant, now.AddSeconds(1));
            service.IsThrottled(userA, tenant).Should().BeTrue();
            service.IsThrottled(userB, tenant).Should().BeFalse("the throttle is per user+tenant");
        }
        finally
        {
            Cleanup();
        }
    }

    [Fact]
    public async Task Resolver_ShortCircuitsThrottledPairs_FailClosed()
    {
        var throttle = new Mock<IEvaluationDenialThrottleService>();
        throttle
            .Setup(service => service.IsThrottled(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns(true);

        var repository = new Mock<ITenantPermissionRepository>();
        repository
            .Setup(repo => repo.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission { Permissions = new[] { "tenant:read" } });
        var rbac = new Mock<IRbacPermissionResolver>();
        rbac
            .Setup(resolver => resolver.ResolvePermissionsAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RbacResolutionResult(
                new HashSet<string> { "role:grant" },
                new HashSet<string>(),
                []));

        var sut = new EffectivePermissionResolverService(
            repository.Object,
            rbac.Object,
            Array.Empty<IAuthorizationRolePermissionProvider>(),
            new Mock<IResourcePermissionService>().Object,
            Options.Create(new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions()),
            NullLogger<EffectivePermissionResolverService>.Instance,
            jitElevationRepository: null,
            evaluationExtensions: null,
            denialThrottle: throttle.Object);

        var effective = await sut.ResolveAsync(EffectivePermissionContext.ForTenant(Guid.NewGuid(), Guid.NewGuid()));

        effective.Permissions.Should().BeEmpty("a throttled pair fails closed");
        effective.Throttled.Should().BeTrue();
        effective.ContextValid.Should().BeTrue();
        // No permission store is consulted for a throttled pair.
        rbac.VerifyNoOtherCalls();
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HasPermissionAsync_DeniedOutcome_FeedsTheThrottle()
    {
        var throttle = new Mock<IEvaluationDenialThrottleService>();
        throttle
            .Setup(service => service.IsThrottled(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns(false);

        var repository = new Mock<ITenantPermissionRepository>();
        repository
            .Setup(repo => repo.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission?)null);
        var rbac = new Mock<IRbacPermissionResolver>();
        rbac
            .Setup(resolver => resolver.ResolvePermissionsAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RbacResolutionResult(new HashSet<string>(), new HashSet<string>(), []));
        var roleProvider = new Mock<IAuthorizationRolePermissionProvider>();
        roleProvider
            .Setup(provider => provider.GetPermissionsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string>)[]);

        var sut = new EffectivePermissionResolverService(
            repository.Object,
            rbac.Object,
            new[] { roleProvider.Object },
            new Mock<IResourcePermissionService>().Object,
            Options.Create(new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions()),
            NullLogger<EffectivePermissionResolverService>.Instance,
            jitElevationRepository: null,
            evaluationExtensions: null,
            denialThrottle: throttle.Object);

        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();

        (await sut.HasPermissionAsync(user, tenant, "missing:permission")).Should().BeFalse();
        throttle.Verify(service => service.RecordDenial(user, tenant), Times.Once);

        // An allowed outcome must NOT feed the throttle.
        repository
            .Setup(repo => repo.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission { Permissions = new[] { "granted:permission" } });
        (await sut.HasPermissionAsync(user, tenant, "granted:permission")).Should().BeTrue();
        throttle.Verify(service => service.RecordDenial(user, tenant), Times.Once);
    }
}
