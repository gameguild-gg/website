using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Users;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Products.UnitTests;

public class ProductsCoverageCompletionTests
{
    [Fact]
    public void Product_BundleOperations_CoverSuccessAndFailureBranches()
    {
        var product = Product.Create("Bundle", isBundle: true);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var item = product.AddToBundleTypeSafe(first, quantity: 2, displayOrder: 10);

        item.IncludedProductId.Should().Be(first);
        product.GetBundleProductIds().Should().ContainSingle().Which.Should().Be(first);
        product.RemoveFromBundle(Guid.NewGuid()).Should().BeFalse();
        product.RemoveFromBundle(first).Should().BeTrue();

#pragma warning disable CS0618
        product.SetBundleItemIds(new[] { second, first });
        product.GetBundleItemIds().Should().Equal(second, first);

        product.SetBundleItemIds(null);
        product.GetBundleItemIds().Should().BeEmpty();
#pragma warning restore CS0618

        product.AddToBundleTypeSafe(first);
        var duplicate = () => product.AddToBundleTypeSafe(first);
        duplicate.Should().Throw<InvalidOperationException>().WithMessage("*already in this bundle*");

        var nonBundle = Product.Create("Course");
        var addToNonBundle = () => nonBundle.AddToBundleTypeSafe(Guid.NewGuid());
        addToNonBundle.Should().Throw<InvalidOperationException>().WithMessage("*non-bundle*");

#pragma warning disable CS0618
        var setOnNonBundle = () => nonBundle.SetBundleItemIds(new[] { Guid.NewGuid() });
#pragma warning restore CS0618
        setOnNonBundle.Should().Throw<InvalidOperationException>().WithMessage("*non-bundle*");
    }

    [Fact]
    public void Product_DeprecatedCommissionAccessors_CoverFallbackAndConfiguredBranches()
    {
        var product = Product.Create("Course");

#pragma warning disable CS0618
        product.ReferralCommissionPercentage.Should().Be(30m);
        product.AffiliateCommissionPercentage.Should().Be(30m);
        product.MaxAffiliateDiscount.Should().Be(0m);
        product.ReferralCommissionPercentage = 10m;
        product.AffiliateCommissionPercentage = 10m;
        product.MaxAffiliateDiscount = 10m;

        product.CommissionConfig = ProductCommissionConfig.Create(product.Id, 12m, 14m, 8m);

        product.ReferralCommissionPercentage.Should().Be(12m);
        product.AffiliateCommissionPercentage.Should().Be(14m);
        product.MaxAffiliateDiscount.Should().Be(8m);
        var setReferral = () => product.ReferralCommissionPercentage = 20m;
        var setAffiliate = () => product.AffiliateCommissionPercentage = 20m;
        var setDiscount = () => product.MaxAffiliateDiscount = 20m;
#pragma warning restore CS0618

        setReferral.Should().Throw<InvalidOperationException>().WithMessage("*CommissionConfig*");
        setAffiliate.Should().Throw<InvalidOperationException>().WithMessage("*CommissionConfig*");
        setDiscount.Should().Throw<InvalidOperationException>().WithMessage("*CommissionConfig*");
    }

    [Fact]
    public void CreatorExtensions_FromIUser_CoverInterfaceOverloadAndNullGuard()
    {
        var user = new Mock<IUser>();
        var id = Guid.NewGuid();
        user.SetupGet(x => x.Id).Returns(id);
        user.SetupProperty(x => x.Name, "Interface User");
        user.SetupProperty(x => x.Email, "interface@example.com");
        user.SetupProperty(x => x.IsActive, true);

        var info = user.Object.ToCreatorInfo();

        info.Id.Should().Be(id);
        info.Name.Should().Be("Interface User");
        info.Email.Should().Be("interface@example.com");
        info.IsActive.Should().BeTrue();

        IUser? nullUser = null;
        var act = () => nullUser!.ToCreatorInfo();
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Product_GetCreatorInfo_WhenCreatorLoaded_ReturnsCreatorInfo()
    {
        var userId = Guid.NewGuid();
        var product = new Product
        {
            Creator = new User
            {
                Id = userId,
                Name = "Creator",
                Email = "creator@example.com",
                IsActive = true
            }
        };

        var info = product.GetCreatorInfo();

        info.Should().NotBeNull();
        info!.Id.Should().Be(userId);
        info.Name.Should().Be("Creator");
    }

    [Fact]
    public void ProductPricing_AdditionalBranches_CoverPartialCtorVersionsAndSaleCombinations()
    {
        var (pricing, initial) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Standard", 100m, "USD", 80m, null, null, true);
        pricing.Versions.Add(initial);

        pricing.GetVersionAt(SystemClock.UtcNow).Should().Be(initial);
        pricing.GetVersionAt(SystemClock.UtcNow.AddYears(-5)).Should().BeNull();
        pricing.GetCurrentActiveVersion().Should().Be(initial);

        var next = pricing.UpdateBasePrice(120m, "increase", Guid.NewGuid());
        initial.IsActive.Should().BeFalse();
        next.PriceVersion.Should().Be(2);

        var noActive = ProductPricing.CreateWithVersion(Guid.NewGuid(), "NoActive", 10m).Pricing;
        noActive.UpdateSalePrice(5m).PriceVersion.Should().Be(2);

        var (salePricing, saleVersion) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Sale", 100m);
        salePricing.Versions.Add(saleVersion);
        salePricing.UpdateSalePrice(90m).PriceVersion.Should().Be(2);

        var (pricePricing, priceVersion) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Prices", 100m);
        pricePricing.Versions.Add(priceVersion);
        pricePricing.UpdatePrices(120m, null).PriceVersion.Should().Be(2);

        var partial = new ProductPricing(new { Name = "Partial" });
        partial.Should().NotBeNull();
    }

    [Fact]
    public void ProductPricingVersion_Supersede_WhenInactive_Throws()
    {
        var (pricing, version) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Standard", 100m);

        version.Invoking(v => v.GetType()
                .GetMethod("Supersede", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(v, new object[] { SystemClock.UtcNow }))
            .Should().NotThrow();

        var secondSupersede = () => version.GetType()
            .GetMethod("Supersede", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(version, new object[] { SystemClock.UtcNow });

        secondSupersede.Should().Throw<System.Reflection.TargetInvocationException>()
            .WithInnerException<InvalidOperationException>();
        pricing.Should().NotBeNull();
    }

    [Fact]
    public void PromoCode_AdditionalBranches_CoverInvalidPeriodAndUnknownDiscountType()
    {
        var invalidPeriod = new PromoCode
        {
            IsActive = true,
            ValidFrom = SystemClock.UtcNow.AddDays(1),
            ValidUntil = SystemClock.UtcNow.AddDays(2)
        };

        invalidPeriod.IsCurrentlyValid().Should().BeFalse();

        var unknownType = new PromoCode
        {
            IsActive = true,
            Type = (PromoCodeType)999
        };

        unknownType.CalculateDiscount(100m).Should().Be(0m);

        new PromoCode
        {
            IsActive = true,
            ValidFrom = null,
            ValidUntil = SystemClock.UtcNow.AddDays(1)
        }.IsCurrentlyValid().Should().BeTrue();

        new PromoCode
        {
            IsActive = true,
            ValidFrom = SystemClock.UtcNow.AddDays(-1),
            ValidUntil = null
        }.IsCurrentlyValid().Should().BeTrue();

        new PromoCode
        {
            IsActive = true,
            ValidFrom = null,
            ValidUntil = SystemClock.UtcNow.AddDays(-1)
        }.IsCurrentlyValid().Should().BeFalse();

        foreach (var (from, until, expected) in DateWindowCases())
        {
            new PromoCode
            {
                IsActive = true,
                ValidFrom = from,
                ValidUntil = until
            }.IsCurrentlyValid().Should().Be(expected);
        }
    }

    [Fact]
    public void PromoStackingAndUserProduct_DateBranches_CoverRemainingShortCircuitSides()
    {
        var exclusiveSecond = new PromoStackingRule
        {
            IsActive = true,
            AllowExclusiveStacking = false,
            AllowSameTypeStacking = true
        };

        exclusiveSecond.CanStack(
            new PromoCode { Type = PromoCodeType.PercentageOff, IsExclusive = false },
            new PromoCode { Type = PromoCodeType.FixedAmountOff, IsExclusive = true }).Should().BeFalse();

        exclusiveSecond.CanStack(
            new PromoCode { Type = PromoCodeType.PercentageOff, IsExclusive = false },
            new PromoCode { Type = PromoCodeType.FixedAmountOff, IsExclusive = false }).Should().BeTrue();

        foreach (var (allowExclusive, firstExclusive, secondExclusive) in new[]
                 {
                     (false, false, false),
                     (false, true, false),
                     (false, false, true),
                     (true, true, false),
                     (true, false, true)
                 })
        {
            new PromoStackingRule
            {
                IsActive = true,
                AllowExclusiveStacking = allowExclusive,
                AllowSameTypeStacking = true
            }.CanStack(
                new PromoCode { Type = PromoCodeType.PercentageOff, IsExclusive = firstExclusive },
                new PromoCode { Type = PromoCodeType.FixedAmountOff, IsExclusive = secondExclusive });
        }

        new UserProduct
        {
            AccessStatus = ProductAccessStatus.Active,
            AccessStartDate = null,
            AccessEndDate = SystemClock.UtcNow.AddDays(1)
        }.HasActiveAccess().Should().BeTrue();

        new UserProduct
        {
            AccessStatus = ProductAccessStatus.Active,
            AccessStartDate = SystemClock.UtcNow.AddDays(-1),
            AccessEndDate = null
        }.HasActiveAccess().Should().BeTrue();

        new UserProduct
        {
            AccessStatus = ProductAccessStatus.Active,
            AccessStartDate = null,
            AccessEndDate = SystemClock.UtcNow.AddDays(-1)
        }.HasActiveAccess().Should().BeFalse();

        new UserProduct
        {
            AccessStatus = ProductAccessStatus.Active,
            AccessStartDate = SystemClock.UtcNow.AddDays(1),
            AccessEndDate = null
        }.HasActiveAccess().Should().BeFalse();

        foreach (var (from, until, expected) in DateWindowCases())
        {
            new UserProduct
            {
                AccessStatus = ProductAccessStatus.Active,
                AccessStartDate = from,
                AccessEndDate = until
            }.HasActiveAccess().Should().Be(expected);
        }
    }

    [Fact]
    public void ProductMappingExtensions_CoverFallbackAndSaleBranches()
    {
        var product = Product.Create("Mapped");
        product.ToDto().ReferralCommissionPercentage.Should().Be(30m);

        product.CommissionConfig = ProductCommissionConfig.Create(product.Id, 15m, 16m, 17m);
        var productDto = product.ToDto();
        productDto.ReferralCommissionPercentage.Should().Be(15m);
        productDto.AffiliateCommissionPercentage.Should().Be(16m);
        productDto.MaxAffiliateDiscount.Should().Be(17m);

        var promo = new PromoCode { PromoCodeUses = null! };
        promo.ToDto().UsageCount.Should().Be(0);

        var activeSale = ProductPricing.CreateWithVersion(
            Guid.NewGuid(),
            "Sale",
            100m,
            "USD",
            75m,
            null,
            SystemClock.UtcNow.AddDays(1),
            true).Pricing;
        activeSale.ToDto().CurrentPrice.Should().Be(75m);

        var expiredSale = ProductPricing.CreateWithVersion(
            Guid.NewGuid(),
            "Expired",
            100m,
            "USD",
            75m,
            SystemClock.UtcNow.AddDays(-3),
            SystemClock.UtcNow.AddDays(-1),
            true).Pricing;
        expiredSale.ToDto().CurrentPrice.Should().Be(100m);

        ProductPricing.CreateWithVersion(
            Guid.NewGuid(),
            "StartOnly",
            100m,
            "USD",
            75m,
            SystemClock.UtcNow.AddDays(-1),
            null,
            true).Pricing.ToDto().CurrentPrice.Should().Be(75m);

        ProductPricing.CreateWithVersion(Guid.NewGuid(), "NoSale", 100m)
            .Pricing.ToDto().CurrentPrice.Should().Be(100m);

        ProductPricing.CreateWithVersion(
            Guid.NewGuid(),
            "NullStartExpired",
            100m,
            "USD",
            75m,
            null,
            SystemClock.UtcNow.AddDays(-1),
            true).Pricing.ToDto().CurrentPrice.Should().Be(100m);

        foreach (var (from, until, expected) in DateWindowCases())
        {
            var pricing = ProductPricing.CreateWithVersion(
                Guid.NewGuid(),
                $"Case-{expected}-{from?.Ticks}-{until?.Ticks}",
                100m,
                "USD",
                75m,
                from,
                until,
                true).Pricing;
            pricing.ToDto().IsSaleActive.Should().Be(expected);
        }
    }

    [Fact]
    public async Task CreatePromoCodeCommandHandler_WhenCodeIsUnique_CreatesAndMapsDto()
    {
        var repository = new Mock<IPromoCodeRepository>();
        PromoCode? persisted = null;
        repository.Setup(x => x.CodeExistsAsync("save10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repository.Setup(x => x.AddAsync(It.IsAny<PromoCode>(), It.IsAny<CancellationToken>()))
            .Callback<PromoCode, CancellationToken>((promo, _) => persisted = promo)
            .Returns(Task.CompletedTask);
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new CreatePromoCodeCommandHandler(repository.Object);
        var creatorId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var result = await handler.Handle(new CreatePromoCodeCommand(
            "save10",
            "Save 10",
            "Discount",
            PromoCodeType.PercentageOff,
            DiscountPercentage: 10m,
            Currency: "USD",
            MinimumOrderAmount: 50m,
            MaxUses: 100,
            MaxUsesPerUser: 1,
            ValidFrom: SystemClock.UtcNow.AddDays(-1),
            ValidUntil: SystemClock.UtcNow.AddDays(1),
            IsActive: true,
            IsExclusive: true,
            StackingPriority: 5,
            ProductId: productId,
            CreatedBy: creatorId), CancellationToken.None);

        result.Code.Should().Be("SAVE10");
        result.Name.Should().Be("Save 10");
        result.UsageCount.Should().Be(0);
        persisted.Should().NotBeNull();
        persisted!.CreatedBy.Should().Be(creatorId);
        persisted.ProductId.Should().Be(productId);
    }

    [Fact]
    public void PricingEngineService_IsSaleActive_CoversAllDateBranches()
    {
        var service = new PricingEngineService(Mock.Of<IProductRepository>(), Mock.Of<IPromoCodeRepository>());

        service.IsSaleActive(ProductPricing.CreateWithVersion(Guid.NewGuid(), "NoSale", 100m).Pricing).Should().BeFalse();
        service.IsSaleActive(ProductPricing.CreateWithVersion(Guid.NewGuid(), "Open", 100m, salePrice: 80m).Pricing).Should().BeTrue();
        service.IsSaleActive(ProductPricing.CreateWithVersion(Guid.NewGuid(), "Future", 100m, "USD", 80m, SystemClock.UtcNow.AddDays(1), null, false).Pricing).Should().BeFalse();
        service.IsSaleActive(ProductPricing.CreateWithVersion(Guid.NewGuid(), "Expired", 100m, "USD", 80m, null, SystemClock.UtcNow.AddDays(-1), false).Pricing).Should().BeFalse();
        service.IsSaleActive(ProductPricing.CreateWithVersion(Guid.NewGuid(), "StartOnly", 100m, "USD", 80m, SystemClock.UtcNow.AddDays(-1), null, false).Pricing).Should().BeTrue();

        foreach (var (from, until, expected) in DateWindowCases())
        {
            var pricing = ProductPricing.CreateWithVersion(
                Guid.NewGuid(),
                $"Sale-{expected}-{from?.Ticks}-{until?.Ticks}",
                100m,
                "USD",
                80m,
                from,
                until,
                false).Pricing;
            service.IsSaleActive(pricing).Should().Be(expected);
        }
    }

    [Fact]
    public async Task ProductRepositories_UpdateDeleteAndSave_CoverMutationBranches()
    {
        await using var db = CreateDbContext();
        var product = Product.Create("Repo Product", ProductType.Course);
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync();
        var repository = new ProductRepository(db);

        product.Name = "Updated";
        await repository.UpdateAsync(product);
        await repository.SaveChangesAsync();
        (await repository.GetByIdAsync(product.Id)).Should().NotBeNull();

        await repository.DeleteAsync(product);
        await repository.SaveChangesAsync();
        db.Set<Product>().Should().BeEmpty();
    }

    [Fact]
    public async Task OtherRepositories_UpdateDeleteAndSave_CoverMutationBranches()
    {
        await using var db = CreateDbContext();
        var product = Product.Create("Repo Product", ProductType.Course);
        var pricing = ProductPricing.CreateWithVersion(product.Id, "Standard", 25m).Pricing;
        var promoCode = new PromoCode { Id = Guid.NewGuid(), Code = "CODE", Name = "Code", CreatedBy = Guid.NewGuid() };
        var userProduct = new UserProduct
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ProductId = product.Id,
            AcquisitionType = ProductAcquisitionType.Grant
        };
        db.Set<Product>().Add(product);
        db.Set<ProductPricing>().Add(pricing);
        db.Set<PromoCode>().Add(promoCode);
        db.Set<UserProduct>().Add(userProduct);
        await db.SaveChangesAsync();

        var pricingRepository = new ProductPricingRepository(db);
        pricing.Name = "Changed";
        await pricingRepository.UpdateAsync(pricing);
        await pricingRepository.DeleteAsync(pricing);
        await pricingRepository.SaveChangesAsync();

        var promoRepository = new PromoCodeRepository(db);
        promoCode.Name = "Changed";
        await promoRepository.UpdateAsync(promoCode);
        var usage = new PromoCodeUse
        {
            Id = Guid.NewGuid(),
            PromoCodeId = promoCode.Id,
            UserId = Guid.NewGuid(),
            DiscountApplied = 5m
        };
        await promoRepository.RecordUsageAsync(usage);
        await promoRepository.GetUsageCountAsync(promoCode.Id);
        await promoRepository.GetUserUsageCountAsync(promoCode.Id, usage.UserId);
        await promoRepository.DeleteAsync(promoCode);
        await promoRepository.SaveChangesAsync();

        var userProductRepository = new UserProductRepository(db);
        await userProductRepository.UpdateAsync(userProduct);
        await userProductRepository.DeleteAsync(userProduct);
        await userProductRepository.SaveChangesAsync();

        db.Set<ProductPricing>().Should().BeEmpty();
        db.Set<PromoCode>().Should().BeEmpty();
        db.Set<UserProduct>().Should().BeEmpty();
    }

    [Fact]
    public async Task EntitlementControllers_PrivateMappersAndFallbackUser_CoverBranches()
    {
        var service = new Mock<IEntitlementService>();
        var actor = new Mock<IActorContextAccessor>();
        actor.SetupGet(x => x.ActorContext).Returns(ActorContext.Anonymous);
        var productId = Guid.NewGuid();
        service.Setup(x => x.HasAccessAsync(Guid.Empty, productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        service.Setup(x => x.GetUserEntitlementsAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new EntitlementInfo(
                    productId,
                    "Product",
                    ProductAccessStatus.Active,
                    ProductAcquisitionType.Grant,
                    SystemClock.UtcNow,
                    null,
                    false,
                    null,
                    0m,
                    "USD")
            });
        service.Setup(x => x.GetAllActiveEntitlementsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new EntitlementInfo(
                    productId,
                    "Product",
                    ProductAccessStatus.Active,
                    ProductAcquisitionType.Subscription,
                    SystemClock.UtcNow,
                    SystemClock.UtcNow.AddDays(30),
                    true,
                    EntitlementSubscriptionStatus.Active,
                    9m,
                    "USD")
                ,
                new EntitlementInfo(
                    Guid.NewGuid(),
                    "OneTime",
                    ProductAccessStatus.Active,
                    ProductAcquisitionType.Grant,
                    null,
                    null,
                    false,
                    null,
                    0m,
                    "USD")
            });

        var entitlementController = new EntitlementsController(service.Object, actor.Object, new CommandHandlerSender(service.Object));
        var check = await entitlementController.CheckAccess(productId);
        check.Result.Should().BeOfType<OkObjectResult>();

        var listed = await entitlementController.ListEntitlements("active");
        var listedOk = listed.Result.Should().BeOfType<OkObjectResult>().Subject;
        listedOk.Value.Should().BeAssignableTo<IEnumerable<EntitlementInfoDto>>()
            .Which.Should().ContainSingle(e => e.SubscriptionStatus == EntitlementSubscriptionStatus.Active.ToString());

        var userController = new UserEntitlementsController(service.Object, actor.Object);
        var mine = await userController.GetMyEntitlements();
        var mineOk = mine.Result.Should().BeOfType<OkObjectResult>().Subject;
        mineOk.Value.Should().BeAssignableTo<IEnumerable<EntitlementInfoDto>>()
            .Which.Should().ContainSingle(e => e.ProductId == productId);

        var userId = Guid.NewGuid();
        actor.SetupGet(x => x.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = userId.ToString(),
            TenantId = null,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            IsAuthenticated = true
        });
        service.Setup(x => x.GetUserEntitlementsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new EntitlementInfo(productId, "Product", ProductAccessStatus.Active, ProductAcquisitionType.Subscription, null, null, true, EntitlementSubscriptionStatus.Active, 0m, "USD")
            });

        var authenticatedCheck = await entitlementController.CheckAccess(productId);
        authenticatedCheck.Result.Should().BeOfType<OkObjectResult>();

        var myAuthenticated = await userController.GetMyEntitlements();
        var authenticatedOk = myAuthenticated.Result.Should().BeOfType<OkObjectResult>().Subject;
        authenticatedOk.Value.Should().BeAssignableTo<IEnumerable<EntitlementInfoDto>>()
            .Which.Should().ContainSingle(e => e.ProductId == productId);
    }

    [Fact]
    public async Task PromoCodesController_GetUserId_CoversSubIdInvalidAndMissingClaims()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<ValidatePromoCodeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoCodeValidationResult(true, Code: "OK"));
        mediator.Setup(x => x.Send(It.IsAny<ApplyPromoCodesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoCodeApplicationResult(100m, 100m, 0m, [], []));
        var controller = new PromoCodesController(mediator.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        await controller.ValidatePromoCode(new ValidatePromoCodeRequest("OK", 100m));

        controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim("sub", "not-a-guid")
            }));

        await controller.ApplyPromoCodes(new ApplyPromoCodesRequest(100m, new List<string> { "OK" }));

        var validUserId = Guid.NewGuid();
        controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim("id", validUserId.ToString())
            }));

        await controller.ValidatePromoCode(new ValidatePromoCodeRequest("OK", 100m));

        mediator.Verify(x => x.Send(It.Is<ValidatePromoCodeCommand>(c => c.UserId == null), It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(x => x.Send(It.Is<ApplyPromoCodesCommand>(c => c.UserId == null), It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(x => x.Send(It.Is<ValidatePromoCodeCommand>(c => c.UserId == validUserId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProductsController_CanIncludeUnpublished_CoversAuthenticationBranches()
    {
        var mediator = new Mock<IMediator>();
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.SetupGet(x => x.ActorContext).Returns(ActorContext.Anonymous);
        var productId = Guid.NewGuid();
        mediator.Setup(x => x.Send(It.IsAny<ProductExistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var controller = new ProductsController(mediator.Object, actorAccessor.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.ProductExists(productId, includeUnpublished: true);

        result.Should().BeOfType<OkResult>();
        mediator.Verify(x => x.Send(It.Is<ProductExistsQuery>(q => !q.IncludeUnpublished), It.IsAny<CancellationToken>()), Times.Once);

        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        actorAccessor.SetupGet(x => x.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = actorId.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });
        mediator.Setup(x => x.Send(It.IsAny<GetProductByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductDto(
                productId, "Draft", null, null, null, ProductType.Program, false, false, actorId,
                null, 0m, 0m, 0m, DateTime.UtcNow, DateTime.UtcNow, null, tenantId));

        await controller.ProductExists(productId, includeUnpublished: true);
        await controller.ProductExists(productId, includeUnpublished: false);

        mediator.Verify(x => x.Send(It.Is<GetProductByIdQuery>(q => q.IncludeUnpublished), It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(x => x.Send(It.Is<ProductExistsQuery>(q => !q.IncludeUnpublished), It.IsAny<CancellationToken>()), Times.Exactly(2));

        actorAccessor.SetupGet(x => x.ActorContext).Returns(ActorContext.Anonymous);
        var controllerWithoutHttpContext = new ProductsController(mediator.Object, actorAccessor.Object);
        await controllerWithoutHttpContext.ProductExists(productId, includeUnpublished: true);
        await controllerWithoutHttpContext.ProductExists(productId, includeUnpublished: false);
        mediator.Verify(x => x.Send(It.Is<ProductExistsQuery>(q => !q.IncludeUnpublished), It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    private static IEnumerable<(DateTime? From, DateTime? Until, bool Expected)> DateWindowCases()
    {
        var now = SystemClock.UtcNow;
        var past = now.AddDays(-1);
        var future = now.AddDays(1);

        yield return (null, null, true);
        yield return (null, future, true);
        yield return (null, past, false);
        yield return (past, null, true);
        yield return (past, future, true);
        yield return (past, past, false);
        yield return (future, null, false);
        yield return (future, future, false);
        yield return (future, past, false);
    }

    private static ProductsRepositoryTestDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProductsRepositoryTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ProductsRepositoryTestDbContext(options);
    }

    private sealed class ProductsRepositoryTestDbContext(DbContextOptions<ProductsRepositoryTestDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>().Ignore(p => p.Creator);
            modelBuilder.Entity<Product>().Ignore(p => p.BundleItems);
            modelBuilder.Entity<Product>().Ignore(p => p.IncludedInBundles);
            modelBuilder.Entity<Product>().Ignore(p => p.SubscriptionPlans);
            modelBuilder.Entity<Product>().Ignore(p => p.CommissionConfig);
            modelBuilder.Entity<PromoCode>().Ignore(p => p.CreatedByUser);
            modelBuilder.Entity<PromoCodeUse>().Ignore(p => p.PromoCode);
            modelBuilder.Entity<PromoCodeUse>().Ignore(p => p.User);
            modelBuilder.Entity<ProductPricingVersion>().Ignore(v => v.ProductPricing);
            modelBuilder.Entity<ProductPricing>()
                .HasOne(p => p.Product)
                .WithMany(p => p.Pricing)
                .HasForeignKey(p => p.ProductId);
            modelBuilder.Entity<PromoCode>()
                .HasOne(p => p.Product)
                .WithMany(p => p.PromoCodes)
                .HasForeignKey(p => p.ProductId);
            modelBuilder.Entity<UserProduct>()
                .Ignore(p => p.User);
            modelBuilder.Entity<UserProduct>()
                .Ignore(p => p.GiftedByUser);
            modelBuilder.Entity<UserProduct>()
                .HasOne(p => p.Product)
                .WithMany(p => p.UserProducts)
                .HasForeignKey(p => p.ProductId);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Transactions are not needed for coverage tests.");
    }
}
