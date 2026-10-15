using FluentAssertions;
using GameGuild.CQRS.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
///     Tests for the content-type grant layer of the canonical effective-permission
///     resolver (issues #360/#365): qualified-only name minting, tenant → content-type →
///     resource inheritance, DENY-WINS, tenant/user/content-type isolation, inactive and
///     expired rows, tenant defaults, and the fail-closed null-store behavior.
/// </summary>
public class EffectivePermissionResolverContentTypeLayerTests
{
    private static readonly Guid SystemAccountId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly Mock<ITenantPermissionRepository> _repository = new();
    private readonly Mock<IRbacPermissionResolver> _rbacResolver = new();
    private readonly Mock<IAuthorizationRolePermissionProvider> _roleProvider = new();
    private readonly Mock<IResourcePermissionService> _resourceService = new();
    private readonly Mock<IContentTypePermissionGrantRepository> _ctGrantRepository = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private EffectivePermissionResolverService CreateSut()
    {
        var options = Options.Create(
            new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions
            {
                SystemAccountId = SystemAccountId
            });

        return new EffectivePermissionResolverService(
            _repository.Object,
            _rbacResolver.Object,
            [_roleProvider.Object],
            _resourceService.Object,
            options,
            NullLogger<EffectivePermissionResolverService>.Instance,
            contentTypePermissionGrantRepository: _ctGrantRepository.Object);
    }

    private void SetupNoData()
    {
        _repository
            .Setup(r => r.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission?)null);
        _rbacResolver
            .Setup(r => r.ResolvePermissionsAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RbacResolutionResult(new HashSet<string>(), new HashSet<string>(), []));
        _roleProvider
            .Setup(p => p.GetPermissionsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string>)[]);
        _resourceService
            .Setup(s => s.GetUserResourcesAsync(It.IsAny<TenantId>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ResourceUserPermission>());
    }

    private void SetupNoContentTypeGrants()
        => _ctGrantRepository
            .Setup(r => r.GetBySubjectAndContentTypeAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContentTypePermissionGrant?)null);

    private void SetupContentTypeRow(
        Guid? userId,
        Guid tenantId,
        string contentTypeName,
        string[] permissions,
        string[] denyPermissions,
        bool isActive = true,
        DateTime? expiresAt = null)
        => _ctGrantRepository
            .Setup(r => r.GetBySubjectAndContentTypeAsync(userId, tenantId, contentTypeName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentTypePermissionGrant
            {
                UserId = userId,
                TenantId = tenantId,
                ContentTypeName = contentTypeName,
                Permissions = permissions,
                DenyPermissions = denyPermissions,
                IsActive = isActive,
                ExpiresAt = expiresAt
            });

    // ── Qualified-only minting: content-type grants stay scoped ───────────

    [Fact]
    public async Task ResolveAsync_UserContentTypeGrant_MintsQualifiedNameOnly()
    {
        SetupNoData();
        SetupNoContentTypeGrants();
        SetupContentTypeRow(_userId, _tenantId, "Course", ["Read", "Write"], []);

        var result = await CreateSut().ResolveAsync(
            EffectivePermissionContext.ForContentType(_userId, _tenantId, "Course"));

        result.Permissions.Should().Contain("Course.Read");
        result.Permissions.Should().Contain("Course.Write");
        // Scoped minting: the unqualified (tenant-wide) name is never minted from a
        // content-type grant — it must not leak tenant-wide capability.
        result.Permissions.Should().NotContain("Read");
        result.Permissions.Should().NotContain("Write");
        result.Sources["Course.Read"].Should().Be(PermissionSource.ContentTypeGrant);
    }

    [Fact]
    public async Task ResolveAsync_TenantDefaultContentTypeRow_IsAttributedAsContentTypeDefault()
    {
        SetupNoData();
        SetupNoContentTypeGrants();
        SetupContentTypeRow(null, _tenantId, "Course", ["Read"], []);

        var result = await CreateSut().ResolveAsync(
            EffectivePermissionContext.ForContentType(_userId, _tenantId, "Course"));

        result.Permissions.Should().Contain("Course.Read");
        result.Sources["Course.Read"].Should().Be(PermissionSource.ContentTypeDefault);
    }

    [Fact]
    public async Task ResolveAsync_FixedRowOrder_TenantDefaultWinsAttribution()
    {
        SetupNoData();
        SetupNoContentTypeGrants();
        SetupContentTypeRow(null, _tenantId, "Course", ["Read"], []);
        SetupContentTypeRow(_userId, _tenantId, "Course", ["Read"], []);

        var result = await CreateSut().ResolveAsync(
            EffectivePermissionContext.ForContentType(_userId, _tenantId, "Course"));

        // The tenant default row is evaluated before the user-specific row (fixed
        // layer order), so first-writer-wins attribution goes to the tenant default.
        result.Sources["Course.Read"].Should().Be(PermissionSource.ContentTypeDefault);
        result.Permissions.Should().Contain("Course.Read");
    }

    // ── Inheritance: tenant → content-type → resource ──────────────────────

    [Fact]
    public async Task ResolveAsync_TenantLayerGrant_SatisfiesContentTypeScopedResolution()
    {
        SetupNoData();
        SetupNoContentTypeGrants();
        _repository
            .Setup(r => r.GetByUserAndTenantAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission
            {
                UserId = _userId,
                TenantId = _tenantId,
                Permissions = ["Read"],
                DenyPermissions = []
            });

        var result = await CreateSut().ResolveAsync(
            EffectivePermissionContext.ForContentType(_userId, _tenantId, "Course"));

        // Tenant-layer grants flow down: the unqualified name is present, so
        // layer-Auto checks for Course.Read pass through the tenant grant.
        result.Permissions.Should().Contain("Read");
    }

    [Fact]
    public async Task ResolveAsync_ContentTypeGrant_AppliesToResourcesOfTheSameType()
    {
        SetupNoData();
        SetupNoContentTypeGrants();
        SetupContentTypeRow(_userId, _tenantId, "Course", ["Read"], []);

        var result = await CreateSut().ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, "Course", "42"));

        // A resource of type Course is content of content type Course: the grant's
        // qualified name is minted in the resource context (ct → resource inheritance).
        result.Permissions.Should().Contain("Course.Read");
        result.Sources["Course.Read"].Should().Be(PermissionSource.ContentTypeGrant);
    }

    [Fact]
    public async Task ResolveAsync_ContentTypeGrant_DoesNotApplyToResourcesOfAnotherType()
    {
        SetupNoData();
        SetupNoContentTypeGrants();
        SetupContentTypeRow(_userId, _tenantId, "Course", ["Read"], []);

        var result = await CreateSut().ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, "Project", "42"));

        result.Permissions.Should().NotContain("Course.Read");
        result.Permissions.Should().NotContain("Project.Read");
        result.Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_ExplicitContentTypeOnResourceContext_MintsBothTypes()
    {
        SetupNoData();
        SetupNoContentTypeGrants();
        SetupContentTypeRow(_userId, _tenantId, "Asset", ["Read"], []);

        var result = await CreateSut().ResolveAsync(
            new EffectivePermissionContext
            {
                UserId = _userId,
                TenantId = _tenantId,
                ContentType = "Asset",
                ResourceType = "Asset",
                ResourceId = "7"
            });

        result.Permissions.Should().Contain("Asset.Read");
    }

    // ── Context isolation ──────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ContentTypeGrantOfAnotherUser_NeverConsulted()
    {
        SetupNoData();
        // The repository lookup is exact-match by construction: any row returned for
        // another user must never contribute. Simulated by returning null for the
        // actual user and verifying the engine did not mint anything.
        SetupNoContentTypeGrants();

        var result = await CreateSut().ResolveAsync(
            EffectivePermissionContext.ForContentType(_userId, _tenantId, "Course"));

        result.Permissions.Should().BeEmpty();
        _ctGrantRepository.Verify(
            r => r.GetBySubjectAndContentTypeAsync(_userId, _tenantId, "Course", It.IsAny<CancellationToken>()),
            Times.Once);
        _ctGrantRepository.Verify(
            r => r.GetBySubjectAndContentTypeAsync(null, _tenantId, "Course", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_TenantScopedResolution_DoesNotConsultContentTypeStore()
    {
        SetupNoData();

        var result = await CreateSut().ResolveAsync(
            EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().BeEmpty();
        _ctGrantRepository.VerifyNoOtherCalls();
    }

    // ── DENY-WINS at the content-type layer ────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ContentTypeDeny_SubtractsTheQualifiedName()
    {
        SetupNoData();
        SetupNoContentTypeGrants();
        SetupContentTypeRow(_userId, _tenantId, "Course", ["Read"], denyPermissions: ["Write"]);
        SetupContentTypeRow(null, _tenantId, "Course", ["Write"], []);

        var result = await CreateSut().ResolveAsync(
            EffectivePermissionContext.ForContentType(_userId, _tenantId, "Course"));

        // The user's deny subtracts the tenant default's allow of Course.Write
        // (DENY-WINS across the layer's rows).
        result.Permissions.Should().Contain("Course.Read");
        result.Permissions.Should().NotContain("Course.Write");
        // The deny stays scoped: the unqualified tenant name is untouched.
        result.Permissions.Should().NotContain("Write");
    }

    // ── Fail-closed: inactive/expired rows contribute nothing ──────────────

    [Fact]
    public async Task ResolveAsync_InactiveContentTypeRow_ContributesNothing()
    {
        SetupNoData();
        SetupNoContentTypeGrants();
        SetupContentTypeRow(_userId, _tenantId, "Course", ["Read"], [], isActive: false);

        var result = await CreateSut().ResolveAsync(
            EffectivePermissionContext.ForContentType(_userId, _tenantId, "Course"));

        result.Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_ExpiredContentTypeRow_ContributesNothing()
    {
        SetupNoData();
        SetupNoContentTypeGrants();
        SetupContentTypeRow(_userId, _tenantId, "Course", ["Read"], [], expiresAt: SystemClock.UtcNow.AddSeconds(-1));

        var result = await CreateSut().ResolveAsync(
            EffectivePermissionContext.ForContentType(_userId, _tenantId, "Course"));

        result.Permissions.Should().BeEmpty();
    }

    // ── Missing store: the layer degrades to absent (fail-closed) ──────────

    [Fact]
    public async Task ResolveAsync_WithoutContentTypeStore_ResolvesWithoutTheLayer()
    {
        SetupNoData();
        var options = Options.Create(
            new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions
            {
                SystemAccountId = SystemAccountId
            });
        var resolver = new EffectivePermissionResolverService(
            _repository.Object,
            _rbacResolver.Object,
            [_roleProvider.Object],
            _resourceService.Object,
            options,
            NullLogger<EffectivePermissionResolverService>.Instance);

        var result = await resolver.ResolveAsync(
            EffectivePermissionContext.ForContentType(_userId, _tenantId, "Course"));

        result.ContextValid.Should().BeTrue();
        result.Permissions.Should().BeEmpty();
    }
}
