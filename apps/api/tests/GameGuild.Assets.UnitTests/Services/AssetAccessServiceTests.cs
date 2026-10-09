using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GameGuild.Features;
using GameGuild.Identity.Tenants;

namespace GameGuild.Assets.UnitTests.Services;

public class AssetAccessServiceTests
{
    private readonly Mock<IAssetReferenceRepository> _referenceRepositoryMock;
    private readonly Mock<ITransformedAssetRepository> _transformedAssetRepositoryMock;
    private readonly Mock<IAssetStorageService> _storageServiceMock;
    private readonly Mock<IAssetTokenService> _tokenServiceMock;
    private readonly Mock<ITenantMemberRepository> _tenantMemberRepositoryMock;
    private readonly Mock<IFeatureFlagEvaluationService> _featureServiceMock;
    private readonly Mock<IAssetParentAuthorizationResolver> _parentAuthorizationResolverMock;
    private readonly Mock<IAssetFolderAuthorizationService> _folderAuthorizationServiceMock;
    private readonly Mock<IAssetScopedAccessService> _scopedAccessServiceMock;
    private readonly AssetAccessOptions _options;
    private readonly AssetAccessService _service;

    public AssetAccessServiceTests()
    {
        _referenceRepositoryMock = new Mock<IAssetReferenceRepository>();
        _transformedAssetRepositoryMock = new Mock<ITransformedAssetRepository>();
        _storageServiceMock = new Mock<IAssetStorageService>();
        _tokenServiceMock = new Mock<IAssetTokenService>();
        _tenantMemberRepositoryMock = new Mock<ITenantMemberRepository>();
        _featureServiceMock = new Mock<IFeatureFlagEvaluationService>();
        _parentAuthorizationResolverMock = new Mock<IAssetParentAuthorizationResolver>();
        _folderAuthorizationServiceMock = new Mock<IAssetFolderAuthorizationService>();
        _scopedAccessServiceMock = new Mock<IAssetScopedAccessService>();
        _folderAuthorizationServiceMock
            .Setup(x => x.CanReadAsync(It.IsAny<AssetReference>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Mock<ILogger<AssetAccessService>> loggerMock = new Mock<ILogger<AssetAccessService>>();
        _options = new AssetAccessOptions
        {
            BaseUrl = "https://cdn.example.com",
            DefaultExpiryMinutes = 60,
            UsePresignedUrls = true
        };

        _service = new AssetAccessService(
            _referenceRepositoryMock.Object,
            _transformedAssetRepositoryMock.Object,
            _storageServiceMock.Object,
            _tokenServiceMock.Object,
            _tenantMemberRepositoryMock.Object,
            _featureServiceMock.Object,
            [_parentAuthorizationResolverMock.Object],
            _folderAuthorizationServiceMock.Object,
            _scopedAccessServiceMock.Object,
            Options.Create(_options),
            loggerMock.Object);
    }

    #region ValidateAccessAsync Tests

    [Fact]
    public async Task ValidateAccessAsync_ReferenceNotFound_ReturnsNotFound()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetReference?)null);

        // Act
        var result = await _service.ValidateAccessAsync(assetReferenceId, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.IsValid.Should().BeFalse();
        result.DeniedReason.Should().Be(AssetAccessDeniedReason.NotFound);
    }

    [Fact]
    public async Task ValidateAccessAsync_DeletedReference_ReturnsNotFound()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var reference = CreateAssetReference(assetReferenceId, AssetAccessPolicy.Public);
        // Set DeletedAt to mark as deleted (IsDeleted is computed from DeletedAt)
        typeof(AssetReference).GetProperty("DeletedAt")?.SetValue(reference, DateTime.UtcNow);

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        // Act
        var result = await _service.ValidateAccessAsync(assetReferenceId, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.IsValid.Should().BeFalse();
        result.DeniedReason.Should().Be(AssetAccessDeniedReason.NotFound);
    }

    [Fact]
    public async Task ValidateAccessAsync_PublicAsset_ReturnsValid()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var reference = CreateAssetReference(assetReferenceId, AssetAccessPolicy.Public);

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        // Act
        var result = await _service.ValidateAccessAsync(assetReferenceId, null, null);

        // Assert
        result.IsValid.Should().BeTrue();
        result.DeniedReason.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAccessAsync_UnlistedAsset_ReturnsValid()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var reference = CreateAssetReference(assetReferenceId, AssetAccessPolicy.Unlisted);

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        // Act
        var result = await _service.ValidateAccessAsync(assetReferenceId, null, null);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAccessAsync_AuthenticatedAsset_NoUser_ReturnsDenied()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var reference = CreateAssetReference(assetReferenceId, AssetAccessPolicy.Authenticated);

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        // Act
        var result = await _service.ValidateAccessAsync(assetReferenceId, userId: null, Guid.NewGuid());

        // Assert
        result.IsValid.Should().BeFalse();
        result.DeniedReason.Should().Be(AssetAccessDeniedReason.AuthenticationRequired);
    }

    [Fact]
    public async Task ValidateAccessAsync_AuthenticatedAsset_WithUser_ReturnsValid()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var reference = CreateAssetReference(assetReferenceId, AssetAccessPolicy.Authenticated);

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        // Act
        var result = await _service.ValidateAccessAsync(assetReferenceId, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAccessAsync_OwnerOnlyAsset_NoUser_ReturnsDenied()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var reference = CreateAssetReference(assetReferenceId, AssetAccessPolicy.OwnerOnly);

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        // Act
        var result = await _service.ValidateAccessAsync(assetReferenceId, userId: null, Guid.NewGuid());

        // Assert
        result.IsValid.Should().BeFalse();
        result.DeniedReason.Should().Be(AssetAccessDeniedReason.AuthenticationRequired);
    }

    [Fact]
    public async Task ValidateAccessAsync_OwnerOnlyAsset_NotOwner_ReturnsDenied()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var reference = CreateAssetReference(assetReferenceId, AssetAccessPolicy.OwnerOnly, ownerId);

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        // Act
        var result = await _service.ValidateAccessAsync(assetReferenceId, differentUserId, Guid.NewGuid());

        // Assert
        result.IsValid.Should().BeFalse();
        result.DeniedReason.Should().Be(AssetAccessDeniedReason.OwnershipRequired);
    }

    [Fact]
    public async Task ValidateAccessAsync_OwnerOnlyAsset_IsOwner_ReturnsValid()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var reference = CreateAssetReference(assetReferenceId, AssetAccessPolicy.OwnerOnly, ownerId);

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        // Act
        var result = await _service.ValidateAccessAsync(assetReferenceId, ownerId, Guid.NewGuid());

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAccessAsync_PrivateParentScopedAsset_ParentManagerCanReadDraftAsset()
    {
        var assetReferenceId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var collaboratorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var reference = CreateAssetReference(
            assetReferenceId,
            AssetAccessPolicy.Private,
            ownerId,
            "ProgramContent",
            parentId);

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        _parentAuthorizationResolverMock.Setup(x => x.Supports("ProgramContent")).Returns(true);
        _parentAuthorizationResolverMock
            .Setup(x => x.CanManageAsync(parentId, collaboratorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.ValidateAccessAsync(assetReferenceId, collaboratorId, tenantId);

        result.IsValid.Should().BeTrue();
        result.DeniedReason.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAccessAsync_PrivateParentScopedAsset_NonManagerCannotReadDraftAsset()
    {
        var assetReferenceId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var learnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var reference = CreateAssetReference(
            assetReferenceId,
            AssetAccessPolicy.Private,
            ownerId,
            "ProgramContent",
            parentId);

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        _parentAuthorizationResolverMock.Setup(x => x.Supports("ProgramContent")).Returns(true);
        _parentAuthorizationResolverMock
            .Setup(x => x.CanManageAsync(parentId, learnerId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ValidateAccessAsync(assetReferenceId, learnerId, tenantId);

        result.IsValid.Should().BeFalse();
        result.DeniedReason.Should().Be(AssetAccessDeniedReason.OwnershipRequired);
    }

    [Fact]
    public async Task ValidateAccessAsync_InheritedAsset_NoUser_ReturnsDenied()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var reference = CreateAssetReference(assetReferenceId, AssetAccessPolicy.Inherited);

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        // Act
        var result = await _service.ValidateAccessAsync(assetReferenceId, userId: null, Guid.NewGuid());

        // Assert
        result.IsValid.Should().BeFalse();
        result.DeniedReason.Should().Be(AssetAccessDeniedReason.AuthenticationRequired);
    }

    [Fact]
    public async Task ValidateAccessAsync_InheritedAsset_WithoutParentMetadata_ReturnsDenied()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var reference = CreateAssetReference(assetReferenceId, AssetAccessPolicy.Inherited);

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        // Act
        var result = await _service.ValidateAccessAsync(assetReferenceId, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.IsValid.Should().BeFalse();
        result.DeniedReason.Should().Be(AssetAccessDeniedReason.OwnershipRequired);
    }

    [Fact]
    public async Task ValidateAccessAsync_InheritedAsset_ParentAccessDenied_ReturnsDenied()
    {
        var assetReferenceId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var reference = CreateAssetReference(
            assetReferenceId,
            AssetAccessPolicy.Inherited,
            parentResourceType: "Project",
            parentResourceId: parentId);

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        _parentAuthorizationResolverMock.Setup(x => x.Supports("Project")).Returns(true);
        _parentAuthorizationResolverMock
            .Setup(x => x.CanReadAsync(parentId, userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ValidateAccessAsync(assetReferenceId, userId, tenantId);

        result.IsValid.Should().BeFalse();
        result.DeniedReason.Should().Be(AssetAccessDeniedReason.OwnershipRequired);
    }

    [Fact]
    public async Task ValidateAccessAsync_InheritedAsset_ParentAccessGranted_ReturnsValid()
    {
        var assetReferenceId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var reference = CreateAssetReference(
            assetReferenceId,
            AssetAccessPolicy.Inherited,
            parentResourceType: "Project",
            parentResourceId: parentId);

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        _parentAuthorizationResolverMock.Setup(x => x.Supports("Project")).Returns(true);
        _parentAuthorizationResolverMock
            .Setup(x => x.CanReadAsync(parentId, userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.ValidateAccessAsync(assetReferenceId, userId, tenantId);

        result.IsValid.Should().BeTrue();
        result.DeniedReason.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAccessAsync_InheritedAsset_FolderRestrictionDenied_ReturnsDenied()
    {
        var assetReferenceId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reference = CreateAssetReference(
            assetReferenceId,
            AssetAccessPolicy.Inherited,
            parentResourceType: "Project",
            parentResourceId: parentId);
        reference.MoveToFolder(Guid.NewGuid());

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        _parentAuthorizationResolverMock.Setup(x => x.Supports("Project")).Returns(true);
        _parentAuthorizationResolverMock
            .Setup(x => x.CanReadAsync(parentId, userId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _folderAuthorizationServiceMock
            .Setup(x => x.CanReadAsync(reference, userId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ValidateAccessAsync(assetReferenceId, userId, Guid.NewGuid());

        result.IsValid.Should().BeFalse();
        result.DeniedReason.Should().Be(AssetAccessDeniedReason.OwnershipRequired);
    }

    [Fact]
    public async Task ValidateAccessAsync_InheritedAsset_ExactTemporaryGrant_AllowsOnlySubmittedReference()
    {
        var assetReferenceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var reference = CreateAssetReference(
            assetReferenceId,
            AssetAccessPolicy.Inherited,
            parentResourceType: "Project",
            parentResourceId: Guid.NewGuid());
        _referenceRepositoryMock.Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        _parentAuthorizationResolverMock.Setup(x => x.Supports("Project")).Returns(true);
        _parentAuthorizationResolverMock.Setup(x => x.CanReadAsync(
                It.IsAny<Guid>(), userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _scopedAccessServiceMock.Setup(x => x.HasActiveGrantAsync(
                assetReferenceId, userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.ValidateAccessAsync(assetReferenceId, userId, tenantId);

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region ValidateToken Tests

    [Fact]
    public void ValidateToken_ValidToken_ReturnsTrue()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var token = "valid-token";

        _tokenServiceMock
            .Setup(x => x.ValidateToken(token, assetReferenceId, tenantId))
            .Returns(new AssetTokenPayload(
                assetReferenceId,
                1,
                DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
                AssetAccessPolicy.Public,
                string.Empty,
                tenantId));

        // Act
        var result = _service.ValidateToken(token, assetReferenceId, tenantId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateToken_InvalidToken_ReturnsFalse()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var token = "invalid-token";

        _tokenServiceMock
            .Setup(x => x.ValidateToken(token, assetReferenceId, tenantId))
            .Returns((AssetTokenPayload?)null);

        // Act
        var result = _service.ValidateToken(token, assetReferenceId, tenantId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateToken_NullTenantId_UsesEmptyGuid()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var token = "token";

        _tokenServiceMock
            .Setup(x => x.ValidateToken(token, assetReferenceId, Guid.Empty))
            .Returns(new AssetTokenPayload(
                assetReferenceId,
                1,
                DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
                AssetAccessPolicy.Public,
                string.Empty,
                Guid.Empty));

        // Act
        var result = _service.ValidateToken(token, assetReferenceId, tenantId: null);

        // Assert
        result.Should().BeTrue();
        _tokenServiceMock.Verify(x => x.ValidateToken(token, assetReferenceId, Guid.Empty), Times.Once);
    }

    #endregion

    #region GenerateDirectStorageUrlAsync Tests

    [Fact]
    public async Task GenerateDirectStorageUrlAsync_ReferenceNotFound_ReturnsNull()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        _referenceRepositoryMock
            .Setup(x => x.GetByIdWithContentAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetReference?)null);

        // Act
        var result = await _service.GenerateDirectStorageUrlAsync(assetReferenceId, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateDirectStorageUrlAsync_ContentNull_ReturnsNull()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var reference = CreateAssetReference(assetReferenceId, AssetAccessPolicy.Public);
        // Content is null

        _referenceRepositoryMock
            .Setup(x => x.GetByIdWithContentAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        // Act
        var result = await _service.GenerateDirectStorageUrlAsync(assetReferenceId, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateDirectStorageUrlAsync_AccessDenied_ReturnsNull()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var reference = CreateAssetReferenceWithContent(assetReferenceId, AssetAccessPolicy.OwnerOnly, Guid.NewGuid());
        
        _referenceRepositoryMock
            .Setup(x => x.GetByIdWithContentAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        
        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        // Act - different user than owner
        var result = await _service.GenerateDirectStorageUrlAsync(assetReferenceId, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateDirectStorageUrlAsync_Success_ReturnsUrl()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var reference = CreateAssetReferenceWithContent(assetReferenceId, AssetAccessPolicy.Public, Guid.NewGuid());
        var presignedUrl = "https://s3.example.com/bucket/key?signature=abc";

        _referenceRepositoryMock
            .Setup(x => x.GetByIdWithContentAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        
        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        _storageServiceMock
            .Setup(x => x.GeneratePresignedUrlAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(presignedUrl);

        // Act
        var result = await _service.GenerateDirectStorageUrlAsync(assetReferenceId, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().NotBeNull();
        result!.Url.Should().Be(presignedUrl);
        result.Token.Should().BeEmpty();
        result.MimeType.Should().Be("image/png");
    }

    [Fact]
    public async Task GenerateDirectStorageUrlAsync_RecordsAccess()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var reference = CreateAssetReferenceWithContent(assetReferenceId, AssetAccessPolicy.Public, Guid.NewGuid());

        _referenceRepositoryMock
            .Setup(x => x.GetByIdWithContentAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        
        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        _storageServiceMock
            .Setup(x => x.GeneratePresignedUrlAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://example.com");

        // Act
        await _service.GenerateDirectStorageUrlAsync(assetReferenceId, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        _referenceRepositoryMock.Verify(
            x => x.RecordAccessAsync(assetReferenceId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateAccessAsync_AssetFromAnotherTenant_FailsClosedWithTenantMismatch()
    {
        // SECURITY (cross-tenant): a member of the REQUEST tenant must not reach an asset
        // that belongs to a different tenant, regardless of the asset's access policy.
        var assetReferenceId = Guid.NewGuid();
        var assetTenant = Guid.NewGuid();
        var requestTenant = Guid.NewGuid();
        var reference = CreateAssetReference(assetReferenceId, AssetAccessPolicy.Public);
        reference.SetTenantId(assetTenant);

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        var result = await _service.ValidateAccessAsync(assetReferenceId, Guid.NewGuid(), requestTenant);

        result.IsValid.Should().BeFalse();
        result.DeniedReason.Should().Be(AssetAccessDeniedReason.TenantMismatch);

        // Even tenant owners of the request tenant are denied: the asset is foreign.
        _tenantMemberRepositoryMock
            .Setup(x => x.GetByUserAndTenantAsync(It.IsAny<Guid>(), requestTenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMember
            {
                TenantId = requestTenant,
                Role = TenantRole.Owner.Value,
                IsActive = true
            });

        var ownerResult = await _service.ValidateAccessAsync(assetReferenceId, Guid.NewGuid(), requestTenant);
        ownerResult.IsValid.Should().BeFalse();
        ownerResult.DeniedReason.Should().Be(AssetAccessDeniedReason.TenantMismatch);
    }

    [Fact]
    public async Task ValidateAccessAsync_SameTenantAsset_DoesNotTripTenantMismatch()
    {
        var assetReferenceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var reference = CreateAssetReference(assetReferenceId, AssetAccessPolicy.Public);
        reference.SetTenantId(tenantId);

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        var result = await _service.ValidateAccessAsync(assetReferenceId, Guid.NewGuid(), tenantId);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAccessAsync_ExplicitCrossTenantPath_OnlyWhenPermitted()
    {
        // The explicit cross-tenant path (SystemAdmin surfaces) opts in via
        // permitCrossTenant; without it the mismatch denial is fail-closed.
        var assetReferenceId = Guid.NewGuid();
        var assetTenant = Guid.NewGuid();
        var requestTenant = Guid.NewGuid();
        var reference = CreateAssetReference(assetReferenceId, AssetAccessPolicy.Public);
        reference.SetTenantId(assetTenant);

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        var permitted = await _service.ValidateAccessAsync(
            assetReferenceId, Guid.NewGuid(), requestTenant, CancellationToken.None, permitCrossTenant: true);
        permitted.IsValid.Should().BeTrue();

        var denied = await _service.ValidateAccessAsync(
            assetReferenceId, Guid.NewGuid(), requestTenant, CancellationToken.None, permitCrossTenant: false);
        denied.IsValid.Should().BeFalse();
        denied.DeniedReason.Should().Be(AssetAccessDeniedReason.TenantMismatch);
    }

    #endregion

    #region Helper Methods

    private static AssetReference CreateAssetReference(
        Guid id,
        AssetAccessPolicy policy,
        Guid? createdByUserId = null,
        string? parentResourceType = null,
        Guid? parentResourceId = null)
    {
        var reference = new AssetReference(
            Guid.NewGuid(),
            createdByUserId ?? Guid.NewGuid(),
            "Test Asset",
            policy,
            parentResourceType,
            parentResourceId);
        
        typeof(AssetReference).GetProperty("Id")?.SetValue(reference, id);
        
        return reference;
    }

    private static AssetReference CreateAssetReferenceWithContent(Guid id, AssetAccessPolicy policy, Guid createdByUserId)
    {
        var contentId = Guid.NewGuid();
        var content = new AssetContent(
            "test-bucket",
            "test/object.png",
            "abc123hash",
            "image/png",
            1024,
            100,
            100);
        typeof(AssetContent).GetProperty("Id")?.SetValue(content, contentId);

        var reference = new AssetReference(
            contentId,
            createdByUserId,
            "Test Asset",
            policy,
            null,
            null);
        typeof(AssetReference).GetProperty("Id")?.SetValue(reference, id);
        typeof(AssetReference).GetProperty("Content")?.SetValue(reference, content);
        
        return reference;
    }

    #endregion
}
