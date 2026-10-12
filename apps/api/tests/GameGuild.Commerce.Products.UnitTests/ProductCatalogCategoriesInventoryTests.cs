using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Products.UnitTests;

/// <summary>
///     Issue #401 unit coverage: hierarchical categories (entity, validator, repository,
///     command/query handlers), per-product inventory (entity, service, availability,
///     configure command) and bundle savings hints.
/// </summary>
public sealed class ProductCatalogCategoriesInventoryTests
{
    // ── ProductCategory entity ────────────────────────────────────────────

    [Fact]
    public void Category_Create_NormalizesSlug_And_TrimsName()
    {
        var category = ProductCategory.Create("  Courses  ", "Game Dev", attributesJson: null, tenantId: Guid.NewGuid());

        category.Name.Should().Be("Courses");
        category.Slug.Should().Be("game-dev");
        category.ParentCategoryId.Should().BeNull();
        category.GetAttributes().Should().BeEmpty();
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[1,2]")]
    public void Category_Create_RejectsMalformedAttributes(string payload)
    {
        var act = () => ProductCategory.Create("Courses", "courses", attributesJson: payload);
        act.Should().Throw<ArgumentException>().WithMessage("*JSON object*");
    }

    [Fact]
    public void Category_Create_RejectsMoreThanTwentyAttributeKeys()
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(
            Enumerable.Range(0, ProductCategory.MaxAttributeKeys + 1).ToDictionary(i => $"key{i}", i => i.ToString()));
        var act = () => ProductCategory.Create("Courses", "courses", attributesJson: payload);
        act.Should().Throw<ArgumentException>().WithMessage($"*{ProductCategory.MaxAttributeKeys}*");
    }

    [Fact]
    public void Category_GetAttributes_ParsesStoredPayload_And_ToleratesCorruption()
    {
        var category = ProductCategory.Create("Courses", "courses", attributesJson: "{\"theme\":\"dark\"}");
        category.GetAttributes()["theme"].Should().Be("dark");

        var corrupt = ProductCategory.Create("Broken", "broken");
        typeof(ProductCategory).GetProperty(nameof(ProductCategory.AttributesJson))!.SetValue(category, "{oops");
        category.GetAttributes().Should().BeEmpty();
    }

    // ── Hierarchy validator ───────────────────────────────────────────────

    [Fact]
    public void Validator_RejectsCategoryAsItsOwnParent()
    {
        var validator = new ProductCategoryValidator();
        var id = Guid.NewGuid();
        var result = validator.ValidateParent(id, id, new Dictionary<Guid, Guid?> { [id] = null });

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(CategoryValidationError.SelfParent);
    }

    [Fact]
    public void Validator_RejectsUnknownParent()
    {
        var validator = new ProductCategoryValidator();
        var result = validator.ValidateParent(Guid.NewGuid(), Guid.NewGuid(), new Dictionary<Guid, Guid?>());

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(CategoryValidationError.UnknownParent);
    }

    [Fact]
    public void Validator_RejectsCycle()
    {
        var validator = new ProductCategoryValidator();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var map = new Dictionary<Guid, Guid?> { [a] = b, [b] = a };

        var result = validator.ValidateParent(a, b, map);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(CategoryValidationError.CycleDetected);
    }

    [Fact]
    public void Validator_RejectsDepthBeyondCap_WhenSubtreeWouldGrowTooDeep()
    {
        var validator = new ProductCategoryValidator();
        var ids = Enumerable.Range(0, ProductCategory.MaxDepth + 1).Select(_ => Guid.NewGuid()).ToList();
        var map = new Dictionary<Guid, Guid?>();
        for (var i = 1; i < ids.Count; i++)
            map[ids[i]] = ids[i - 1];
        map[ids[0]] = null;

        // Moving the root under the deepest node exceeds the depth cap.
        var result = validator.ValidateParent(ids[0], ids[^1], map);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(CategoryValidationError.DepthExceeded);
    }

    [Fact]
    public void Validator_AllowsValidReparent_WithinDepthCap()
    {
        var validator = new ProductCategoryValidator();
        var root = Guid.NewGuid();
        var child = Guid.NewGuid();
        var grandChild = Guid.NewGuid();
        var map = new Dictionary<Guid, Guid?> { [root] = null, [child] = root, [grandChild] = child };

        var result = validator.ValidateParent(grandChild, root, map);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_DetectsPreExistingCycleInAncestorChain()
    {
        var validator = new ProductCategoryValidator();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var map = new Dictionary<Guid, Guid?> { [a] = b, [b] = a, [c] = null };

        var result = validator.ValidateParent(c, a, map);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(CategoryValidationError.CycleDetected);
    }

    // ── Repository (in-memory) ────────────────────────────────────────────

    [Fact]
    public async Task CategoryRepository_ResolvesDescendants_And_TenantScopes()
    {
        await using var db = CreateDbContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var root = ProductCategory.Create("Root", "root", tenantId: tenantA);
        var child = ProductCategory.Create("Child", "child", root.Id, tenantId: tenantA);
        var grandChild = ProductCategory.Create("Grand", "grand", child.Id, tenantId: tenantA);
        var otherTenant = ProductCategory.Create("Other", "other", tenantId: tenantB);
        db.Set<ProductCategory>().AddRange(root, child, grandChild, otherTenant);
        await db.SaveChangesAsync();

        var repository = new ProductCategoryRepository(db);

        var descendants = await repository.GetCategoryAndDescendantIdsAsync(root.Id, tenantA);
        descendants.Should().BeEquivalentTo(new[] { root.Id, child.Id, grandChild.Id });

        (await repository.GetCategoryAndDescendantIdsAsync(Guid.NewGuid(), tenantA)).Should().BeEmpty();
        (await repository.HasChildrenAsync(grandChild.Id)).Should().BeFalse();
        (await repository.HasChildrenAsync(root.Id)).Should().BeTrue();

        (await repository.GetBySlugAsync("ROOT", tenantA)).Should().NotBeNull();
        (await repository.GetBySlugAsync("other", tenantA)).Should().BeNull();
    }

    [Fact]
    public async Task CategoryRepository_CountsAssignedProducts()
    {
        await using var db = CreateDbContext();
        var category = ProductCategory.Create("Courses", "courses");
        db.Set<ProductCategory>().Add(category);
        db.Set<Product>().AddRange(
            MakeProduct("In category", category.Id),
            MakeProduct("Also in category", category.Id),
            Product.Create("Uncategorized"));
        await db.SaveChangesAsync();

        var repository = new ProductCategoryRepository(db);
        (await repository.CountProductsInCategoryAsync(category.Id)).Should().Be(2);
    }

    // ── Category command handlers ─────────────────────────────────────────

    [Fact]
    public async Task CreateCategory_StoresHierarchy_And_Audits()
    {
        await using var db = CreateDbContext();
        var repository = new ProductCategoryRepository(db);
        var audit = new Mock<IProductCatalogAuditLogger>();
        var tenantId = Guid.NewGuid();
        var handler = new CreateProductCategoryCommandHandler(
            repository, new ProductCategoryValidator(), audit.Object);

        var dto = await handler.Handle(
            new CreateProductCategoryCommand(
                "Courses", "courses",
                Description: "All courses",
                AttributesJson: "{\"theme\":\"dark\"}",
                TenantId: tenantId,
                ActorUserId: Guid.NewGuid()),
            CancellationToken.None);

        dto.Slug.Should().Be("courses");
        dto.Depth.Should().Be(0);
        dto.Path.Should().ContainInOrder("courses");
        dto.Attributes["theme"].Should().Be("dark");
        audit.Verify(logger => logger.LogMutationAsync(
            It.IsAny<Guid?>(), "Create", "Category", dto.Id, It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateCategory_RejectsDuplicateSlug()
    {
        await using var db = CreateDbContext();
        var repository = new ProductCategoryRepository(db);
        var tenantId = Guid.NewGuid();
        db.Set<ProductCategory>().Add(ProductCategory.Create("Courses", "courses", tenantId: tenantId));
        await db.SaveChangesAsync();

        var handler = new CreateProductCategoryCommandHandler(repository, new ProductCategoryValidator(), Mock.Of<IProductCatalogAuditLogger>());
        var act = () => handler.Handle(
            new CreateProductCategoryCommand("Courses again", "Courses", TenantId: tenantId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ProductCategoryValidationException>()
            .WithMessage("*slug*");
    }

    [Fact]
    public async Task CreateCategory_RejectsUnknownParent()
    {
        await using var db = CreateDbContext();
        var repository = new ProductCategoryRepository(db);
        var handler = new CreateProductCategoryCommandHandler(repository, new ProductCategoryValidator(), Mock.Of<IProductCatalogAuditLogger>());

        var act = () => handler.Handle(
            new CreateProductCategoryCommand("Sub", "sub", ParentCategoryId: Guid.NewGuid(), TenantId: Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<ProductCategoryValidationException>()
            .Where(ex => ex.Error == CategoryValidationError.UnknownParent);
    }

    [Fact]
    public async Task UpdateCategory_ReparentsSafely_And_BlocksCycles()
    {
        await using var db = CreateDbContext();
        var repository = new ProductCategoryRepository(db);
        var tenantId = Guid.NewGuid();
        var root = ProductCategory.Create("Root", "root", tenantId: tenantId);
        var child = ProductCategory.Create("Child", "child", root.Id, tenantId: tenantId);
        db.Set<ProductCategory>().AddRange(root, child);
        await db.SaveChangesAsync();

        var handler = new UpdateProductCategoryCommandHandler(repository, new ProductCategoryValidator(), Mock.Of<IProductCatalogAuditLogger>());

        // Moving the parent under its own child closes a cycle.
        var act = () => handler.Handle(
            new UpdateProductCategoryCommand(root.Id, ParentCategoryId: child.Id, Reparent: true),
            CancellationToken.None);
        await act.Should().ThrowAsync<ProductCategoryValidationException>()
            .Where(ex => ex.Error == CategoryValidationError.CycleDetected);

        // A legal reparent of the child to root level succeeds.
        var dto = await handler.Handle(
            new UpdateProductCategoryCommand(child.Id, ParentCategoryId: null, Reparent: true, Name: "Renamed"),
            CancellationToken.None);
        dto.ParentCategoryId.Should().BeNull();
        dto.Name.Should().Be("Renamed");
    }

    [Fact]
    public async Task UpdateCategory_EnforcesOptimisticConcurrency()
    {
        await using var db = CreateDbContext();
        var repository = new ProductCategoryRepository(db);
        var category = ProductCategory.Create("Old", "old");
        db.Set<ProductCategory>().Add(category);
        await db.SaveChangesAsync();

        var handler = new UpdateProductCategoryCommandHandler(repository, new ProductCategoryValidator(), Mock.Of<IProductCatalogAuditLogger>());
        var act = () => handler.Handle(
            new UpdateProductCategoryCommand(category.Id, Name: "New", ExpectedVersion: category.Version + 5),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task DeleteCategory_BlocksWhileChildrenOrProductsExist()
    {
        await using var db = CreateDbContext();
        var repository = new ProductCategoryRepository(db);
        var root = ProductCategory.Create("Root", "root");
        var child = ProductCategory.Create("Child", "child", root.Id);
        db.Set<ProductCategory>().AddRange(root, child);
        await db.SaveChangesAsync();

        var handler = new DeleteProductCategoryCommandHandler(repository, Mock.Of<IProductCatalogAuditLogger>());

        // Child categories block deletion.
        var actWithChildren = () => handler.Handle(new DeleteProductCategoryCommand(root.Id), CancellationToken.None);
        await actWithChildren.Should().ThrowAsync<ProductCategoryValidationException>().WithMessage("*child*");

        // Remove the child; an assigned product still blocks deletion.
        db.Set<ProductCategory>().Remove(child);
        var product = MakeProduct("Assigned", root.Id);
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync();

        var actWithProducts = () => handler.Handle(new DeleteProductCategoryCommand(root.Id), CancellationToken.None);
        await actWithProducts.Should().ThrowAsync<ProductCategoryValidationException>().WithMessage("*products*");

        // Unassigned leaf soft-deletes without throwing.
        var leaf = ProductCategory.Create("Leaf", "leaf");
        db.Set<ProductCategory>().Add(leaf);
        await db.SaveChangesAsync();

        var delete = () => handler.Handle(new DeleteProductCategoryCommand(leaf.Id), CancellationToken.None);
        await delete.Should().NotThrowAsync();
    }

    // ── Category queries + DTO mapping ────────────────────────────────────

    [Fact]
    public async Task CategoryQueries_MapDepthAndPath()
    {
        await using var db = CreateDbContext();
        var repository = new ProductCategoryRepository(db);
        var tenantId = Guid.NewGuid();
        var root = ProductCategory.Create("Root", "root", tenantId: tenantId);
        var child = ProductCategory.Create("Child", "child", root.Id, tenantId: tenantId);
        db.Set<ProductCategory>().AddRange(root, child);
        await db.SaveChangesAsync();

        var listHandler = new GetProductCategoriesQueryHandler(repository);
        var dtos = await listHandler.Handle(new GetProductCategoriesQuery(tenantId), CancellationToken.None);

        var childDto = dtos.Single(dto => dto.Id == child.Id);
        childDto.Depth.Should().Be(1);
        childDto.Path.Should().ContainInOrder("root", "child");

        var byIdHandler = new GetProductCategoryByIdQueryHandler(repository);
        var fetched = await byIdHandler.Handle(new GetProductCategoryByIdQuery(root.Id), CancellationToken.None);
        fetched.Should().NotBeNull();
        fetched!.Slug.Should().Be("root");
    }

    // ── Catalog filtering (categories + in-stock) ─────────────────────────

    [Fact]
    public async Task ProductsPaged_FilterByCategorySubtree()
    {
        await using var db = CreateDbContext();
        var categoryRepository = new ProductCategoryRepository(db);
        var productRepository = new ProductRepository(db);
        var tenantId = Guid.NewGuid();
        var root = ProductCategory.Create("Root", "root", tenantId: tenantId);
        var child = ProductCategory.Create("Child", "child", root.Id, tenantId: tenantId);
        db.Set<ProductCategory>().AddRange(root, child);
        db.Set<Product>().AddRange(
            MakeProduct("Root product", root.Id, tenantId),
            MakeProduct("Child product", child.Id, tenantId),
            MakeProduct("Unrelated", tenantId: tenantId));
        await db.SaveChangesAsync();

        var handler = new GetProductsPagedQueryHandler(productRepository, categoryRepository);
        var result = await handler.Handle(
            new GetProductsPagedQuery(CategoryId: root.Id),
            CancellationToken.None);

        result.Items.Select(dto => dto.Name).Should().BeEquivalentTo("Root product", "Child product");
        result.Items.All(dto => dto.CategoryId.HasValue).Should().BeTrue();

        // Unknown category: deterministic empty page.
        var empty = await handler.Handle(new GetProductsPagedQuery(CategoryId: Guid.NewGuid()), CancellationToken.None);
        empty.Items.Should().BeEmpty();
        empty.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task ProductsPaged_FilterByRealTimeAvailability()
    {
        await using var db = CreateDbContext();
        var categoryRepository = new ProductCategoryRepository(db);
        var productRepository = new ProductRepository(db);
        var trackedInStock = Product.Create("Tracked in stock");
        var trackedOut = Product.Create("Tracked out");
        var untracked = Product.Create("Untracked");
        trackedInStock.Inventory = ProductInventory.Create(trackedInStock.Id, stockQuantity: 5, trackInventory: true);
        trackedOut.Inventory = ProductInventory.Create(trackedOut.Id, stockQuantity: 2, trackInventory: true);
        trackedOut.Inventory.Reserve(2);
        db.Set<Product>().AddRange(trackedInStock, trackedOut, untracked);
        await db.SaveChangesAsync();

        var handler = new GetProductsPagedQueryHandler(productRepository, categoryRepository);

        var inStock = await handler.Handle(new GetProductsPagedQuery(InStockOnly: true), CancellationToken.None);
        inStock.Items.Select(dto => dto.Name).Should().BeEquivalentTo("Tracked in stock", "Untracked");

        var outOfStock = await handler.Handle(new GetProductsPagedQuery(InStockOnly: false), CancellationToken.None);
        outOfStock.Items.Select(dto => dto.Name).Should().BeEquivalentTo("Tracked out");
    }

    // ── Inventory entity ──────────────────────────────────────────────────

    [Fact]
    public void Inventory_Entity_GuardsQuantityTransitions()
    {
        var inventory = ProductInventory.Create(Guid.NewGuid(), stockQuantity: 5, trackInventory: true, lowStockThreshold: 2);
        inventory.AvailableQuantity.Should().Be(5);

        inventory.Reserve(3);
        inventory.AvailableQuantity.Should().Be(2);
        inventory.IsLowStock.Should().BeTrue();

        var overReserve = () => inventory.Reserve(3);
        overReserve.Should().Throw<InvalidOperationException>().WithMessage("*Insufficient*");

        inventory.CommitReservation(3);
        inventory.StockQuantity.Should().Be(2);
        inventory.ReservedQuantity.Should().Be(0);

        inventory.Restock(4);
        inventory.StockQuantity.Should().Be(6);

        inventory.ReleaseReservation(10);
        inventory.ReservedQuantity.Should().Be(0);

        var badConfigure = () => inventory.Configure(stockQuantity: 0, trackInventory: true, lowStockThreshold: 0);
        badConfigure.Should().NotThrow("stock above reserved");

        inventory.Reserve(2);
        var shrinkBelowReserved = () => inventory.Configure(stockQuantity: 1, trackInventory: true, lowStockThreshold: 0);
        shrinkBelowReserved.Should().Throw<InvalidOperationException>().WithMessage("*reserved*");
    }

    [Fact]
    public void Inventory_Untracked_IsAlwaysInStock_And_MethodsAreNoOps()
    {
        var inventory = ProductInventory.Create(Guid.NewGuid(), stockQuantity: 0, trackInventory: false);

        inventory.IsInStock(100).Should().BeFalse("IsInStock reports tracked availability only");
        inventory.Reserve(50);
        inventory.CommitReservation(50);
        inventory.ReleaseReservation(50);
        inventory.Restock(50);
        inventory.StockQuantity.Should().Be(50, "restock accumulates raw units");
        inventory.ReservedQuantity.Should().Be(0);
    }

    // ── Inventory service + order-flow contract ───────────────────────────

    [Fact]
    public async Task InventoryService_ReserveCommitReleaseRestore()
    {
        await using var db = CreateDbContext();
        var repository = new ProductInventoryRepository(db);
        var service = new ProductInventoryService(repository);
        var productId = Guid.NewGuid();
        db.Set<ProductInventory>().Add(ProductInventory.Create(productId, stockQuantity: 3, trackInventory: true));
        await db.SaveChangesAsync();

        (await service.TryReserveAsync(productId, 2, CancellationToken.None)).Should().BeTrue();
        (await service.TryReserveAsync(productId, 2, CancellationToken.None)).Should().BeFalse("only one unit left");

        (await service.TryCommitSaleAsync([(productId, 2)], CancellationToken.None)).Should().BeTrue();
        var afterCommit = await repository.GetByProductIdAsync(productId);
        afterCommit!.StockQuantity.Should().Be(1);
        afterCommit.ReservedQuantity.Should().Be(0);

        await service.ReleaseAsync([(productId, 1)], CancellationToken.None);
        (await repository.GetByProductIdAsync(productId)).Should().NotBeNull();

        await service.RestoreAsync([(productId, 2)], CancellationToken.None);
        (await repository.GetByProductIdAsync(productId))!.StockQuantity.Should().Be(3);

        // Untracked products: everything succeeds without rows.
        var digital = Guid.NewGuid();
        (await service.TryReserveAsync(digital, 99, CancellationToken.None)).Should().BeTrue();
        (await service.TryCommitSaleAsync([(digital, 99)], CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task InventoryService_CommitFails_WhenStockShrankBelowReservation()
    {
        await using var db = CreateDbContext();
        var repository = new ProductInventoryRepository(db);
        var service = new ProductInventoryService(repository);
        var productId = Guid.NewGuid();
        var inventory = ProductInventory.Create(productId, stockQuantity: 5, trackInventory: true);
        db.Set<ProductInventory>().Add(inventory);
        await db.SaveChangesAsync();

        inventory.Reserve(4);
        await db.SaveChangesAsync();

        // Admin corrects stock downward below the outstanding reservation.
        inventory.Configure(stockQuantity: 2, trackInventory: true, lowStockThreshold: 0);
        await db.SaveChangesAsync();

        (await service.TryCommitSaleAsync([(productId, 4)], CancellationToken.None)).Should().BeFalse();
    }

    // ── Availability query + configure command ────────────────────────────

    [Fact]
    public async Task Availability_ReportsUntrackedAsAlwaysInStock()
    {
        await using var db = CreateDbContext();
        var product = Product.Create("Digital");
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync();

        var handler = new GetProductAvailabilityQueryHandler(
            new ProductRepository(db), new ProductInventoryRepository(db));
        var availability = await handler.Handle(new GetProductAvailabilityQuery(product.Id), CancellationToken.None);

        availability.Should().NotBeNull();
        availability!.TrackInventory.Should().BeFalse();
        availability.IsInStock.Should().BeTrue();
        availability.StockQuantity.Should().BeNull();
    }

    [Fact]
    public async Task Availability_ReportsTrackedState()
    {
        await using var db = CreateDbContext();
        var product = Product.Create("Physical");
        var inventory = ProductInventory.Create(product.Id, stockQuantity: 10, trackInventory: true, lowStockThreshold: 3);
        inventory.Reserve(8);
        db.Set<Product>().Add(product);
        db.Set<ProductInventory>().Add(inventory);
        await db.SaveChangesAsync();

        var handler = new GetProductAvailabilityQueryHandler(
            new ProductRepository(db), new ProductInventoryRepository(db));
        var availability = await handler.Handle(new GetProductAvailabilityQuery(product.Id), CancellationToken.None);

        availability!.TrackInventory.Should().BeTrue();
        availability.StockQuantity.Should().Be(10);
        availability.ReservedQuantity.Should().Be(8);
        availability.AvailableQuantity.Should().Be(2);
        availability.IsInStock.Should().BeTrue();
        availability.IsLowStock.Should().BeTrue();
    }

    [Fact]
    public async Task SetInventory_CreatesThenReconfigures_And_Audits()
    {
        await using var db = CreateDbContext();
        var product = Product.Create("Widget");
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync();

        var audit = new Mock<IProductCatalogAuditLogger>();
        var handler = new SetProductInventoryCommandHandler(
            new ProductRepository(db), new ProductInventoryRepository(db), audit.Object);

        var created = await handler.Handle(
            new SetProductInventoryCommand(product.Id, StockQuantity: 7, TrackInventory: true, ActorUserId: Guid.NewGuid()),
            CancellationToken.None);
        created.TrackInventory.Should().BeTrue();
        created.StockQuantity.Should().Be(7);
        created.AvailableQuantity.Should().Be(7);

        var updated = await handler.Handle(
            new SetProductInventoryCommand(product.Id, StockQuantity: 4, TrackInventory: true, LowStockThreshold: 1),
            CancellationToken.None);
        updated.StockQuantity.Should().Be(4);

        audit.Verify(logger => logger.LogMutationAsync(
            It.IsAny<Guid?>(), "ConfigureInventory", "Inventory", It.IsAny<Guid>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        var unknown = () => handler.Handle(
            new SetProductInventoryCommand(Guid.NewGuid(), 1, true), CancellationToken.None);
        await unknown.Should().ThrowAsync<ProductNotFoundException>();
    }

    // ── Bundle savings ────────────────────────────────────────────────────

    [Fact]
    public void BundleSavings_ComputesSumOfPartsMinusBundlePrice()
    {
        var calculator = new BundleSavingsCalculator();
        var componentA = Product.Create("A");
        componentA.Pricing.Add(ProductPricing.CreateWithVersion(componentA.Id, "Standard", 60m).Pricing);
        var componentB = Product.Create("B");
        componentB.Pricing.Add(ProductPricing.CreateWithVersion(componentB.Id, "Standard", 40m).Pricing);

        var bundle = Product.Create("Bundle", isBundle: true);
        bundle.Pricing.Add(ProductPricing.CreateWithVersion(bundle.Id, "Bundle", 80m).Pricing);
        var itemA = bundle.AddToBundleTypeSafe(componentA.Id);
        itemA.SetBundleDiscount(50m); // A costs 30 in the bundle
        bundle.AddToBundleTypeSafe(componentB.Id); // B costs 40

        var hint = calculator.Calculate(bundle, new Dictionary<Guid, Product>
        {
            [componentA.Id] = componentA,
            [componentB.Id] = componentB
        });

        hint.Should().NotBeNull();
        hint!.BundlePrice.Should().Be(80m);
        hint.SumOfParts.Should().Be(70m); // 30 + 40
        hint.Savings.Should().Be(-10m); // bundle costs more: hint exposes it
        hint.SavingsPercentage.Should().Be(-14.29m);
        hint.ComponentsPriced.Should().Be(2);
    }

    [Fact]
    public void BundleSavings_ReturnsNull_WhenEitherSideLacksPricing()
    {
        var calculator = new BundleSavingsCalculator();
        var component = Product.Create("A"); // no pricing
        var bundle = Product.Create("Bundle", isBundle: true);
        bundle.Pricing.Add(ProductPricing.CreateWithVersion(bundle.Id, "Bundle", 80m).Pricing);
        bundle.AddToBundleTypeSafe(component.Id);

        calculator.Calculate(bundle, new Dictionary<Guid, Product> { [component.Id] = component })
            .Should().BeNull();
    }

    // ── Test infrastructure ───────────────────────────────────────────────

    private static Product MakeProduct(string name, Guid? categoryId = null, Guid? tenantId = null)
    {
        var product = Product.Create(name, tenantId: tenantId);
        product.CategoryId = categoryId;
        return product;
    }

    private static CatalogTestDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CatalogTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CatalogTestDbContext(options);
    }

    private sealed class CatalogTestDbContext(DbContextOptions<CatalogTestDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(entity =>
            {
                entity.Ignore(p => p.Creator);
                entity.Ignore(p => p.BundleItems);
                entity.Ignore(p => p.IncludedInBundles);
                entity.Ignore(p => p.SubscriptionPlans);
                entity.Ignore(p => p.CommissionConfig);
                entity.Ignore(p => p.Category); // CategoryId is kept as a scalar for filters
                entity.HasOne(p => p.Inventory)
                    .WithOne(i => i.Product)
                    .HasForeignKey<ProductInventory>(i => i.ProductId);
            });

            modelBuilder.Entity<ProductCategory>(entity =>
            {
                entity.Ignore(c => c.Children);
                entity.Ignore(c => c.Parent);
            });
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Transactions are not needed for these tests.");
    }
}
