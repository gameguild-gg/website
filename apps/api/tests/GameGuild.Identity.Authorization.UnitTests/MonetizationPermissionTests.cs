using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization.Models;
using GameGuild.Identity.Context.Actors;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests;

/// <summary>
///     Issue #346: the Monetize / ViewAnalytics / Configure permission trio must be
///     first-class tenant permissions — registered in the central registry, exposed via
///     the facade, and enforced by the CQRS authorization pipeline.
/// </summary>
public sealed class MonetizationPermissionTests
{
    // ─── Registry & facade ───

    [Fact]
    public void MonetizationPermission_Keys_AreRegisteredInTheCentralRegistry()
    {
        PermissionRegistry.Keys.Should().Contain(MonetizationPermission.Keys.Monetize);
        PermissionRegistry.Keys.Should().Contain(MonetizationPermission.Keys.ViewAnalytics);
        PermissionRegistry.Keys.Should().Contain(MonetizationPermission.Keys.Configure);
    }

    [Fact]
    public void MonetizationPermission_Instances_CarryTheMonetizationResource()
    {
        MonetizationPermission.Monetize.Key.Should().Be("monetization:monetize");
        MonetizationPermission.ViewAnalytics.Key.Should().Be("monetization:view-analytics");
        MonetizationPermission.Configure.Key.Should().Be("monetization:configure");

        foreach (var permission in new[] { MonetizationPermission.Monetize, MonetizationPermission.ViewAnalytics, MonetizationPermission.Configure })
        {
            permission.Resource.Should().Be("monetization");
            PermissionRegistry.GetByKey(permission.Key).Should().NotBeNull();
        }
    }

    [Fact]
    public void MonetizationPermission_WildcardScope_IsRecognized()
    {
        // A tenant granting "monetization:*" must be able to satisfy the monetization
        // permission family via the registry's wildcard validation.
        PermissionRegistry.IsValidKey("monetization:*").Should().BeTrue();
    }

    [Fact]
    public void Permissions_Facade_ExposesMonetizationConstants()
    {
        Permissions.MonetizationMonetize.Should().Be(MonetizationPermission.Keys.Monetize);
        Permissions.MonetizationViewAnalytics.Should().Be(MonetizationPermission.Keys.ViewAnalytics);
        Permissions.MonetizationConfigure.Should().Be(MonetizationPermission.Keys.Configure);
    }

    [Fact]
    public void PermissionOperationType_HasCheckMember_ForDecisionAuditing()
    {
        // Endpoint-level permission decisions are audited as Check entries.
        ((int)PermissionOperationType.Check).Should().Be(11);
        Enum.GetNames<PermissionOperationType>().Should().Contain("Check");
    }

    // ─── CQRS pipeline enforcement ───

    [Fact]
    public async Task AuthorizationBehavior_MonetizationQuery_WithoutPermission_IsDenied()
    {
        var (accessor, acl) = SetupActor(permissions: Array.Empty<string>());
        var behavior = new AuthorizationBehavior<MonetizedQuery, string>(accessor.Object, acl.Object);

        var act = () => behavior.Handle(
            new MonetizedQuery(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        (await act.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage($"*{MonetizationPermission.Keys.ViewAnalytics}*");
    }

    [Fact]
    public async Task AuthorizationBehavior_MonetizationQuery_WithPermission_IsAllowed()
    {
        var (accessor, acl) = SetupActor(permissions: [MonetizationPermission.Keys.ViewAnalytics]);
        var behavior = new AuthorizationBehavior<MonetizedQuery, string>(accessor.Object, acl.Object);

        var result = await behavior.Handle(
            new MonetizedQuery(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task AuthorizationBehavior_MonetizationQuery_Unauthenticated_IsDenied()
    {
        var actor = new ActorContext
        {
            IsAuthenticated = false,
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Roles = new HashSet<string>()
        };
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(a => a.ActorContext).Returns(actor);
        var behavior = new AuthorizationBehavior<MonetizedQuery, string>(accessor.Object, Mock.Of<IAccessControlListService>());

        var act = () => behavior.Handle(
            new MonetizedQuery(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AuthorizationBehavior_MonetizationQuery_SystemAdmin_BypassesPermission()
    {
        var actor = new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SystemAdmin" }
        };
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(a => a.ActorContext).Returns(actor);
        var behavior = new AuthorizationBehavior<MonetizedQuery, string>(accessor.Object, Mock.Of<IAccessControlListService>());

        var result = await behavior.Handle(
            new MonetizedQuery(),
            () => Task.FromResult("admin-ok"),
            CancellationToken.None);

        result.Should().Be("admin-ok");
    }

    [Fact]
    public async Task AuthorizationBehavior_MonetizationConfigureCommand_WithoutPermission_IsDenied()
    {
        var (accessor, acl) = SetupActor(permissions: [MonetizationPermission.Keys.ViewAnalytics]);
        var behavior = new AuthorizationBehavior<MonetizedCommand, string>(accessor.Object, acl.Object);

        var act = () => behavior.Handle(
            new MonetizedCommand(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        (await act.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage($"*{MonetizationPermission.Keys.Configure}*");
    }

    // ─── Setup & fixtures ───

    private static (Mock<IActorContextAccessor> Accessor, Mock<IAccessControlListService> Acl) SetupActor(
        string[] permissions)
    {
        var actor = new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase),
            Roles = new HashSet<string>()
        };
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(a => a.ActorContext).Returns(actor);
        return (accessor, new Mock<IAccessControlListService>());
    }

    [AuthorizeRequest(MonetizationPermission.Keys.ViewAnalytics)]
    public sealed record MonetizedQuery : IRequestBase;

    [AuthorizeRequest(MonetizationPermission.Keys.Configure)]
    public sealed record MonetizedCommand : IRequestBase;
}