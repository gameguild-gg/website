#pragma warning disable CS8600, CS8602, CS8604

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.CQRS;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Authorization.Caching;
using GameGuild.Identity.Authorization.Controllers;
using GameGuild.Identity.Authorization.Models;
using GameGuild.Identity.Authorization.Utilities;
using GameGuild.Identity.Context.Actors;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests;

public sealed class AuthorizationCoverageCompletionTests
{
    [Fact]
    public void ResourceSharingModels_Cover_RecordConstructors_And_ResultFactories()
    {
        var userId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();
        var tenantId = TenantId.New();
        var now = SystemClock.UtcNow;
        var permissions = new[] { "read", "Owner" };

        var access = new ResourceAccessDto(userId, "User", "u@example.com", permissions, now, now.AddDays(1), true);
        access.UserId.Should().Be(userId);

        var shareRequest = new ShareResourceRequest("target@example.com", permissions, now.AddDays(2), "hello");
        shareRequest.Message.Should().Be("hello");

        ShareResult.SuccessWithUser(userId, "u@example.com").UserId.Should().Be(userId);
        ShareResult.SuccessWithInvitation(invitationId, "n@example.com", "https://invite").InvitationLink.Should().Be("https://invite");
        ShareResult.Failure("bad").ErrorMessage.Should().Be("bad");

        var updateRequest = new UpdatePermissionsRequest(userId, permissions, now.AddDays(3));
        updateRequest.Permissions.Should().Contain("Owner");

        PermissionUpdateResult.SuccessResult(userId, permissions).UpdatedPermissions.Should().Equal(permissions);
        PermissionUpdateResult.Failure("denied").ErrorMessage.Should().Be("denied");

        var removeAccessRequest = new RemoveAccessRequest(userId, "cleanup");
        removeAccessRequest.Reason.Should().Be("cleanup");

        var pending = new PendingInvitationDto(invitationId, "invite@example.com", permissions, now, now.AddDays(4), "Pending");
        pending.InvitationId.Should().Be(invitationId);

        var invitation = new ResourceInvitation
        {
            Id = invitationId,
            TenantId = tenantId,
            Email = "invite@example.com",
            ResourceType = "Document",
            ResourceId = "doc-1",
            Permissions = permissions,
            InvitedByUserId = Guid.NewGuid(),
            InvitedByUserName = "Inviter",
            Message = "msg",
            InvitedAt = now,
            ExpiresAt = now.AddDays(5)
        };

        var dto = new ResourceInvitationDto(
            invitationId,
            tenantId.Value,
            invitation.Email,
            invitation.ResourceType,
            invitation.ResourceId,
            permissions,
            invitation.Message,
            invitation.InvitedByUserName,
            invitation.InvitedAt,
            invitation.ExpiresAt,
            invitation.Status.ToString());
        dto.Status.Should().Be("Pending");

        var decline = new DeclineInvitationRequest("no");
        decline.Reason.Should().Be("no");

        InvitationActionResult.SuccessResult(invitation, InvitationStatus.Accepted).Status.Should().Be("Accepted");
        InvitationActionResult.Failure(invitationId, "missing").ErrorMessage.Should().Be("missing");

        var apply = new ApplyTemplateRequest("owner-template", [userId]);
        apply.UserIds.Should().Contain(userId);

        var config = new ResourceSharingConfig();
        config.AllowedPermissions.Should().Contain("admin");

        var resourceUsers = new ResourceUsersResponse
        {
            ResourceType = "Document",
            ResourceId = "doc-1",
            Users = [access],
            PendingInvitations = [pending],
            TotalCount = 2
        };
        resourceUsers.TotalCount.Should().Be(2);

        var owner = new ResourceUser
        {
            UserId = userId,
            ResourceType = "Document",
            ResourceId = "doc-1",
            Permissions = permissions,
            GrantedAt = now,
            GrantedByUserId = Guid.NewGuid(),
            IsActive = true
        };
        owner.IsOwner.Should().BeTrue();

        var nonOwner = owner with { Permissions = ["read"] };
        nonOwner.IsOwner.Should().BeFalse();

        var bulk = new BulkShareResult
        {
            Success = false,
            SuccessCount = 1,
            FailureCount = 1,
            Results = [ShareResult.Failure("bad")],
            ErrorMessage = "partial"
        };
        bulk.Results.Should().HaveCount(1);
    }

    [Fact]
    public void PermissionAttributes_Cover_Constructors_NullGuards_And_Markers()
    {
        var requires = new RequiresPermissionAttribute("users:read");
        requires.PermissionName.Should().Be("users:read");
        FluentActions.Invoking(() => new RequiresPermissionAttribute(null!))
            .Should().Throw<ArgumentNullException>();

        var require = new RequirePermissionAttribute("users:write");
        require.PermissionName.Should().Be("users:write");
        FluentActions.Invoking(() => new RequirePermissionAttribute(null!))
            .Should().Throw<ArgumentNullException>();

        var resource = new RequireResourcePermissionAttribute<TestResourcePermission, TestResource>(TestResourcePermission.Edit, "resourceId");
        resource.RequiredPermission.Should().Be(TestResourcePermission.Edit);
        resource.ResourceIdParameterName.Should().Be("resourceId");
        resource.ResourceType.Should().Be(typeof(TestResource));
        resource.PermissionEnumType.Should().Be(typeof(TestResourcePermission));
        ((IResourcePermissionMarker)resource).RequiredPermission.Should().Be(TestResourcePermission.Edit);
        ((IResourcePermissionMarker)resource).ResourceIdParameterName.Should().Be("resourceId");
        FluentActions.Invoking(() => new RequireResourcePermissionAttribute<TestResourcePermission, TestResource>(TestResourcePermission.Read, null!))
            .Should().Throw<ArgumentNullException>();

        var content = new RequireContentTypePermissionAttribute<TestResource>("read");
        content.Permission.Should().Be("read");
        content.ResourceType.Should().Be(typeof(TestResource));
        ((IContentTypePermissionMarker)content).Permission.Should().Be("read");
        FluentActions.Invoking(() => new RequireContentTypePermissionAttribute<TestResource>(null!))
            .Should().Throw<ArgumentNullException>();

        var tenant = new RequireTenantPermissionAttribute("tenant:admin");
        tenant.Permission.Should().Be("tenant:admin");
        FluentActions.Invoking(() => new RequireTenantPermissionAttribute(null!))
            .Should().Throw<ArgumentNullException>();

        var request = new AuthorizeRequestAttribute("documents:read")
        {
            Layer = PermissionLayer.Resource,
            ResourceType = "Document",
            ResourceIdProperty = "DocumentId",
            RequireSystemAdmin = true,
            RequireTenantAdmin = true
        };
        request.Permission.Should().Be("documents:read");
        request.ResourceIdProperty.Should().Be("DocumentId");
    }

    [Fact]
    public void DbAuthorizationPolicyProvider_HasNoStaticDomainFallbackHelpers()
    {
        var privateStaticMethods = typeof(DbAuthorizationPolicyProvider)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Select(method => method.Name);

        privateStaticMethods.Should().NotContain(new[]
        {
            "TryBuildStaticFallbackPolicy",
            "MapPolicyToPermission",
            "HasPermission",
            "PermissionMatches"
        });
    }

    [Fact]
    public void RuleTypes_Cover_All_Switch_Cases_And_Defaults()
    {
        RuleTypes.IsValid(null).Should().BeFalse();
        RuleTypes.IsValid("").Should().BeFalse();
        RuleTypes.IsValid("not-real").Should().BeFalse();

        foreach (var ruleType in RuleTypes.All)
        {
            RuleTypes.IsValid(ruleType.ToLowerInvariant()).Should().BeTrue();
            RuleTypes.GetDescription(ruleType).Should().NotBeNullOrWhiteSpace();
            RuleTypes.GetRequiredParameters(ruleType).Should().NotBeNull();
        }

        RuleTypes.GetRequiredParameters(RuleTypes.RequireAllPermissions).Should().Equal("permissions");
        RuleTypes.GetRequiredParameters(RuleTypes.RequireAnyPermission).Should().Equal("permissions");
        RuleTypes.GetRequiredParameters(RuleTypes.RequireIpAllowList).Should().Equal("cidrs");
        RuleTypes.GetRequiredParameters(RuleTypes.RequireTimeWindow).Should().Equal("windows");
        RuleTypes.GetRequiredParameters("unknown").Should().BeEmpty();
        RuleTypes.GetDescription("unknown").Should().Be("Unknown rule type");
    }

    [Fact]
    public void ResourceType_Covers_Value_Equality_And_Operators()
    {
        var first = new ConcreteResourceType("Document", "Documents");
        var same = new ConcreteResourceType("Document", "Documents again");
        var different = new ConcreteResourceType("Project", "Projects");

        first.ToString().Should().Be("Document");
        first.Equals((object)same).Should().BeTrue();
        first.Equals((object)different).Should().BeFalse();
        first.Equals("Document").Should().BeFalse();
        first.GetHashCode().Should().Be(same.GetHashCode());
        (first == same).Should().BeTrue();
        (first != different).Should().BeTrue();
        first.Equals((ResourceType?)null).Should().BeFalse();
    }

    [Fact]
    public void AuthorizationBehavior_PrivateHelpers_Cover_ResourceId_And_Permission_Mapping()
    {
        GetResourceId(new RequestWithGuid(Guid.Parse("11111111-1111-1111-1111-111111111111")), "ResourceId")
            .Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        GetResourceId(new RequestWithString("22222222-2222-2222-2222-222222222222"), "ResourceId")
            .Should().Be(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        GetResourceId(new RequestWithString("not-a-guid"), "ResourceId").Should().Be(Guid.Empty);
        GetResourceId(new RequestWithNumber(12), "ResourceId").Should().Be(Guid.Empty);
        GetResourceId(new RequestWithGuid(Guid.NewGuid()), "Missing").Should().Be(Guid.Empty);

        MapPermission("course:manage").Should().Be(AccessLevel.Admin);
        MapPermission("admin").Should().Be(AccessLevel.Admin);
        MapPermission("delete").Should().Be(AccessLevel.Admin);
        MapPermission("remove").Should().Be(AccessLevel.Admin);
        MapPermission("write").Should().Be(AccessLevel.Write);
        MapPermission("edit").Should().Be(AccessLevel.Write);
        MapPermission("update").Should().Be(AccessLevel.Write);
        MapPermission("create").Should().Be(AccessLevel.Write);
        MapPermission("read").Should().Be(AccessLevel.Read);
        MapPermission("view").Should().Be(AccessLevel.Read);
        MapPermission("get").Should().Be(AccessLevel.Read);
        MapPermission("list").Should().Be(AccessLevel.Read);
        // Fail closed: unknown permission patterns throw instead of defaulting to Write.
        var unknown = () => MapPermission("custom");
        unknown.Should().Throw<UnauthorizedAccessException>();

        var attribute = new AuthorizeRequestAttribute("x");
        attribute.Permission.Should().Be("x");
    }

    [Fact]
    public async Task AuthorizationBehavior_Handle_Covers_ActorGetter_And_ResourceAuthorization()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actor = new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = userId.ToString(),
            TenantId = tenantId,
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tenant:allowed" },
            Roles = new HashSet<string>()
        };

        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(a => a.ActorContext).Returns(actor);
        var acl = new Mock<IAccessControlListService>();
        acl.Setup(a => a.HasAccessAsync(
                It.IsAny<AclSubject>(),
                tenantId,
                "Document",
                "33333333-3333-3333-3333-333333333333",
                AccessLevel.Read,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var behavior = new AuthorizationBehavior<AuthorizedResourceRequest, string>(accessor.Object, acl.Object);

        var result = await behavior.Handle(
            new AuthorizedResourceRequest(Guid.Parse("33333333-3333-3333-3333-333333333333")),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        result.Should().Be("ok");

        var tenantBehavior = new AuthorizationBehavior<AuthorizedTenantRequest, string>(accessor.Object, acl.Object);
        var tenantResult = await tenantBehavior.Handle(new AuthorizedTenantRequest(), () => Task.FromResult("tenant"), CancellationToken.None);
        tenantResult.Should().Be("tenant");
    }

    [Fact]
    public void ClaimsPrincipalAccessors_Cover_Valid_Invalid_And_Missing_Claims()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var principal = Principal(
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(AuthorizationClaims.TenantId, tenantId.ToString()));

        var httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        var httpAccessor = new HttpContextClaimsPrincipalAccessor(httpContextAccessor);
        httpAccessor.ClaimsPrincipal.Should().Be(principal);
        httpAccessor.IsAuthenticated.Should().BeTrue();
        httpAccessor.GetUserId().Should().Be(userId);
        httpAccessor.GetTenantId().Should().Be(tenantId);

        httpContextAccessor.HttpContext = null;
        httpAccessor.IsAuthenticated.Should().BeFalse();
        httpAccessor.GetUserId().Should().BeNull();
        httpAccessor.GetTenantId().Should().BeNull();

        var subPrincipal = Principal(new Claim(AuthorizationClaims.Sub, userId.ToString()));
        var staticAccessor = new StaticClaimsPrincipalAccessor(subPrincipal);
        staticAccessor.IsAuthenticated.Should().BeTrue();
        staticAccessor.GetUserId().Should().Be(userId);
        staticAccessor.GetTenantId().Should().BeNull();

        staticAccessor.ClaimsPrincipal = Principal(new Claim(ClaimTypes.NameIdentifier, "bad"), new Claim(AuthorizationClaims.TenantId, "bad"));
        staticAccessor.GetUserId().Should().BeNull();
        staticAccessor.GetTenantId().Should().BeNull();

        FluentActions.Invoking(() => new HttpContextClaimsPrincipalAccessor(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ClaimsExtractor_Covers_All_Fallbacks_And_Invalid_Parsing()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var principal = Principal(
            new Claim(ClaimNames.Subject, userId.ToString()),
            new Claim(ClaimNames.TenantId, tenantId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, "jwt-id"),
            new Claim(JwtRegisteredClaimNames.Iat, "1700000000"),
            new Claim(ClaimNames.Email, "primary@example.com"),
            new Claim(ClaimTypes.Name, "Name One"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimNames.Role, "Editor"),
            new Claim("roles", "Reviewer"),
            new Claim("permission", "read"),
            new Claim("permissions", "write"),
            new Claim(ClaimNames.MfaVerified, "true"),
            new Claim(ClaimNames.EmailVerified, "true"),
            new Claim(ClaimNames.Amr, "mfa"),
            new Claim("token_version", "3"),
            new Claim("grant_type", "client_credentials"),
            new Claim("actor_type", "service"),
            new Claim("custom", "value"));

        ClaimsExtractor.GetUserId(principal).Should().Be(userId.ToString());
        ClaimsExtractor.GetUserIdAsGuid(principal).Should().Be(userId);
        ClaimsExtractor.GetJti(principal).Should().Be("jwt-id");
        ClaimsExtractor.GetIssuedAt(principal).Should().Be(1700000000);
        ClaimsExtractor.GetIssuedAtDateTime(principal).Should().NotBeNull();
        ClaimsExtractor.GetEmail(principal).Should().Be("primary@example.com");
        ClaimsExtractor.GetName(principal).Should().Be("Name One");
        ClaimsExtractor.GetRoles(principal).Should().Contain(["Admin", "Editor", "Reviewer"]);
        ClaimsExtractor.GetTenantId(principal).Should().Be(tenantId.ToString());
        ClaimsExtractor.GetTenantIdAsGuid(principal).Should().Be(tenantId);
        ClaimsExtractor.GetGrantType(principal).Should().Be("client_credentials");
        ClaimsExtractor.GetActorType(principal).Should().Be("service");
        ClaimsExtractor.GetPermissions(principal).Should().Contain(["read", "write"]);
        ClaimsExtractor.IsMfaVerified(principal).Should().BeTrue();
        ClaimsExtractor.IsEmailVerified(principal).Should().BeTrue();
        ClaimsExtractor.GetAmr(principal).Should().Be("mfa");
        ClaimsExtractor.GetTokenVersion(principal).Should().Be("3");
        ClaimsExtractor.GetClaim(principal, "custom").Should().Be("value");
        ClaimsExtractor.IsAuthenticated(principal).Should().BeTrue();

        var fallback = Principal(
            new Claim(ClaimNames.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, "fallback@example.com"),
            new Claim("name", "Fallback Name"),
            new Claim(ClaimNames.TenantIdAlt, tenantId.ToString()));
        ClaimsExtractor.GetUserId(fallback).Should().Be(userId.ToString());
        ClaimsExtractor.GetEmail(fallback).Should().Be("fallback@example.com");
        ClaimsExtractor.GetName(fallback).Should().Be("Fallback Name");
        ClaimsExtractor.GetTenantId(fallback).Should().Be(tenantId.ToString());

        var userIdFallback = Principal(new Claim(ClaimNames.UserId, userId.ToString()));
        ClaimsExtractor.GetUserId(userIdFallback).Should().Be(userId.ToString());

        var invalid = Principal(
            new Claim(ClaimNames.Subject, "not-guid"),
            new Claim(ClaimNames.TenantId, "not-guid"),
            new Claim(JwtRegisteredClaimNames.Iat, "not-long"),
            new Claim(ClaimNames.MfaVerified, "nope"),
            new Claim(ClaimNames.EmailVerified, "nope"));
        ClaimsExtractor.GetUserIdAsGuid(invalid).Should().BeNull();
        ClaimsExtractor.GetTenantIdAsGuid(invalid).Should().BeNull();
        ClaimsExtractor.GetIssuedAt(invalid).Should().BeNull();
        ClaimsExtractor.GetIssuedAtDateTime(invalid).Should().BeNull();
        ClaimsExtractor.IsMfaVerified(invalid).Should().BeFalse();
        ClaimsExtractor.IsEmailVerified(invalid).Should().BeFalse();

        var empty = new ClaimsPrincipal(new ClaimsIdentity());
        ClaimsExtractor.GetUserId(empty).Should().BeNull();
        ClaimsExtractor.GetUserIdAsGuid(empty).Should().BeNull();
        ClaimsExtractor.GetTenantId(empty).Should().BeNull();
        ClaimsExtractor.GetTenantIdAsGuid(empty).Should().BeNull();
        ClaimsExtractor.GetIssuedAt(empty).Should().BeNull();
        ClaimsExtractor.GetIssuedAtDateTime(empty).Should().BeNull();
        ClaimsExtractor.IsAuthenticated(empty).Should().BeFalse();
    }

    [Fact]
    public async Task RequireTimeWindow_Covers_Private_Window_And_Timezone_Branches()
    {
        var evaluator = new RequireTimeWindowRuleEvaluator();
        var context = CreateAuthorizationContext();

        var invalidWindows = await evaluator.EvaluateAsync(context, RuleParameters.FromJson("{\"windows\": 1}"));
        invalidWindows.IsSuccess.Should().BeFalse();

        var nestedNonArray = await evaluator.EvaluateAsync(context, RuleParameters.FromJson("{\"windows\": {\"windows\": 1}}"));
        nestedNonArray.IsSuccess.Should().BeFalse();

        foreach (var timezone in new[]
                 {
                     "America/New_York",
                     "America/Chicago",
                     "America/Denver",
                     "America/Los_Angeles",
                     "America/Sao_Paulo",
                     "Europe/London",
                     "Europe/Paris",
                     "Europe/Berlin",
                     "Asia/Tokyo",
                     "Asia/Shanghai",
                     "Asia/Singapore",
                     "Australia/Sydney",
                     "UTC",
                     "Custom/Zone"
                 })
        {
            ConvertIanaToWindows(timezone).Should().NotBeNullOrWhiteSpace();
        }

        IsWithinWindow("""{"daysOfWeek":"bad","startTime":"08:00","endTime":"17:00"}""", 1, TimeSpan.FromHours(9))
            .Should().BeTrue();
        IsWithinWindow("""{"daysOfWeek":[2],"startTime":"08:00","endTime":"17:00"}""", 1, TimeSpan.FromHours(9))
            .Should().BeFalse();
        IsWithinWindow("""{"daysOfWeek":[1],"startTime":"bad","endTime":"bad"}""", 1, TimeSpan.FromHours(9))
            .Should().BeTrue();
        IsWithinWindow("""{"startTime":"22:00","endTime":"06:00"}""", 1, TimeSpan.FromHours(23))
            .Should().BeTrue();
        IsWithinWindow("""{"startTime":"22:00","endTime":"06:00"}""", 1, TimeSpan.FromHours(12))
            .Should().BeFalse();
        IsWithinWindow("""{"startTime":"08:00","endTime":"17:00"}""", 1, TimeSpan.FromHours(7))
            .Should().BeFalse();
        IsWithinWindow("""{"startTime":"08:00"}""", 1, TimeSpan.FromHours(7))
            .Should().BeFalse();
        IsWithinWindow("""{"endTime":"17:00"}""", 1, TimeSpan.FromHours(18))
            .Should().BeFalse();
    }

    [Fact]
    public async Task RequireIpAllowList_Covers_Context_ForwardedFor_Cidr_And_Mismatch_Branches()
    {
        var context = CreateAuthorizationContext();
        var noHttp = new RequireIpAllowListRuleEvaluator(new HttpContextAccessor());
        (await noHttp.EvaluateAsync(context, RuleParameters.FromJson("""{"cidrs":["10.0.0.0/8"]}""")))
            .IsSuccess.Should().BeFalse();

        var httpContext = new DefaultHttpContext();
        var evaluator = new RequireIpAllowListRuleEvaluator(new HttpContextAccessor { HttpContext = httpContext });
        (await evaluator.EvaluateAsync(context, RuleParameters.FromJson("""{"cidrs":["10.0.0.0/8"]}""")))
            .IsSuccess.Should().BeFalse();

        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("172.16.0.10");
        httpContext.Request.Headers["X-Forwarded-For"] = "bad-ip, 10.0.0.1";
        (await evaluator.EvaluateAsync(context, RuleParameters.FromJson("""{"cidrs":["172.16.0.0/16"]}""")))
            .IsSuccess.Should().BeTrue();

        httpContext.Request.Headers["X-Forwarded-For"] = "10.1.2.3, 172.16.0.10";
        (await evaluator.EvaluateAsync(context, RuleParameters.FromJson("""{"cidrs":["10.0.0.0/8"]}""")))
            .IsSuccess.Should().BeTrue();

        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:192.168.1.20");
        httpContext.Request.Headers.Remove("X-Forwarded-For");
        (await evaluator.EvaluateAsync(context, RuleParameters.FromJson("""{"cidrs":["192.168.1.0/24"],"checkForwardedFor":false}""")))
            .IsSuccess.Should().BeTrue();

        foreach (var cidr in new[] { "bad", "not-ip/24", "10.0.0.0/bad", "2001:db8::/32" })
        {
            (await evaluator.EvaluateAsync(context, RuleParameters.FromJson($$"""{"cidrs":["{{cidr}}"],"checkForwardedFor":false}""")))
                .IsSuccess.Should().BeFalse();
        }

        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("2001:db8::1");
        (await evaluator.EvaluateAsync(context, RuleParameters.FromJson("""{"cidrs":["10.0.0.0/8"],"checkForwardedFor":false}""")))
            .IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ResourcePermissionFilter_PrivateHelpers_Cover_Attribute_And_ResourceId_Branches()
    {
        var actionDescriptor = ActionDescriptorFactory
            .CreateActionDescriptor(typeof(TestController).GetMethod(nameof(TestController.Action))!, typeof(TestController).GetTypeInfo());

        var attrs = InvokePrivateStatic<IEnumerable<Attribute>>(
                typeof(ResourcePermissionAuthorizationFilter),
                "GetPermissionAttributes",
                actionDescriptor)
            .ToList();
        attrs.Should().HaveCountGreaterThan(1);

        foreach (var attr in attrs)
        {
            InvokePrivateStatic<bool>(typeof(ResourcePermissionAuthorizationFilter), "IsPermissionAttribute", attr)
                .Should().BeTrue();
        }

        InvokePrivateStatic<bool>(typeof(ResourcePermissionAuthorizationFilter), "IsPermissionAttribute", new ObsoleteAttribute())
            .Should().BeFalse();

        var routeId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["id"] = routeId.ToString();
        InvokePrivateStatic<Guid?>(typeof(ResourcePermissionAuthorizationFilter), "ExtractResourceId", httpContext, "id")
            .Should().Be(routeId);

        httpContext.Request.RouteValues["id"] = "bad";
        var queryId = Guid.NewGuid();
        httpContext.Request.QueryString = new QueryString($"?id={queryId}");
        InvokePrivateStatic<Guid?>(typeof(ResourcePermissionAuthorizationFilter), "ExtractResourceId", httpContext, "id")
            .Should().Be(queryId);

        httpContext.Request.QueryString = new QueryString("?id=bad");
        InvokePrivateStatic<Guid?>(typeof(ResourcePermissionAuthorizationFilter), "ExtractResourceId", httpContext, "id")
            .Should().BeNull();
    }

    [Fact]
    public async Task CachedAclService_Covers_Principal_And_User_Resource_Invalidation()
    {
        var inner = new Mock<IAccessControlListService>();
        var metrics = new Mock<ICacheMetricsService>();
        var hybrid = new Mock<IHybridPermissionCache>();
        var tenantVersion = new Mock<ITenantSecurityVersionStore>();
        var userVersion = new Mock<IUserSecurityVersionStore>();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grantorId = Guid.NewGuid();

        tenantVersion.Setup(v => v.GetVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        userVersion.Setup(v => v.GetVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        hybrid.Setup(h => h.GetValueAsync<AccessLevel>(It.IsAny<string>(), "acl", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CacheResult<AccessLevel>.Miss());
        inner.Setup(i => i.EvaluateAccessAsync(It.IsAny<AclSubject>(), tenantId, "Document", "doc-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessLevel.Read);
        inner.Setup(i => i.GetAccessLevelAsync(userId, tenantId, "Document", "doc-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessLevel.Write);

        var service = new CachedAccessControlListService(
            inner.Object,
            new MemoryCache(new MemoryCacheOptions()),
            tenantVersion.Object,
            userVersion.Object,
            Options.Create(new AuthorizationCacheOptions { AccessControlListTtlSeconds = 60 }),
            hybrid.Object,
            metrics.Object);

        var subject = AclSubject.ForUser(userId, [Guid.NewGuid()], [Guid.NewGuid()]);
        (await service.EvaluateAccessAsync(subject, tenantId, "Document", "doc-1")).Should().Be(AccessLevel.Read);
        await service.GrantAccessAsync(grantorId, AclPrincipalType.User, userId, tenantId, "Document", "doc-1", AccessLevel.Admin);
        await service.DenyAccessAsync(grantorId, AclPrincipalType.User, userId, tenantId, "Document", "doc-1", AccessLevel.Read);
        await service.RevokeAccessAsync(grantorId, AclPrincipalType.User, userId, tenantId, "Document", "doc-1");

        (await service.GetAccessLevelAsync(userId, tenantId, "Document", "doc-1")).Should().Be(AccessLevel.Write);
        await service.GrantAccessAsync(grantorId, userId, tenantId, "Document", "doc-1", AccessLevel.Admin);
        await service.RevokeAccessAsync(grantorId, userId, tenantId, "Document", "doc-1");

        metrics.Verify(m => m.RecordEviction(CacheLevel.L1, "acl"), Times.AtLeastOnce);
    }

    [Fact]
    public void Handler_Actor_PrivateProperties_Cover_Getters()
    {
        var actor = new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        };
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(a => a.ActorContext).Returns(actor);

        PrivateActor(new UpdateUserPermissionsCommandHandler(
            Mock.Of<IResourcePermissionService>(),
            accessor.Object,
            Mock.Of<IPermissionQueryService>(),
            NullLogger<UpdateUserPermissionsCommandHandler>.Instance)).Should().BeSameAs(actor);

        PrivateActor(new ShareResourceCommandHandler(
            Mock.Of<IResourcePermissionService>(),
            accessor.Object,
            Mock.Of<IPermissionQueryService>(),
            NullLogger<ShareResourceCommandHandler>.Instance)).Should().BeSameAs(actor);

        PrivateActor(new AcceptResourceInvitationCommandHandler(
            Mock.Of<IApplicationDbContext>(),
            Mock.Of<IResourcePermissionService>(),
            accessor.Object,
            NullLogger<AcceptResourceInvitationCommandHandler>.Instance)).Should().BeSameAs(actor);

        PrivateActor(new DeclineResourceInvitationCommandHandler(
            Mock.Of<IApplicationDbContext>(),
            Mock.Of<IResourcePermissionService>(),
            accessor.Object,
            NullLogger<DeclineResourceInvitationCommandHandler>.Instance)).Should().BeSameAs(actor);

        PrivateActor(new RevokeResourceInvitationCommandHandler(
            Mock.Of<IApplicationDbContext>(),
            Mock.Of<IResourcePermissionService>(),
            accessor.Object,
            NullLogger<RevokeResourceInvitationCommandHandler>.Instance)).Should().BeSameAs(actor);

        PrivateActor(new RemoveUserAccessCommandHandler(
            Mock.Of<IResourcePermissionService>(),
            accessor.Object,
            Mock.Of<IPermissionQueryService>(),
            NullLogger<RemoveUserAccessCommandHandler>.Instance)).Should().BeSameAs(actor);

        PrivateActor(new GetResourceInvitationQueryHandler(
            Mock.Of<IApplicationDbContext>(),
            accessor.Object,
            NullLogger<GetResourceInvitationQueryHandler>.Instance)).Should().BeSameAs(actor);

        PrivateActor(new GetPendingResourceInvitationsQueryHandler(
            Mock.Of<IApplicationDbContext>(),
            accessor.Object)).Should().BeSameAs(actor);
    }

    [Fact]
    public void PermissionStatics_Cover_Scoped_And_Unscoped_Static_Constructors()
    {
        var permissions = new Permission[]
        {
            AdminPermission.Wildcard,
            AdminPermission.Admin,
            AdminPermission.TenantAdmin,
            UsersPermission.Read,
            UsersPermission.Create,
            UsersPermission.Update,
            UsersPermission.Delete,
            UsersPermission.Admin,
            UsersPermission.Purge,
            UsersPermission.EditSelf,
            UsersPermission.DeleteSelf,
            UsersPermission.ReadSelf,
            UsersPermission.Manage,
            ContentPermission.Read,
            ContentPermission.Write,
            ContentPermission.Admin,
            ProjectPermission.Read,
            ProjectPermission.Write,
            ProjectPermission.Admin,
            CoursePermission.Read,
            CoursePermission.Manage,
            ProductsPermission.Read,
            ProductsPermission.Create,
            ProductsPermission.Update,
            ProductsPermission.Delete,
            ProductsPermission.Manage,
            ProductsPermission.PricingManage,
            PromoCodesPermission.Read,
            PromoCodesPermission.Create,
            PromoCodesPermission.Update,
            PromoCodesPermission.Delete,
            PromoCodesPermission.Manage,
            OrdersPermission.Read,
            OrdersPermission.ReadAll,
            OrdersPermission.Create,
            OrdersPermission.Update,
            OrdersPermission.Delete,
            OrdersPermission.Capture,
            OrdersPermission.Hold,
            OrdersPermission.Release,
            OrdersPermission.Refund,
            OrdersPermission.Manage,
            EntitlementsPermission.ReadSelf,
            EntitlementsPermission.ReadAll,
            EntitlementsPermission.Grant,
            EntitlementsPermission.Revoke,
            EntitlementsPermission.Manage,
            AssetsPermission.Read,
            AssetsPermission.Create,
            AssetsPermission.Update,
            AssetsPermission.Delete,
            AssetsPermission.Admin,
            AssetsPermission.Moderate,
            AssetsPermission.Transform,
            AssetsPermission.GenerateUrl,
            AssetsPermission.Report
        };

        permissions.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Key));
        permissions.Should().Contain(p => p.Scope == "self");
        permissions.Should().Contain(p => p.Scope == "all");
        permissions.Should().Contain(p => p.Scope == "manage");
        AdminPermission.Wildcard.Action.Should().Be("*");

        var custom = CreatePermission(typeof(AdminPermission), "admin", "Admin without action separator");
        custom.Action.Should().Be("admin");

        foreach (var permissionType in new[]
                 {
                     typeof(UsersPermission),
                     typeof(ContentPermission),
                     typeof(ProjectPermission),
                     typeof(CoursePermission),
                     typeof(ProductsPermission),
                     typeof(PromoCodesPermission),
                     typeof(OrdersPermission),
                     typeof(EntitlementsPermission),
                     typeof(AssetsPermission)
                 })
        {
            CreatePermission(permissionType, "custom:read", "Unscoped").Scope.Should().BeNull();
            CreatePermission(permissionType, "custom:read:tenant", "Scoped").Scope.Should().Be("tenant");
        }
    }

    [Fact]
    public void PermissionBase_Covers_Equality_And_Null_Guards()
    {
        var first = new TestPermission("users", "read", null, "Read users");
        var same = new TestPermission("users", "read", null, "Read users again");
        var scoped = new TestPermission("users", "read", "self", "Read self");

        first.Key.Should().Be("users:read");
        scoped.Key.Should().Be("users:read:self");
        first.ToString().Should().Be("users:read");
        ((string)first).Should().Be("users:read");
        first.Equals(same).Should().BeTrue();
        first.Equals((object)same).Should().BeTrue();
        first.Equals(scoped).Should().BeFalse();
        first.Equals((Permission?)null).Should().BeFalse();
        first.Equals("users:read").Should().BeFalse();
        first.GetHashCode().Should().Be(same.GetHashCode());
        (first == same).Should().BeTrue();
        (first != scoped).Should().BeTrue();

        FluentActions.Invoking(() => new TestPermission(null!, "read", null, "desc"))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new TestPermission("users", null!, null, "desc"))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new TestPermission("users", "read", null, null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EffectivePermissions_And_AuditLog_Models_Cover_Computed_Members()
    {
        var permissionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "read", "write" };
        var effective = new EffectivePermissions
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Permissions = permissionSet,
            Sources = new Dictionary<string, PermissionSource> { ["read"] = PermissionSource.Static },
            RoleContributions =
            [
                new RoleContribution(Guid.NewGuid(), "Admin", ["read", "write"], false, null)
            ]
        };

        effective.HasPermission("read").Should().BeTrue();
        effective.HasPermission("delete").Should().BeFalse();
        effective.HasAllPermissions(["read", "write"]).Should().BeTrue();
        effective.HasAllPermissions(["read", "delete"]).Should().BeFalse();
        effective.HasAnyPermission(["delete", "write"]).Should().BeTrue();
        effective.HasAnyPermission(["delete"]).Should().BeFalse();
        effective.ResolvedAt.Should().BeCloseTo(SystemClock.UtcNow, TimeSpan.FromSeconds(5));

        var audit = new PermissionAuditLog
        {
            PermissionType = "tenant:read",
            Success = true
        };
        audit.Permission.Should().Be("tenant:read");
        audit.IsSuccessful().Should().BeTrue();
        audit.IsFailed().Should().BeFalse();

        audit.Success = false;
        audit.IsSuccessful().Should().BeFalse();
        audit.IsFailed().Should().BeTrue();
    }

    [Fact]
    public void ResourceInvitationQueryMappings_Cover_Internal_Static_Mapping()
    {
        var tenantId = TenantId.New();
        var invitation = new ResourceInvitation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "Invite@Example.com ",
            ResourceType = "Document",
            ResourceId = "doc-1",
            Permissions = ["read"],
            Message = "msg",
            InvitedByUserId = Guid.NewGuid(),
            InvitedByUserName = "Inviter",
            ExpiresAt = SystemClock.UtcNow.AddDays(1)
        };

        var mapperType = typeof(GetResourceInvitationQuery).Assembly
            .GetType("GameGuild.Identity.Authorization.ResourceInvitationQueryMappings");
        mapperType.Should().NotBeNull();

        var dto = InvokePrivateStatic<ResourceInvitationDto>(mapperType!, "MapInvitation", invitation);
        dto.InvitationId.Should().Be(invitation.Id);
        dto.TenantId.Should().Be(tenantId.Value);
        dto.Status.Should().Be("Pending");

        InvokePrivateStatic<bool>(mapperType!, "EmailsMatch", " invite@example.com ", "INVITE@example.com")
            .Should().BeTrue();
        InvokePrivateStatic<bool>(mapperType!, "EmailsMatch", "left@example.com", "right@example.com")
            .Should().BeFalse();

        var acceptEmailMatch = InvokePrivateStatic<bool>(
            typeof(AcceptResourceInvitationCommandHandler),
            "EmailsMatch",
            " invite@example.com ",
            "INVITE@example.com");
        acceptEmailMatch.Should().BeTrue();

        var declineEmailMatch = InvokePrivateStatic<bool>(
            typeof(DeclineResourceInvitationCommandHandler),
            "EmailsMatch",
            "left@example.com",
            "right@example.com");
        declineEmailMatch.Should().BeFalse();
    }

    [Fact]
    public void Authorization_Extension_And_Module_Paths_Cover_Service_Registration()
    {
        var services = new ServiceCollection();
        services.AddActorContextIntegration().Should().BeSameAs(services);
        services.Should().Contain(d => d.ServiceType == typeof(IActorContextAccessor));

        var module = new AuthorizationModule();
        module.Name.Should().Be("Authorization");
        module.Order.Should().Be(15);
        module.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_NullGuards_And_Default_Constructors_Are_Exercised_For_Remaining_Plumbing()
    {
        var types = new[]
        {
            typeof(AbacPolicyRepository),
            typeof(ConditionalPolicyRepository),
            typeof(DataMaskingRuleRepository),
            typeof(PolicyBundleRepository),
            typeof(PolicyBundleDeploymentRepository),
            typeof(PermissionTemplateVersionRepository),
            typeof(PermissionTemplateMigrationRepository),
            typeof(PolicyRegistryAuditLogRepository),
            typeof(AbacPolicyEvaluator),
            typeof(PolicyGateService),
            typeof(MemoryPolicyCache),
            typeof(ConditionalPolicyEvaluator),
            typeof(EnvironmentHandler),
            typeof(ResourceAccessHandler),
            typeof(RequireMfaRuleEvaluator),
            typeof(SelfOrPermissionRuleEvaluator),
            typeof(TenantMatchRuleEvaluator),
            typeof(OwnerOrAclRuleEvaluator),
            typeof(JitElevationService),
            typeof(DelegatedAdminService),
            typeof(PermissionDelegationService),
            typeof(PermissionAuditService),
            typeof(SoDService),
            typeof(RbacPermissionResolver),
            typeof(RulesetProvider),
            typeof(CachedPolicyDefinitionStore),
            typeof(AuthorizationTenantResolver),
            typeof(HttpAuthorizationTenantContext),
            typeof(AccessReviewsController),
            typeof(DelegatedAdminController),
            typeof(JitElevationsController),
            typeof(PermissionAnalyticsController),
            typeof(PermissionDelegationsController),
            typeof(TenantPermissionsController),
            typeof(SoDController),
            typeof(AccessReviewMiddleware),
            typeof(AbacPolicyMiddleware),
            typeof(PermissionCachingMiddleware)
        };

        var created = 0;
        foreach (var type in types)
        {
            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!TryCreateArguments(ctor, out var args))
                    continue;

                try
                {
                    var instance = ctor.Invoke(args);
                    foreach (var property in type.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
                                 .Where(p => p.GetMethod is not null && p.GetIndexParameters().Length == 0))
                    {
                        try
                        {
                            property.GetValue(instance);
                        }
                        catch (TargetInvocationException)
                        {
                        }
                    }
                    created++;
                }
                catch (TargetInvocationException)
                {
                    // Some framework classes validate runtime state in the constructor.
                }

                var parameters = ctor.GetParameters();
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType.IsValueType)
                        continue;

                    var nullArgs = args.ToArray();
                    nullArgs[i] = null;
                    try
                    {
                        ctor.Invoke(nullArgs);
                    }
                    catch (TargetInvocationException)
                    {
                    }
                    catch (ArgumentException)
                    {
                    }
                }
            }
        }

        created.Should().BeGreaterThan(10);
    }

    [Fact]
    public void Small_Remaining_Models_Extensions_And_Handler_Properties_Are_Covered()
    {
        var actor = new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        };
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(a => a.ActorContext).Returns(actor);

        PrivateActor(new GrantTenantPermissionCommandHandler(
            Mock.Of<IPermissionGrantService>(),
            accessor.Object,
            NullLogger<GrantTenantPermissionCommandHandler>.Instance)).Should().BeSameAs(actor);
        PrivateActor(new RevokeTenantPermissionCommandHandler(
            Mock.Of<IPermissionGrantService>(),
            accessor.Object,
            NullLogger<RevokeTenantPermissionCommandHandler>.Instance)).Should().BeSameAs(actor);
        PrivateActor(new SetGlobalDefaultPermissionsCommandHandler(
            Mock.Of<IPermissionGrantService>(),
            accessor.Object,
            NullLogger<SetGlobalDefaultPermissionsCommandHandler>.Instance)).Should().BeSameAs(actor);
        PrivateActor(new SetTenantDefaultPermissionsCommandHandler(
            Mock.Of<IPermissionGrantService>(),
            accessor.Object,
            NullLogger<SetTenantDefaultPermissionsCommandHandler>.Instance)).Should().BeSameAs(actor);
        PrivateActor(new DenyTenantPermissionCommandHandler(
            Mock.Of<IPermissionGrantService>(),
            accessor.Object,
            NullLogger<DenyTenantPermissionCommandHandler>.Instance)).Should().BeSameAs(actor);
        PrivateActor(new RemoveDenyPermissionsCommandHandler(
            Mock.Of<IPermissionGrantService>(),
            accessor.Object,
            NullLogger<RemoveDenyPermissionsCommandHandler>.Instance)).Should().BeSameAs(actor);

        PrivateActor(new GetTenantPermissionsQueryHandler(
            Mock.Of<IPermissionQueryService>(),
            accessor.Object,
            NullLogger<GetTenantPermissionsQueryHandler>.Instance)).Should().BeSameAs(actor);
        PrivateActor(new GetEffectivePermissionsQueryHandler(
            accessor.Object,
            Mock.Of<IPermissionQueryService>(),
            NullLogger<GetEffectivePermissionsQueryHandler>.Instance)).Should().BeSameAs(actor);
        PrivateActor(new HasPermissionQueryHandler(
            accessor.Object,
            Mock.Of<IPermissionQueryService>(),
            NullLogger<HasPermissionQueryHandler>.Instance)).Should().BeSameAs(actor);
        PrivateActor(new GetResourceUsersQueryHandler(
            Mock.Of<IResourcePermissionService>(),
            accessor.Object,
            Mock.Of<IPermissionQueryService>(),
            NullLogger<GetResourceUsersQueryHandler>.Instance)).Should().BeSameAs(actor);

        var response = new GetResourceUsersResponse
        {
            ResourceType = "Document",
            ResourceId = "doc-1",
            Users =
            [
                new ResourceUser
                {
                    UserId = Guid.NewGuid(),
                    ResourceType = "Document",
                    ResourceId = "doc-1",
                    Permissions = ["Owner"],
                    GrantedAt = SystemClock.UtcNow,
                    GrantedByUserId = Guid.NewGuid(),
                    IsActive = true
                }
            ]
        };
        response.TotalCount.Should().Be(1);
        response.OwnerCount.Should().Be(1);

        var endpointBuilder = new Mock<Microsoft.AspNetCore.Routing.IEndpointRouteBuilder>();
        new AuthorizationModule().MapEndpoints(endpointBuilder.Object).Should().BeSameAs(endpointBuilder.Object);

        var app = new ApplicationBuilder(new ServiceCollection()
            .AddSingleton<IActorContextAccessor, ActorContextAccessor>()
            .BuildServiceProvider());
        app.UseActorContext().Should().BeSameAs(app);

        var reminder = new AccessReviewReminderNotification(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        reminder.CampaignId.Should().NotBeEmpty();

        var updateRule = new UpdateSoDRuleRequest("rule", "desc", ["a", "b"], SoDRuleType.PermissionConflict, true);
        updateRule.IsEnabled.Should().BeTrue();
        var resolution = new ResolveViolationRequest(Guid.NewGuid(), SoDResolutionAction.GrantException, "noted");
        resolution.Notes.Should().Be("noted");
    }

    [Fact]
    public void ResourceAccessHandler_PrivateHelpers_Cover_Subject_Tenant_And_Resource_Branches()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var tenantContext = new Mock<IAuthorizationTenantContext>();
        tenantContext.SetupGet(t => t.HasTenant).Returns(true);
        tenantContext.SetupGet(t => t.TenantId).Returns(tenantId);

        var handler = new ResourceAccessHandler(
            tenantContext.Object,
            Mock.Of<IAccessControlListService>(),
            Options.Create(new AuthorizationTokenOptions
            {
                RoleIdClaimType = "role_id",
                GroupIdClaimType = "group_id",
                TenantClaimType = "tenant_id"
            }),
            NullLogger<ResourceAccessHandler>.Instance);

        var anonymous = InvokePrivate<AclSubject>(handler, "BuildAclSubject", new ClaimsPrincipal(new ClaimsIdentity()));
        anonymous.IsAuthenticated.Should().BeFalse();

        var subject = InvokePrivate<AclSubject>(
            handler,
            "BuildAclSubject",
            Principal(
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("role_id", roleId.ToString()),
                new Claim("role_id", "bad"),
                new Claim("group_id", groupId.ToString()),
                new Claim("group_id", "bad")));
        subject.UserId.Should().Be(userId);
        subject.RoleIds.Should().Contain(roleId);
        subject.GroupIds.Should().Contain(groupId);

        var args = new object?[] { Principal(), null };
        InvokePrivate<bool>(handler, "TryGetTenantId", args).Should().BeTrue();
        args[1].Should().Be(tenantId);

        tenantContext.SetupGet(t => t.TenantId).Returns(Guid.Empty);
        args = [Principal(), null];
        InvokePrivate<bool>(handler, "TryGetTenantId", args).Should().BeFalse();

        tenantContext.SetupGet(t => t.HasTenant).Returns(false);
        tenantContext.SetupGet(t => t.TenantId).Returns((Guid?)null);
        var claimTenant = Guid.NewGuid();
        args = [Principal(new Claim("tenant_id", claimTenant.ToString())), null];
        InvokePrivate<bool>(handler, "TryGetTenantId", args).Should().BeTrue();
        args[1].Should().Be(claimTenant);

        args = [Principal(new Claim("tenant_id", "bad")), null];
        InvokePrivate<bool>(handler, "TryGetTenantId", args).Should().BeFalse();

        var aclResource = new TestAclResource(userId, tenantId, "Document", "doc-1");
        GetResourceIdentifiers(aclResource, new ResourceAccessRequirement(resourceType: "Fallback"))
            .Should().Be(("Document", "doc-1"));
        GetResourceIdentifiers(new TestOwnedResource(userId, tenantId), new ResourceAccessRequirement(resourceType: "OwnerResource"))
            .Should().Be(("OwnerResource", userId.ToString()));
        GetResourceIdentifiers(new object(), new ResourceAccessRequirement(resourceType: ""))
            .Should().Be((null, null));
    }

    [Fact]
    public async Task PolicyGateService_Covers_Static_Conditional_Abac_Environment_And_Helper_Branches()
    {
        var conditional = new Mock<IConditionalPolicyEvaluator>();
        var abac = new Mock<IAbacPolicyEvaluator>();
        conditional.Setup(c => c.EvaluateAsync(It.IsAny<ConditionalPolicyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalPolicyResult(true));
        abac.Setup(a => a.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AbacEvaluationResult(AbacDecision.Permit));

        var service = new PolicyGateService(conditional.Object, abac.Object, NullLogger<PolicyGateService>.Instance);
        var context = new PolicyGateContext
        {
            ActorId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ResourceType = "Document",
            ResourceId = Guid.NewGuid(),
            Action = "read",
            IpAddress = "10.0.0.1",
            UserAgent = "curl/8",
            GeoLocation = "US/CA",
            DeviceFingerprint = "device",
            Attributes = new Dictionary<string, object>
            {
                ["roles"] = new[] { "admin", "manager" },
                ["environment"] = "test",
                ["auth-time"] = SystemClock.UtcNow,
                ["mfa-verified"] = true,
                ["risk-score"] = 10,
                ["custom"] = "value"
            }
        };

        (await service.EvaluateGatesAsync(context)).IsAllowed.Should().BeTrue();
        (await service.EvaluateGateAsync(PolicyGateType.Environment, context)).IsAllowed.Should().BeTrue();
        (await service.EvaluateGateAsync((PolicyGateType)999, context)).IsAllowed.Should().BeTrue();

        var staticDenied = await service.EvaluateGateAsync(PolicyGateType.Static, context with
        {
            IpAddress = "127.0.0.1",
            Attributes = new Dictionary<string, object> { ["environment"] = "production" }
        });
        staticDenied.IsAllowed.Should().BeFalse();

        var missingAgent = await service.EvaluateGateAsync(PolicyGateType.Static, context with
        {
            UserAgent = null,
            Attributes = new Dictionary<string, object> { ["require-user-agent"] = "true" }
        });
        missingAgent.IsAllowed.Should().BeFalse();

        conditional.Setup(c => c.EvaluateAsync(It.IsAny<ConditionalPolicyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalPolicyResult(false, Guid.NewGuid(), "policy", "denied"));
        (await service.EvaluateGateAsync(PolicyGateType.Conditional, context)).IsAllowed.Should().BeFalse();

        conditional.Setup(c => c.EvaluateAsync(It.IsAny<ConditionalPolicyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalPolicyResult(true));
        abac.Setup(a => a.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AbacEvaluationResult(AbacDecision.Deny, Guid.NewGuid(), "abac", "no"));
        (await service.EvaluateGateAsync(PolicyGateType.Abac, context)).IsAllowed.Should().BeFalse();

        InvokePrivateStatic<IReadOnlyList<string>>(typeof(PolicyGateService), "GetRolesFromAttributes", (IReadOnlyDictionary<string, object>?)null)
            .Should().BeEmpty();
        InvokePrivateStatic<IReadOnlyList<string>>(typeof(PolicyGateService), "GetRolesFromAttributes", new Dictionary<string, object> { ["roles"] = new List<string> { "r1" } })
            .Should().Equal("r1");
        InvokePrivateStatic<DateTime?>(typeof(PolicyGateService), "GetDateTimeFromAttributes", context.Attributes, "auth-time")
            .Should().NotBeNull();
        InvokePrivateStatic<DateTime?>(typeof(PolicyGateService), "GetDateTimeFromAttributes", context.Attributes, "missing")
            .Should().BeNull();
        InvokePrivateStatic<bool?>(typeof(PolicyGateService), "GetBoolFromAttributes", context.Attributes, "mfa-verified")
            .Should().BeTrue();
        InvokePrivateStatic<int?>(typeof(PolicyGateService), "GetIntFromAttributes", context.Attributes, "risk-score")
            .Should().Be(10);
        InvokePrivateStatic<Dictionary<string, string>>(typeof(PolicyGateService), "GetStringAttributesFromDict", context.Attributes)
            .Should().ContainKey("custom");
        InvokePrivateStatic<Dictionary<string, string>>(typeof(PolicyGateService), "GetStringAttributesFromDict", (IReadOnlyDictionary<string, object>?)null)
            .Should().BeEmpty();
    }

    [Fact]
    public void MemoryPolicyCache_Covers_Distributed_Success_Failure_And_Invalidation_Paths()
    {
        var distributed = new DictionaryDistributedCache();
        var options = Options.Create(new AuthorizationCacheOptions
        {
            UseDistributedCache = true,
            PolicyTtlSeconds = 60
        });
        var cache = new MemoryPolicyCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }),
            options,
            distributed,
            NullLogger<MemoryPolicyCache>.Instance);
        var policy = new AuthorizationPolicyBuilder("Bearer")
            .RequireAuthenticatedUser()
            .Build();

        cache.Set("policy", "tenant", 1, policy);
        cache.Get("policy", "tenant", 1).Should().NotBeNull();
        cache.Invalidate("missing");
        cache.Invalidate("policy", "tenant");
        cache.Get("policy", "tenant", 1).Should().NotBeNull();
        cache.Invalidate("tenant");

        var throwingCache = new MemoryPolicyCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }),
            options,
            new ThrowingDistributedCache(),
            NullLogger<MemoryPolicyCache>.Instance);
        throwingCache.Get("missing", "tenant", 1).Should().BeNull();
        FluentActions.Invoking(() => throwingCache.Set("policy", "tenant", 1, policy))
            .Should().Throw<InvalidOperationException>();

        InvokePrivateStatic<AuthorizationPolicy?>(typeof(MemoryPolicyCache), "DeserializePolicy", "not json")
            .Should().BeNull();
        InvokePrivateStatic<AuthorizationPolicy?>(
                typeof(MemoryPolicyCache),
                "DeserializePolicy",
                """{"AuthenticationSchemes":["Bearer"],"RequireAuthenticatedUser":true,"RequirementTypes":[]}""")
            .Should().NotBeNull();
    }

    [Fact]
    public async Task CacheInvalidationService_Covers_Event_And_Key_Handling_Branches()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var hybrid = new Mock<IHybridPermissionCache>();
        var version = new Mock<ITenantSecurityVersionStore>();
        var metrics = new Mock<ICacheMetricsService>();
        var service = new CacheInvalidationService(
            new MemoryCache(new MemoryCacheOptions()),
            version.Object,
            hybrid.Object,
            metrics.Object,
            Options.Create(new AuthorizationCacheOptions { UseDistributedCache = true, UsePubSubInvalidation = true }),
            NullLogger<CacheInvalidationService>.Instance);

        service.TrackKey(tenantId, $"perm:{tenantId}:{userId}:v1");
        service.TrackKey(tenantId, $"acl:{tenantId}:user:Document:doc-1:v1");
        service.TrackKey(tenantId, $"policy:{tenantId}:policy-a:v1");

        await service.InvalidateUserAsync(userId, tenantId);
        await service.InvalidateResourceAsync(tenantId, "Document", "doc-1");
        await service.InvalidatePolicyAsync(tenantId, "policy-a");
        await service.InvalidatePolicyAsync(tenantId);
        await service.InvalidateTenantAsync(tenantId);
        await service.PublishInvalidationAsync(new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.Policy,
            TenantId = tenantId,
            OriginInstanceId = "remote"
        });

        foreach (var invalidationType in Enum.GetValues<CacheInvalidationType>())
        {
            service.HandleInvalidationEvent(new CacheInvalidationEvent
            {
                Type = invalidationType,
                TenantId = tenantId,
                UserId = userId,
                ResourceType = "Document",
                ResourceId = "doc-1",
                PolicyName = "policy-a",
                OriginInstanceId = "remote"
            });
        }

        service.HandleInvalidationEvent(new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.Resource,
            TenantId = tenantId,
            OriginInstanceId = "remote"
        });
    }

    [Fact]
    public async Task RuleEvaluator_EdgeCases_Cover_Remaining_Rule_Branches()
    {
        var parameters = RuleParameters.FromJson("""
        {
          "name": "value",
          "array": ["a", 1, "b"],
          "single": "one",
          "number": 42,
          "numberText": "43",
          "badNumber": "bad",
          "trueText": "true",
          "falseText": "false",
          "object": {}
        }
        """);
        parameters.GetRequiredString("name").Should().Be("value");
        FluentActions.Invoking(() => parameters.GetRequiredString("missing"))
            .Should().Throw<InvalidOperationException>();
        parameters.GetStringArray("array").Should().Equal("a", "b");
        parameters.GetStringArray("single").Should().Equal("one");
        parameters.GetStringArray("object").Should().BeEmpty();
        parameters.GetBool("trueText").Should().BeTrue();
        parameters.GetBool("falseText", true).Should().BeFalse();
        parameters.GetBool("object", true).Should().BeTrue();
        parameters.GetInt("number").Should().Be(42);
        parameters.GetInt("numberText").Should().Be(43);
        parameters.GetInt("badNumber", 7).Should().Be(7);
        parameters.HasParameter("name").Should().BeTrue();
        parameters.GetRaw("name").Should().NotBeNull();
        RuleParameters.FromDictionary(null).HasParameter("x").Should().BeFalse();
        RuleParameters.FromDictionary(new Dictionary<string, JsonElement>()).HasParameter("x").Should().BeFalse();

        var mfa = new RequireMfaRuleEvaluator();
        (await mfa.EvaluateAsync(new AuthorizationHandlerContext([new TestRequirement()], new ClaimsPrincipal(new ClaimsIdentity()), null), new RuleParameters()))
            .IsSuccess.Should().BeFalse();
        (await mfa.EvaluateAsync(CreateAuthorizationContextWithClaims(new Claim("amr", "pwd")), new RuleParameters()))
            .IsSuccess.Should().BeFalse();
        (await mfa.EvaluateAsync(CreateAuthorizationContextWithClaims(new Claim("mfa_verified", "true")), RuleParameters.FromJson("""{"requireRecent":true}""")))
            .IsSuccess.Should().BeFalse();
        (await mfa.EvaluateAsync(CreateAuthorizationContextWithClaims(new Claim("amr", "mfa"), new Claim("mfa_time", "bad")), RuleParameters.FromJson("""{"requireRecent":true}""")))
            .IsSuccess.Should().BeFalse();
        (await mfa.EvaluateAsync(CreateAuthorizationContextWithClaims(new Claim("amr", "mfa"), new Claim("mfa_time", DateTime.UtcNow.ToString("O"))), RuleParameters.FromJson("""{"requireRecent":true}""")))
            .IsSuccess.Should().BeTrue();

        InvokePrivateStatic<Guid?>(typeof(SelfOrPermissionRuleEvaluator), "GetTargetUserIdFromResource", null, new RuleParameters())
            .Should().BeNull();
        InvokePrivateStatic<Guid?>(typeof(SelfOrPermissionRuleEvaluator), "GetTargetUserIdFromResource", new TestUserIdResource(Guid.NewGuid()), new RuleParameters())
            .Should().NotBeNull();
        InvokePrivateStatic<Guid?>(typeof(SelfOrPermissionRuleEvaluator), "GetTargetUserIdFromResource", new { UserId = Guid.NewGuid() }, new RuleParameters())
            .Should().NotBeNull();
        InvokePrivateStatic<Guid?>(typeof(SelfOrPermissionRuleEvaluator), "GetTargetUserIdFromResource", new Dictionary<string, object> { ["UserId"] = Guid.NewGuid() }, new RuleParameters())
            .Should().NotBeNull();
        InvokePrivateStatic<Guid?>(typeof(SelfOrPermissionRuleEvaluator), "GetTargetUserIdFromResource", new object(), new RuleParameters())
            .Should().BeNull();
    }

    [Fact]
    public void Conditional_Abac_Environment_And_Repository_Edge_Branches_Are_Covered()
    {
        var conditional = new ConditionalPolicyEvaluator(
            Mock.Of<IConditionalPolicyRepository>(),
            NullLogger<ConditionalPolicyEvaluator>.Instance);
        var conditionalContext = new ConditionalPolicyContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Document",
            Guid.NewGuid(),
            "read",
            ["admin"],
            IpAddress: "10.0.0.1",
            UserAgent: "Mozilla",
            DeviceFingerprint: "device-a",
            GeoCountry: "US",
            GeoRegion: "CA",
            AuthenticationTime: SystemClock.UtcNow.AddMinutes(-5),
            IsMfaVerified: true,
            RiskScore: 10,
            CustomAttributes: new Dictionary<string, string> { ["department"] = "sales" });

        InvokePrivate<bool>(conditional, "EvaluateTimeConditions", "not-json").Should().BeTrue();
        InvokePrivate<bool>(conditional, "EvaluateTimeConditions", JsonSerializer.Serialize(new { DaysOfWeek = new[] { ((int)SystemClock.UtcNow.DayOfWeek + 1) % 7 } })).Should().BeFalse();
        InvokePrivate<bool>(conditional, "EvaluateTimeConditions", JsonSerializer.Serialize(new { StartTime = "23:59", EndTime = "00:00" })).Should().BeFalse();
        InvokePrivate<bool>(conditional, "EvaluateEnvironmentConditions", JsonSerializer.Serialize(new { RequireMfa = true }), conditionalContext with { IsMfaVerified = false }).Should().BeFalse();
        InvokePrivate<bool>(conditional, "EvaluateEnvironmentConditions", JsonSerializer.Serialize(new { MaxRiskScore = 5 }), conditionalContext).Should().BeFalse();
        InvokePrivate<bool>(conditional, "EvaluateEnvironmentConditions", JsonSerializer.Serialize(new { MaxSessionAgeMinutes = 1 }), conditionalContext with { AuthenticationTime = SystemClock.UtcNow.AddMinutes(-5) }).Should().BeFalse();
        InvokePrivate<bool>(conditional, "EvaluateLocationConditions", JsonSerializer.Serialize(new { AllowedCountries = new[] { "CA" } }), conditionalContext).Should().BeFalse();
        InvokePrivate<bool>(conditional, "EvaluateLocationConditions", JsonSerializer.Serialize(new { BlockedCountries = new[] { "US" } }), conditionalContext).Should().BeFalse();
        InvokePrivate<bool>(conditional, "EvaluateLocationConditions", JsonSerializer.Serialize(new { AllowedIpRanges = new[] { "10.0.0.0/24" } }), conditionalContext).Should().BeTrue();
        InvokePrivate<bool>(conditional, "EvaluateLocationConditions", JsonSerializer.Serialize(new { AllowedIpRanges = new[] { "192.168.0.0/24" } }), conditionalContext).Should().BeFalse();
        InvokePrivate<bool>(conditional, "IsIpInRange", "10.0.0.1", "bad/cidr").Should().BeFalse();
        InvokePrivate<bool>(conditional, "IsIpInRange", "10.0.0.1", "10.0.0.2").Should().BeFalse();
        InvokePrivate<bool>(conditional, "EvaluateDeviceConditions", JsonSerializer.Serialize(new { AllowedFingerprints = new[] { "device-b" } }), conditionalContext).Should().BeFalse();
        InvokePrivate<bool>(conditional, "EvaluateDeviceConditions", JsonSerializer.Serialize(new { BlockedUserAgents = new[] { "moz" } }), conditionalContext).Should().BeFalse();
        InvokePrivate<bool>(conditional, "EvaluateDeviceConditions", "bad-json", conditionalContext).Should().BeTrue();
        InvokePrivate<bool>(conditional, "EvaluateCustomConditions", JsonSerializer.Serialize(new { department = "engineering" }), conditionalContext).Should().BeFalse();
        InvokePrivate<bool>(conditional, "EvaluateCustomConditions", JsonSerializer.Serialize(new { missing = "sales" }), conditionalContext).Should().BeFalse();
        InvokePrivate<bool>(conditional, "EvaluateCustomConditions", "bad-json", conditionalContext).Should().BeTrue();

        var abac = new AbacPolicyEvaluator(Mock.Of<IAbacPolicyRepository>(), NullLogger<AbacPolicyEvaluator>.Instance);
        var abacContext = new AbacRequestContext(
            new Dictionary<string, object> { ["role"] = "admin", ["age"] = "42", ["enabled"] = true, ["disabled"] = false, ["groups"] = new[] { "sales", "ops" } },
            new Dictionary<string, object> { ["resource.type"] = "Document", ["classification"] = "public" },
            new Dictionary<string, object> { ["action"] = "read" },
            new Dictionary<string, object> { ["ip"] = "10.0.0.1" });
        InvokePrivate<bool>(abac, "EvaluateJsonConditions", """{"missing":"value"}""", abacContext.SubjectAttributes).Should().BeFalse();
        InvokePrivate<bool>(abac, "EvaluateJsonConditions", """{"role":"ADMIN","age":42,"enabled":true,"disabled":false,"groups":["sales"]}""", abacContext.SubjectAttributes).Should().BeTrue();
        InvokePrivate<bool>(abac, "EvaluateJsonConditions", "bad-json", abacContext.SubjectAttributes).Should().BeFalse();
        InvokePrivate<bool>(
                abac,
                "EvaluatePolicy",
                new AbacPolicy { ResourceType = "Document" },
                abacContext with { ResourceAttributes = new Dictionary<string, object>() })
            .Should().BeFalse();
        InvokePrivate<bool>(abac, "EvaluatePolicy", new AbacPolicy { ResourceType = "Other" }, abacContext).Should().BeFalse();
        InvokePrivate<bool>(abac, "EvaluatePolicy", new AbacPolicy { SubjectConditions = """{"role":"user"}""" }, abacContext).Should().BeFalse();
        InvokePrivate<bool>(abac, "EvaluatePolicy", new AbacPolicy { ResourceConditions = """{"classification":"private"}""" }, abacContext).Should().BeFalse();
        InvokePrivate<bool>(abac, "EvaluatePolicy", new AbacPolicy { EnvironmentConditions = """{"ip":"127.0.0.1"}""" }, abacContext).Should().BeFalse();
        InvokePrivate<bool>(abac, "EvaluatePolicy", new AbacPolicy { ActionConditions = """{"action":"write"}""" }, abacContext).Should().BeFalse();

        InvokePrivateStatic<bool>(typeof(EnvironmentHandler), "IsIpAllowed", IPAddress.Parse("10.0.0.5"), new[] { "10.0.0.0/24" }).Should().BeTrue();
        InvokePrivateStatic<bool>(typeof(EnvironmentHandler), "IsIpAllowed", IPAddress.Parse("10.0.0.5"), new[] { "10.0.0.5" }).Should().BeTrue();
        InvokePrivateStatic<bool>(typeof(EnvironmentHandler), "IsIpAllowed", IPAddress.Parse("10.0.0.5"), new[] { "bad", "10.0.0.6" }).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(EnvironmentHandler), "TryParseIpRange", "bad", null, 0).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(EnvironmentHandler), "IsInRange", IPAddress.Parse("::1"), IPAddress.Parse("10.0.0.0"), 24).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(EnvironmentHandler), "IsInRange", IPAddress.Parse("10.0.1.5"), IPAddress.Parse("10.0.0.0"), 24).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(EnvironmentHandler), "IsInRange", IPAddress.Parse("10.0.0.129"), IPAddress.Parse("10.0.0.0"), 25).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(EnvironmentHandler), "IsWithinTimeWindow", DateTimeOffset.UtcNow, new[] { new TimeWindow { Start = TimeOnly.MinValue, End = TimeOnly.MaxValue } }).Should().BeTrue();
        InvokePrivateStatic<bool>(typeof(EnvironmentHandler), "IsWithinTimeWindow", DateTimeOffset.UtcNow, Array.Empty<TimeWindow>()).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(EnvironmentHandler), "IsDeviceTypeAllowed", "Mozilla Mobile", new[] { "mobile" }).Should().BeTrue();
        InvokePrivateStatic<bool>(typeof(EnvironmentHandler), "IsDeviceTypeAllowed", "Mozilla iPad", new[] { "tablet" }).Should().BeTrue();
        InvokePrivateStatic<bool>(typeof(EnvironmentHandler), "IsDeviceTypeAllowed", "Mozilla", new[] { "desktop" }).Should().BeTrue();

        var dbContext = new DbContext(new DbContextOptionsBuilder<DbContext>().Options);
        TouchRepositorySet(new JitElevationRequestRepository(dbContext));
        TouchRepositorySet(new PermissionDelegationRepository(dbContext));
        TouchRepositorySet(new SoDRuleRepository(dbContext));
        TouchRepositorySet(new SoDViolationRepository(dbContext));
        TouchRepositorySet(new AccessReviewCampaignRepository(dbContext));
        TouchRepositorySet(new AccessReviewItemRepository(dbContext));
        TouchRepositorySet(new DelegatedAdminScopeRepository(dbContext));
        TouchRepositorySet(new TenantPermissionRepository(Mock.Of<IApplicationDbContext>()));
        TouchRepositorySet(new PermissionAuditLogRepository(Mock.Of<IApplicationDbContext>()));
        TouchRepositorySet(new DynamicRoleRepository(
            Mock.Of<IApplicationDbContext>(),
            Mock.Of<ICacheInvalidationService>()));
        TouchRepositorySet(new DynamicRoleAssignmentRepository(
            Mock.Of<IApplicationDbContext>(),
            Mock.Of<ICacheInvalidationService>()));
    }

    [Fact]
    public async Task Repository_Update_Delete_And_Legacy_Acl_Overloads_Are_Covered()
    {
        var policySet = new Mock<DbSet<PolicyDefinitionEntity>>();
        var aclSet = new Mock<DbSet<AccessControlListEntry>>();
        var versionSet = new Mock<DbSet<TenantSecurityVersion>>();
        var dbContext = new Mock<IApplicationDbContext>();
        dbContext.Setup(c => c.Set<PolicyDefinitionEntity>()).Returns(policySet.Object);
        dbContext.Setup(c => c.Set<AccessControlListEntry>()).Returns(aclSet.Object);
        dbContext.Setup(c => c.Set<TenantSecurityVersion>()).Returns(versionSet.Object);

        var policy = new PolicyDefinitionEntity { PolicyName = "documents.read", PolicyVersion = 4, Version = 1 };
        await new PolicyDefinitionRepository(dbContext.Object).UpdateAsync(policy);
        policy.PolicyVersion.Should().Be(5);
        policySet.Verify(s => s.Update(policy), Times.Once);

        await new PolicyDefinitionRepository(dbContext.Object).DeleteAsync(policy);
        policy.DeletedAt.Should().NotBeNull();
        policySet.Verify(s => s.Update(policy), Times.Exactly(2));

        var entry = new AccessControlListEntry
        {
            TenantId = Guid.NewGuid(),
            PrincipalType = AclPrincipalType.User,
            PrincipalId = Guid.NewGuid(),
            ResourceType = "Document",
            ResourceId = "doc-1",
            AccessLevel = AccessLevel.Read,
            GrantedBy = Guid.NewGuid(),
            Version = 1
        };
        await new AccessControlListEntryRepository(dbContext.Object).UpdateAsync(entry);
        aclSet.Verify(s => s.Update(entry), Times.Once);

        await new AccessControlListEntryRepository(dbContext.Object).DeleteAsync(entry);
        entry.DeletedAt.Should().NotBeNull();
        aclSet.Verify(s => s.Update(entry), Times.Exactly(2));

        var version = new TenantSecurityVersion { TenantId = Guid.NewGuid() };
        await new TenantSecurityVersionRepository(dbContext.Object).UpdateAsync(version);
        version.UpdatedAt.Should().BeCloseTo(SystemClock.UtcNow, TimeSpan.FromSeconds(5));
        versionSet.Verify(s => s.Update(version), Times.Once);

        var repository = new Mock<IAccessControlListEntryRepository>();
        var versions = new Mock<ITenantSecurityVersionRepository>();
        var service = new DatabaseAccessControlListService(repository.Object, versions.Object);
        var grantorId = Guid.NewGuid();
        var granteeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        repository.Setup(r => r.GetByPrincipalAndResourceAsync(
                tenantId,
                AclPrincipalType.User,
                granteeId,
                "Document",
                "doc-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccessControlListEntry?)null);

        await service.GrantAccessAsync(grantorId, granteeId, tenantId, "Document", "doc-1", AccessLevel.Write);
        repository.Verify(r => r.AddAsync(
            It.Is<AccessControlListEntry>(e => e.PrincipalType == AclPrincipalType.User && e.PrincipalId == granteeId),
            It.IsAny<CancellationToken>()), Times.Once);

        repository.Setup(r => r.GetByPrincipalAndResourceAsync(
                tenantId,
                AclPrincipalType.User,
                granteeId,
                "Document",
                "doc-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        await service.RevokeAccessAsync(grantorId, granteeId, tenantId, "Document", "doc-1");
        repository.Verify(r => r.DeleteAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Remaining_Model_Service_And_Cache_Branches_Are_Covered()
    {
        var options = Options.Create(new AuthorizationCacheOptions
        {
            UseDistributedCache = true,
            PolicyTtlSeconds = 60
        });
        var policy = new AuthorizationPolicyBuilder("Bearer").RequireAuthenticatedUser().Build();

        var throwingTenantInvalidation = new MemoryPolicyCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }),
            options,
            new ThrowingDistributedCache(),
            NullLogger<MemoryPolicyCache>.Instance);
        FluentActions.Invoking(() => throwingTenantInvalidation.Set("policy", "tenant", 1, policy))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => throwingTenantInvalidation.Invalidate("tenant"))
            .Should().Throw<InvalidOperationException>();

        var throwingPolicyInvalidation = new MemoryPolicyCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }),
            options,
            new ThrowingDistributedCache(),
            NullLogger<MemoryPolicyCache>.Instance);
        FluentActions.Invoking(() => throwingPolicyInvalidation.Set("policy", "tenant", 1, policy))
            .Should().Throw<InvalidOperationException>();
        var tenantKeys = (System.Collections.Concurrent.ConcurrentDictionary<string, HashSet<string>>)typeof(MemoryPolicyCache)
            .GetField("_tenantKeys", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(throwingPolicyInvalidation)!;
        tenantKeys["tenant"] = ["policy|tenant|1"];
        FluentActions.Invoking(() => throwingPolicyInvalidation.Invalidate("policy", "tenant"))
            .Should().Throw<InvalidOperationException>();

        InvokePrivateStatic<AuthorizationPolicy?>(typeof(MemoryPolicyCache), "DeserializePolicy", "null")
            .Should().BeNull();

        var aclEntry = new AccessControlListEntry
        {
            PrincipalType = AclPrincipalType.Role,
            PrincipalId = Guid.NewGuid()
        };
        var userId = Guid.NewGuid();
        aclEntry.PrincipalType = AclPrincipalType.User;
        aclEntry.PrincipalId = userId;
        aclEntry.PrincipalType.Should().Be(AclPrincipalType.User);
        aclEntry.PrincipalId.Should().Be(userId);
        aclEntry.IsActive = false;
        aclEntry.IsEffective.Should().BeFalse();

        var masking = new DataMaskingRule { MaskingType = MaskingType.Partial, ShowFirst = 3, ShowLast = 3 };
        masking.ApplyMasking("secret").Should().Be("secret");
        masking.ApplyMasking("").Should().Be("");
        masking.IsEnabled = false;
        masking.ApplyMasking("secret").Should().Be("secret");
        masking.IsEnabled = true;
        masking.MaskingType = MaskingType.PatternMask;
        masking.MaskingPattern = null;
        masking.ApplyMasking("secret").Should().Be("******");
        masking.ExemptUsers = userId.ToString();
        masking.IsUserExempt(userId).Should().BeTrue();

        var delegation = new PermissionDelegation
        {
            DelegatedPermissions = ["read"],
            StartsAt = SystemClock.UtcNow.AddMinutes(1),
            UsageLimit = 1,
            UsageCount = 1
        };
        delegation.IsValidNow().Should().BeFalse();
        delegation.AllowsPermission("read").Should().BeFalse();
        FluentActions.Invoking(() => delegation.RecordUsage()).Should().Throw<InvalidOperationException>();
        delegation.StartsAt = SystemClock.UtcNow.AddMinutes(-1);
        delegation.UsageCount = 0;
        delegation.RecordUsage();
        delegation.IsActive.Should().BeFalse();
        delegation.HasUsageRemaining().Should().BeFalse();
        delegation.GetRemainingUsage().Should().Be(0);

        var campaign = new AccessReviewCampaign
        {
            Status = AccessReviewStatus.Draft,
            StartDate = SystemClock.UtcNow.AddMinutes(-1),
            EndDate = SystemClock.UtcNow.AddMinutes(1),
            TotalItems = 2,
            ReviewedItems = 1
        };
        FluentActions.Invoking(() => campaign.Complete(Guid.NewGuid())).Should().Throw<InvalidOperationException>();
        campaign.Start();
        campaign.Complete(Guid.NewGuid());
        FluentActions.Invoking(campaign.Cancel).Should().Throw<InvalidOperationException>();
        campaign.GetCompletionPercentage().Should().Be(50);

        var reviewItem = new AccessReviewItem { Status = AccessReviewItemStatus.Approved };
        reviewItem.NeedsReminder(7).Should().BeFalse();
        reviewItem.Status = AccessReviewItemStatus.Pending;
        reviewItem.LastReminderSent = SystemClock.UtcNow.AddDays(-8);
        reviewItem.NeedsReminder(7).Should().BeTrue();

        new RuleDefinition { Type = "" }.Validate().IsValid.Should().BeFalse();
        new RuleDefinition { Type = "not-real" }.Validate().IsValid.Should().BeFalse();
        new RuleDefinition { Type = RuleTypes.RequireAllPermissions }.Validate().Errors.Should().Contain(e => e.Contains("permissions"));
        using var paramDocument = JsonDocument.Parse("""{"permissions":["read"]}""");
        new RuleDefinition
        {
            Type = RuleTypes.RequireAllPermissions,
            Params = new Dictionary<string, JsonElement> { ["permissions"] = paramDocument.RootElement.GetProperty("permissions").Clone() }
        }.Validate().IsValid.Should().BeTrue();

        RuleParameters.FromJson("null").HasParameter("x").Should().BeFalse();
        RuleParameters.FromJson("""{"object":{}}""").GetInt("object", 9).Should().Be(9);
        using var dictionaryDocument = JsonDocument.Parse("""{"answer":42}""");
        RuleParameters.FromDictionary(new Dictionary<string, JsonElement>
        {
            ["answer"] = dictionaryDocument.RootElement.GetProperty("answer").Clone()
        }).GetInt("answer").Should().Be(42);

        TimeWindow.Parse("09:00-17:00@UTC")!.TimeZone.Should().Be(TimeZoneInfo.Utc);
        TimeWindow.Parse("09:00-17:00-extra").Should().BeNull();
        TimeWindow.Parse("bad-17:00").Should().BeNull();
        JsonSerializer.Deserialize<TimeWindow>("""{"start":"bad","end":"17:00"}""").Should().BeNull();
        JsonSerializer.Deserialize<TimeWindow>("""{"start":"09:00","end":"bad"}""").Should().BeNull();
        JsonSerializer.Deserialize<TimeWindow>("""{"start":"09:00","end":"17:00","timezone":"UTC"}""")!.ToString()
            .Should().Be("09:00-17:00@UTC");

        var merger = new DefaultPolicyMerger();
        var baseDefinition = new PolicyDefinition
        {
            PolicyName = "documents.read",
            RequireAuthentication = true,
            AuthenticationSchemes = ["Bearer"],
            RequiredRoles = ["Reader"],
            RequiredPermissions = ["documents:read"],
            Rules =
            [
                new PolicyRule
                {
                    Type = RuleTypes.RequireAllPermissions,
                    Params = new Dictionary<string, object> { ["permissions"] = new[] { "documents:read" } }
                }
            ],
            UseRuleBasedEvaluation = true,
            Version = 1
        };
        merger.Merge(baseDefinition, null).Should().BeSameAs(baseDefinition);
        var merged = merger.Merge(baseDefinition, new PolicyDefinition
        {
            PolicyName = "documents.read",
            AuthenticationSchemes = [],
            RequiredRoles = [],
            RequiredPermissions = [],
            Rules = [],
            Version = 2
        });
        merged.Rules.Should().HaveCount(1);
        merger.Build(baseDefinition).Requirements.Should().Contain(r => r is RulesetRequirement);

        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.SetupGet(a => a.ActorContext).Returns(new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            Permissions = new HashSet<string>(),
            Roles = new HashSet<string>()
        });
        var grantService = new PermissionGrantService(
            Mock.Of<ITenantPermissionRepository>(),
            Mock.Of<IPermissionAuditService>(),
            Mock.Of<ITenantSecurityVersionStore>(),
            actorAccessor.Object,
            NullLogger<PermissionGrantService>.Instance);
        FluentActions.Invoking(() => InvokePrivate<object>(grantService, "ValidateGlobalDefaultAuthorization", null, "test"))
            .Should().Throw<TargetInvocationException>()
            .WithInnerException<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Remaining_Line_Method_Gaps_Are_Covered()
    {
        var conditional = new Mock<IConditionalPolicyEvaluator>();
        var abac = new Mock<IAbacPolicyEvaluator>();
        conditional.Setup(c => c.EvaluateAsync(It.IsAny<ConditionalPolicyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalPolicyResult(true));
        abac.Setup(a => a.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AbacEvaluationResult(AbacDecision.Permit));
        var gateService = new PolicyGateService(conditional.Object, abac.Object, NullLogger<PolicyGateService>.Instance);
        var staticResult = await gateService.EvaluateGateAsync(PolicyGateType.Static, new PolicyGateContext
        {
            ActorId = Guid.NewGuid(),
            ResourceType = "Document",
            Action = "read"
        });
        staticResult.IsAllowed.Should().BeTrue();

        InvokePrivateStatic<IReadOnlyList<string>>(
                typeof(PolicyGateService),
                "GetRolesFromAttributes",
                new Dictionary<string, object> { ["roles"] = new HashSet<string> { "set-role" } })
            .Should().Equal("set-role");
        InvokePrivateStatic<IReadOnlyList<string>>(
                typeof(PolicyGateService),
                "GetRolesFromAttributes",
                new Dictionary<string, object> { ["roles"] = 123 })
            .Should().BeEmpty();
        InvokePrivateStatic<DateTime?>(typeof(PolicyGateService), "GetDateTimeFromAttributes", new Dictionary<string, object> { ["auth-time"] = "bad" }, "auth-time")
            .Should().BeNull();
        InvokePrivateStatic<bool?>(typeof(PolicyGateService), "GetBoolFromAttributes", new Dictionary<string, object> { ["mfa"] = "true" }, "mfa")
            .Should().BeNull();
        InvokePrivateStatic<int?>(typeof(PolicyGateService), "GetIntFromAttributes", new Dictionary<string, object> { ["risk"] = "1" }, "risk")
            .Should().BeNull();

        var abacEvaluator = new AbacPolicyEvaluator(Mock.Of<IAbacPolicyRepository>(), NullLogger<AbacPolicyEvaluator>.Instance);
        var abacContext = new AbacRequestContext(
            new Dictionary<string, object> { ["role"] = "admin", ["age"] = "bad", ["enabled"] = "true", ["disabled"] = true, ["groups"] = new[] { "ops" } },
            new Dictionary<string, object> { ["resource.type"] = "Document" },
            new Dictionary<string, object> { ["action"] = "read" },
            new Dictionary<string, object>());
        InvokePrivate<bool>(abacEvaluator, "EvaluateJsonConditions", "null", abacContext.SubjectAttributes).Should().BeTrue();
        InvokePrivate<bool>(abacEvaluator, "EvaluatePolicy", new AbacPolicy { ResourceType = "Document" }, abacContext).Should().BeTrue();
        InvokePrivate<bool>(abacEvaluator, "EvaluatePolicy", new AbacPolicy { SubjectConditions = "null" }, abacContext).Should().BeTrue();
        InvokePrivate<bool>(abacEvaluator, "EvaluatePolicy", new AbacPolicy { ResourceConditions = "null" }, abacContext).Should().BeTrue();
        InvokePrivate<bool>(abacEvaluator, "EvaluatePolicy", new AbacPolicy { EnvironmentConditions = "null" }, abacContext).Should().BeTrue();
        InvokePrivate<bool>(abacEvaluator, "EvaluatePolicy", new AbacPolicy { ActionConditions = "null" }, abacContext).Should().BeTrue();
        using var compareDocument = JsonDocument.Parse("""
        {
          "string": "admin",
          "number": 42,
          "trueValue": true,
          "falseValue": false,
          "array": ["sales"],
          "object": {}
        }
        """);
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", null, compareDocument.RootElement.GetProperty("string")).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", new NullStringObject(), compareDocument.RootElement.GetProperty("string")).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", "41", compareDocument.RootElement.GetProperty("number")).Should().BeFalse();
        using var fractionalNumber = JsonDocument.Parse("1.5");
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", "1", fractionalNumber.RootElement).Should().BeFalse();
        InvokePrivate<bool>(
                abacEvaluator,
                "EvaluatePolicy",
                new AbacPolicy { ResourceType = "Document" },
                new AbacRequestContext(
                    new Dictionary<string, object>(),
                    new Dictionary<string, object> { ["resource.type"] = null! },
                    new Dictionary<string, object>(),
                    new Dictionary<string, object>()))
            .Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", "bad", compareDocument.RootElement.GetProperty("number")).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", false, compareDocument.RootElement.GetProperty("trueValue")).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", true, compareDocument.RootElement.GetProperty("falseValue")).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", new[] { "ops" }, compareDocument.RootElement.GetProperty("array")).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", "admin", compareDocument.RootElement.GetProperty("string")).Should().BeTrue();
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", "user", compareDocument.RootElement.GetProperty("string")).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", "42", compareDocument.RootElement.GetProperty("number")).Should().BeTrue();
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", true, compareDocument.RootElement.GetProperty("trueValue")).Should().BeTrue();
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", "true", compareDocument.RootElement.GetProperty("trueValue")).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", false, compareDocument.RootElement.GetProperty("falseValue")).Should().BeTrue();
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", "false", compareDocument.RootElement.GetProperty("falseValue")).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", "sales", compareDocument.RootElement.GetProperty("array")).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", "anything", compareDocument.RootElement.GetProperty("object")).Should().BeFalse();

        InvokePrivateStatic<IReadOnlyList<RuleDefinition>>(typeof(DefaultPolicyMerger), "ConvertToRuleDefinitions", (IReadOnlyList<PolicyRule>?)null)
            .Should().BeEmpty();
        InvokePrivateStatic<IReadOnlyList<RuleDefinition>>(typeof(DefaultPolicyMerger), "ConvertToRuleDefinitions", (IReadOnlyList<PolicyRule>)Array.Empty<PolicyRule>())
            .Should().BeEmpty();
        InvokePrivateStatic<Dictionary<string, JsonElement>?>(typeof(DefaultPolicyMerger), "ConvertParams", (IReadOnlyDictionary<string, object>?)null)
            .Should().BeNull();
        InvokePrivateStatic<Dictionary<string, JsonElement>?>(typeof(DefaultPolicyMerger), "ConvertParams", new Dictionary<string, object>())
            .Should().BeNull();
        var merger = new DefaultPolicyMerger();
        merger.Merge(
            new PolicyDefinition { PolicyName = "p", AuthenticationSchemes = [], RequiredRoles = [], RequiredPermissions = [] },
            new PolicyDefinition { PolicyName = "p", AuthenticationSchemes = ["ApiKey"], RequiredRoles = ["Admin"], RequiredPermissions = ["p"] })
            .AuthenticationSchemes.Should().Equal("ApiKey");
        merger.Build(new PolicyDefinition { PolicyName = "roles", RequiredRoles = ["Admin"] }).Requirements.Should().NotBeEmpty();
        merger.Build(new PolicyDefinition { PolicyName = "permissions", RequiredPermissions = ["read"] }).Requirements.Should().Contain(r => r is PermissionRequirement);
        merger.Build(new PolicyDefinition { PolicyName = "empty", RequireAuthentication = false })
            .Requirements.Should().NotBeEmpty();

        var attributes = InvokePrivateStatic<Dictionary<string, string>>(
            typeof(ActorContextMiddleware),
            "ExtractAttributes",
            Principal(
                new Claim(ClaimTypes.Email, "claim@example.com"),
                new Claim("email", "duplicate@example.com"),
                new Claim("tenant_setting:theme", "dark")),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        attributes.Should().Contain("email", "claim@example.com");
        attributes.Should().Contain("tenant_setting:theme", "dark");
        attributes.Should().ContainKey("tenant_id");

        var invalidListResult = InvokePrivateStatic<(IReadOnlyList<string> Values, bool IsValid)>(
            typeof(DatabasePolicyDefinitionStore),
            "DeserializeList",
            "not-json");
        invalidListResult.Values.Should().BeEmpty();
        invalidListResult.IsValid.Should().BeFalse();
        var invalidRulesResult = InvokePrivateStatic<(IReadOnlyList<PolicyRule>? Values, bool IsValid)>(
            typeof(DatabasePolicyDefinitionStore),
            "DeserializeRules",
            "not-json");
        invalidRulesResult.Values.Should().BeNull();
        invalidRulesResult.IsValid.Should().BeFalse();
        var validRulesResult = InvokePrivateStatic<(IReadOnlyList<PolicyRule>? Values, bool IsValid)>(
            typeof(DatabasePolicyDefinitionStore),
            "DeserializeRules",
            """
        [{"type":null,"description":"d","params":{"x":1},"enabled":false}]
        """);
        validRulesResult.IsValid.Should().BeTrue();
        validRulesResult.Values!.Single().Type.Should().BeEmpty();

        InvokePrivateStatic<bool>(typeof(EnvironmentHandler), "TryParseIpRange", "not-ip/24", null, 0).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(EnvironmentHandler), "TryParseIpRange", "10.0.0.0/nope", null, 0).Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(EnvironmentHandler), "IsInRange", IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.0"), 25).Should().BeTrue();
        InvokePrivateStatic<bool>(typeof(EnvironmentHandler), "IsDeviceTypeAllowed", "Mozilla iPhone", new[] { "desktop" }).Should().BeFalse();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Accept-Language"] = "en-US;q=0.9,pt-BR;q=0.8";
        httpContext.User = Principal(
            new Claim("ui_culture", "fr-FR"),
            new Claim("timezone", "UTC"),
            new Claim("date_format", "yyyy-MM-dd"),
            new Claim("number_format", "N2"));
        var localization = new LocalizationContext(new HttpContextAccessor { HttpContext = httpContext });
        localization.CultureCode.Should().Be("en-US");
        localization.UICultureCode.Should().Be("fr-FR");
        localization.TimeZone.Should().Be("UTC");
        localization.DateFormat.Should().Be("yyyy-MM-dd");
        localization.NumberFormat.Should().Be("N2");

        var resolverRepository = new Mock<ITenantPermissionRepository>();
        resolverRepository.Setup(r => r.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission?)null);
        var rbacMock = new Mock<IRbacPermissionResolver>();
        rbacMock.Setup(r => r.ResolvePermissionsAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RbacResolutionResult(new HashSet<string>(), new HashSet<string>(), []));
        var effectiveResolver = new EffectivePermissionResolverService(
            resolverRepository.Object,
            rbacMock.Object,
            [Mock.Of<IAuthorizationRolePermissionProvider>()],
            Mock.Of<IResourcePermissionService>(),
            Options.Create(new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions()),
            NullLogger<EffectivePermissionResolverService>.Instance);
        (await effectiveResolver.ResolveAsync(Guid.NewGuid(), null)).Permissions.Should().BeEmpty();

        var resolver = new DefaultTenantResolver();
        var resolvedTenant = await ((IAuthorizationTenantResolver)resolver).ResolveTenantIdAsync(new DefaultHttpContext());
        resolvedTenant.Should().Be("resolved");

        foreach (var createNullRepository in new Action[]
                 {
                     () => new JitElevationRequestRepository(null!),
                     () => new PermissionDelegationRepository(null!),
                     () => new SoDRuleRepository(null!),
                     () => new SoDViolationRepository(null!),
                     () => new AccessReviewCampaignRepository(null!),
                     () => new AccessReviewItemRepository(null!),
                     () => new DelegatedAdminScopeRepository(null!)
                 })
        {
            FluentActions.Invoking(createNullRepository).Should().Throw<ArgumentNullException>();
        }

        var sodService = new SoDService(
            Mock.Of<ISoDRuleRepository>(),
            Mock.Of<ISoDViolationRepository>(),
            NullLogger<SoDService>.Instance);
        (await (Task<bool>)typeof(SoDService)
            .GetMethod("CheckRuleViolationAsync", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [new SoDRule(), Guid.NewGuid(), null, CancellationToken.None])!)
            .Should().BeFalse();
    }

    [Fact]
    public async Task Remaining_Branch_Edge_Cases_Are_Covered()
    {
        var conditional = new ConditionalPolicyEvaluator(
            Mock.Of<IConditionalPolicyRepository>(),
            NullLogger<ConditionalPolicyEvaluator>.Instance);
        var conditionalContext = new ConditionalPolicyContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Document",
            Guid.NewGuid(),
            "read",
            [],
            IpAddress: "10.0.0.1",
            UserAgent: "Mozilla",
            DeviceFingerprint: "device",
            GeoCountry: null,
            AuthenticationTime: null,
            IsMfaVerified: true,
            RiskScore: null,
            CustomAttributes: null);
        InvokePrivate<bool>(conditional, "EvaluateTimeConditions", "null").Should().BeTrue();
        InvokePrivate<bool>(conditional, "EvaluateTimeConditions", """{"StartTime":"00:00","EndTime":"23:59"}""").Should().BeTrue();
        InvokePrivate<bool>(conditional, "EvaluateTimeConditions", """{"StartTime":"bad","EndTime":"23:59"}""").Should().BeTrue();
        var currentTime = TimeOnly.FromDateTime(SystemClock.UtcNow);
        var overnightStart = currentTime.Add(TimeSpan.FromHours(1));
        var overnightEnd = currentTime.Add(TimeSpan.FromHours(-1));
        InvokePrivate<bool>(
                conditional,
                "EvaluateTimeConditions",
                $$"""{"StartTime":"{{overnightStart:HH:mm}}","EndTime":"{{overnightEnd:HH:mm}}"}""")
            .Should().BeFalse();
        InvokePrivate<bool>(conditional, "EvaluateEnvironmentConditions", "null", conditionalContext).Should().BeTrue();
        InvokePrivate<bool>(conditional, "EvaluateEnvironmentConditions", """{"MaxRiskScore":5}""", conditionalContext).Should().BeTrue();
        InvokePrivate<bool>(conditional, "EvaluateEnvironmentConditions", """{"MaxSessionAgeMinutes":5}""", conditionalContext).Should().BeTrue();
        InvokePrivate<bool>(conditional, "EvaluateLocationConditions", "null", conditionalContext).Should().BeTrue();
        InvokePrivate<bool>(conditional, "EvaluateLocationConditions", """{"AllowedCountries":["US"]}""", conditionalContext).Should().BeFalse();
        InvokePrivate<bool>(conditional, "EvaluateLocationConditions", """{"BlockedCountries":["US"]}""", conditionalContext).Should().BeTrue();
        InvokePrivate<bool>(conditional, "EvaluateDeviceConditions", "null", conditionalContext).Should().BeTrue();
        InvokePrivate<bool>(conditional, "EvaluateDeviceConditions", """{"AllowedFingerprints":["device"]}""", conditionalContext).Should().BeTrue();
        InvokePrivate<bool>(conditional, "EvaluateDeviceConditions", """{"BlockedUserAgents":["bot"]}""", conditionalContext).Should().BeTrue();
        InvokePrivate<bool>(conditional, "EvaluateCustomConditions", "null", conditionalContext).Should().BeTrue();

        var policy = new AuthorizationPolicyBuilder("Bearer").RequireAuthenticatedUser().Build();
        var cacheOptions = Options.Create(new AuthorizationCacheOptions { UseDistributedCache = true, PolicyTtlSeconds = 60 });
        var nullLoggerThrowingCache = new MemoryPolicyCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }),
            cacheOptions,
            new ThrowingDistributedCache());
        nullLoggerThrowingCache.Get("missing", "tenant", 1).Should().BeNull();
        FluentActions.Invoking(() => nullLoggerThrowingCache.Set("policy", "tenant", 1, policy))
            .Should().Throw<InvalidOperationException>();

        var successfulPolicyInvalidation = new MemoryPolicyCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }),
            cacheOptions,
            new DictionaryDistributedCache());
        var tenantKeys = (System.Collections.Concurrent.ConcurrentDictionary<string, HashSet<string>>)typeof(MemoryPolicyCache)
            .GetField("_tenantKeys", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(successfulPolicyInvalidation)!;
        tenantKeys["tenant"] = ["policy|tenant|1"];
        successfulPolicyInvalidation.Invalidate("policy", "tenant");

        var noDistributedPolicyInvalidation = new MemoryPolicyCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }),
            Options.Create(new AuthorizationCacheOptions { UseDistributedCache = false, PolicyTtlSeconds = 60 }));
        var noDistributedKeys = (System.Collections.Concurrent.ConcurrentDictionary<string, HashSet<string>>)typeof(MemoryPolicyCache)
            .GetField("_tenantKeys", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(noDistributedPolicyInvalidation)!;
        noDistributedKeys["tenant"] = ["policy|tenant|2"];
        noDistributedPolicyInvalidation.Invalidate("policy", "tenant");

        var emptyLanguageContext = new DefaultHttpContext();
        emptyLanguageContext.Request.Headers["Accept-Language"] = "";
        var emptyLocalization = new LocalizationContext(new HttpContextAccessor { HttpContext = emptyLanguageContext });
        emptyLocalization.CultureCode.Should().BeNull();
        var missingHttpLocalization = new LocalizationContext(new HttpContextAccessor());
        missingHttpLocalization.CultureCode.Should().BeNull();
        missingHttpLocalization.UICultureCode.Should().BeNull();
        missingHttpLocalization.TimeZone.Should().BeNull();
        missingHttpLocalization.DateFormat.Should().BeNull();
        missingHttpLocalization.NumberFormat.Should().BeNull();

        var delegation = new PermissionDelegation
        {
            DelegatedPermissions = ["read"],
            StartsAt = SystemClock.UtcNow.AddMinutes(-1),
            ExpiresAt = SystemClock.UtcNow.AddMinutes(10)
        };
        delegation.IsValidNow().Should().BeTrue();
        delegation.AllowsPermission("read").Should().BeTrue();
        delegation.IsExpired().Should().BeFalse();
        delegation.HasUsageRemaining().Should().BeTrue();
        delegation.GetRemainingUsage().Should().BeNull();
        delegation.ExpiresAt = SystemClock.UtcNow.AddMinutes(-1);
        delegation.IsValidNow().Should().BeFalse();
        delegation.IsExpired().Should().BeTrue();
        FluentActions.Invoking(() => delegation.Extend(SystemClock.UtcNow.AddMinutes(-1)))
            .Should().Throw<ArgumentException>();
        delegation.Extend(SystemClock.UtcNow.AddMinutes(5));
        delegation.UpdatedAt.Should().NotBeNull();

        InvokePrivateStatic<bool>(typeof(RequireIpAllowListRuleEvaluator), "IsIpInCidr", IPAddress.Parse("10.0.0.1"), "2001:db8::/32")
            .Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(RequireIpAllowListRuleEvaluator), "IsIpInCidr", IPAddress.Parse("2001:db8::1"), "10.0.0.0/8")
            .Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(RequireIpAllowListRuleEvaluator), "IsIpInCidr", IPAddress.Parse("10.0.0.1"), "10.0.0.0/40")
            .Should().BeFalse();
        var timeEvaluator = new RequireTimeWindowRuleEvaluator();
        (await timeEvaluator.EvaluateAsync(CreateAuthorizationContext(), RuleParameters.FromJson("""{"timezone":"UTC-Fallback","windows":[{}]}""")))
            .IsSuccess.Should().BeTrue();
        (await timeEvaluator.EvaluateAsync(CreateAuthorizationContext(), RuleParameters.FromJson("""{"timezone":"America/New_York","windows":[{}]}""")))
            .IsSuccess.Should().BeTrue();
        (await timeEvaluator.EvaluateAsync(CreateAuthorizationContext(), RuleParameters.FromJson("""{"timezone":"No/Such","windows":[]}""")))
            .IsSuccess.Should().BeFalse();
        IsWithinWindow("{}", 1, TimeSpan.FromHours(9)).Should().BeTrue();
        IsWithinWindow("""{"startTime":"08:00"}""", 1, TimeSpan.FromHours(9)).Should().BeTrue();
        IsWithinWindow("""{"endTime":"17:00"}""", 1, TimeSpan.FromHours(9)).Should().BeTrue();

        var resourcePermission = new ResourceUserPermission
        {
            TenantId = TenantId.New(),
            UserId = Guid.NewGuid(),
            ResourceType = "Document",
            ResourceId = "doc-1",
            Permissions = ["read", "write"],
            GrantedByUserId = Guid.NewGuid()
        };
        resourcePermission.HasPermission("delete").Should().BeFalse();
        resourcePermission.HasAnyPermission(["delete"]).Should().BeFalse();
        resourcePermission.HasAllPermissions(["read", "delete"]).Should().BeFalse();
        resourcePermission.Revoke(Guid.NewGuid()).Should().BeTrue();
        resourcePermission.Revoke(Guid.NewGuid()).Should().BeFalse();
        resourcePermission.UpdatePermissions(["read"], Guid.NewGuid()).Should().BeFalse();
        resourcePermission.HasPermission("read").Should().BeFalse();
        resourcePermission.HasAnyPermission(["read"]).Should().BeFalse();
        resourcePermission.HasAllPermissions(["read"]).Should().BeFalse();

        var invitation = new ResourceInvitation
        {
            TenantId = TenantId.New(),
            Email = "invite@example.com",
            ResourceType = "Document",
            ResourceId = "doc-1",
            Permissions = ["read"],
            InvitedByUserId = Guid.NewGuid(),
            InvitedByUserName = "Inviter",
            ExpiresAt = SystemClock.UtcNow.AddDays(1),
            Status = InvitationStatus.Accepted
        };
        invitation.Revoke(Guid.NewGuid()).Should().BeFalse();

        var httpTenantContext = new DefaultHttpContext();
        httpTenantContext.Items["AuthorizationTenantId"] = Guid.NewGuid();
        var tenantContext = new HttpAuthorizationTenantContext(new HttpContextAccessor { HttpContext = httpTenantContext });
        tenantContext.TenantId.Should().NotBeNull();
        httpTenantContext.Items["AuthorizationTenantId"] = Guid.Empty.ToString();
        tenantContext.TenantId.Should().BeNull();
        httpTenantContext.Items.Remove("AuthorizationTenantId");
        httpTenantContext.Items["TenantId"] = Guid.Empty;
        tenantContext.TenantId.Should().BeNull();
        httpTenantContext.Items["TenantId"] = "bad";
        tenantContext.TenantId.Should().BeNull();

        var response = new GetPendingResourceInvitationsResponse { Invitations = [new ResourceInvitationDto(Guid.NewGuid(), Guid.NewGuid(), "x@y.com", "Document", "doc-1", ["read"], null, "n", SystemClock.UtcNow, SystemClock.UtcNow.AddDays(1), "Pending")] };
        response.TotalCount.Should().Be(1);

        var rulesetProvider = new RulesetProvider(
            Mock.Of<IPolicyDefinitionRepository>(),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<RulesetProvider>.Instance);
        var parseResult = InvokePrivate<(List<string> Values, bool IsValid)>(
            rulesetProvider,
            "ParseJsonArray",
            (object?)null);
        parseResult.Values.Should().BeEmpty();
        parseResult.IsValid.Should().BeTrue();

        var ownEventService = new CacheInvalidationService(
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<ITenantSecurityVersionStore>(),
            Mock.Of<IHybridPermissionCache>(),
            Mock.Of<ICacheMetricsService>(),
            Options.Create(new AuthorizationCacheOptions()),
            NullLogger<CacheInvalidationService>.Instance);
        var instanceId = (string)typeof(CacheInvalidationService)
            .GetField("_instanceId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(ownEventService)!;
        ownEventService.HandleInvalidationEvent(new CacheInvalidationEvent { OriginInstanceId = instanceId });

        var accessReview = new AccessReviewCampaign { Status = AccessReviewStatus.Completed };
        FluentActions.Invoking(accessReview.Start).Should().Throw<InvalidOperationException>();

        FluentActions.Invoking(() => new AccessReviewService(
                null!,
                Mock.Of<IAccessReviewItemRepository>(),
                NullLogger<AccessReviewService>.Instance))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AccessReviewService(
                Mock.Of<IAccessReviewCampaignRepository>(),
                null!,
                NullLogger<AccessReviewService>.Instance))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AccessReviewService(
                Mock.Of<IAccessReviewCampaignRepository>(),
                Mock.Of<IAccessReviewItemRepository>(),
                null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PermissionAnalyticsService(null!, NullLogger<PermissionAnalyticsService>.Instance))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PermissionAnalyticsService(Mock.Of<IPermissionAuditLogRepository>(), null!))
            .Should().Throw<ArgumentNullException>();

        var authTenantOptions = Options.Create(new TenancyOptions());
        authTenantOptions.Value.Resolution.EnableHeader = false;
        authTenantOptions.Value.Resolution.EnableSubdomain = false;
        authTenantOptions.Value.Resolution.EnableQueryString = true;
        authTenantOptions.Value.Resolution.QueryStringKey = "tenant";
        var authResolver = new AuthorizationTenantResolver(authTenantOptions, Options.Create(new AuthorizationTokenOptions()));
        var queryTenantContext = new DefaultHttpContext();
        queryTenantContext.Request.QueryString = new QueryString("?tenant=query-tenant");
        authResolver.ResolveFromRequest(queryTenantContext).Should().Be("query-tenant");
        queryTenantContext.Request.QueryString = new QueryString("?tenant=");
        authResolver.ResolveFromRequest(queryTenantContext).Should().BeNull();

        var scopeUserId = Guid.NewGuid();
        var scope = new DelegatedAdminScope
        {
            IsActive = true,
            StartsAt = SystemClock.UtcNow.AddMinutes(-1),
            ExpiresAt = SystemClock.UtcNow.AddMinutes(10),
            CanManageUsers = true,
            CanManageResources = true,
            AllowedUserIds = scopeUserId.ToString(),
            AllowedResourceTypes = "Document"
        };
        scope.IsValid().Should().BeTrue();
        scope.CanManageUser(scopeUserId).Should().BeTrue();
        scope.CanManageResourceType("Document").Should().BeTrue();
        scope.IsActive = false;
        scope.IsValid().Should().BeFalse();
        scope.CanManageUser(scopeUserId).Should().BeFalse();
        scope.CanManageResourceType("Document").Should().BeFalse();

        var jit = new JitElevationRequest
        {
            Status = ElevationRequestStatus.Active,
            StartsAt = SystemClock.UtcNow.AddMinutes(1),
            CreatedAt = SystemClock.UtcNow.AddMinutes(1),
            ExpiresAt = SystemClock.UtcNow.AddMinutes(5)
        };
        jit.IsActive().Should().BeFalse();
        jit.StartsAt = SystemClock.UtcNow.AddMinutes(-1);
        jit.IsActive().Should().BeTrue();
        jit.IsExpired().Should().BeFalse();
        jit.ExpiresAt = SystemClock.UtcNow.AddMinutes(-1);
        jit.IsExpired().Should().BeTrue();
        var futureJit = new JitElevationRequest
        {
            Status = ElevationRequestStatus.Pending,
            StartsAt = SystemClock.UtcNow.AddMinutes(10),
            ExpiresAt = SystemClock.UtcNow.AddMinutes(20)
        };
        futureJit.Approve(Guid.NewGuid());
        futureJit.Status.Should().Be(ElevationRequestStatus.Approved);

        ClaimsExtractor.GetName(Principal(new Claim("name", "fallback"))).Should().Be("fallback");
        ClaimsExtractor.GetAmr(new ClaimsPrincipal(new ClaimsIdentity())).Should().BeNull();
        ClaimsExtractor.GetTokenVersion(new ClaimsPrincipal(new ClaimsIdentity())).Should().BeNull();
        ClaimsExtractor.GetClaim(new ClaimsPrincipal(new ClaimsIdentity()), "missing").Should().BeNull();

        ClaimsExtractor.GetUserId(Principal(new Claim(ClaimNames.NameIdentifier, "name-id"))).Should().Be("name-id");
        ClaimsExtractor.GetTenantId(Principal(new Claim(ClaimNames.TenantIdAlt, "tenant-alt"))).Should().Be("tenant-alt");
        CreatePermission(typeof(SystemPermission), "system:manage:defaults", "Scoped system permission").Scope.Should().Be("defaults");

        new StaticClaimsPrincipalAccessor().IsAuthenticated.Should().BeFalse();
        new StaticClaimsPrincipalAccessor().GetUserId().Should().BeNull();
        new StaticClaimsPrincipalAccessor().GetTenantId().Should().BeNull();
        var emptyHttpAccessor = new HttpContextClaimsPrincipalAccessor(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        emptyHttpAccessor.IsAuthenticated.Should().BeFalse();
        emptyHttpAccessor.GetUserId().Should().BeNull();
        emptyHttpAccessor.GetTenantId().Should().BeNull();
        var authenticatedAccessor = new HttpContextClaimsPrincipalAccessor(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = Principal(new Claim(AuthorizationClaims.Sub, Guid.NewGuid().ToString()))
            }
        });
        authenticatedAccessor.IsAuthenticated.Should().BeTrue();
        var staticAuthenticated = new StaticClaimsPrincipalAccessor(Principal(new Claim(AuthorizationClaims.Sub, Guid.NewGuid().ToString())));
        staticAuthenticated.IsAuthenticated.Should().BeTrue();
        staticAuthenticated.GetUserId().Should().NotBeNull();
        new StaticClaimsPrincipalAccessor(new ClaimsPrincipal()).IsAuthenticated.Should().BeFalse();
        var namedPrincipal = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test", nameType: ClaimTypes.Name, roleType: ClaimTypes.Role));
        ((ClaimsIdentity)namedPrincipal.Identity!).AddClaim(new Claim(ClaimTypes.Name, "identity-name"));
        ClaimsExtractor.IsAuthenticated(namedPrincipal).Should().BeTrue();

        var listResult = InvokePrivateStatic<(IReadOnlyList<string> Values, bool IsValid)>(
            typeof(DatabasePolicyDefinitionStore),
            "DeserializeList",
            "null");
        listResult.Values.Should().BeEmpty();
        listResult.IsValid.Should().BeFalse();
        var rulesResult = InvokePrivateStatic<(IReadOnlyList<PolicyRule>? Values, bool IsValid)>(
            typeof(DatabasePolicyDefinitionStore),
            "DeserializeRules",
            "null");
        rulesResult.Values.Should().BeNull();
        rulesResult.IsValid.Should().BeFalse();

        var fallbackTenantId = Guid.NewGuid();
        var fallbackHttpTenantContext = new DefaultHttpContext();
        fallbackHttpTenantContext.Items["TenantId"] = fallbackTenantId.ToString();
        new HttpAuthorizationTenantContext(new HttpContextAccessor { HttpContext = fallbackHttpTenantContext })
            .TenantId.Should().Be(fallbackTenantId);

        var debugLogger = new PolicyEvaluationLogger(NullLogger<PolicyEvaluationLogger>.Instance);
        var debugEndpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(new PolicyDebugAttribute { Enabled = true, PolicyNames = ["p1", "p2"] }),
            "debug");
        debugLogger.GetDebugSettings(debugEndpoint)!.PolicyNames.Should().Contain("p1");

        var permissionRegistry = new RuleEvaluatorRegistry([new RequireMfaRuleEvaluator()]);
        permissionRegistry.GetEvaluator("missing").Should().BeNull();

        var samePermission = AdminPermission.Admin;
        samePermission.Equals(samePermission).Should().BeTrue();

        var nullLoggerTenantInvalidation = new MemoryPolicyCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }),
            Options.Create(new AuthorizationCacheOptions { UseDistributedCache = true, PolicyTtlSeconds = 60 }),
            new ThrowingDistributedCache());
        FluentActions.Invoking(() => nullLoggerTenantInvalidation.Set("policy", "tenant", 1, new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => nullLoggerTenantInvalidation.Invalidate("tenant"))
            .Should().Throw<InvalidOperationException>();
        var nullLoggerPolicyInvalidation = new MemoryPolicyCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }),
            Options.Create(new AuthorizationCacheOptions { UseDistributedCache = true, PolicyTtlSeconds = 60 }),
            new ThrowingDistributedCache());
        var nullLoggerKeys = (System.Collections.Concurrent.ConcurrentDictionary<string, HashSet<string>>)typeof(MemoryPolicyCache)
            .GetField("_tenantKeys", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(nullLoggerPolicyInvalidation)!;
        nullLoggerKeys["tenant"] = ["policy|tenant|1"];
        FluentActions.Invoking(() => nullLoggerPolicyInvalidation.Invalidate("policy", "tenant"))
            .Should().Throw<InvalidOperationException>();

        var gateConditional = new Mock<IConditionalPolicyEvaluator>();
        var gateAbac = new Mock<IAbacPolicyEvaluator>();
        var gateService = new PolicyGateService(gateConditional.Object, gateAbac.Object, NullLogger<PolicyGateService>.Instance);
        var staticDenied = await gateService.EvaluateGateAsync(PolicyGateType.Static, new PolicyGateContext
        {
            ActorId = Guid.NewGuid(),
            ResourceType = "Document",
            Action = "read",
            IpAddress = "127.0.0.1",
            Attributes = new Dictionary<string, object> { ["environment"] = "production" }
        });
        staticDenied.DenialReason.Should().Be("Localhost access blocked in production");
        (await gateService.EvaluateGateAsync(PolicyGateType.Environment, new PolicyGateContext
        {
            ActorId = Guid.NewGuid(),
            ResourceType = "Document",
            Action = "read",
            UserAgent = "wget/1"
        })).GateDetails.Should().HaveCount(1);

        new DefaultPolicyMerger().Merge(
            new PolicyDefinition { PolicyName = "auth", RequireAuthentication = false },
            new PolicyDefinition { PolicyName = "auth", RequireAuthentication = false })
            .RequireAuthentication.Should().BeFalse();
        FluentActions.Invoking(() => new DefaultPolicyMerger().Build(new PolicyDefinition
            {
                PolicyName = "rules-empty-with-role",
                RequireAuthentication = false,
                UseRuleBasedEvaluation = true,
                Rules = [],
                RequiredRoles = ["Admin"]
            }))
            .Should().NotThrow();

        InvokePrivate<bool>(conditional, "EvaluateTimeConditions", """{"StartTime":"","EndTime":"23:59"}""").Should().BeTrue();
        InvokePrivate<bool>(conditional, "EvaluateTimeConditions", """{"StartTime":"00:00","EndTime":"00:01"}""").Should().BeFalse();
        var nullParseResult = InvokePrivate<(List<string> Values, bool IsValid)>(
            rulesetProvider,
            "ParseJsonArray",
            "null");
        nullParseResult.Values.Should().BeEmpty();
        nullParseResult.IsValid.Should().BeFalse();
        var emptyListResult = InvokePrivateStatic<(IReadOnlyList<string> Values, bool IsValid)>(
            typeof(DatabasePolicyDefinitionStore),
            "DeserializeList",
            "");
        emptyListResult.Values.Should().BeEmpty();
        emptyListResult.IsValid.Should().BeTrue();
        var emptyArrayResult = InvokePrivateStatic<(IReadOnlyList<string> Values, bool IsValid)>(
            typeof(DatabasePolicyDefinitionStore),
            "DeserializeList",
            "[]");
        emptyArrayResult.Values.Should().BeEmpty();
        emptyArrayResult.IsValid.Should().BeTrue();

        InvokePrivateStatic<string>(typeof(PolicyEvaluationLogger), "GetUserId", Principal(new Claim("sub", "sub-id")))
            .Should().Be("sub-id");
        var namedIdentity = new ClaimsIdentity(authenticationType: "test", nameType: ClaimTypes.Name, roleType: ClaimTypes.Role);
        namedIdentity.AddClaim(new Claim(ClaimTypes.Name, "display-name"));
        InvokePrivateStatic<string>(typeof(PolicyEvaluationLogger), "GetUserId", new ClaimsPrincipal(namedIdentity))
            .Should().Be("display-name");

        InvokePrivateStatic<Guid?>(typeof(SelfOrPermissionRuleEvaluator), "GetTargetUserIdFromResource", new TestUserIdResource(null), new RuleParameters())
            .Should().BeNull();
        InvokePrivateStatic<Guid?>(typeof(SelfOrPermissionRuleEvaluator), "GetTargetUserIdFromResource", new { UserId = (Guid?)null }, new RuleParameters())
            .Should().BeNull();

        InvokePrivateStatic<AclSubject>(typeof(OwnerOrAclRuleEvaluator), "BuildAclSubject", new ClaimsPrincipal(), "group_id")
            .IsAuthenticated.Should().BeFalse();

        var aclEntry = new AccessControlListEntry { PrincipalType = AclPrincipalType.User, PrincipalId = null };
        aclEntry.PrincipalId.Should().BeNull();
        aclEntry.PrincipalId = Guid.NewGuid();
        aclEntry.PrincipalId.Should().NotBeNull();
        aclEntry.IsActive = true;
        aclEntry.ExpiresAt = null;
        aclEntry.IsEffective.Should().BeTrue();
        aclEntry.ExpiresAt = SystemClock.UtcNow.AddMinutes(-1);
        aclEntry.IsEffective.Should().BeFalse();

        var defaultMask = new DataMaskingRule { MaskingType = (MaskingType)999 };
        defaultMask.ApplyMasking("secret").Should().Be("secret");
        new DataMaskingRule { ExemptUsers = null }.IsUserExempt(Guid.NewGuid()).Should().BeFalse();

        GetResourceIdentifiers(new TestOwnedResource(Guid.NewGuid(), Guid.NewGuid()), new ResourceAccessRequirement(resourceType: ""))
            .Should().Be((null, null));
        GetResourceIdentifiers(new object(), new ResourceAccessRequirement(resourceType: "Document"))
            .Should().Be((null, null));

        var tenantHandlerContext = new Mock<IAuthorizationTenantContext>();
        tenantHandlerContext.SetupGet(c => c.HasTenant).Returns(true);
        tenantHandlerContext.SetupGet(c => c.TenantId).Returns(Guid.NewGuid());
        var tenantResolver = new Mock<IAuthorizationTenantResolver>();
        tenantResolver.Setup(r => r.GetUserDefaultTenant(It.IsAny<ClaimsPrincipal>())).Returns("other");
        var tenantOptions = Options.Create(new TenancyOptions { DefaultTenantId = "default" });
        var tokenOptions = Options.Create(new AuthorizationTokenOptions { TenantClaimType = "tenant_id" });
        var tenantHandler = new TenantMatchHandler(
            tenantHandlerContext.Object,
            tenantResolver.Object,
            tenantOptions,
            tokenOptions,
            NullLogger<TenantMatchHandler>.Instance);
        var tenantRequirement = new TenantMatchRequirement(strictMatch: false);
        var tenantAuthContext = new AuthorizationHandlerContext([tenantRequirement], Principal(), null);
        await tenantHandler.HandleAsync(tenantAuthContext);
        tenantAuthContext.HasSucceeded.Should().BeFalse();

        var ownerSubject = InvokePrivateStatic<AclSubject>(
            typeof(OwnerOrAclRuleEvaluator),
            "BuildAclSubject",
            Principal(new Claim(ClaimNames.Subject, Guid.Empty.ToString()), new Claim(ClaimNames.Role, "bad"), new Claim(ClaimNames.Group, "bad")),
            "group_id");
        ownerSubject.UserId.Should().BeNull();
        ownerSubject.IsAuthenticated.Should().BeTrue();

        var inactiveDelegation = new PermissionDelegation
        {
            IsActive = false,
            StartsAt = SystemClock.UtcNow.AddMinutes(-1),
            DelegatedPermissions = ["read"]
        };
        inactiveDelegation.IsValidNow().Should().BeFalse();
        inactiveDelegation.AllowsPermission("read").Should().BeFalse();
        inactiveDelegation.ExpiresAt = null;
        inactiveDelegation.IsExpired().Should().BeFalse();

        var primaryStringTenantId = Guid.NewGuid();
        var primaryStringContext = new DefaultHttpContext();
        primaryStringContext.Items["AuthorizationTenantId"] = primaryStringTenantId.ToString();
        new HttpAuthorizationTenantContext(new HttpContextAccessor { HttpContext = primaryStringContext })
            .TenantId.Should().Be(primaryStringTenantId);
        var fallbackGuidTenantId = Guid.NewGuid();
        var fallbackGuidContext = new DefaultHttpContext();
        fallbackGuidContext.Items["TenantId"] = fallbackGuidTenantId;
        new HttpAuthorizationTenantContext(new HttpContextAccessor { HttpContext = fallbackGuidContext })
            .TenantId.Should().Be(fallbackGuidTenantId);

        var cachedAcl = new CachedAccessControlListService(
            Mock.Of<IAccessControlListService>(),
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<ITenantSecurityVersionStore>(),
            Mock.Of<IUserSecurityVersionStore>(),
            Options.Create(new AuthorizationCacheOptions { AccessControlListTtlSeconds = 60 }),
            hybridCache: null,
            metrics: null);
        var cacheTenantId = Guid.NewGuid();
        InvokePrivateStatic<string>(
                typeof(CachedAccessControlListService),
                "BuildSubjectCacheKey",
                AclSubject.Anonymous,
                cacheTenantId,
                "Document",
                "doc-1",
                1L,
                1L,
                0L)
            .Should().Contain("anon:nr:ng");
        InvokePrivate<object>(cachedAcl, "CacheAccessLevel", $"acl:{cacheTenantId}:{Guid.NewGuid()}:Document:doc-1:tv1:uv1:gv1", cacheTenantId.ToString(), AccessLevel.Read, false);
        cachedAcl.InvalidateTenant(cacheTenantId.ToString());
    }

    [Fact]
    public async Task Final_Authorization_Branch_Gaps_Are_Covered()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var httpSubAccessor = new HttpContextClaimsPrincipalAccessor(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = Principal(new Claim(AuthorizationClaims.Sub, userId.ToString()))
            }
        });
        httpSubAccessor.GetUserId().Should().Be(userId);

        var httpNullIdentityAccessor = new HttpContextClaimsPrincipalAccessor(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        });
        httpNullIdentityAccessor.IsAuthenticated.Should().BeFalse();

        var staticNameIdentifierAccessor = new StaticClaimsPrincipalAccessor(
            Principal(new Claim(ClaimTypes.NameIdentifier, userId.ToString())));
        staticNameIdentifierAccessor.GetUserId().Should().Be(userId);
        new StaticClaimsPrincipalAccessor(new ClaimsPrincipal(new ClaimsIdentity()))
            .GetUserId().Should().BeNull();

        new AccessControlListEntry { IsActive = false, ExpiresAt = null }.IsEffective.Should().BeFalse();
        new AccessControlListEntry { IsActive = true, ExpiresAt = SystemClock.UtcNow.AddMinutes(5) }
            .IsEffective.Should().BeTrue();
        new AccessControlListEntry { IsActive = true, ExpiresAt = SystemClock.UtcNow.AddMinutes(-5) }
            .IsEffective.Should().BeFalse();

        var neverExpiresScope = new DelegatedAdminScope
        {
            IsActive = true,
            StartsAt = SystemClock.UtcNow.AddMinutes(-1),
            ExpiresAt = null,
            CanManageResources = true,
            AllowedResourceTypes = null
        };
        neverExpiresScope.IsValid().Should().BeTrue();
        neverExpiresScope.CanManageResourceType("Document").Should().BeFalse();
        new DelegatedAdminScope
        {
            IsActive = true,
            StartsAt = SystemClock.UtcNow.AddMinutes(1),
            ExpiresAt = null
        }.IsValid().Should().BeFalse();
        new DelegatedAdminScope
        {
            IsActive = true,
            StartsAt = SystemClock.UtcNow.AddMinutes(-10),
            ExpiresAt = SystemClock.UtcNow.AddMinutes(-1)
        }.IsValid().Should().BeFalse();

        new JitElevationRequest { Status = ElevationRequestStatus.Pending, ExpiresAt = SystemClock.UtcNow.AddMinutes(-1) }
            .IsExpired().Should().BeFalse();
        var immediateJit = new JitElevationRequest
        {
            Status = ElevationRequestStatus.Pending,
            StartsAt = null,
            ExpiresAt = SystemClock.UtcNow.AddMinutes(5)
        };
        immediateJit.Approve(Guid.NewGuid(), "approved");
        immediateJit.Status.Should().Be(ElevationRequestStatus.Active);
        var pastStartJit = new JitElevationRequest
        {
            Status = ElevationRequestStatus.Pending,
            StartsAt = SystemClock.UtcNow.AddMinutes(-1),
            ExpiresAt = SystemClock.UtcNow.AddMinutes(5)
        };
        pastStartJit.Approve(Guid.NewGuid());
        pastStartJit.Status.Should().Be(ElevationRequestStatus.Active);

        var noLimitDelegation = new PermissionDelegation
        {
            IsActive = true,
            StartsAt = SystemClock.UtcNow.AddMinutes(-1),
            ExpiresAt = null,
            UsageLimit = null,
            DelegatedPermissions = ["read"]
        };
        noLimitDelegation.IsValidNow().Should().BeTrue();
        noLimitDelegation.RecordUsage();
        noLimitDelegation.IsActive.Should().BeTrue();
        new PermissionDelegation
        {
            IsActive = true,
            StartsAt = SystemClock.UtcNow.AddMinutes(1),
            DelegatedPermissions = ["read"]
        }.IsValidNow().Should().BeFalse();
        new PermissionDelegation
        {
            IsActive = true,
            StartsAt = SystemClock.UtcNow.AddMinutes(-2),
            ExpiresAt = SystemClock.UtcNow.AddMinutes(-1),
            DelegatedPermissions = ["read"]
        }.IsValidNow().Should().BeFalse();
        new PermissionDelegation
        {
            IsActive = true,
            StartsAt = SystemClock.UtcNow.AddMinutes(-2),
            UsageLimit = 1,
            UsageCount = 1,
            DelegatedPermissions = ["read"]
        }.IsValidNow().Should().BeFalse();

        InvokePrivateStatic<bool>(typeof(EnvironmentHandler), "IsDeviceTypeAllowed", "Mozilla Tablet", new[] { "tablet" })
            .Should().BeTrue();

        var resourceAccessHandler = new ResourceAccessHandler(
            Mock.Of<IAuthorizationTenantContext>(),
            Mock.Of<IAccessControlListService>(),
            Options.Create(new AuthorizationTokenOptions()),
            NullLogger<ResourceAccessHandler>.Instance);
        InvokePrivate<AclSubject>(resourceAccessHandler, "BuildAclSubject", new ClaimsPrincipal())
            .IsAuthenticated.Should().BeFalse();

        var routeNullContext = new DefaultHttpContext();
        routeNullContext.Request.RouteValues["id"] = null;
        InvokePrivateStatic<Guid?>(typeof(ResourcePermissionAuthorizationFilter), "ExtractResourceId", routeNullContext, "id")
            .Should().BeNull();

        var fallbackTenantContext = new DefaultHttpContext();
        fallbackTenantContext.Items["TenantId"] = Guid.Empty.ToString();
        new HttpAuthorizationTenantContext(new HttpContextAccessor { HttpContext = fallbackTenantContext })
            .TenantId.Should().BeNull();
        var invalidPrimaryTenantContext = new DefaultHttpContext();
        invalidPrimaryTenantContext.Items["AuthorizationTenantId"] = "bad";
        new HttpAuthorizationTenantContext(new HttpContextAccessor { HttpContext = invalidPrimaryTenantContext })
            .TenantId.Should().BeNull();
        var invalidFallbackTenantContext = new DefaultHttpContext();
        invalidFallbackTenantContext.Items["TenantId"] = "bad";
        new HttpAuthorizationTenantContext(new HttpContextAccessor { HttpContext = invalidFallbackTenantContext })
            .TenantId.Should().BeNull();

        var tenantContextMock = new Mock<IAuthorizationTenantContext>();
        tenantContextMock.SetupGet(c => c.HasTenant).Returns(false);
        tenantContextMock.SetupGet(c => c.TenantId).Returns((Guid?)null);
        var tenantResolverMock = new Mock<IAuthorizationTenantResolver>();
        tenantResolverMock.Setup(r => r.ResolveFromClaims(It.IsAny<ClaimsPrincipal>())).Returns("base");
        tenantResolverMock.Setup(r => r.GetUserDefaultTenant(It.IsAny<ClaimsPrincipal>())).Returns("different");
        var tenantMatchHandler = new TenantMatchHandler(
            tenantContextMock.Object,
            tenantResolverMock.Object,
            Options.Create(new TenancyOptions { DefaultTenantId = "base" }),
            Options.Create(new AuthorizationTokenOptions { TenantClaimType = "tenant_id" }),
            NullLogger<TenantMatchHandler>.Instance);
        var tenantMatchContext = new AuthorizationHandlerContext(
            [new TenantMatchRequirement(strictMatch: false)],
            Principal(new Claim("tenant_id", "other")),
            null);
        await tenantMatchHandler.HandleAsync(tenantMatchContext);
        tenantMatchContext.HasSucceeded.Should().BeTrue();
        var strictTenantMismatchContext = new AuthorizationHandlerContext(
            [new TenantMatchRequirement(strictMatch: true)],
            Principal(new Claim("tenant_id", "other")),
            null);
        await tenantMatchHandler.HandleAsync(strictTenantMismatchContext);
        strictTenantMismatchContext.HasSucceeded.Should().BeFalse();

        var cultureHttpContext = new DefaultHttpContext();
        cultureHttpContext.Request.Headers["Accept-Language"] = "en-US;q=0.9,pt-BR;q=0.8";
        new LocalizationContext(new HttpContextAccessor { HttpContext = cultureHttpContext })
            .CultureCode.Should().Be("en-US");

        CreatePermission(typeof(AdminPermission), "admin", "No scope").Scope.Should().BeNull();
        CreatePermission(typeof(AdminPermission), "admin:read:tenant", "Scoped").Scope.Should().Be("tenant");
        CreatePermission(typeof(SystemPermission), "system", "No scope").Action.Should().Be("system");

        InvokePrivateStatic<bool>(typeof(RequireIpAllowListRuleEvaluator), "IsIpInCidr", IPAddress.Parse("10.0.0.1"), "10.0.0.0/-1")
            .Should().BeFalse();

        var nullIdentityContext = new AuthorizationHandlerContext([new TestRequirement()], new ClaimsPrincipal(), null);
        (await new RequireMfaRuleEvaluator().EvaluateAsync(nullIdentityContext, new RuleParameters()))
            .IsSuccess.Should().BeFalse();
        (await new TenantMatchRuleEvaluator(Mock.Of<IAuthorizationTenantContext>()).EvaluateAsync(nullIdentityContext, new RuleParameters()))
            .IsSuccess.Should().BeFalse();

        using var compareDocument = JsonDocument.Parse("""{"number":42}""");
        InvokePrivateStatic<bool>(typeof(AbacPolicyEvaluator), "CompareValues", null, compareDocument.RootElement.GetProperty("number"))
            .Should().BeFalse();

        var aclMetrics = new Mock<ICacheMetricsService>();
        var aclWithMetrics = new CachedAccessControlListService(
            Mock.Of<IAccessControlListService>(),
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<ITenantSecurityVersionStore>(),
            Mock.Of<IUserSecurityVersionStore>(),
            Options.Create(new AuthorizationCacheOptions { AccessControlListTtlSeconds = 60 }),
            hybridCache: null,
            metrics: aclMetrics.Object);
        var aclKeys = (System.Collections.Concurrent.ConcurrentDictionary<string, HashSet<string>>)typeof(CachedAccessControlListService)
            .GetField("_tenantCacheKeys", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(aclWithMetrics)!;
        var aclTenantId = Guid.NewGuid();
        var aclUserId = Guid.NewGuid();
        aclKeys[aclTenantId.ToString()] =
        [
            $"acl:{aclTenantId}:{aclUserId}:Document:doc-1:tv1:uv1:gv1",
            $"acl:{aclTenantId}:subject:Document:doc-1:tv1:uv1:gv1"
        ];
        aclWithMetrics.InvalidateTenant(aclTenantId.ToString());
        aclMetrics.Verify(m => m.RecordEviction(CacheLevel.L1, "acl"), Times.Exactly(2));

        var aclWithoutMetrics = new CachedAccessControlListService(
            Mock.Of<IAccessControlListService>(),
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<ITenantSecurityVersionStore>(),
            Mock.Of<IUserSecurityVersionStore>(),
            Options.Create(new AuthorizationCacheOptions { AccessControlListTtlSeconds = 60 }),
            hybridCache: null,
            metrics: null);
        var aclNullMetricKeys = (System.Collections.Concurrent.ConcurrentDictionary<string, HashSet<string>>)typeof(CachedAccessControlListService)
            .GetField("_tenantCacheKeys", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(aclWithoutMetrics)!;
        aclNullMetricKeys[aclTenantId.ToString()] =
        [
            $"acl:{aclTenantId}:{aclUserId}:Document:doc-1:tv1:uv1:gv1",
            $"acl:{aclTenantId}:subject:Document:doc-1:tv1:uv1:gv1"
        ];
        InvokePrivate<object>(aclWithoutMetrics, "InvalidatePrincipalResourceCache", AclPrincipalType.User, aclUserId, aclTenantId, "Document", "doc-1");
        aclNullMetricKeys[aclTenantId.ToString()] = [$"acl:{aclTenantId}:{aclUserId}:Document:doc-1:tv1:uv1:gv1"];
        InvokePrivate<object>(aclWithoutMetrics, "InvalidateUserResourceCache", aclUserId, aclTenantId, "Document", "doc-1");

        var policyMetrics = new Mock<ICacheMetricsService>();
        var policyStore = new CachedPolicyDefinitionStore(
            Mock.Of<IPolicyDefinitionStore>(),
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<ITenantSecurityVersionStore>(),
            Options.Create(new AuthorizationCacheOptions { PolicyTtlSeconds = 60 }),
            hybridCache: null,
            metrics: policyMetrics.Object);
        var policyKeys = (System.Collections.Concurrent.ConcurrentDictionary<string, HashSet<string>>)typeof(CachedPolicyDefinitionStore)
            .GetField("_tenantCacheKeys", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(policyStore)!;
        policyKeys["tenant"] = ["policy:alpha:tenant:v1"];
        policyStore.InvalidateTenant("tenant");
        policyKeys["tenant"] = ["policy:alpha:tenant:v2", "policy:beta:tenant:v2"];
        policyStore.InvalidatePolicy("alpha", "tenant");
        policyMetrics.Verify(m => m.RecordEviction(CacheLevel.L1, "policy"), Times.Exactly(2));

        var conditional = new ConditionalPolicyEvaluator(
            Mock.Of<IConditionalPolicyRepository>(),
            NullLogger<ConditionalPolicyEvaluator>.Instance);
        var futureStart = TimeOnly.FromDateTime(SystemClock.UtcNow.AddSeconds(5));
        InvokePrivate<bool>(
                conditional,
                "EvaluateTimeConditions",
                $$"""{"StartTime":"{{futureStart:HH:mm:ss}}","EndTime":"23:59:59"}""")
            .Should().BeFalse();

        var merger = new DefaultPolicyMerger();
        var merged = merger.Merge(
            new PolicyDefinition
            {
                PolicyName = "merge",
                RequireAuthentication = true,
                RequireAccessControlListAccess = true,
                ResourceType = "Document",
                MinimumAccessLevel = "Read",
                Version = 5,
                UseRuleBasedEvaluation = true,
                Rules = [new PolicyRule { Type = RuleTypes.RequireMfa }]
            },
            new PolicyDefinition
            {
                PolicyName = "merge",
                RequireAuthentication = false,
                ResourceType = null,
                MinimumAccessLevel = null,
                Version = 2,
                UseRuleBasedEvaluation = false,
                Rules = null
            });
        merged.RequireAuthentication.Should().BeTrue();
        merged.RequireAccessControlListAccess.Should().BeTrue();
        merged.Rules.Should().NotBeNull();
        merger.Merge(
                new PolicyDefinition { PolicyName = "tenant-resource", ResourceType = "Base", MinimumAccessLevel = "Read" },
                new PolicyDefinition { PolicyName = "tenant-resource", ResourceType = "Override", MinimumAccessLevel = "Admin" })
            .ResourceType.Should().Be("Override");
        merger.Build(new PolicyDefinition
        {
            PolicyName = "rules-enabled",
            RequireAuthentication = false,
            UseRuleBasedEvaluation = true,
            Rules = [new PolicyRule { Type = RuleTypes.RequireMfa }]
        }).Requirements.Should().Contain(r => r is RulesetRequirement);
        merger.Build(new PolicyDefinition
        {
            PolicyName = "rules-null",
            RequireAuthentication = false,
            UseRuleBasedEvaluation = true,
            Rules = null,
            RequiredRoles = ["Admin"]
        }).Requirements.Should().NotBeEmpty();
        merger.Build(new PolicyDefinition
        {
            PolicyName = "rules-disabled",
            RequireAuthentication = false,
            UseRuleBasedEvaluation = false,
            Rules = [new PolicyRule { Type = RuleTypes.RequireMfa }],
            RequiredPermissions = ["documents:read"]
        }).Requirements.Should().NotBeEmpty();

        var logger = new PolicyEvaluationLogger(NullLogger<PolicyEvaluationLogger>.Instance);
        logger.GetDebugSettings(new object()).Should().BeNull();
        logger.GetDebugSettings(new Endpoint(null, new EndpointMetadataCollection(), "debug-empty"))
            .Should().BeNull();
        logger.GetDebugSettings(new Endpoint(
                null,
                new EndpointMetadataCollection(new PolicyDebugAttribute { Enabled = false }),
                "debug-disabled"))
            .Should().BeNull();
        InvokePrivateStatic<string>(typeof(PolicyEvaluationLogger), "GetUserId", Principal(new Claim(ClaimTypes.NameIdentifier, "name-id")))
            .Should().Be("name-id");
        InvokePrivateStatic<string>(typeof(PolicyEvaluationLogger), "GetUserId", new ClaimsPrincipal())
            .Should().Be("(anonymous)");

        var gateService = new PolicyGateService(
            Mock.Of<IConditionalPolicyEvaluator>(),
            Mock.Of<IAbacPolicyEvaluator>(),
            NullLogger<PolicyGateService>.Instance);
        InvokePrivate<PolicyGateResult>(gateService, "EvaluateEnvironmentGate", new PolicyGateContext
            {
                ActorId = Guid.NewGuid(),
                ResourceType = "Document",
                Action = "read",
                UserAgent = ""
            })
            .IsAllowed.Should().BeTrue();

        var rulesetProvider = new RulesetProvider(
            Mock.Of<IPolicyDefinitionRepository>(),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<RulesetProvider>.Instance);
        InvokePrivate<PolicyRuleset>(
                rulesetProvider,
                "ConvertToRuleset",
                new PolicyDefinitionEntity
                {
                    PolicyName = "acl-policy",
                    RequireAccessControlListAccess = true,
                    MinimumAccessLevel = "Write"
                })
            .Rules.Should().Contain(r => r.Type == RuleTypes.OwnerOrAcl);
        InvokePrivate<PolicyRuleset>(
                rulesetProvider,
                "ConvertToRuleset",
                new PolicyDefinitionEntity
                {
                    PolicyName = "acl-policy-default",
                    RequireAccessControlListAccess = true,
                    MinimumAccessLevel = null
                })
            .Rules.Should().Contain(r => r.Params["minimumAccessLevel"].GetString() == "Read");

        ClaimsExtractor.GetName(Principal(new Claim(ClaimTypes.Name, "display"))).Should().Be("display");
        ClaimsExtractor.GetName(new ClaimsPrincipal(new ClaimsIdentity())).Should().BeNull();
        ClaimsExtractor.IsAuthenticated(new ClaimsPrincipal()).Should().BeFalse();

        var cacheStats = new CacheStatistics { L2Hits = 2, Misses = 2 };
        cacheStats.L2HitRate.Should().Be(0.5);
    }

    private static Guid GetResourceId(object request, string propertyName)
    {
        return InvokePrivateStatic<Guid>(
            typeof(AuthorizationBehavior<AuthorizedResourceRequest, string>),
            "GetResourceIdFromRequest",
            request,
            propertyName);
    }

    private static AccessLevel MapPermission(string permission)
    {
        return InvokePrivateStatic<AccessLevel>(
            typeof(AuthorizationBehavior<AuthorizedResourceRequest, string>),
            "MapPermissionToAccessLevel",
            permission);
    }

    private static string ConvertIanaToWindows(string timezone)
    {
        return InvokePrivateStatic<string>(
            typeof(RequireTimeWindowRuleEvaluator),
            "ConvertIanaToWindows",
            timezone);
    }

    private static bool IsWithinWindow(string json, int day, TimeSpan currentTime)
    {
        using var document = JsonDocument.Parse(json);
        return InvokePrivateStatic<bool>(
            typeof(RequireTimeWindowRuleEvaluator),
            "IsWithinWindow",
            document.RootElement,
            day,
            currentTime);
    }

    private static T InvokePrivateStatic<T>(Type type, string methodName, params object?[] args)
    {
        var method = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(m => m.Name == methodName && m.GetParameters().Length == args.Length);
        method.Should().NotBeNull();
        try
        {
            return (T)method.Invoke(null, args)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // Surface the private method's own exception (e.g. the fail-closed
            // permission-mapping denial) instead of the reflection wrapper.
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // unreachable
        }
    }

    private static T InvokePrivate<T>(object instance, string methodName, params object?[] args)
    {
        var method = instance.GetType()
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(m => m.Name == methodName && m.GetParameters().Length == args.Length);
        method.Should().NotBeNull();
        return (T)method.Invoke(instance, args)!;
    }

    private static void TouchRepositorySet(object repository)
    {
        foreach (var property in repository.GetType().GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
                     .Where(p => p.GetMethod is not null
                                 && p.GetIndexParameters().Length == 0
                                 && p.PropertyType.IsGenericType
                                 && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>)))
        {
            try
            {
                property.GetValue(repository);
            }
            catch (TargetInvocationException)
            {
            }
        }
    }

    private static (string? resourceType, string? resourceId) GetResourceIdentifiers(object resource, ResourceAccessRequirement requirement)
    {
        return InvokePrivateStatic<(string?, string?)>(
            typeof(ResourceAccessHandler),
            "GetResourceIdentifiers",
            resource,
            requirement);
    }

    private static Permission CreatePermission(Type permissionType, string key, string description)
    {
        var constructor = permissionType.GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            [typeof(string), typeof(string)],
            modifiers: null);
        constructor.Should().NotBeNull();
        return (Permission)constructor!.Invoke([key, description]);
    }

    private static bool TryCreateArguments(ConstructorInfo ctor, out object?[] args)
    {
        var parameters = ctor.GetParameters();
        args = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            if (!TryCreateValue(parameters[i].ParameterType, out args[i]))
                return false;
        }

        return true;
    }

    private static bool TryCreateValue(Type type, out object? value)
    {
        if (type == typeof(string))
        {
            value = "value";
            return true;
        }

        if (type == typeof(Guid))
        {
            value = Guid.NewGuid();
            return true;
        }

        if (type == typeof(Guid?))
        {
            value = Guid.NewGuid();
            return true;
        }

        if (type == typeof(CancellationToken))
        {
            value = CancellationToken.None;
            return true;
        }

        if (type == typeof(RequestDelegate))
        {
            value = new RequestDelegate(_ => Task.CompletedTask);
            return true;
        }

        if (type == typeof(DbContext))
        {
            value = new DbContext(new DbContextOptionsBuilder<DbContext>().Options);
            return true;
        }

        if (type == typeof(IMemoryCache))
        {
            value = new MemoryCache(new MemoryCacheOptions());
            return true;
        }

        if (type == typeof(IHttpContextAccessor))
        {
            value = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            return true;
        }

        if (type == typeof(IServiceProvider))
        {
            value = new ServiceCollection().BuildServiceProvider();
            return true;
        }

        if (type == typeof(ILogger))
        {
            value = NullLogger.Instance;
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IOptions<>))
        {
            var optionsValue = Activator.CreateInstance(type.GetGenericArguments()[0])!;
            value = typeof(Options)
                .GetMethod(nameof(Options.Create))!
                .MakeGenericMethod(type.GetGenericArguments()[0])
                .Invoke(null, [optionsValue]);
            return true;
        }

        if (type.IsArray)
        {
            value = Array.CreateInstance(type.GetElementType()!, 0);
            return true;
        }

        if (type.IsEnum)
        {
            value = Enum.GetValues(type).GetValue(0);
            return true;
        }

        if (type.IsInterface || type.IsAbstract)
        {
            value = typeof(Mock)
                .GetMethods()
                .Single(m => m.Name == nameof(Mock.Of) && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
                .MakeGenericMethod(type)
                .Invoke(null, null);
            return true;
        }

        var parameterless = type.GetConstructor(Type.EmptyTypes);
        if (parameterless != null)
        {
            value = Activator.CreateInstance(type);
            return true;
        }

        value = null;
        return false;
    }

    private static ActorContext PrivateActor(object handler)
    {
        var property = handler.GetType().GetProperty("Actor", BindingFlags.NonPublic | BindingFlags.Instance);
        property.Should().NotBeNull();
        return (ActorContext)property!.GetValue(handler)!;
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static AuthorizationHandlerContext CreateAuthorizationContext()
    {
        return new AuthorizationHandlerContext(
            [new TestRequirement()],
            Principal(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())),
            null);
    }

    private static AuthorizationHandlerContext CreateAuthorizationContextWithClaims(params Claim[] claims)
    {
        return new AuthorizationHandlerContext([new TestRequirement()], Principal(claims), null);
    }

    private sealed record RequestWithGuid(Guid ResourceId);

    private sealed record RequestWithString(string ResourceId);

    private sealed record RequestWithNumber(int ResourceId);

    private sealed class TestResource;

    private sealed record TestOwnedResource(Guid OwnerId, Guid TenantId) : IOwnedResource;

    private sealed record TestAclResource(Guid OwnerId, Guid TenantId, string ResourceType, string ResourceId)
        : IAccessControlListResource;

    private sealed record TestUserIdResource(Guid? UserId) : IUserIdResource;

    private sealed class TestPermission(string resource, string action, string? scope, string description)
        : Permission(resource, action, scope, description);

    private enum TestResourcePermission
    {
        Read,
        Edit
    }

    private sealed class TestRequirement : IAuthorizationRequirement;

    private sealed class DefaultTenantResolver : IAuthorizationTenantResolver
    {
        public string? ResolveFromRequest(HttpContext context) => "resolved";

        public string? ResolveFromClaims(ClaimsPrincipal principal) => null;

        public string? GetUserDefaultTenant(ClaimsPrincipal principal) => null;
    }

    private sealed class NullStringObject
    {
        public override string? ToString() => null;
    }

    private sealed class DictionaryDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _values = new();

        public byte[]? Get(string key) => _values.GetValueOrDefault(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _values[key] = value;

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key) => _values.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("cache failure");

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("cache failure");

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new InvalidOperationException("cache failure");

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw new InvalidOperationException("cache failure");

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key) => throw new InvalidOperationException("cache failure");

        public Task RemoveAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("cache failure");
    }

    [AuthorizeRequest("read", ResourceType = "Document", ResourceIdProperty = nameof(ResourceId))]
    private sealed record AuthorizedResourceRequest(Guid ResourceId) : IQuery<string>;

    [AuthorizeRequest("tenant:allowed")]
    private sealed record AuthorizedTenantRequest : IQuery<string>;

    [RequireTenantPermission("controller:tenant")]
    private sealed class TestController
    {
        [RequiresPermission("action:simple")]
        [RequirePermission("action:alias")]
        [RequireResourcePermission<TestResourcePermission, TestResource>(TestResourcePermission.Read)]
        [RequireContentTypePermission<TestResource>("content:read")]
        public void Action()
        {
        }
    }
}

internal static class ActionDescriptorFactory
{
    internal static Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor CreateActionDescriptor(
        MethodInfo method,
        TypeInfo controllerType)
    {
        return new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor
        {
            MethodInfo = method,
            ControllerTypeInfo = controllerType,
            ControllerName = controllerType.Name,
            ActionName = method.Name
        };
    }
}
