using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace GameGuild.Commerce.Products.UnitTests;

/// <summary>
/// Tests for GetProductsPagedQueryValidator
/// </summary>
public class GetProductsPagedQueryValidatorTests
{
    private readonly GetProductsPagedQueryValidator _validator = new();

    [Fact]
    public void DefaultQuery_ShouldPass()
    {
        var query = new GetProductsPagedQuery();
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(100)]
    public void Skip_ValidValues_ShouldPass(int skip)
    {
        var query = new GetProductsPagedQuery(Skip: skip);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.Skip);
    }

    [Fact]
    public void Skip_Negative_ShouldFail()
    {
        var query = new GetProductsPagedQuery(Skip: -1);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Skip);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Take_ValidValues_ShouldPass(int take)
    {
        var query = new GetProductsPagedQuery(Take: take);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.Take);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Take_InvalidValues_ShouldFail(int take)
    {
        var query = new GetProductsPagedQuery(Take: take);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Take);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("Name")]
    [InlineData("createdat")]
    [InlineData("CreatedAt")]
    [InlineData("updatedat")]
    [InlineData("UpdatedAt")]
    public void SortBy_ValidFields_ShouldPass(string sortBy)
    {
        var query = new GetProductsPagedQuery(SortBy: sortBy);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.SortBy);
    }

    [Fact]
    public void SortBy_InvalidField_ShouldFail()
    {
        var query = new GetProductsPagedQuery(SortBy: "InvalidField");
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Fact]
    public void SortBy_NullOrEmpty_SkipsValidation()
    {
        var query = new GetProductsPagedQuery(SortBy: "");
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.SortBy);
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("ASC")]
    [InlineData("desc")]
    [InlineData("DESC")]
    public void SortDirection_ValidValues_ShouldPass(string dir)
    {
        var query = new GetProductsPagedQuery(SortDirection: dir);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.SortDirection);
    }

    [Fact]
    public void SortDirection_Invalid_ShouldFail()
    {
        var query = new GetProductsPagedQuery(SortDirection: "SIDEWAYS");
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.SortDirection);
    }

    [Fact]
    public void SortDirection_Empty_SkipsValidation()
    {
        var query = new GetProductsPagedQuery(SortDirection: "");
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.SortDirection);
    }

    [Fact]
    public void SearchTerm_Within200_ShouldPass()
    {
        var query = new GetProductsPagedQuery(SearchTerm: new string('a', 200));
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.SearchTerm);
    }

    [Fact]
    public void SearchTerm_Exceeds200_ShouldFail()
    {
        var query = new GetProductsPagedQuery(SearchTerm: new string('a', 201));
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.SearchTerm);
    }

    [Fact]
    public void SearchTerm_Null_SkipsValidation()
    {
        var query = new GetProductsPagedQuery(SearchTerm: null);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.SearchTerm);
    }

    [Fact]
    public void AllFiltersSet_ValidValues_ShouldPass()
    {
        var query = new GetProductsPagedQuery(
            Type: ProductType.Program,
            CreatorId: Guid.NewGuid(),
            SearchTerm: "test",
            IsBundle: true,
            Skip: 5,
            Take: 25,
            SortBy: "name",
            SortDirection: "asc");
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }
}

/// <summary>
/// Tests for UpdatePromoCodeCommandValidator
/// </summary>
public class UpdatePromoCodeCommandValidatorTests
{
    private readonly UpdatePromoCodeCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid());
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Id_Empty_ShouldFail()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.Empty);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Name_Exceeds255_ShouldFail()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), Name: new string('n', 256));
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_Within255_ShouldPass()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), Name: new string('n', 255));
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Description_Exceeds1000_ShouldFail()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), Description: new string('d', 1001));
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_Within1000_ShouldPass()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), Description: new string('d', 1000));
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(50)]
    [InlineData(100)]
    public void DiscountPercentage_ValidRange_ShouldPass(double pct)
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), DiscountPercentage: (decimal)pct);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.DiscountPercentage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100.01)]
    public void DiscountPercentage_OutOfRange_ShouldFail(double pct)
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), DiscountPercentage: (decimal)pct);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.DiscountPercentage);
    }

    [Fact]
    public void DiscountPercentage_Null_SkipsValidation()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), DiscountPercentage: null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.DiscountPercentage);
    }

    [Fact]
    public void DiscountAmount_Positive_ShouldPass()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), DiscountAmount: 10m);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.DiscountAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void DiscountAmount_ZeroOrNegative_ShouldFail(double amount)
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), DiscountAmount: (decimal)amount);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.DiscountAmount);
    }

    [Fact]
    public void Currency_Exactly3Chars_ShouldPass()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), Currency: "USD");
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Currency);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDE")]
    public void Currency_Not3Chars_ShouldFail(string currency)
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), Currency: currency);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void MinimumOrderAmount_Zero_ShouldPass()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), MinimumOrderAmount: 0m);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.MinimumOrderAmount);
    }

    [Fact]
    public void MinimumOrderAmount_Negative_ShouldFail()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), MinimumOrderAmount: -1m);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.MinimumOrderAmount);
    }

    [Fact]
    public void MaxUses_Positive_ShouldPass()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), MaxUses: 100);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxUses);
    }

    [Fact]
    public void MaxUses_Zero_ShouldFail()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), MaxUses: 0);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.MaxUses);
    }

    [Fact]
    public void MaxUsesPerUser_Positive_ShouldPass()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), MaxUsesPerUser: 5);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxUsesPerUser);
    }

    [Fact]
    public void MaxUsesPerUser_Zero_ShouldFail()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), MaxUsesPerUser: 0);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.MaxUsesPerUser);
    }

    [Fact]
    public void ValidUntil_AfterValidFrom_ShouldPass()
    {
        var from = DateTime.UtcNow;
        var until = from.AddDays(30);
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), ValidFrom: from, ValidUntil: until);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.ValidUntil);
    }

    [Fact]
    public void ValidUntil_BeforeValidFrom_ShouldFail()
    {
        var from = DateTime.UtcNow;
        var until = from.AddDays(-1);
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), ValidFrom: from, ValidUntil: until);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.ValidUntil);
    }

    [Fact]
    public void ValidUntil_OnlyUntilSet_SkipsValidation()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), ValidUntil: DateTime.UtcNow.AddDays(30));
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.ValidUntil);
    }

    [Fact]
    public void StackingPriority_Zero_ShouldPass()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), StackingPriority: 0);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.StackingPriority);
    }

    [Fact]
    public void StackingPriority_Negative_ShouldFail()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), StackingPriority: -1);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.StackingPriority);
    }

    [Fact]
    public void AllFieldsSet_Valid_ShouldPass()
    {
        var from = DateTime.UtcNow;
        var cmd = new UpdatePromoCodeCommand(
            Id: Guid.NewGuid(),
            Name: "SUMMER50",
            Description: "Summer sale",
            DiscountPercentage: 50m,
            DiscountAmount: 10m,
            Currency: "USD",
            MinimumOrderAmount: 25m,
            MaxUses: 1000,
            MaxUsesPerUser: 3,
            ValidFrom: from,
            ValidUntil: from.AddMonths(3),
            StackingPriority: 5);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }
}

/// <summary>
/// Tests for ProductBundleValidator service
/// </summary>
public class ProductBundleValidatorTests
{
    private readonly ProductBundleValidator _validator = new();

    private static Product CreateBundle(string name = "Bundle", List<ProductBundleItem>? items = null)
    {
        var bundle = Product.Create(name, isBundle: true);
        if (items != null)
        {
            foreach (var item in items)
            {
                bundle.BundleItems.Add(item);
            }
        }
        return bundle;
    }

    private static Product CreateProduct(string name = "Product")
        => Product.Create(name, isBundle: false);

    // --- ValidateAddToBundle ---

    [Fact]
    public void ValidateAddToBundle_ValidProduct_ReturnsSuccess()
    {
        var bundle = CreateBundle();
        var product = CreateProduct();

        var result = _validator.ValidateAddToBundle(bundle, product);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void ValidateAddToBundle_NotABundle_ReturnsFail()
    {
        var notBundle = CreateProduct("Not a bundle");
        var product = CreateProduct();

        var result = _validator.ValidateAddToBundle(notBundle, product);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(BundleValidationErrorCode.NotABundle);
    }

    [Fact]
    public void ValidateAddToBundle_SameProduct_ReturnsFail()
    {
        var bundle = CreateBundle();

        var result = _validator.ValidateAddToBundle(bundle, bundle);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(BundleValidationErrorCode.CannotIncludeSelf);
    }

    [Fact]
    public void ValidateAddToBundle_AlreadyInBundle_ReturnsFail()
    {
        var product = CreateProduct();
        var bundleItem = ProductBundleItem.Create(Guid.NewGuid(), product.Id);
        var bundle = CreateBundle(items: new List<ProductBundleItem> { bundleItem });

        var result = _validator.ValidateAddToBundle(bundle, product);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(BundleValidationErrorCode.ProductAlreadyInBundle);
    }

    [Fact]
    public void ValidateAddToBundle_NestedBundle_ReturnsFail()
    {
        var bundle = CreateBundle();
        var otherBundle = CreateBundle("Inner Bundle");

        var result = _validator.ValidateAddToBundle(bundle, otherBundle);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(BundleValidationErrorCode.BundleCannotContainBundles);
    }

    [Fact]
    public void ValidateAddToBundle_MaxSizeExceeded_ReturnsFail()
    {
        var items = Enumerable.Range(0, ProductBundleValidator.MaxBundleSize)
            .Select(_ => ProductBundleItem.Create(Guid.NewGuid(), Guid.NewGuid()))
            .ToList();
        var bundle = CreateBundle(items: items);
        var product = CreateProduct();

        var result = _validator.ValidateAddToBundle(bundle, product);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(BundleValidationErrorCode.MaxBundleSizeExceeded);
    }

    // --- ValidateBundleConfiguration ---

    [Fact]
    public void ValidateBundleConfiguration_ValidProducts_ReturnsSuccess()
    {
        var bundle = CreateBundle();
        var products = new[] { CreateProduct("A"), CreateProduct("B") };

        var result = _validator.ValidateBundleConfiguration(bundle, products);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateBundleConfiguration_NotABundle_ReturnsFail()
    {
        var notBundle = CreateProduct();
        var products = new[] { CreateProduct("A") };

        var result = _validator.ValidateBundleConfiguration(notBundle, products);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(BundleValidationErrorCode.NotABundle);
    }

    [Fact]
    public void ValidateBundleConfiguration_ExceedsMaxSize_ReturnsFail()
    {
        var bundle = CreateBundle();
        var products = Enumerable.Range(0, ProductBundleValidator.MaxBundleSize + 1)
            .Select(i => CreateProduct($"P{i}"))
            .ToList();

        var result = _validator.ValidateBundleConfiguration(bundle, products);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(BundleValidationErrorCode.MaxBundleSizeExceeded);
    }

    [Fact]
    public void ValidateBundleConfiguration_ContainsSelf_ReturnsFail()
    {
        var bundle = CreateBundle();
        var products = new[] { bundle, CreateProduct("A") };

        var result = _validator.ValidateBundleConfiguration(bundle, products);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(BundleValidationErrorCode.CannotIncludeSelf);
    }

    [Fact]
    public void ValidateBundleConfiguration_ContainsNestedBundle_ReturnsFail()
    {
        var bundle = CreateBundle();
        var nestedBundle = CreateBundle("Nested");

        var result = _validator.ValidateBundleConfiguration(bundle, new[] { nestedBundle });

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(BundleValidationErrorCode.BundleCannotContainBundles);
    }

    // --- HasCircularReference ---

    [Fact]
    public void HasCircularReference_NotABundle_ReturnsFalse()
    {
        var product = CreateProduct();
        var result = _validator.HasCircularReference(product, new[] { product });
        result.Should().BeFalse();
    }

    [Fact]
    public void HasCircularReference_NoCycle_ReturnsFalse()
    {
        var bundle = CreateBundle();
        var product = CreateProduct();
        var item = ProductBundleItem.Create(bundle.Id, product.Id);
        bundle.BundleItems.Add(item);

        var result = _validator.HasCircularReference(bundle, new[] { bundle, product });
        result.Should().BeFalse();
    }

    [Fact]
    public void HasCircularReference_DirectCycle_ReturnsTrue()
    {
        // A bundle that references itself via BundleItems
        var bundle = CreateBundle();
        var selfItem = ProductBundleItem.Create(Guid.NewGuid(), bundle.Id);
        bundle.BundleItems.Add(selfItem);

        var result = _validator.HasCircularReference(bundle, new[] { bundle });
        result.Should().BeTrue();
    }

    [Fact]
    public void HasCircularReference_ProductNotInMap_ReturnsFalse()
    {
        var bundle = CreateBundle();
        var missingId = Guid.NewGuid();
        var item = ProductBundleItem.Create(bundle.Id, missingId);
        bundle.BundleItems.Add(item);

        var result = _validator.HasCircularReference(bundle, new[] { bundle });
        result.Should().BeFalse();
    }
}

/// <summary>
/// Tests for BundleValidationResult
/// </summary>
public class BundleValidationResultTests
{
    [Fact]
    public void Success_SetsIsValid()
    {
        var result = BundleValidationResult.Success();
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Failure_SetsAllFields()
    {
        var result = BundleValidationResult.Failure("msg", BundleValidationErrorCode.NotABundle);
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("msg");
        result.ErrorCode.Should().Be(BundleValidationErrorCode.NotABundle);
    }

    [Theory]
    [InlineData(BundleValidationErrorCode.NotABundle)]
    [InlineData(BundleValidationErrorCode.ProductAlreadyInBundle)]
    [InlineData(BundleValidationErrorCode.CannotIncludeSelf)]
    [InlineData(BundleValidationErrorCode.CircularReference)]
    [InlineData(BundleValidationErrorCode.MaxBundleSizeExceeded)]
    [InlineData(BundleValidationErrorCode.InvalidProductType)]
    [InlineData(BundleValidationErrorCode.ProductNotFound)]
    [InlineData(BundleValidationErrorCode.BundleCannotContainBundles)]
    public void ErrorCode_AllValues_AreValid(BundleValidationErrorCode code)
    {
        var result = BundleValidationResult.Failure("test", code);
        result.ErrorCode.Should().Be(code);
    }
}

/// <summary>
/// Tests for ProductBundleItem entity
/// </summary>
public class ProductBundleItemTests
{
    [Fact]
    public void Create_ValidInputs_ReturnsItem()
    {
        var bundleId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var item = ProductBundleItem.Create(bundleId, productId, quantity: 2, displayOrder: 3, isRequired: false);

        item.Should().NotBeNull();
        item.BundleProductId.Should().Be(bundleId);
        item.IncludedProductId.Should().Be(productId);
        item.Quantity.Should().Be(2);
        item.DisplayOrder.Should().Be(3);
        item.IsRequired.Should().BeFalse();
        item.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_DefaultValues_Correct()
    {
        var item = ProductBundleItem.Create(Guid.NewGuid(), Guid.NewGuid());
        item.Quantity.Should().Be(1);
        item.DisplayOrder.Should().Be(0);
        item.IsRequired.Should().BeTrue();
        item.BundleDiscountPercentage.Should().BeNull();
    }

    [Fact]
    public void Create_EmptyBundleProductId_Throws()
    {
        var act = () => ProductBundleItem.Create(Guid.Empty, Guid.NewGuid());
        act.Should().Throw<ArgumentException>().WithParameterName("bundleProductId");
    }

    [Fact]
    public void Create_EmptyIncludedProductId_Throws()
    {
        var act = () => ProductBundleItem.Create(Guid.NewGuid(), Guid.Empty);
        act.Should().Throw<ArgumentException>().WithParameterName("includedProductId");
    }

    [Fact]
    public void Create_SameIds_Throws()
    {
        var id = Guid.NewGuid();
        var act = () => ProductBundleItem.Create(id, id);
        act.Should().Throw<ArgumentException>().WithParameterName("includedProductId");
    }

    [Fact]
    public void Create_ZeroQuantity_Throws()
    {
        var act = () => ProductBundleItem.Create(Guid.NewGuid(), Guid.NewGuid(), quantity: 0);
        act.Should().Throw<ArgumentException>().WithParameterName("quantity");
    }

    [Fact]
    public void Create_WithTenantId_SetsIt()
    {
        var tenantId = Guid.NewGuid();
        var item = ProductBundleItem.Create(Guid.NewGuid(), Guid.NewGuid(), tenantId: tenantId);
        item.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void SetQuantity_Valid_Updates()
    {
        var item = ProductBundleItem.Create(Guid.NewGuid(), Guid.NewGuid());
        item.SetQuantity(5);
        item.Quantity.Should().Be(5);
    }

    [Fact]
    public void SetQuantity_Zero_Throws()
    {
        var item = ProductBundleItem.Create(Guid.NewGuid(), Guid.NewGuid());
        var act = () => item.SetQuantity(0);
        act.Should().Throw<ArgumentException>().WithParameterName("quantity");
    }

    [Fact]
    public void SetDisplayOrder_Updates()
    {
        var item = ProductBundleItem.Create(Guid.NewGuid(), Guid.NewGuid());
        item.SetDisplayOrder(10);
        item.DisplayOrder.Should().Be(10);
    }

    [Fact]
    public void SetRequired_Updates()
    {
        var item = ProductBundleItem.Create(Guid.NewGuid(), Guid.NewGuid());
        item.SetRequired(false);
        item.IsRequired.Should().BeFalse();
        item.SetRequired(true);
        item.IsRequired.Should().BeTrue();
    }

    [Fact]
    public void SetBundleDiscount_Valid_Updates()
    {
        var item = ProductBundleItem.Create(Guid.NewGuid(), Guid.NewGuid());
        item.SetBundleDiscount(25.5m);
        item.BundleDiscountPercentage.Should().Be(25.5m);
    }

    [Fact]
    public void SetBundleDiscount_Null_ClearsDiscount()
    {
        var item = ProductBundleItem.Create(Guid.NewGuid(), Guid.NewGuid());
        item.SetBundleDiscount(10m);
        item.SetBundleDiscount(null);
        item.BundleDiscountPercentage.Should().BeNull();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    public void SetBundleDiscount_OutOfRange_Throws(double pct)
    {
        var item = ProductBundleItem.Create(Guid.NewGuid(), Guid.NewGuid());
        var act = () => item.SetBundleDiscount((decimal)pct);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void SetBundleDiscount_BoundaryValues_ShouldSucceed(double pct)
    {
        var item = ProductBundleItem.Create(Guid.NewGuid(), Guid.NewGuid());
        item.SetBundleDiscount((decimal)pct);
        item.BundleDiscountPercentage.Should().Be((decimal)pct);
    }
}
