using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using GameGuild.Commerce;
using GameGuild.Commerce.Payments;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Products module registration
/// </summary>
public static class ProductsModule
{
    /// <summary>
    /// Configure services for the Products module
    /// </summary>
    public static IServiceCollection AddProductsModule(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductCategoryRepository, ProductCategoryRepository>();
        services.AddScoped<IProductInventoryRepository, ProductInventoryRepository>();
        services.AddScoped<IProductPricingRepository, ProductPricingRepository>();
        services.AddScoped<IPromoCodeRepository, PromoCodeRepository>();
        services.AddScoped<IPromoStackingRuleRepository, PromoStackingRuleRepository>();
        services.AddScoped<IUserProductRepository, UserProductRepository>();
        services.AddScoped<IPricingRuleRepository, PricingRuleRepository>();

        // Register services
        services.AddScoped<IPricingEngineService, PricingEngineService>();
        services.AddScoped<IPromoDiscountCalculator, PromoDiscountCalculatorAdapter>();
        services.AddScoped<IProductPricingService, ProductPricingService>();
        services.AddScoped<IPromoCodeService, PromoCodeService>();
        services.AddScoped<IPromoCodeUsageRecorder, PromoCodeUsageRecorder>();
        services.AddScoped<IPromoCodeAuditLogger, PromoCodeAuditLogger>();
        services.AddScoped<IUserProductService, UserProductService>();
        services.AddScoped<IEntitlementService, EntitlementService>();
        services.AddScoped<IProductCategoryValidator, ProductCategoryValidator>();
        services.AddScoped<IProductCatalogAuditLogger, ProductCatalogAuditLogger>();
        services.AddScoped<IProductInventoryService, ProductInventoryService>();
        services.AddScoped<IBundleSavingsCalculator, BundleSavingsCalculator>();

        // Note: Order-related services are now in GameGuild.Commerce.Orders module
        // CQRS handlers are automatically registered by assembly scanning in ApplicationLayerExtensions

        return services;
    }

    /// <summary>
    /// Configure EF Core model for the Products module
    /// </summary>
    public static void ConfigureProductsModel(ModelBuilder modelBuilder)
    {
        // Configure DbSets with fluent API
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.CreatorId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(4000);
            entity.Property(e => e.ShortDescription).HasMaxLength(500);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            // Note: ReferralCommissionPercentage, MaxAffiliateDiscount, AffiliateCommissionPercentage
            // are now [NotMapped] - use ProductCommissionConfig entity instead

            entity.HasMany(e => e.Pricing)
                .WithOne(p => p.Product)
                .HasForeignKey(p => p.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.UserProducts)
                .WithOne(up => up.Product)
                .HasForeignKey(up => up.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.BundleItems)
                .WithOne(bi => bi.BundleProduct)
                .HasForeignKey(bi => bi.BundleProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.IncludedInBundles)
                .WithOne(bi => bi.IncludedProduct)
                .HasForeignKey(bi => bi.IncludedProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Catalog category assignment (issue #401): single category, restrict delete
            // while products still reference it.
            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CategoryId);
        });

        // Hierarchical catalog categories (issue #401): self-referencing parent FK,
        // tenant-unique slug, jsonb attributes.
        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ParentCategoryId);
            entity.HasIndex(e => e.DisplayOrder);
            entity.HasIndex(e => new { e.TenantId, e.Slug }).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.AttributesJson).HasColumnType("jsonb");
            entity.HasOne(e => e.Parent)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Per-product inventory (issue #401): one-to-one with Product, untracked by default.
        modelBuilder.Entity<ProductInventory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProductId).IsUnique();
            entity.HasIndex(e => e.TrackInventory);
            entity.Property(e => e.StockQuantity);
            entity.Property(e => e.ReservedQuantity);
            entity.Property(e => e.LowStockThreshold);
            entity.HasOne(e => e.Product)
                .WithOne(p => p.Inventory)
                .HasForeignKey<ProductInventory>(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductPricing>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.IsDefault);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.BasePrice).HasPrecision(10, 2);
            entity.Property(e => e.SalePrice).HasPrecision(10, 2);
            entity.Property(e => e.Currency).HasMaxLength(3).HasDefaultValue("USD");
        });

        modelBuilder.Entity<UserProduct>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.ProductId }).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.AccessStatus);
            entity.Property(e => e.PricePaid).HasPrecision(10, 2);
            entity.Property(e => e.Currency).HasMaxLength(3).HasDefaultValue("USD");
        });

        modelBuilder.Entity<PromoCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.CampaignId);
            entity.HasIndex(e => new { e.IsActive, e.ValidUntil });
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.DiscountPercentage).HasPrecision(5, 2);
            entity.Property(e => e.DiscountAmount).HasPrecision(10, 2);
            entity.Property(e => e.MaxDiscountAmount).HasPrecision(10, 2);
            entity.Property(e => e.Currency).HasMaxLength(3).HasDefaultValue("USD");
            entity.Property(e => e.CampaignId).HasMaxLength(100);
        });

        modelBuilder.Entity<PromoCodeUse>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PromoCodeId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.PromoCodeId, e.UserId });
            entity.HasIndex(e => e.UsedAt);
            // Unique nullable index: a repeated idempotency key is a replay, never a second use.
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.Property(e => e.DiscountApplied).HasPrecision(10, 2);
            entity.Property(e => e.Currency).HasMaxLength(3).HasDefaultValue("USD");
            entity.Property(e => e.IdempotencyKey).HasMaxLength(200);
        });

        modelBuilder.Entity<PromoCodeUserUsageCounter>(entity =>
        {
            entity.HasKey(e => new { e.PromoCodeId, e.UserId });
            entity.Property(e => e.UseCount).HasDefaultValue(0);
        });

        modelBuilder.Entity<ProductSubscriptionPlan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.IsActive);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Price).HasPrecision(10, 2);
            entity.Property(e => e.Currency).HasMaxLength(3).HasDefaultValue("USD");
        });

        modelBuilder.Entity<GameGuild.Commerce.PricingRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.RuleType);
            entity.HasIndex(e => e.IsActive);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasMany(e => e.PricingTiers)
                .WithOne(t => t.PricingRule)
                .HasForeignKey(t => t.PricingRuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameGuild.Commerce.PricingRuleTier>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PricingRuleId);
        });

        modelBuilder.Entity<PricingTier>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProductId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UnitPrice).HasPrecision(10, 2);
            entity.Property(e => e.Currency).HasMaxLength(3).HasDefaultValue("USD");
        });

        modelBuilder.Entity<PromoStackingRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.IsActive);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.MaxTotalDiscountPercentage).HasPrecision(5, 2);
            entity.Property(e => e.MaxTotalDiscountAmount).HasPrecision(10, 2);
        });

        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.Status });
            entity.HasIndex(e => new { e.TenantId, e.CustomerId });
            entity.HasIndex(e => new { e.TenantId, e.Priority });
            entity.Property(e => e.CustomerName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ReporterName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.ReporterEmail).HasMaxLength(320);
            entity.Property(e => e.Subject).IsRequired().HasMaxLength(180);
            entity.Property(e => e.Category).HasMaxLength(80);
            entity.Property(e => e.AssignedToName).HasMaxLength(150);
            entity.Property(e => e.ResolutionSummary).HasMaxLength(1000);
            entity.Property(e => e.LastMessagePreview).HasMaxLength(240);

            entity.HasMany(e => e.Messages)
                .WithOne(e => e.Ticket)
                .HasForeignKey(e => e.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupportTicketMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.TicketId });
            entity.Property(e => e.AuthorName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.AuthorEmail).HasMaxLength(320);
            entity.Property(e => e.Body).IsRequired().HasMaxLength(4000);
        });

        // Note: Order and OrderLineItem configurations are now in GameGuild.Commerce.Orders module
    }
}
