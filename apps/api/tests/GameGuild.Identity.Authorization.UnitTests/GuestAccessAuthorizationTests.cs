using System.Security.Claims;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests;

/// <summary>
///     Guest access authorization (issue #213): fail-closed admission of anonymous guests —
/// own claim/role namespace, enumerated permission allowlist, deny everything else.
/// </summary>
public sealed class GuestAccessAuthorizationTests
{
    private static ClaimsPrincipal Guest(params Claim[] extraClaims)
    {
        var claims = new List<Claim>
        {
            new(ClaimNames.GuestId, Guid.NewGuid().ToString()),
            new(ClaimNames.Role, GuestIdentity.RoleName)
        };
        claims.AddRange(extraClaims);
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "GuestBearer"));
    }

    private static ClaimsPrincipal User(params Claim[] extraClaims)
    {
        var claims = new List<Claim> { new(ClaimNames.Subject, Guid.NewGuid().ToString()) };
        claims.AddRange(extraClaims);
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer"));
    }

    private static async Task<bool> EvaluateAsync(ClaimsPrincipal principal, GuestAccessRequirement requirement)
    {
        var handler = new GuestAccessHandler(NullLogger<GuestAccessHandler>.Instance);
        var context = new AuthorizationHandlerContext([requirement], principal, null);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact]
    public async Task WellFormedGuest_IsAdmitted_ByBasePolicy()
    {
        (await EvaluateAsync(Guest(), new GuestAccessRequirement(null))).Should().BeTrue();
    }

    [Fact]
    public async Task WellFormedGuest_HoldsEveryAllowlistedPermission()
    {
        foreach (var permission in GuestPermission.Allowlist)
        {
            (await EvaluateAsync(Guest(), new GuestAccessRequirement(permission))).Should()
                .BeTrue($"guests hold the enumerated permission '{permission}'");
        }
    }

    [Theory]
    [InlineData("users:read")]
    [InlineData("admin:*")]
    [InlineData("tenant:admin")]
    [InlineData("content:write")]
    [InlineData("")]
    public async Task Guest_IsDeniedEveryNonAllowlistedPermission(string permission)
    {
        // The requirement constructor itself refuses non-allowlisted keys; simulating a
        // smuggled requirement through the principal path: the principal simply does not
        // hold such a permission, and HasGuestPermission fails closed.
        Guest().HasGuestPermission(permission).Should().BeFalse();
        (await EvaluateAsync(Guest(), new GuestAccessRequirement(null))).Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticatedUser_IsDeniedGuestAccess()
    {
        (await EvaluateAsync(User(), new GuestAccessRequirement(null))).Should().BeFalse();
    }

    [Fact]
    public async Task AnonymousPrincipal_IsDeniedGuestAccess()
    {
        (await EvaluateAsync(new ClaimsPrincipal(new ClaimsIdentity()), new GuestAccessRequirement(null))).Should().BeFalse();
    }

    [Fact]
    public async Task GuestRoleWithoutGuestId_IsDenied()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimNames.Role, GuestIdentity.RoleName)],
            authenticationType: "GuestBearer"));
        (await EvaluateAsync(principal, new GuestAccessRequirement(null))).Should().BeFalse();
        principal.IsGuest().Should().BeFalse();
    }

    [Fact]
    public async Task GuestIdWithoutGuestRole_IsDenied()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimNames.GuestId, Guid.NewGuid().ToString())],
            authenticationType: "GuestBearer"));
        (await EvaluateAsync(principal, new GuestAccessRequirement(null))).Should().BeFalse();
        principal.IsGuest().Should().BeFalse();
    }

    [Theory]
    [InlineData(ClaimNames.Subject)]
    [InlineData(ClaimNames.UserId)]
    [InlineData(ClaimNames.NameIdentifier)]
    public async Task PrincipalMixingUserAndGuestClaims_IsDenied(string userClaimType)
    {
        // Defense in depth: even a token that smuggles a user-id claim alongside the guest
        // namespace is rejected — a guest must never resolve to a real user id.
        var principal = Guest(new Claim(userClaimType, Guid.NewGuid().ToString()));
        principal.IsGuest().Should().BeFalse();
        (await EvaluateAsync(principal, new GuestAccessRequirement(null))).Should().BeFalse();
    }

    [Fact]
    public void GuestPrincipal_CarriesNoTenantResolution()
    {
        var guest = Guest();
        // Tenant isolation: a guest carries no tenant claim, so tenant-context resolution
        // and every tenant-scoped permission check fail closed.
        guest.HasClaim(c => c.Type is ClaimNames.TenantId or ClaimNames.TenantIdAlt).Should().BeFalse();
        guest.FindFirst(ClaimNames.Subject).Should().BeNull();
        guest.FindFirst(ClaimNames.UserId).Should().BeNull();
    }

    [Fact]
    public void PermissionHandler_FailsClosed_ForGuestPrincipals()
    {
        // The DB-backed permission path requires a user id; a guest has none, so every
        // [RequiresPermission] endpoint denies guests regardless of the permission asked.
        var tenantContext = new Mock<IAuthorizationTenantContext>();
        tenantContext.SetupGet(c => c.HasTenant).Returns(false);
        tenantContext.SetupGet(c => c.TenantId).Returns((Guid?)null);
        var handler = new PermissionHandler(
            tenantContext.Object,
            Mock.Of<IAuthorizationPermissionService>(),
            Microsoft.Extensions.Options.Options.Create(new AuthorizationTokenOptions()),
            NullLogger<PermissionHandler>.Instance);

        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement("users:read")],
            Guest(),
            resource: null);

        handler.HandleAsync(context);
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public void GuestAccessRequirement_RejectsNonAllowlistedPermissions()
    {
        var act = () => new GuestAccessRequirement("users:read");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GuestAccessPolicyFor_RejectsNonAllowlistedPermissions()
    {
        var act = () => AuthorizationPolicies.GuestAccessFor("admin:*");
        act.Should().Throw<ArgumentOutOfRangeException>();
        AuthorizationPolicies.GuestAccessFor(GuestPermission.Keys.BrowseCatalog)
            .Should().Be($"GuestAccess:{GuestPermission.Keys.BrowseCatalog}");
    }

    [Fact]
    public void Allowlist_MatchesTheTypedPermissionInstances()
    {
        // The enumerated allowlist is exactly the typed guest permission keys — no more, no less.
        GuestPermission.Allowlist.Should().BeEquivalentTo(
        [
            GuestPermission.Keys.BrowseCatalog,
            GuestPermission.Keys.ReadPublishedContent
        ]);
        GuestPermission.BrowseCatalog.Key.Should().Be(GuestPermission.Keys.BrowseCatalog);
        GuestPermission.ReadPublishedContent.Key.Should().Be(GuestPermission.Keys.ReadPublishedContent);
    }

    [Fact]
    public void GetGuestPermissions_ReturnsAllowlistOnlyForGuests()
    {
        Guest().GetGuestPermissions().Should().BeEquivalentTo(GuestPermission.Allowlist);
        User().GetGuestPermissions().Should().BeEmpty();
    }
}
