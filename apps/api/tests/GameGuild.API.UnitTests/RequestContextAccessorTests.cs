using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using GameGuild.API.Context;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Moq;

namespace GameGuild.API.UnitTests;

public sealed class RequestContextAccessorTests
{
    [Fact]
    public void CurrentContext_PrefersAuthenticatedActorValues()
    {
        var actorUserId = Guid.NewGuid();
        var actorTenantId = Guid.NewGuid();
        var httpContext = CreateHttpContext(
            isAuthenticated: true,
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("tenant", Guid.NewGuid().ToString()));
        var accessor = CreateAccessor(
            httpContext,
            CreateActor(actorUserId.ToString(), actorTenantId, isAuthenticated: true));

        accessor.CurrentUserId.Should().Be(actorUserId);
        accessor.CurrentTenantId.Should().Be(actorTenantId);
        accessor.IsAuthenticated.Should().BeTrue();
        accessor.HasTenantContext.Should().BeTrue();
    }

    [Fact]
    public void CurrentContext_UsesClaimsAndPrincipalAuthentication()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContext(
            isAuthenticated: true,
            new Claim(ClaimTypes.NameIdentifier, " "),
            new Claim("sub", userId.ToString()),
            new Claim("tenant", tenantId.ToString()));
        var accessor = CreateAccessor(httpContext);

        accessor.CurrentUserId.Should().Be(userId);
        accessor.CurrentTenantId.Should().Be(tenantId);
        accessor.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void CurrentContext_InvalidClaimsAndHeaders_ReturnNull()
    {
        var httpContext = CreateHttpContext(
            isAuthenticated: false,
            new Claim(ClaimTypes.NameIdentifier, "not-a-guid"),
            new Claim("tenant", "not-a-guid"));
        httpContext.Request.Headers["X-Tenant-Id"] = "also-not-a-guid";
        var accessor = CreateAccessor(httpContext);

        accessor.CurrentUserId.Should().BeNull();
        accessor.CurrentTenantId.Should().BeNull();
        accessor.IsAuthenticated.Should().BeFalse();
        accessor.HasTenantContext.Should().BeFalse();
    }

    [Fact]
    public void CurrentTenantId_WhenValidatedTenantItemHasWrongType_UsesHeaderFallback()
    {
        var tenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[HttpContextKeys.AuthorizationTenantId] = tenantId.ToString();
        httpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();

        var accessor = CreateAccessor(httpContext);

        accessor.CurrentTenantId.Should().Be(tenantId);
    }

    [Fact]
    public void CurrentTenantId_UsesTenantValidatedByMiddleware()
    {
        var tenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[HttpContextKeys.AuthorizationTenantId] = tenantId;

        var accessor = CreateAccessor(httpContext);

        accessor.CurrentTenantId.Should().Be(tenantId);
        accessor.HasTenantContext.Should().BeTrue();
    }

    [Fact]
    public void CurrentTenantId_WhenContextOrItemIsMissing_ReturnsNull()
    {
        var missingContextAccessor = CreateAccessor(httpContext: null);
        var missingItemAccessor = CreateAccessor(new DefaultHttpContext());

        missingContextAccessor.CurrentTenantId.Should().BeNull();
        missingItemAccessor.CurrentTenantId.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenIdentityIsIncomplete_ReturnsNull()
    {
        var authenticatedWithoutId = CreateAccessor(CreateHttpContext(isAuthenticated: true));
        var userId = Guid.NewGuid();
        var unauthenticatedWithId = CreateAccessor(CreateHttpContext(
            isAuthenticated: false,
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())));

        (await authenticatedWithoutId.GetCurrentUserAsync()).Should().BeNull();
        (await unauthenticatedWithId.GetCurrentUserAsync()).Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentUserAsync_UsesActorAttributesBeforePrincipalClaims()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContext(
            isAuthenticated: true,
            new Claim(ClaimTypes.Email, "principal@example.com"),
            new Claim(ClaimTypes.Name, "Principal Name"));
        var actor = CreateActor(
            userId.ToString(),
            tenantId,
            isAuthenticated: true,
            new Dictionary<string, string>
            {
                ["email"] = "actor@example.com",
                ["name"] = "Actor Name"
            });

        var result = await CreateAccessor(httpContext, actor).GetCurrentUserAsync();

        result.Should().Be(new UserInfo(userId, "actor@example.com", "Actor Name", true, tenantId));
    }

    [Fact]
    public async Task GetCurrentUserAsync_UsesStandardPrincipalClaims()
    {
        var userId = Guid.NewGuid();
        var httpContext = CreateHttpContext(
            isAuthenticated: true,
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, "standard@example.com"),
            new Claim(ClaimTypes.Name, "Standard Name"));

        var result = await CreateAccessor(httpContext).GetCurrentUserAsync();

        result.Should().Be(new UserInfo(userId, "standard@example.com", "Standard Name", true, null));
    }

    [Fact]
    public async Task GetCurrentUserAsync_UsesCustomEmailAndIdentityNameFallbacks()
    {
        var userId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("email", "custom@example.com"),
                new Claim("display_name", "Identity Name")
            ],
            "Test",
            "display_name",
            ClaimTypes.Role);
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var result = await CreateAccessor(httpContext).GetCurrentUserAsync();

        result.Should().Be(new UserInfo(userId, "custom@example.com", "Identity Name", true, null));
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenNamesAreMissing_UsesEmail()
    {
        var userId = Guid.NewGuid();
        var withEmail = CreateHttpContext(
            isAuthenticated: true,
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("email", "fallback@example.com"));
        var withoutEmail = CreateHttpContext(
            isAuthenticated: true,
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

        var emailResult = await CreateAccessor(withEmail).GetCurrentUserAsync();
        var emptyResult = await CreateAccessor(withoutEmail).GetCurrentUserAsync();

        emailResult.Should().Be(new UserInfo(userId, "fallback@example.com", "fallback@example.com", true, null));
        emptyResult.Should().Be(new UserInfo(userId, string.Empty, string.Empty, true, null));
    }

    [Fact]
    public void IsAuthenticated_WhenHttpContextOrIdentityIsMissing_ReturnsFalse()
    {
        var missingContext = CreateAccessor(httpContext: null);
        var missingIdentity = CreateAccessor(new DefaultHttpContext
        {
            User = new ClaimsPrincipal()
        });

        missingContext.IsAuthenticated.Should().BeFalse();
        missingIdentity.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithActorAndNoPrincipal_UsesEmptyFallbacks()
    {
        var userId = Guid.NewGuid();
        var actor = CreateActor(userId.ToString(), tenantId: null, isAuthenticated: true);

        var missingContextResult = await CreateAccessor(httpContext: null, actor).GetCurrentUserAsync();
        var missingIdentityResult = await CreateAccessor(
            new DefaultHttpContext { User = new ClaimsPrincipal() },
            actor).GetCurrentUserAsync();

        missingContextResult.Should().Be(new UserInfo(userId, string.Empty, string.Empty, true, null));
        missingIdentityResult.Should().Be(new UserInfo(userId, string.Empty, string.Empty, true, null));
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenActorIdExistsButAuthenticationDoesNot_ReturnsNull()
    {
        var actor = CreateActor(Guid.NewGuid().ToString(), tenantId: null, isAuthenticated: false);

        var result = await CreateAccessor(httpContext: null, actor).GetCurrentUserAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenAuthenticatedActorHasNoId_ReturnsNull()
    {
        var actor = CreateActor(subjectId: null, tenantId: null, isAuthenticated: true);

        var result = await CreateAccessor(httpContext: null, actor).GetCurrentUserAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentTenantAsync_UsesClaimValues()
    {
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContext(
            isAuthenticated: true,
            new Claim("tenant", tenantId.ToString()),
            new Claim("tenant_name", " "),
            new Claim("tenantName", "Claim Tenant"),
            new Claim("tenant_slug", "claim-tenant"));

        var result = await CreateAccessor(httpContext).GetCurrentTenantAsync();

        result.Should().Be(new TenantInfo(tenantId, "Claim Tenant", "claim-tenant", true));
    }

    [Fact]
    public async Task GetCurrentTenantAsync_UsesHeaderValues()
    {
        var tenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
        httpContext.Request.Headers["X-Tenant-Name"] = "Header Tenant";
        httpContext.Request.Headers["X-Tenant-Slug"] = "header-tenant";

        var result = await CreateAccessor(httpContext).GetCurrentTenantAsync();

        result.Should().Be(new TenantInfo(tenantId, "Header Tenant", "header-tenant", true));
    }

    [Fact]
    public async Task GetCurrentTenantAsync_UsesDefaultsForMissingOrBlankMetadata()
    {
        var tenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
        httpContext.Request.Headers["X-Tenant-Name"] = " ";
        httpContext.Request.Headers["X-Tenant-Slug"] = " ";

        var result = await CreateAccessor(httpContext).GetCurrentTenantAsync();

        result.Should().Be(new TenantInfo(tenantId, "Current tenant", tenantId.ToString("D"), true));
    }

    [Fact]
    public async Task GetCurrentTenantAsync_WhenTenantIsMissing_ReturnsNull()
    {
        var result = await CreateAccessor(httpContext: null).GetCurrentTenantAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task AsyncAccessors_HonorCancellation()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var accessor = CreateAccessor(httpContext: null);

        var userAction = () => accessor.GetCurrentUserAsync(source.Token);
        var tenantAction = () => accessor.GetCurrentTenantAsync(source.Token);

        await userAction.Should().ThrowAsync<OperationCanceledException>();
        await tenantAction.Should().ThrowAsync<OperationCanceledException>();
    }

    private static RequestContextAccessor CreateAccessor(
        HttpContext? httpContext,
        ActorContext? actorContext = null)
    {
        var actorContextAccessor = new Mock<IActorContextAccessor>();
        actorContextAccessor
            .SetupGet(accessor => accessor.ActorContext)
            .Returns(actorContext ?? ActorContext.Anonymous);
        return new RequestContextAccessor(
            actorContextAccessor.Object,
            new HttpContextAccessor { HttpContext = httpContext });
    }

    private static DefaultHttpContext CreateHttpContext(
        bool isAuthenticated,
        params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, isAuthenticated ? "Test" : null);
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    private static ActorContext CreateActor(
        string? subjectId,
        Guid? tenantId,
        bool isAuthenticated,
        IReadOnlyDictionary<string, string>? customAttributes = null)
    {
        return new ActorContext
        {
            ActorKind = isAuthenticated ? ActorKind.User : ActorKind.Anonymous,
            SubjectId = subjectId,
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = new ActorAttributes { Custom = customAttributes ?? new Dictionary<string, string>() },
            AuthScheme = isAuthenticated ? "Test" : null,
            IsAuthenticated = isAuthenticated
        };
    }
}
