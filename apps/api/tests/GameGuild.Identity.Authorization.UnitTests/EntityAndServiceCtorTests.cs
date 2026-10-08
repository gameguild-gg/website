using System.Security.Claims;
using FluentAssertions;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Authorization.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MsAuthorizationOptions = Microsoft.AspNetCore.Authorization.AuthorizationOptions;

namespace GameGuild.Identity.Authorization.UnitTests;

/// <summary>
/// R5 tests targeting entities (ResourceUserPermission, ResourceInvitation, ConditionalPolicy),
/// LocalizationContext, and service constructors to push coverage past 75%.
/// </summary>
public class EntityAndServiceCtorTests
{
    // ─── ResourceUserPermission Entity ─────────────────────────────────

    [Fact]
    public void ResourceUserPermission_CanCreate_WithRequiredProperties()
    {
        var perm = new ResourceUserPermission
        {
            TenantId = new TenantId(Guid.NewGuid()),
            UserId = Guid.NewGuid(),
            ResourceType = "course",
            ResourceId = "course-123",
            Permissions = new[] { "read", "write" },
            GrantedByUserId = Guid.NewGuid()
        };
        perm.ResourceType.Should().Be("course");
        perm.ResourceId.Should().Be("course-123");
        perm.Permissions.Should().HaveCount(2);
    }

    [Fact]
    public void ResourceUserPermission_IsActive_WhenNotRevokedNotExpired()
    {
        var perm = new ResourceUserPermission
        {
            TenantId = new TenantId(Guid.NewGuid()),
            UserId = Guid.NewGuid(),
            ResourceType = "project",
            ResourceId = "proj-1",
            Permissions = new[] { "read" },
            GrantedByUserId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };
        perm.IsActive.Should().BeTrue();
        perm.CanAccess.Should().BeTrue();
        perm.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void ResourceUserPermission_IsExpired_WhenPastExpiry()
    {
        var perm = new ResourceUserPermission
        {
            TenantId = new TenantId(Guid.NewGuid()),
            UserId = Guid.NewGuid(),
            ResourceType = "project",
            ResourceId = "proj-1",
            Permissions = new[] { "read" },
            GrantedByUserId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };
        perm.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void ResourceUserPermission_Revoke_SetsRevokedFields()
    {
        var perm = new ResourceUserPermission
        {
            TenantId = new TenantId(Guid.NewGuid()),
            UserId = Guid.NewGuid(),
            ResourceType = "doc",
            ResourceId = "doc-1",
            Permissions = new[] { "read" },
            GrantedByUserId = Guid.NewGuid()
        };
        var revokedBy = Guid.NewGuid();
        var result = perm.Revoke(revokedBy, "policy violation");
        result.Should().BeTrue();
        perm.RevokedAt.Should().NotBeNull();
        perm.RevokedByUserId.Should().Be(revokedBy);
        perm.RevocationReason.Should().Be("policy violation");
        perm.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ResourceUserPermission_Revoke_AlreadyRevoked_ReturnsFalse()
    {
        var perm = new ResourceUserPermission
        {
            TenantId = new TenantId(Guid.NewGuid()),
            UserId = Guid.NewGuid(),
            ResourceType = "doc",
            ResourceId = "doc-1",
            Permissions = new[] { "read" },
            GrantedByUserId = Guid.NewGuid()
        };
        perm.Revoke(Guid.NewGuid());
        var second = perm.Revoke(Guid.NewGuid());
        second.Should().BeFalse();
    }

    [Fact]
    public void ResourceUserPermission_UpdatePermissions_UpdatesArray()
    {
        var perm = new ResourceUserPermission
        {
            TenantId = new TenantId(Guid.NewGuid()),
            UserId = Guid.NewGuid(),
            ResourceType = "repo",
            ResourceId = "repo-1",
            Permissions = new[] { "read" },
            GrantedByUserId = Guid.NewGuid()
        };
        var result = perm.UpdatePermissions(new[] { "read", "write", "admin" }, Guid.NewGuid());
        result.Should().BeTrue();
        perm.Permissions.Should().Contain("admin");
    }

    [Fact]
    public void ResourceUserPermission_RecordAccess_SetsLastAccessedAt()
    {
        var perm = new ResourceUserPermission
        {
            TenantId = new TenantId(Guid.NewGuid()),
            UserId = Guid.NewGuid(),
            ResourceType = "file",
            ResourceId = "file-1",
            Permissions = new[] { "read" },
            GrantedByUserId = Guid.NewGuid()
        };
        perm.LastAccessedAt.Should().BeNull();
        perm.RecordAccess();
        perm.LastAccessedAt.Should().NotBeNull();
    }

    [Fact]
    public void ResourceUserPermission_HasPermission_ReturnsCorrectly()
    {
        var perm = new ResourceUserPermission
        {
            TenantId = new TenantId(Guid.NewGuid()),
            UserId = Guid.NewGuid(),
            ResourceType = "asset",
            ResourceId = "a-1",
            Permissions = new[] { "read", "write" },
            GrantedByUserId = Guid.NewGuid()
        };
        perm.HasPermission("read").Should().BeTrue();
        perm.HasPermission("delete").Should().BeFalse();
    }

    [Fact]
    public void ResourceUserPermission_HasAnyPermission_ReturnsCorrectly()
    {
        var perm = new ResourceUserPermission
        {
            TenantId = new TenantId(Guid.NewGuid()),
            UserId = Guid.NewGuid(),
            ResourceType = "asset",
            ResourceId = "a-1",
            Permissions = new[] { "read" },
            GrantedByUserId = Guid.NewGuid()
        };
        perm.HasAnyPermission(new[] { "write", "read" }).Should().BeTrue();
        perm.HasAnyPermission(new[] { "delete", "admin" }).Should().BeFalse();
    }

    [Fact]
    public void ResourceUserPermission_HasAllPermissions_ReturnsCorrectly()
    {
        var perm = new ResourceUserPermission
        {
            TenantId = new TenantId(Guid.NewGuid()),
            UserId = Guid.NewGuid(),
            ResourceType = "asset",
            ResourceId = "a-1",
            Permissions = new[] { "read", "write", "admin" },
            GrantedByUserId = Guid.NewGuid()
        };
        perm.HasAllPermissions(new[] { "read", "write" }).Should().BeTrue();
        perm.HasAllPermissions(new[] { "read", "delete" }).Should().BeFalse();
    }

    [Fact]
    public void ResourceUserPermission_IsOwner_DefaultsFalse()
    {
        var perm = new ResourceUserPermission
        {
            TenantId = new TenantId(Guid.NewGuid()),
            UserId = Guid.NewGuid(),
            ResourceType = "x",
            ResourceId = "x-1",
            Permissions = new[] { "read" },
            GrantedByUserId = Guid.NewGuid()
        };
        perm.IsOwner.Should().BeFalse();
    }

    // ─── ResourceInvitation Entity ────────────────────────────────────

    [Fact]
    public void ResourceInvitation_CanCreate_WithRequiredProperties()
    {
        var inv = new ResourceInvitation
        {
            TenantId = new TenantId(Guid.NewGuid()),
            Email = "user@example.com",
            ResourceType = "project",
            ResourceId = "proj-1",
            Permissions = new[] { "read" },
            InvitedByUserId = Guid.NewGuid()
        };
        inv.Email.Should().Be("user@example.com");
        inv.IsPending.Should().BeTrue();
    }

    [Fact]
    public void ResourceInvitation_Accept_ChangesStatus()
    {
        var inv = new ResourceInvitation
        {
            TenantId = new TenantId(Guid.NewGuid()),
            Email = "user@example.com",
            ResourceType = "course",
            ResourceId = "c-1",
            Permissions = new[] { "read" },
            InvitedByUserId = Guid.NewGuid()
        };
        var acceptUserId = Guid.NewGuid();
        var result = inv.Accept(acceptUserId);
        result.Should().BeTrue();
        inv.IsPending.Should().BeFalse();
        inv.AcceptedByUserId.Should().Be(acceptUserId);
        inv.AcceptedAt.Should().NotBeNull();
    }

    [Fact]
    public void ResourceInvitation_Decline_SetsDeclinedFields()
    {
        var inv = new ResourceInvitation
        {
            TenantId = new TenantId(Guid.NewGuid()),
            Email = "user@example.com",
            ResourceType = "course",
            ResourceId = "c-1",
            Permissions = new[] { "read" },
            InvitedByUserId = Guid.NewGuid()
        };
        var result = inv.Decline("not interested");
        result.Should().BeTrue();
        inv.DeclinedAt.Should().NotBeNull();
        inv.DeclineReason.Should().Be("not interested");
    }

    [Fact]
    public void ResourceInvitation_Revoke_SetsRevokedFields()
    {
        var inv = new ResourceInvitation
        {
            TenantId = new TenantId(Guid.NewGuid()),
            Email = "user@example.com",
            ResourceType = "doc",
            ResourceId = "d-1",
            Permissions = new[] { "write" },
            InvitedByUserId = Guid.NewGuid()
        };
        var revokedBy = Guid.NewGuid();
        var result = inv.Revoke(revokedBy);
        result.Should().BeTrue();
        inv.RevokedAt.Should().NotBeNull();
        inv.RevokedByUserId.Should().Be(revokedBy);
        inv.CanBeRevoked.Should().BeFalse();
    }

    [Fact]
    public void ResourceInvitation_CanBeAccepted_WhenPendingAndNotExpired()
    {
        var inv = new ResourceInvitation
        {
            TenantId = new TenantId(Guid.NewGuid()),
            Email = "a@b.com",
            ResourceType = "x",
            ResourceId = "x-1",
            Permissions = new[] { "read" },
            InvitedByUserId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        inv.CanBeAccepted.Should().BeTrue();
    }

    [Fact]
    public void ResourceInvitation_IsExpired_WhenPastExpiry()
    {
        var inv = new ResourceInvitation
        {
            TenantId = new TenantId(Guid.NewGuid()),
            Email = "a@b.com",
            ResourceType = "x",
            ResourceId = "x-1",
            Permissions = new[] { "read" },
            InvitedByUserId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };
        inv.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void ResourceInvitation_AcceptAlreadyAccepted_ReturnsFalse()
    {
        var inv = new ResourceInvitation
        {
            TenantId = new TenantId(Guid.NewGuid()),
            Email = "a@b.com",
            ResourceType = "x",
            ResourceId = "x-1",
            Permissions = new[] { "read" },
            InvitedByUserId = Guid.NewGuid()
        };
        inv.Accept(Guid.NewGuid());
        inv.Accept(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void ResourceInvitation_DeclineAlreadyDeclined_ReturnsFalse()
    {
        var inv = new ResourceInvitation
        {
            TenantId = new TenantId(Guid.NewGuid()),
            Email = "a@b.com",
            ResourceType = "x",
            ResourceId = "x-1",
            Permissions = new[] { "read" },
            InvitedByUserId = Guid.NewGuid()
        };
        inv.Decline();
        inv.Decline().Should().BeFalse();
    }

    // ─── ConditionalPolicy Entity ────────────────────────────────────

    [Fact]
    public void ConditionalPolicy_DefaultValues()
    {
        var policy = new ConditionalPolicy();
        policy.Id.Should().NotBeEmpty();
        policy.Name.Should().BeEmpty();
        policy.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void ConditionalPolicy_IsActive_WhenEnabled()
    {
        var policy = new ConditionalPolicy { Name = "test" };
        policy.IsActive().Should().BeTrue();
    }

    [Fact]
    public void ConditionalPolicy_Disable_SetsNotActive()
    {
        var policy = new ConditionalPolicy { Name = "test" };
        policy.Disable();
        policy.IsEnabled.Should().BeFalse();
        policy.IsActive().Should().BeFalse();
    }

    [Fact]
    public void ConditionalPolicy_Enable_AfterDisable()
    {
        var policy = new ConditionalPolicy { Name = "test" };
        policy.Disable();
        policy.Enable();
        policy.IsEnabled.Should().BeTrue();
        policy.IsActive().Should().BeTrue();
    }

    [Fact]
    public void ConditionalPolicy_SetPriority_ValidValue()
    {
        var policy = new ConditionalPolicy { Name = "test" };
        policy.SetPriority(10);
        policy.Priority.Should().Be(10);
    }

    [Fact]
    public void ConditionalPolicy_SetPriority_NegativeThrows()
    {
        var policy = new ConditionalPolicy { Name = "test" };
        var act = () => policy.SetPriority(-1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConditionalPolicy_AppliesTo_MatchesPermissionType()
    {
        var policy = new ConditionalPolicy
        {
            Name = "p1",
            PermissionType = "content.read"
        };
        policy.AppliesTo("content.read").Should().BeTrue();
        policy.AppliesTo("content.write").Should().BeFalse();
    }

    [Fact]
    public void ConditionalPolicy_AppliesTo_NullPermissionType_MatchesAll()
    {
        var policy = new ConditionalPolicy { Name = "p1", PermissionType = null };
        policy.AppliesTo("anything").Should().BeTrue();
    }

    [Fact]
    public void ConditionalPolicy_AppliesToResourceType_Matches()
    {
        var policy = new ConditionalPolicy { Name = "p1", ResourceType = "project" };
        policy.AppliesToResourceType("project").Should().BeTrue();
        policy.AppliesToResourceType("course").Should().BeFalse();
    }

    [Fact]
    public void ConditionalPolicy_AppliesToResourceType_Null_MatchesAll()
    {
        var policy = new ConditionalPolicy { Name = "p1", ResourceType = null };
        policy.AppliesToResourceType("anything").Should().BeTrue();
    }

    [Fact]
    public void ConditionalPolicy_AllProperties_SetCorrectly()
    {
        var policy = new ConditionalPolicy
        {
            Name = "time-restriction",
            Description = "Restrict access during off-hours",
            ConditionType = PolicyConditionType.Time,
            Action = PolicyAction.Deny,
            Priority = 5,
            TimeConditions = "{\"start\":\"18:00\",\"end\":\"06:00\"}",
            CreatedBy = Guid.NewGuid()
        };
        policy.ConditionType.Should().Be(PolicyConditionType.Time);
        policy.Action.Should().Be(PolicyAction.Deny);
        policy.TimeConditions.Should().NotBeNullOrEmpty();
    }

    // ─── LocalizationContext ─────────────────────────────────────────

    [Fact]
    public void LocalizationContext_NullHttpContext_AllPropertiesNull()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);
        var ctx = new LocalizationContext(accessor.Object);
        ctx.CultureCode.Should().BeNull();
        ctx.UICultureCode.Should().BeNull();
        ctx.TimeZone.Should().BeNull();
        ctx.DateFormat.Should().BeNull();
        ctx.NumberFormat.Should().BeNull();
    }

    [Fact]
    public void LocalizationContext_WithClaims_ReadsCultureFromClaims()
    {
        var httpCtx = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim("culture", "en-US"),
            new Claim("ui_culture", "pt-BR"),
            new Claim("timezone", "America/Sao_Paulo"),
            new Claim("date_format", "dd/MM/yyyy"),
            new Claim("number_format", "pt-BR")
        };
        httpCtx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(x => x.HttpContext).Returns(httpCtx);

        var ctx = new LocalizationContext(accessor.Object);
        ctx.CultureCode.Should().Be("en-US");
        ctx.UICultureCode.Should().Be("pt-BR");
        ctx.TimeZone.Should().Be("America/Sao_Paulo");
        ctx.DateFormat.Should().Be("dd/MM/yyyy");
        ctx.NumberFormat.Should().Be("pt-BR");
    }

    [Fact]
    public void LocalizationContext_UICultureFallsToCulture()
    {
        var httpCtx = new DefaultHttpContext();
        var claims = new[] { new Claim("culture", "fr-FR") };
        httpCtx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(x => x.HttpContext).Returns(httpCtx);

        var ctx = new LocalizationContext(accessor.Object);
        ctx.CultureCode.Should().Be("fr-FR");
        ctx.UICultureCode.Should().Be("fr-FR");
    }

    [Fact]
    public void LocalizationContext_NoCultureClaim_FallsToAcceptLanguageHeader()
    {
        var httpCtx = new DefaultHttpContext();
        httpCtx.User = new ClaimsPrincipal(new ClaimsIdentity());
        httpCtx.Request.Headers["Accept-Language"] = "de-DE";
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(x => x.HttpContext).Returns(httpCtx);

        var ctx = new LocalizationContext(accessor.Object);
        ctx.CultureCode.Should().Be("de-DE");
    }

    [Fact]
    public void LocalizationContext_NoClaimsNoHeader_NullCulture()
    {
        var httpCtx = new DefaultHttpContext();
        httpCtx.User = new ClaimsPrincipal(new ClaimsIdentity());
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(x => x.HttpContext).Returns(httpCtx);

        var ctx = new LocalizationContext(accessor.Object);
        ctx.CultureCode.Should().BeNull();
    }

    // ─── Service Constructor Tests ───────────────────────────────────

    [Fact]
    public void EffectivePermissionResolverService_CanConstruct()
    {
        var svc = new EffectivePermissionResolverService(
            Mock.Of<IRbacPermissionResolver>(),
            Mock.Of<ITenantPermissionStore>(),
            Mock.Of<IResourcePermissionStore>(),
            Options.Create(new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions()),
            NullLogger<EffectivePermissionResolverService>.Instance
        );
        svc.Should().NotBeNull();
    }

    [Fact]
    public void DbAuthorizationPolicyProvider_CanConstruct()
    {
        var provider = new DbAuthorizationPolicyProvider(
            Options.Create(new MsAuthorizationOptions()),
            Mock.Of<IPolicyCache>(),
            Mock.Of<IPolicyMerger>(),
            Mock.Of<IServiceScopeFactory>(),
            Options.Create(new GameGuild.Configuration.PresentationLayer.Authorization.TenancyOptions()),
            NullLogger<DbAuthorizationPolicyProvider>.Instance
        );
        provider.Should().NotBeNull();
    }

    [Fact]
    public async Task DbAuthorizationPolicyProvider_GetDefaultPolicyAsync_ReturnsPolicy()
    {
        var provider = new DbAuthorizationPolicyProvider(
            Options.Create(new MsAuthorizationOptions()),
            Mock.Of<IPolicyCache>(),
            Mock.Of<IPolicyMerger>(),
            Mock.Of<IServiceScopeFactory>(),
            Options.Create(new GameGuild.Configuration.PresentationLayer.Authorization.TenancyOptions()),
            NullLogger<DbAuthorizationPolicyProvider>.Instance
        );
        var policy = await provider.GetDefaultPolicyAsync();
        policy.Should().NotBeNull();
    }

    [Fact]
    public async Task DbAuthorizationPolicyProvider_GetFallbackPolicyAsync_ReturnsNullOrPolicy()
    {
        var provider = new DbAuthorizationPolicyProvider(
            Options.Create(new MsAuthorizationOptions()),
            Mock.Of<IPolicyCache>(),
            Mock.Of<IPolicyMerger>(),
            Mock.Of<IServiceScopeFactory>(),
            Options.Create(new GameGuild.Configuration.PresentationLayer.Authorization.TenancyOptions()),
            NullLogger<DbAuthorizationPolicyProvider>.Instance
        );
        // GetFallbackPolicyAsync may return null
        var act = async () => await provider.GetFallbackPolicyAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void SoDService_CanConstruct()
    {
        var svc = new SoDService(
            Mock.Of<ISoDRuleRepository>(),
            Mock.Of<ISoDViolationRepository>(),
            NullLogger<SoDService>.Instance
        );
        svc.Should().NotBeNull();
    }

    [Fact]
    public async Task SoDService_ScanForViolationsAsync_ReturnsZero()
    {
        var svc = new SoDService(
            Mock.Of<ISoDRuleRepository>(),
            Mock.Of<ISoDViolationRepository>(),
            NullLogger<SoDService>.Instance
        );
        var count = await svc.ScanForViolationsAsync(Guid.NewGuid());
        count.Should().Be(0);
    }

    [Fact]
    public void AccessReviewService_CanConstruct()
    {
        var svc = new AccessReviewService(
            Mock.Of<IAccessReviewCampaignRepository>(),
            Mock.Of<IAccessReviewItemRepository>(),
            NullLogger<AccessReviewService>.Instance,
            actorContextAccessor: Mock.Of<GameGuild.Identity.Context.Actors.IActorContextAccessor>());
        svc.Should().NotBeNull();
    }

    [Fact]
    public void AccessReviewService_CanConstruct_WithPublisher()
    {
        var svc = new AccessReviewService(
            Mock.Of<IAccessReviewCampaignRepository>(),
            Mock.Of<IAccessReviewItemRepository>(),
            NullLogger<AccessReviewService>.Instance,
            Mock.Of<GameGuild.CQRS.IPublisher>(),
            Mock.Of<GameGuild.Identity.Context.Actors.IActorContextAccessor>());
        svc.Should().NotBeNull();
    }

    [Fact]
    public void JitElevationService_CanConstruct()
    {
        var svc = new JitElevationService(
            Mock.Of<IJitElevationRequestRepository>(),
            Mock.Of<IPermissionAuditService>(),
            Mock.Of<ITenantSecurityVersionStore>(),
            NullLogger<JitElevationService>.Instance
        );
        svc.Should().NotBeNull();
    }

    [Fact]
    public void ResourcePermissionAuthorizationFilter_CanConstruct()
    {
        var filter = new ResourcePermissionAuthorizationFilter(
            NullLogger<ResourcePermissionAuthorizationFilter>.Instance
        );
        filter.Should().NotBeNull();
    }

    [Fact]
    public void AuthorizationBehavior_CanConstruct()
    {
        var behavior = new AuthorizationBehavior<TestRequest, string>(
            Mock.Of<GameGuild.Identity.Context.Actors.IActorContextAccessor>(),
            Mock.Of<IAccessControlListService>()
        );
        behavior.Should().NotBeNull();
    }

    // ─── Helper types ────────────────────────────────────────────────

    public record TestRequest : GameGuild.CQRS.IRequestBase;
}
