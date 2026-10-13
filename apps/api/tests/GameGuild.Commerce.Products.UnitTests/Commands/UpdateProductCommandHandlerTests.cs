using FluentAssertions;
using GameGuild;
using GameGuild.Commerce.Products;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Products.UnitTests.Commands;

/// <summary>
/// Unit tests for UpdateProductCommandHandler
/// </summary>
public class UpdateProductCommandHandlerTests
{
    private readonly Mock<IProductRepository> _mockRepository;
    private readonly UpdateProductCommandHandler _handler;

    public UpdateProductCommandHandlerTests()
    {
        _mockRepository = new Mock<IProductRepository>();
        _handler = new UpdateProductCommandHandler(_mockRepository.Object, Mock.Of<IProductCategoryRepository>());
    }

    private static Product CreateTestProduct(Guid? id = null, string name = "Original Name")
    {
        var product = new Product
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Description = "Original Description",
            Type = ProductType.Program
        };
        typeof(EntityBase).GetProperty(nameof(EntityBase.Version))!.SetValue(product, 1);
        return product;
    }

    #region Happy Path Tests

    [Fact]
    public async Task Handle_WithValidCommand_UpdatesProduct()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var existingProduct = CreateTestProduct(productId);
        var command = new UpdateProductCommand(
            ProductId: productId,
            Name: "Updated Name",
            Description: "Updated Description");

        _mockRepository.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>(), false, false))
            .ReturnsAsync(existingProduct);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Name");
        result.Description.Should().Be("Updated Description");
    }

    [Fact]
    public async Task Handle_WithPartialUpdate_OnlyUpdatesProvidedFields()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var existingProduct = CreateTestProduct(productId);
        existingProduct.Description = "Keep This Description";
        
        var command = new UpdateProductCommand(
            ProductId: productId,
            Name: "Only Update Name");

        _mockRepository.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>(), false, false))
            .ReturnsAsync(existingProduct);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Name.Should().Be("Only Update Name");
        result.Description.Should().Be("Keep This Description");
    }

    [Fact]
    public async Task Handle_WithImageUrl_UpdatesImageUrl()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var existingProduct = CreateTestProduct(productId);
        var command = new UpdateProductCommand(
            ProductId: productId,
            ImageUrl: "https://new-image.com/img.png");

        _mockRepository.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>(), false, false))
            .ReturnsAsync(existingProduct);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ImageUrl.Should().Be("https://new-image.com/img.png");
    }

    [Fact]
    public async Task Handle_WithProductType_UpdatesType()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var existingProduct = CreateTestProduct(productId);
        var command = new UpdateProductCommand(
            ProductId: productId,
            Type: ProductType.Course);

        _mockRepository.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>(), false, false))
            .ReturnsAsync(existingProduct);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ProductType.Course);
    }

    [Fact]
    public async Task Handle_WithBundleItems_UpdatesBundleItems()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var existingProduct = CreateTestProduct(productId);
        var bundleItems = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var command = new UpdateProductCommand(
            ProductId: productId,
            IsBundle: true,
            BundleItems: bundleItems);

        _mockRepository.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>(), false, false))
            .ReturnsAsync(existingProduct);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsBundle.Should().BeTrue();
        result.BundleItems.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithCommissionValues_CallsSaveChanges()
    {
        // Arrange - commission updates go through commission config not direct product properties
        var productId = Guid.NewGuid();
        var existingProduct = CreateTestProduct(productId);
        var command = new UpdateProductCommand(
            ProductId: productId,
            ReferralCommissionPercentage: 15m,
            AffiliateCommissionPercentage: 20m,
            MaxAffiliateDiscount: 5m);

        _mockRepository.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>(), false, false))
            .ReturnsAsync(existingProduct);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - verify the handler completed successfully and saved
        result.Should().NotBeNull();
        _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        UpdateProductCommand command = null!;

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WithNonExistentProduct_ThrowsProductNotFoundException()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new UpdateProductCommand(ProductId: productId, Name: "Updated");

        _mockRepository.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>(), false, false))
            .ReturnsAsync((Product?)null);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ProductNotFoundException>();
    }

    [Fact]
    public async Task Handle_WithVersionMismatch_ThrowsConcurrencyException()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var existingProduct = CreateTestProduct(productId);
        typeof(EntityBase).GetProperty(nameof(EntityBase.Version))!.SetValue(existingProduct, 2); // Current version is 2
        
        var command = new UpdateProductCommand(
            ProductId: productId,
            Name: "Updated",
            ExpectedVersion: 1); // But client expects version 1

        _mockRepository.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>(), false, false))
            .ReturnsAsync(existingProduct);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task Handle_WithCorrectVersion_Succeeds()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var existingProduct = CreateTestProduct(productId);
        typeof(EntityBase).GetProperty(nameof(EntityBase.Version))!.SetValue(existingProduct, 5);
        
        var command = new UpdateProductCommand(
            ProductId: productId,
            Name: "Updated",
            ExpectedVersion: 5);

        _mockRepository.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>(), false, false))
            .ReturnsAsync(existingProduct);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated");
    }

    #endregion

    #region Repository Interaction Tests

    [Fact]
    public async Task Handle_CallsGetByIdAsync()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var existingProduct = CreateTestProduct(productId);
        var command = new UpdateProductCommand(ProductId: productId, Name: "Updated");

        _mockRepository.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>(), false, false))
            .ReturnsAsync(existingProduct);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>(), false, false), Times.Once);
    }

    [Fact]
    public async Task Handle_CallsSaveChangesAsync()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var existingProduct = CreateTestProduct(productId);
        var command = new UpdateProductCommand(ProductId: productId, Name: "Updated");

        _mockRepository.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>(), false, false))
            .ReturnsAsync(existingProduct);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}

/// <summary>
/// Unit tests for UpdateProductCommand record
/// </summary>
public class UpdateProductCommandTests
{
    [Fact]
    public void UpdateProductCommand_WithProductId_SetsProductIdCorrectly()
    {
        // Arrange
        var productId = Guid.NewGuid();

        // Act
        var command = new UpdateProductCommand(ProductId: productId);

        // Assert
        command.ProductId.Should().Be(productId);
    }

    [Fact]
    public void UpdateProductCommand_WithDefaults_AllOptionalFieldsAreNull()
    {
        // Arrange & Act
        var command = new UpdateProductCommand(ProductId: Guid.NewGuid());

        // Assert
        command.Name.Should().BeNull();
        command.Description.Should().BeNull();
        command.ShortDescription.Should().BeNull();
        command.ImageUrl.Should().BeNull();
        command.Type.Should().BeNull();
        command.IsBundle.Should().BeNull();
        command.BundleItems.Should().BeNull();
        command.ReferralCommissionPercentage.Should().BeNull();
        command.MaxAffiliateDiscount.Should().BeNull();
        command.AffiliateCommissionPercentage.Should().BeNull();
        command.ExpectedVersion.Should().BeNull();
    }
}
