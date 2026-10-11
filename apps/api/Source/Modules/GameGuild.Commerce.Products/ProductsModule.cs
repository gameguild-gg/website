using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using GameGuild.Commerce;

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
        services.AddScoped<IProductPricingRepository, ProductPricingRepository>();
        services.AddScoped<IPromoCodeRepository, PromoCodeRepository>();
        services.AddScoped<IUserProductRepository, UserProductRepository>();
        services.AddScoped<IPricingRuleRepository, PricingRuleRepository>();

        // Register services
        services.AddScoped<IPricingEngineService, PricingEngineService>();
        services.AddScoped<IProductPricingService, ProductPricingService>();
        services.AddScoped<IPromoCodeService, PromoCodeService>();
        services.AddScoped<IUserProductService, UserProductService>();
        services.AddScoped<IEntitlementService, EntitlementService>();

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
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.DiscountPercentage).HasPrecision(5, 2);
            entity.Property(e => e.DiscountAmount).HasPrecision(10, 2);
            entity.Property(e => e.Currency).HasMaxLength(3).HasDefaultValue("USD");
        });

        modelBuilder.Entity<PromoCodeUse>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PromoCodeId);
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.DiscountApplied).HasPrecision(10, 2);
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
