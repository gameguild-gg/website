using FluentAssertions;
using GameGuild.CQRS.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
///     Tests for the registered content-type hierarchy in the canonical resolver
///     (issue #314): ancestor grants inherit downward under the descendant's qualified
///     name, deny entries subtract inherited grants, unregistered/cyclic chains
///     contribute nothing, and the no-provider fallback preserves exact-name semantics.
/// </summary>
public class EffectivePermissionResolverContentTypeHierarchyTests
{
    private static readonly Guid SystemAccountId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly Mock<ITenantPermissionRepository> _repository = new();
    private readonly Mock<IRbacPermissionResolver> _rbacResolver = new();
    private readonly Mock<IAuthorizationRolePermissionProvider> _roleProvider = new();
    private readonly Mock<IResourcePermissionService> _resourceService = new();
    private readonly Mock<IContentTypePermissionGrantRepository> _ctGrantRepository = new();
    private readonly Mock<IContentTypeHierarchyProvider> _hierarchyProvider = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private EffectivePermissionResolverService CreateSut(bool withHierarchy = true)
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
            contentTypePermissionGrantRepository: _ctGrantRepository.Object,
            contentTypeHierarchyProvider: withHierarchy ? _hierarchyProvider.Object : null);
    }

    private void SetupNoBaseData()
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

    private void SetupContentTypeRow(
        Guid? userId,
        string contentTypeName,
        string[] permissions,
        string[]? denyPermissions = null)
        => _ctGrantRepository
            .Setup(r => r.GetBySubjectAndContentTypeAsync(userId, _tenantId, contentTypeName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentTypePermissionGrant
            {
                UserId = userId,
                TenantId = _tenantId,
                ContentTypeName = contentTypeName,
                Permissions = permissions,
                DenyPermissions = denyPermissions ?? [],
                IsActive = true
            });

    private void SetupNoRowForUnknownTypes()
        => _ctGrantRepository
            .Setup(r => r.GetBySubjectAndContentTypeAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContentTypePermissionGrant?)null);

    [Fact]
    public async Task AncestorTenantDefault_InheritsUnderDescendantQualifiedName()
    {
        SetupNoBaseData();
        SetupNoRowForUnknownTypes();
        // "Report" inherits from registered ancestor "Document"; only the ancestor has a grant.
        _hierarchyProvider
            .Setup(h => h.GetAncestorsAsync("Report", _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["Document"]);
        SetupContentTypeRow(null, "Document", ["Read"]);

        var result = await CreateSut().ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, "Report", Guid.NewGuid().ToString()),
            CancellationToken.None);

        result.Permissions.Should().Contain("Report.Read");
        result.Permissions.Should().NotContain("Document.Read");
        result.Sources["Report.Read"].Should().Be(PermissionSource.ContentTypeInheritedDefault);
    }

    [Fact]
    public async Task AncestorUserGrant_InheritsUnderDescendantQualifiedName()
    {
        SetupNoBaseData();
        SetupNoRowForUnknownTypes();
        _hierarchyProvider
            .Setup(h => h.GetAncestorsAsync("Report", _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["Base", "Document"]);
        SetupContentTypeRow(_userId, "Document", ["Write"]);

        var result = await CreateSut().ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, "Report", Guid.NewGuid().ToString()),
            CancellationToken.None);

        result.Permissions.Should().Contain("Report.Write");
        result.Sources["Report.Write"].Should().Be(PermissionSource.ContentTypeInheritedGrant);
    }

    [Fact]
    public async Task DescendantGrant_WinsAttributionOverInheritedAncestorGrant()
    {
        SetupNoBaseData();
        SetupNoRowForUnknownTypes();
        _hierarchyProvider
            .Setup(h => h.GetAncestorsAsync("Report", _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["Document"]);
        SetupContentTypeRow(null, "Document", ["Read"]);
        SetupContentTypeRow(null, "Report", ["Read"]);

        var result = await CreateSut().ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, "Report", Guid.NewGuid().ToString()),
            CancellationToken.None);

        result.Sources["Report.Read"].Should().Be(PermissionSource.ContentTypeDefault);
    }

    [Fact]
    public async Task AncestorDeny_SubtractsInheritedPermission()
    {
        SetupNoBaseData();
        SetupNoRowForUnknownTypes();
        _hierarchyProvider
            .Setup(h => h.GetAncestorsAsync("Report", _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["Document"]);
        SetupContentTypeRow(null, "Document", ["Read"], denyPermissions: ["Read"]);
        SetupContentTypeRow(null, "Report", ["Read"]);

        var result = await CreateSut().ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, "Report", Guid.NewGuid().ToString()),
            CancellationToken.None);

        result.Permissions.Should().NotContain("Report.Read");
    }

    [Fact]
    public async Task NoAncestors_PreservesExactNameSemantics()
    {
        SetupNoBaseData();
        SetupNoRowForUnknownTypes();
        _hierarchyProvider
            .Setup(h => h.GetAncestorsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateSut().ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, "Report", Guid.NewGuid().ToString()),
            CancellationToken.None);

        result.Permissions.Should().NotContain("Report.Read");
    }

    [Fact]
    public async Task NoProvider_PreservesExactNameSemantics()
    {
        SetupNoBaseData();
        SetupNoRowForUnknownTypes();
        // Note: no Setup for the hierarchy provider because it is not injected.

        var result = await CreateSut(withHierarchy: false).ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, "Report", Guid.NewGuid().ToString()),
            CancellationToken.None);

        result.Permissions.Should().NotContain("Report.Read");
        _hierarchyProvider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ThrowingProvider_IsSkipped_FailClosed()
    {
        SetupNoBaseData();
        SetupNoRowForUnknownTypes();
        _hierarchyProvider
            .Setup(h => h.GetAncestorsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("hierarchy store down"));

        var act = () => CreateSut().ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, "Report", Guid.NewGuid().ToString()),
            CancellationToken.None);

        // The resolver's own extension contract skips throwing plugins, but the built-in
        // hierarchy traversal is on the decision path: a broken hierarchy store must
        // surface (never silently widen permissions).
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
