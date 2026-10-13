using FluentAssertions;
using GameGuild.Commerce.Products;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Products.UnitTests.Commands;

/// <summary>
/// Unit tests for CreateProductCommandHandler
/// </summary>
public class CreateProductCommandHandlerTests
{
    private readonly Mock<IProductRepository> _mockRepository;
    private readonly CreateProductCommandHandler _handler;

    public CreateProductCommandHandlerTests()
    {
        _mockRepository = new Mock<IProductRepository>();
        _handler = new CreateProductCommandHandler(_mockRepository.Object, Mock.Of<IProductCategoryRepository>());
    }

    #region Happy Path Tests

    [Fact]
    public async Task Handle_WithValidCommand_CreatesProduct()
    {
        // Arrange
        var command = new CreateProductCommand(
            Name: "Test Product",
            Description: "Test Description",
            ShortDescription: "Short Desc",
            Type: ProductType.Program);

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Product");
        result.Description.Should().Be("Test Description");
        result.Type.Should().Be(ProductType.Program);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithCourseType_CreatesCorrectProductType()
    {
        // Arrange
        var command = new CreateProductCommand(
            Name: "Test Course",
            Type: ProductType.Course);

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ProductType.Course);
    }

    [Fact]
    public async Task Handle_WithBundle_SetsBundleItems()
    {
        // Arrange
        var bundleItemIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var command = new CreateProductCommand(
            Name: "Bundle Product",
            Type: ProductType.Bundle,
            IsBundle: true,
            BundleItems: bundleItemIds);

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsBundle.Should().BeTrue();
        result.BundleItems.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithCreatorId_SetsCreatorId()
    {
        // Arrange
        var creatorId = Guid.NewGuid();
        var command = new CreateProductCommand(
            Name: "Creator Product",
            CreatorId: creatorId);

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.CreatorId.Should().Be(creatorId);
    }

    [Fact]
    public async Task Handle_WithCommissionConfig_SetsCommissionValues()
    {
        // Arrange
        var command = new CreateProductCommand(
            Name: "Commission Product",
            ReferralCommissionPercentage: 25m,
            AffiliateCommissionPercentage: 35m,
            MaxAffiliateDiscount: 10m);

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ReferralCommissionPercentage.Should().Be(25m);
        result.AffiliateCommissionPercentage.Should().Be(35m);
        result.MaxAffiliateDiscount.Should().Be(10m);
    }

    [Fact]
    public async Task Handle_WithImageUrl_SetsImageUrl()
    {
        // Arrange
        var imageUrl = "https://example.com/image.png";
        var command = new CreateProductCommand(
            Name: "Image Product",
            ImageUrl: imageUrl);

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ImageUrl.Should().Be(imageUrl);
    }

    [Fact]
    public async Task Handle_ReturnsNewId()
    {
        // Arrange
        var command = new CreateProductCommand(Name: "Test Product");

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Id.Should().NotBeEmpty();
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        CreateProductCommand command = null!;

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WithEmptyBundleItems_DoesNotSetBundleItems()
    {
        // Arrange
        var command = new CreateProductCommand(
            Name: "Bundle Product",
            IsBundle: true,
            BundleItems: new List<Guid>());

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsBundle.Should().BeTrue();
        result.BundleItems.Should().BeEmpty();
    }

    #endregion

    #region Repository Interaction Tests

    [Fact]
    public async Task Handle_CallsAddAsyncOnce()
    {
        // Arrange
        var command = new CreateProductCommand(Name: "Test Product");

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.AddAsync(
            It.Is<Product>(p => p.Name == "Test Product"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CallsSaveChangesAfterAdd()
    {
        // Arrange
        var command = new CreateProductCommand(Name: "Test Product");
        var callOrder = new List<string>();

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("Add"))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("SaveChanges"))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        callOrder.Should().BeEquivalentTo(new[] { "Add", "SaveChanges" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        // Arrange
        var command = new CreateProductCommand(Name: "Test Product");
        var cts = new CancellationTokenSource();

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Product>(), cts.Token))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveChangesAsync(cts.Token))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, cts.Token);

        // Assert
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Product>(), cts.Token), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(cts.Token), Times.Once);
    }

    #endregion

    #region All ProductType Tests

    [Theory]
    [InlineData(ProductType.Program)]
    [InlineData(ProductType.Course)]
    [InlineData(ProductType.Bundle)]
    [InlineData(ProductType.Subscription)]
    [InlineData(ProductType.Workshop)]
    [InlineData(ProductType.Mentorship)]
    public async Task Handle_WithAllProductTypes_CreatesCorrectType(ProductType productType)
    {
        // Arrange
        var command = new CreateProductCommand(
            Name: $"Test {productType}",
            Type: productType);

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Type.Should().Be(productType);
    }

    #endregion

    #region Timestamps Tests

    [Fact]
    public async Task Handle_SetsCreatedAt()
    {
        // Arrange
        var before = DateTime.UtcNow;
        var command = new CreateProductCommand(Name: "Test Product");

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        var after = DateTime.UtcNow;

        // Assert
        result.CreatedAt.Should().BeAfter(before.AddSeconds(-1));
        result.CreatedAt.Should().BeBefore(after.AddSeconds(1));
    }

    #endregion
}

/// <summary>
/// Unit tests for CreateProductCommand record
/// </summary>
public class CreateProductCommandTests
{
    [Fact]
    public void CreateProductCommand_WithName_SetsNameCorrectly()
    {
        // Arrange & Act
        var command = new CreateProductCommand("Test Product");

        // Assert
        command.Name.Should().Be("Test Product");
    }

    [Fact]
    public void CreateProductCommand_WithDefaults_SetsExpectedDefaults()
    {
        // Arrange & Act
        var command = new CreateProductCommand("Test Product");

        // Assert
        command.Description.Should().BeNull();
        command.ShortDescription.Should().BeNull();
        command.ImageUrl.Should().BeNull();
        command.Type.Should().Be(ProductType.Program);
        command.IsBundle.Should().BeFalse();
        command.CreatorId.Should().BeNull();
        command.BundleItems.Should().BeNull();
        command.ReferralCommissionPercentage.Should().Be(30m);
        command.MaxAffiliateDiscount.Should().Be(0m);
        command.AffiliateCommissionPercentage.Should().Be(30m);
        command.TenantId.Should().BeNull();
    }

    [Fact]
    public void CreateProductCommand_WithAllParameters_SetsAllCorrectly()
    {
        // Arrange
        var bundleItems = new List<Guid> { Guid.NewGuid() };
        var creatorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        var command = new CreateProductCommand(
            Name: "Full Product",
            Description: "Full Description",
            ShortDescription: "Short",
            ImageUrl: "https://example.com/img.png",
            Type: ProductType.Course,
            IsBundle: true,
            CreatorId: creatorId,
            BundleItems: bundleItems,
            ReferralCommissionPercentage: 20m,
            MaxAffiliateDiscount: 5m,
            AffiliateCommissionPercentage: 25m,
            TenantId: tenantId);

        // Assert
        command.Name.Should().Be("Full Product");
        command.Description.Should().Be("Full Description");
        command.ShortDescription.Should().Be("Short");
        command.ImageUrl.Should().Be("https://example.com/img.png");
        command.Type.Should().Be(ProductType.Course);
        command.IsBundle.Should().BeTrue();
        command.CreatorId.Should().Be(creatorId);
        command.BundleItems.Should().BeEquivalentTo(bundleItems);
        command.ReferralCommissionPercentage.Should().Be(20m);
        command.MaxAffiliateDiscount.Should().Be(5m);
        command.AffiliateCommissionPercentage.Should().Be(25m);
        command.TenantId.Should().Be(tenantId);
    }
}
