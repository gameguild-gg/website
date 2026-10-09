using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using GameGuild;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Caching;
using GameGuild.Identity.Authorization.Configuration;

namespace GameGuild.Identity.Authorization.UnitTests;

public class EfConfigAndExtendedCoverageTests
{
    private static ModelBuilder CreateModelBuilder() => new(new ConventionSet());

    // ── EF Configuration Tests (18 configs) ─────────────────────────────
    [Fact]
    public void AccessControlListEntryConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new AccessControlListEntryConfiguration();
        cfg.Configure(mb.Entity<AccessControlListEntry>());
        mb.Model.FindEntityType(typeof(AccessControlListEntry)).Should().NotBeNull();
    }

    [Fact]
    public void TenantSecurityVersionConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new TenantSecurityVersionConfiguration();
        cfg.Configure(mb.Entity<TenantSecurityVersion>());
        mb.Model.FindEntityType(typeof(TenantSecurityVersion)).Should().NotBeNull();
    }

    [Fact]
    public void DynamicRoleConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new DynamicRoleConfiguration();
        cfg.Configure(mb.Entity<DynamicRole>());
        mb.Model.FindEntityType(typeof(DynamicRole)).Should().NotBeNull();
    }

    [Fact]
    public void DynamicRoleAssignmentConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new DynamicRoleAssignmentConfiguration();
        cfg.Configure(mb.Entity<DynamicRoleAssignment>());
        mb.Model.FindEntityType(typeof(DynamicRoleAssignment)).Should().NotBeNull();
    }

    [Fact]
    public void ResourceUserPermissionConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new ResourceUserPermissionConfiguration();
        cfg.Configure(mb.Entity<ResourceUserPermission>());
        mb.Model.FindEntityType(typeof(ResourceUserPermission)).Should().NotBeNull();
    }

    [Fact]
    public void ResourceInvitationConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new ResourceInvitationConfiguration();
        cfg.Configure(mb.Entity<ResourceInvitation>());
        mb.Model.FindEntityType(typeof(ResourceInvitation)).Should().NotBeNull();
    }

    [Fact]
    public void PolicyDefinitionEntityConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new PolicyDefinitionEntityConfiguration();
        cfg.Configure(mb.Entity<PolicyDefinitionEntity>());
        mb.Model.FindEntityType(typeof(PolicyDefinitionEntity)).Should().NotBeNull();
    }

    [Fact]
    public void JitElevationRequestConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new JitElevationRequestConfiguration();
        cfg.Configure(mb.Entity<JitElevationRequest>());
        mb.Model.FindEntityType(typeof(JitElevationRequest)).Should().NotBeNull();
    }

    [Fact]
    public void PermissionDelegationConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new PermissionDelegationConfiguration();
        cfg.Configure(mb.Entity<PermissionDelegation>());
        mb.Model.FindEntityType(typeof(PermissionDelegation)).Should().NotBeNull();
    }

    [Fact]
    public void SoDRuleConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new SoDRuleConfiguration();
        cfg.Configure(mb.Entity<SoDRule>());
        mb.Model.FindEntityType(typeof(SoDRule)).Should().NotBeNull();
    }

    [Fact]
    public void SoDViolationConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new SoDViolationConfiguration();
        cfg.Configure(mb.Entity<SoDViolation>());
        mb.Model.FindEntityType(typeof(SoDViolation)).Should().NotBeNull();
    }

    [Fact]
    public void AccessReviewCampaignConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new AccessReviewCampaignConfiguration();
        cfg.Configure(mb.Entity<AccessReviewCampaign>());
        mb.Model.FindEntityType(typeof(AccessReviewCampaign)).Should().NotBeNull();
    }

    [Fact]
    public void AccessReviewItemConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new AccessReviewItemConfiguration();
        cfg.Configure(mb.Entity<AccessReviewItem>());
        mb.Model.FindEntityType(typeof(AccessReviewItem)).Should().NotBeNull();
    }

    [Fact]
    public void DelegatedAdminScopeConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new DelegatedAdminScopeConfiguration();
        cfg.Configure(mb.Entity<DelegatedAdminScope>());
        mb.Model.FindEntityType(typeof(DelegatedAdminScope)).Should().NotBeNull();
    }

    [Fact]
    public void AbacPolicyConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new AbacPolicyConfiguration();
        cfg.Configure(mb.Entity<AbacPolicy>());
        mb.Model.FindEntityType(typeof(AbacPolicy)).Should().NotBeNull();
    }

    [Fact]
    public void ConditionalPolicyConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new ConditionalPolicyConfiguration();
        cfg.Configure(mb.Entity<ConditionalPolicy>());
        mb.Model.FindEntityType(typeof(ConditionalPolicy)).Should().NotBeNull();
    }

    [Fact]
    public void DataMaskingRuleConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new DataMaskingRuleConfiguration();
        cfg.Configure(mb.Entity<DataMaskingRule>());
        mb.Model.FindEntityType(typeof(DataMaskingRule)).Should().NotBeNull();
    }

    [Fact]
    public void TenantPermissionConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new TenantPermissionConfiguration();
        cfg.Configure(mb.Entity<TenantPermission>());
        mb.Model.FindEntityType(typeof(TenantPermission)).Should().NotBeNull();
    }

    [Fact]
    public void AuthorizationModelConfiguration_ConfiguresModel()
    {
        var mb = CreateModelBuilder();
        var cfg = new AuthorizationModelConfiguration();
        cfg.Configure(mb);
        mb.Should().NotBeNull();
    }

    // ── Repository Constructor Tests ────────────────────────────────────
    [Fact]
    public void AccessControlListEntryRepository_CanBeCreated()
    {
        var repo = new AccessControlListEntryRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void TenantSecurityVersionRepository_CanBeCreated()
    {
        var repo = new TenantSecurityVersionRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void PolicyDefinitionRepository_CanBeCreated()
    {
        var repo = new PolicyDefinitionRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void TenantPermissionRepository_CanBeCreated()
    {
        var repo = new TenantPermissionRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void PermissionAuditLogRepository_CanBeCreated()
    {
        var repo = new PermissionAuditLogRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void DynamicRoleRepository_CanBeCreated()
    {
        var repo = new DynamicRoleRepository(
            Mock.Of<IApplicationDbContext>(),
            Mock.Of<ICacheInvalidationService>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void DynamicRoleAssignmentRepository_CanBeCreated()
    {
        var repo = new DynamicRoleAssignmentRepository(
            Mock.Of<IApplicationDbContext>(),
            Mock.Of<ICacheInvalidationService>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void AbacPolicyRepository_CanBeCreated()
    {
        var repo = new AbacPolicyRepository(new Mock<DbContext>().Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void ConditionalPolicyRepository_CanBeCreated()
    {
        var repo = new ConditionalPolicyRepository(new Mock<DbContext>().Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void DataMaskingRuleRepository_CanBeCreated()
    {
        var repo = new DataMaskingRuleRepository(new Mock<DbContext>().Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void PolicyBundleRepository_CanBeCreated()
    {
        var repo = new PolicyBundleRepository(new Mock<DbContext>().Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void PolicyBundleDeploymentRepository_CanBeCreated()
    {
        var repo = new PolicyBundleDeploymentRepository(new Mock<DbContext>().Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void PermissionTemplateVersionRepository_CanBeCreated()
    {
        var repo = new PermissionTemplateVersionRepository(new Mock<DbContext>().Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void PermissionTemplateMigrationRepository_CanBeCreated()
    {
        var repo = new PermissionTemplateMigrationRepository(new Mock<DbContext>().Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void PolicyRegistryAuditLogRepository_CanBeCreated()
    {
        var repo = new PolicyRegistryAuditLogRepository(new Mock<DbContext>().Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void JitElevationRequestRepository_CanBeCreated()
    {
        var repo = new JitElevationRequestRepository(new Mock<DbContext>().Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void PermissionDelegationRepository_CanBeCreated()
    {
        var repo = new PermissionDelegationRepository(new Mock<DbContext>().Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void SoDRuleRepository_CanBeCreated()
    {
        var repo = new SoDRuleRepository(new Mock<DbContext>().Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void SoDViolationRepository_CanBeCreated()
    {
        var repo = new SoDViolationRepository(new Mock<DbContext>().Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void AccessReviewCampaignRepository_CanBeCreated()
    {
        var repo = new AccessReviewCampaignRepository(new Mock<DbContext>().Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void AccessReviewItemRepository_CanBeCreated()
    {
        var repo = new AccessReviewItemRepository(new Mock<DbContext>().Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void DelegatedAdminScopeRepository_CanBeCreated()
    {
        var repo = new DelegatedAdminScopeRepository(new Mock<DbContext>().Object);
        repo.Should().NotBeNull();
    }

    // ── Service Constructor Tests ───────────────────────────────────────
    [Fact]
    public void CacheMetricsService_CanBeCreated()
    {
        var svc = new CacheMetricsService();
        svc.Should().NotBeNull();
    }

    [Fact]
    public void CacheMetricsService_RecordHit_Works()
    {
        var svc = new CacheMetricsService();
        svc.RecordHit(CacheLevel.L1, "test");
        var stats = svc.GetStatistics();
        stats.L1Hits.Should().Be(1);
    }

    [Fact]
    public void CacheMetricsService_RecordMiss_Works()
    {
        var svc = new CacheMetricsService();
        svc.RecordMiss("test");
        var stats = svc.GetStatistics();
        stats.Misses.Should().Be(1);
    }

    [Fact]
    public void CacheMetricsService_RecordEviction_Works()
    {
        var svc = new CacheMetricsService();
        svc.RecordEviction(CacheLevel.L1, "test");
        var stats = svc.GetStatistics();
        stats.Should().NotBeNull();
    }

    [Fact]
    public void CacheStatistics_ComputedRates_WorkWithZeroRequests()
    {
        var stats = new CacheStatistics();
        stats.L1HitRate.Should().Be(0);
        stats.L2HitRate.Should().Be(0);
        stats.OverallHitRate.Should().Be(0);
    }

    [Fact]
    public void CacheTypeStatistics_HitRate_WorksWithZero()
    {
        var cts = new CacheTypeStatistics();
        cts.HitRate.Should().Be(0);
    }

    [Fact]
    public void InMemoryUserSecurityVersionStore_CanBeCreated()
    {
        var store = new InMemoryUserSecurityVersionStore();
        store.Should().NotBeNull();
    }

    [Fact]
    public async Task InMemoryUserSecurityVersionStore_GetAndIncrement()
    {
        var store = new InMemoryUserSecurityVersionStore();
        var userId = Guid.NewGuid();
        var v0 = await store.GetVersionAsync(userId);
        v0.Should().Be(0);

        var v1 = await store.IncrementVersionAsync(userId);
        v1.Should().Be(1);

        var v2 = await store.GetVersionAsync(userId);
        v2.Should().Be(1);
    }

    [Fact]
    public async Task InMemoryUserSecurityVersionStore_IncrementVersions_Batch()
    {
        var store = new InMemoryUserSecurityVersionStore();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        await store.IncrementVersionsAsync(ids);

        var v1 = await store.GetVersionAsync(ids[0]);
        v1.Should().Be(1);

        var v2 = await store.GetVersionAsync(ids[1]);
        v2.Should().Be(1);
    }

    [Fact]
    public void DatabaseAccessControlListService_CanBeCreated()
    {
        var svc = new DatabaseAccessControlListService(
            Mock.Of<IAccessControlListEntryRepository>(),
            Mock.Of<ITenantSecurityVersionRepository>());
        svc.Should().NotBeNull();
    }

    [Fact]
    public void DatabasePolicyDefinitionStore_CanBeCreated()
    {
        var store = new DatabasePolicyDefinitionStore(Mock.Of<IPolicyDefinitionRepository>());
        store.Should().NotBeNull();
    }

    [Fact]
    public void DatabaseTenantSecurityVersionStore_CanBeCreated()
    {
        var store = new DatabaseTenantSecurityVersionStore(Mock.Of<ITenantSecurityVersionRepository>());
        store.Should().NotBeNull();
    }

    [Fact]
    public void PolicyEvaluationLogger_CanBeCreated()
    {
        var logger = new PolicyEvaluationLogger(Mock.Of<ILogger<PolicyEvaluationLogger>>());
        logger.Should().NotBeNull();
    }

    [Fact]
    public void ConditionalPolicyEvaluator_CanBeCreated()
    {
        var evaluator = new ConditionalPolicyEvaluator(
            Mock.Of<IConditionalPolicyRepository>(),
            Mock.Of<ILogger<ConditionalPolicyEvaluator>>());
        evaluator.Should().NotBeNull();
    }

    [Fact]
    public void ResourcePermissionService_CanBeCreated()
    {
        var svc = new ResourcePermissionService(
            Mock.Of<IApplicationDbContext>(),
            Mock.Of<ILogger<ResourcePermissionService>>());
        svc.Should().NotBeNull();
    }

    [Fact]
    public void RbacPermissionResolver_CanBeCreated()
    {
        var resolver = new RbacPermissionResolver(
            Mock.Of<IDynamicRoleAssignmentRepository>(),
            Mock.Of<IRoleInheritanceEngine>(),
            Mock.Of<ILogger<RbacPermissionResolver>>());
        resolver.Should().NotBeNull();
    }

    [Fact]
    public void ResourcePermissionAuthorizationFilter_CanBeCreated()
    {
        var filter = new ResourcePermissionAuthorizationFilter(
            Mock.Of<ILogger<ResourcePermissionAuthorizationFilter>>());
        filter.Should().NotBeNull();
    }

    // ── Handler/Middleware Constructor Tests ─────────────────────────────
    [Fact]
    public void EnvironmentHandler_CanBeCreated()
    {
        var handler = new EnvironmentHandler(
            Mock.Of<IHttpContextAccessor>(),
            TimeProvider.System,
            Mock.Of<ILogger<EnvironmentHandler>>());
        handler.Should().NotBeNull();
    }

    [Fact]
    public void ActorContextMiddleware_CanBeCreated()
    {
        RequestDelegate next = _ => Task.CompletedTask;
        var mw = new ActorContextMiddleware(next, Mock.Of<ILogger<ActorContextMiddleware>>());
        mw.Should().NotBeNull();
    }

    [Fact]
    public void ContextMiddleware_CanBeCreated()
    {
        RequestDelegate next = _ => Task.CompletedTask;
        var mw = new ContextMiddleware(next);
        mw.Should().NotBeNull();
    }

    // ── Enum Coverage ───────────────────────────────────────────────────
    [Fact]
    public void PermissionLayer_HasValues()
    {
        Enum.GetValues<PermissionLayer>().Should().NotBeEmpty();
    }

    [Fact]
    public void PermissionOperationType_HasValues()
    {
        Enum.GetValues<PermissionOperationType>().Should().NotBeEmpty();
    }

    [Fact]
    public void AbacPolicyEffect_HasValues()
    {
        Enum.GetValues<AbacPolicyEffect>().Should().NotBeEmpty();
    }

    [Fact]
    public void TemplateChangeType_HasValues()
    {
        Enum.GetValues<TemplateChangeType>().Should().NotBeEmpty();
    }

    [Fact]
    public void JitRequestStatus_HasValues()
    {
        Enum.GetValues<JitRequestStatus>().Should().NotBeEmpty();
    }

    [Fact]
    public void DelegationStatus_HasValues()
    {
        Enum.GetValues<DelegationStatus>().Should().NotBeEmpty();
    }

    [Fact]
    public void AccessReviewStatus_HasValues()
    {
        Enum.GetValues<AccessReviewStatus>().Should().NotBeEmpty();
    }

    [Fact]
    public void SoDViolationAction_HasValues()
    {
        Enum.GetValues<SoDViolationAction>().Should().NotBeEmpty();
    }

    [Fact]
    public void PermissionType_HasValues()
    {
        Enum.GetValues<PermissionType>().Should().NotBeEmpty();
    }

    [Fact]
    public void DataMaskingLevel_HasValues()
    {
        Enum.GetValues<DataMaskingLevel>().Should().NotBeEmpty();
    }

    [Fact]
    public void ElevationRequestStatus_HasValues()
    {
        Enum.GetValues<ElevationRequestStatus>().Should().NotBeEmpty();
    }

    [Fact]
    public void AccessReviewType_HasValues()
    {
        Enum.GetValues<AccessReviewType>().Should().NotBeEmpty();
    }

    [Fact]
    public void AccessReviewScope_HasValues()
    {
        Enum.GetValues<AccessReviewScope>().Should().NotBeEmpty();
    }

    [Fact]
    public void AccessReviewItemStatus_HasValues()
    {
        Enum.GetValues<AccessReviewItemStatus>().Should().NotBeEmpty();
    }

    [Fact]
    public void AccessReviewDecision_HasValues()
    {
        Enum.GetValues<AccessReviewDecision>().Should().NotBeEmpty();
    }

    [Fact]
    public void SoDRuleType_HasValues()
    {
        Enum.GetValues<SoDRuleType>().Should().NotBeEmpty();
    }

    [Fact]
    public void SoDSeverity_HasValues()
    {
        Enum.GetValues<SoDSeverity>().Should().NotBeEmpty();
    }

    [Fact]
    public void SoDViolationStatus_HasValues()
    {
        Enum.GetValues<SoDViolationStatus>().Should().NotBeEmpty();
    }

    [Fact]
    public void SoDResolutionAction_HasValues()
    {
        Enum.GetValues<SoDResolutionAction>().Should().NotBeEmpty();
    }

    [Fact]
    public void DelegatedAdminScopeType_HasValues()
    {
        Enum.GetValues<DelegatedAdminScopeType>().Should().NotBeEmpty();
    }

    [Fact]
    public void PolicyConditionType_HasValues()
    {
        Enum.GetValues<PolicyConditionType>().Should().NotBeEmpty();
    }

    [Fact]
    public void CacheInvalidationType_HasValues()
    {
        var values = Enum.GetValues<CacheInvalidationType>();
        values.Should().Contain(CacheInvalidationType.Tenant);
        values.Should().Contain(CacheInvalidationType.User);
        values.Should().Contain(CacheInvalidationType.Resource);
        values.Should().Contain(CacheInvalidationType.Policy);
    }

    [Fact]
    public void InvitationStatus_HasValues()
    {
        Enum.GetValues<InvitationStatus>().Should().NotBeEmpty();
    }

    // ── Record/DTO Tests ────────────────────────────────────────────────
    [Fact]
    public void CacheInvalidationEvent_CanBeCreated()
    {
        var e = new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.Tenant,
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ResourceType = "Project",
            ResourceId = "123",
            PolicyName = "AdminPolicy"
        };
        e.TenantId.Should().NotBeEmpty();
        e.Type.Should().Be(CacheInvalidationType.Tenant);
        e.OriginInstanceId.Should().NotBeNull();
        e.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ConditionalPolicyContext_CanBeCreated()
    {
        var ctx = new ConditionalPolicyContext(
            Guid.NewGuid(), Guid.NewGuid(),
            "Project", Guid.NewGuid(), "Read",
            new List<string> { "Admin" },
            IpAddress: "192.168.1.1",
            UserAgent: "TestAgent",
            IsMfaVerified: true,
            RiskScore: 50);
        ctx.UserId.Should().NotBeEmpty();
        ctx.Action.Should().Be("Read");
        ctx.IsMfaVerified.Should().BeTrue();
    }

    [Fact]
    public void ConditionalPolicyResult_Allowed()
    {
        var result = new ConditionalPolicyResult(true);
        result.IsAllowed.Should().BeTrue();
        result.DeniedByPolicyId.Should().BeNull();
    }

    [Fact]
    public void ConditionalPolicyResult_Denied()
    {
        var result = new ConditionalPolicyResult(
            false, Guid.NewGuid(), "RestrictedPolicy", "Access denied");
        result.IsAllowed.Should().BeFalse();
        result.DenialReason.Should().Be("Access denied");
    }

    [Fact]
    public void PolicyEvaluationDetail_CanBeCreated()
    {
        var detail = new PolicyEvaluationDetail(
            Guid.NewGuid(), "TestPolicy", PolicyAction.Allow, true);
        detail.PolicyName.Should().Be("TestPolicy");
        detail.ConditionsMet.Should().BeTrue();
    }

    [Fact]
    public void TraceSummary_CanBeCreated()
    {
        var ts = new PolicyEvaluationLogger.TraceSummary();
        ts.FailedRequirementNames.Should().NotBeNull();
    }

    // ── Entity Instantiation ────────────────────────────────────────────
    [Fact]
    public void AccessControlListEntry_CanBeCreated()
    {
        var e = new AccessControlListEntry();
        e.Should().NotBeNull();
    }

    [Fact]
    public void DynamicRole_CanBeCreated()
    {
        var r = new DynamicRole();
        r.Should().NotBeNull();
    }

    [Fact]
    public void DynamicRoleAssignment_CanBeCreated()
    {
        var a = new DynamicRoleAssignment();
        a.Should().NotBeNull();
    }

    [Fact]
    public void TenantPermission_CanBeCreated()
    {
        var tp = new TenantPermission();
        tp.Should().NotBeNull();
    }

    [Fact]
    public void PolicyDefinitionEntity_CanBeCreated()
    {
        var p = new PolicyDefinitionEntity();
        p.Should().NotBeNull();
    }

    [Fact]
    public void AbacPolicy_CanBeCreated()
    {
        var p = new AbacPolicy();
        p.Should().NotBeNull();
    }

    [Fact]
    public void ConditionalPolicy_CanBeCreated()
    {
        var p = new ConditionalPolicy();
        p.Should().NotBeNull();
    }

    [Fact]
    public void DataMaskingRule_CanBeCreated()
    {
        var r = new DataMaskingRule();
        r.Should().NotBeNull();
    }

    [Fact]
    public void ResourceUserPermission_CanBeCreated()
    {
        var p = new ResourceUserPermission
        {
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ResourceType = "Project",
            ResourceId = "proj-1",
            Permissions = new[] { "Read" },
            GrantedByUserId = Guid.NewGuid()
        };
        p.Should().NotBeNull();
        p.ResourceType.Should().Be("Project");
    }

    [Fact]
    public void ResourceInvitation_CanBeCreated()
    {
        var i = new ResourceInvitation
        {
            TenantId = Guid.NewGuid(),
            Email = "test@example.com",
            ResourceType = "Project",
            ResourceId = "proj-1",
            Permissions = new[] { "Read" },
            InvitedByUserId = Guid.NewGuid()
        };
        i.Should().NotBeNull();
        i.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void JitElevationRequest_CanBeCreated()
    {
        var r = new JitElevationRequest();
        r.Should().NotBeNull();
    }

    [Fact]
    public void PermissionDelegation_CanBeCreated()
    {
        var d = new PermissionDelegation();
        d.Should().NotBeNull();
    }

    [Fact]
    public void SoDRule_CanBeCreated()
    {
        var r = new SoDRule();
        r.Should().NotBeNull();
    }

    [Fact]
    public void TenantSecurityVersion_CanBeCreated()
    {
        var v = new TenantSecurityVersion();
        v.Should().NotBeNull();
    }

    [Fact]
    public void DelegatedAdminScope_CanBeCreated()
    {
        var s = new DelegatedAdminScope();
        s.Should().NotBeNull();
    }

    [Fact]
    public void PermissionTemplate_CanBeCreated()
    {
        var t = new PermissionTemplate();
        t.Should().NotBeNull();
    }
}
