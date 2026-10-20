using GameGuild.Assets.Commands;

namespace GameGuild.Assets.UnitTests.Commands;

public class RebindAssetParentCommandHandlerTests
{
    private readonly Mock<IAssetReferenceRepository> _referenceRepositoryMock;
    private readonly RebindAssetParentHandler _handler;

    public RebindAssetParentCommandHandlerTests()
    {
        _referenceRepositoryMock = new Mock<IAssetReferenceRepository>();
        _handler = new RebindAssetParentHandler(_referenceRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_OwnerRebindsParent_SetsFieldsAndEmitsEvent()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var parentResourceId = Guid.NewGuid();
        var command = new RebindAssetParentCommand(
            assetReferenceId,
            userId,
            tenantId,
            "page",
            parentResourceId);

        var reference = CreateAssetReference(assetReferenceId, Guid.NewGuid(), userId);
        reference.TenantId = tenantId;

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        _referenceRepositoryMock
            .Setup(x => x.IsOwnedByUserAsync(assetReferenceId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.AssetReferenceId.Should().Be(assetReferenceId);
        result.ParentResourceType.Should().Be("page");
        result.ParentResourceId.Should().Be(parentResourceId);
        reference.ParentResourceType.Should().Be("page");
        reference.ParentResourceId.Should().Be(parentResourceId);
        reference.IntegrationEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AssetReferenceReboundEvent>();
        _referenceRepositoryMock.Verify(
            x => x.UpdateAsync(reference, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CrossTenantRebind_ReturnsNull()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new RebindAssetParentCommand(
            assetReferenceId,
            userId,
            Guid.NewGuid(), // different tenant than the reference
            "page",
            Guid.NewGuid());

        var reference = CreateAssetReference(assetReferenceId, Guid.NewGuid(), userId);
        reference.TenantId = Guid.NewGuid();

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        _referenceRepositoryMock
            .Setup(x => x.IsOwnedByUserAsync(assetReferenceId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _referenceRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<AssetReference>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DetachWithNulls_ClearsParentForOwner()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var command = new RebindAssetParentCommand(
            assetReferenceId,
            userId,
            tenantId,
            null,
            null);

        var reference = CreateAssetReference(assetReferenceId, Guid.NewGuid(), userId, "page", Guid.NewGuid());
        reference.TenantId = tenantId;

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        _referenceRepositoryMock
            .Setup(x => x.IsOwnedByUserAsync(assetReferenceId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ParentResourceType.Should().BeNull();
        result.ParentResourceId.Should().BeNull();
        reference.ParentResourceType.Should().BeNull();
        reference.ParentResourceId.Should().BeNull();
        reference.IntegrationEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AssetReferenceReboundEvent>();
    }

    [Fact]
    public async Task Handle_NotOwner_ReturnsNull()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var command = new RebindAssetParentCommand(
            assetReferenceId,
            userId,
            tenantId,
            "page",
            Guid.NewGuid());

        var reference = CreateAssetReference(assetReferenceId, Guid.NewGuid(), Guid.NewGuid());
        reference.TenantId = tenantId;

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        _referenceRepositoryMock
            .Setup(x => x.IsOwnedByUserAsync(assetReferenceId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _referenceRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<AssetReference>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ReferenceNotFound_ReturnsNull()
    {
        // Arrange
        var command = new RebindAssetParentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "page",
            Guid.NewGuid());

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(command.AssetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetReference?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_TrimsAndNormalizesParentResourceType()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var command = new RebindAssetParentCommand(
            assetReferenceId,
            userId,
            tenantId,
            "  page  ",
            null);

        var reference = CreateAssetReference(assetReferenceId, Guid.NewGuid(), userId);
        reference.TenantId = tenantId;

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        _referenceRepositoryMock
            .Setup(x => x.IsOwnedByUserAsync(assetReferenceId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ParentResourceType.Should().Be("page");
    }

    [Fact]
    public async Task Handle_EmptyParentResourceType_NormalizesToDetach()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var command = new RebindAssetParentCommand(
            assetReferenceId,
            userId,
            tenantId,
            "   ",
            Guid.NewGuid());

        var reference = CreateAssetReference(assetReferenceId, Guid.NewGuid(), userId, "page", Guid.NewGuid());
        reference.TenantId = tenantId;

        _referenceRepositoryMock
            .Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        _referenceRepositoryMock
            .Setup(x => x.IsOwnedByUserAsync(assetReferenceId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ParentResourceType.Should().BeNull();
        result.ParentResourceId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_RecordsReboundEventWithTheActiveUseCaseCorrelation()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var reference = CreateAssetReference(assetReferenceId, Guid.NewGuid(), userId);
        reference.TenantId = tenantId;
        _referenceRepositoryMock.Setup(x => x.GetByIdAsync(assetReferenceId, It.IsAny<CancellationToken>())).ReturnsAsync(reference);
        _referenceRepositoryMock.Setup(x => x.IsOwnedByUserAsync(assetReferenceId, userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var operationAccessor = new UseCaseOperationContextAccessor();
        var correlationId = Guid.NewGuid();
        using var operation = operationAccessor.Begin(new UseCaseOperationContext(
            "assets.rebind-asset-parent", nameof(RebindAssetParentCommand), tenantId, userId, correlationId, null));
        var handler = new RebindAssetParentHandler(
            _referenceRepositoryMock.Object,
            operationContextAccessor: operationAccessor);

        // Act
        var result = await handler.Handle(new RebindAssetParentCommand(
            assetReferenceId, userId, tenantId, "page", Guid.NewGuid()));

        // Assert
        result.Should().NotBeNull();
        reference.IntegrationEvents.Should().ContainSingle().Which.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void Validator_RejectsOverlongParentResourceType()
    {
        // Arrange
        var validator = new RebindAssetParentValidator();
        var command = new RebindAssetParentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new string('x', 101),
            Guid.NewGuid());

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ParentResourceType");
    }

    [Fact]
    public void Validator_AllowsNullParentResourceTypeAndDetach()
    {
        // Arrange
        var validator = new RebindAssetParentValidator();
        var command = new RebindAssetParentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    private static AssetReference CreateAssetReference(
        Guid id,
        Guid contentId,
        Guid createdByUserId,
        string? parentResourceType = null,
        Guid? parentResourceId = null)
    {
        var reference = new AssetReference(
            contentId,
            createdByUserId,
            "Test Asset",
            AssetAccessPolicy.Private,
            parentResourceType,
            parentResourceId);

        // Set Id using reflection since it's likely from EntityBase
        typeof(AssetReference).GetProperty("Id")?.SetValue(reference, id);

        return reference;
    }
}
