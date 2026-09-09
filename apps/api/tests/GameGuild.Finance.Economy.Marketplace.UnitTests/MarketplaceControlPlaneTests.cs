using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using GameGuild.Commerce.Orders;
using GameGuild.Commerce.Products;
using GameGuild.Finance.Economy.Marketplace.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Marketplace.UnitTests;

public sealed class MarketplaceControlPlaneTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PolicyReader_ReturnsEffectiveAndExactSignedVersions()
    {
        await using var context = await MarketplacePolicyContext.CreateAsync();
        var row = ValidPolicy();
        context.Add(row);
        await context.SaveChangesAsync();
        var verifier = new SignatureVerifier(true);
        var reader = new PostgreSqlDurableMarketplacePolicyReader(context, verifier);

        var effective = await reader.GetEffectiveAsync(row.TenantId, row.ProductId, Now);
        var exact = await reader.GetVersionAsync(row.TenantId, row.ProductId, row.Version);

        effective.Should().BeEquivalentTo(exact);
        effective.TenantId.Should().Be(row.TenantId);
        effective.Policy.ProductId.Should().Be(row.ProductId);
        effective.Policy.SellerId.Should().Be(row.SellerId);
        effective.Policy.Version.Should().Be(row.Version);
        effective.Policy.Mode.Should().Be(row.Mode);
        effective.Policy.HardPriceUnits.Should().Be(row.HardPriceUnits);
        effective.Policy.SoftPriceUnits.Should().Be(row.SoftPriceUnits);
        effective.Policy.PlatformFeePpm.Should().Be(row.PlatformFeePpm);
        effective.Policy.EffectiveAt.Should().Be(row.EffectiveAt);
        effective.PlatformFeeWalletId.Should().Be(row.PlatformFeeWalletId);
        effective.RefundHold.Should().Be(TimeSpan.FromTicks(row.RefundHoldTicks));
        effective.PayloadHash.Should().Be(row.PayloadHash);
        effective.KeyId.Should().Be(row.KeyId);
        effective.Signature.Should().Be(row.Signature);
        verifier.Calls.Should().Be(2);
    }

    [Theory]
    [InlineData("effective-tenant")]
    [InlineData("effective-product")]
    [InlineData("version-tenant")]
    [InlineData("version-product")]
    [InlineData("version-number")]
    public async Task PolicyReader_RejectsInvalidIdentifiers(string invalid)
    {
        await using var context = await MarketplacePolicyContext.CreateAsync();
        var row = ValidPolicy();
        var reader = new PostgreSqlDurableMarketplacePolicyReader(context, new SignatureVerifier(true));

        Func<Task> action = invalid switch
        {
            "effective-tenant" => () => reader.GetEffectiveAsync(Guid.Empty, row.ProductId, Now).AsTask(),
            "effective-product" => () => reader.GetEffectiveAsync(row.TenantId, Guid.Empty, Now).AsTask(),
            "version-tenant" => () => reader.GetVersionAsync(Guid.Empty, row.ProductId, 1).AsTask(),
            "version-product" => () => reader.GetVersionAsync(row.TenantId, Guid.Empty, 1).AsTask(),
            _ => () => reader.GetVersionAsync(row.TenantId, row.ProductId, 0).AsTask()
        };

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task PolicyReader_RejectsMissingEffectiveAndExactPolicies()
    {
        await using var context = await MarketplacePolicyContext.CreateAsync();
        var row = ValidPolicy();
        var reader = new PostgreSqlDurableMarketplacePolicyReader(context, new SignatureVerifier(true));

        await FluentActions.Awaiting(() => reader.GetEffectiveAsync(row.TenantId, row.ProductId, Now).AsTask())
            .Should().ThrowAsync<MarketplaceCurrencyPolicyException>();
        await FluentActions.Awaiting(() => reader.GetVersionAsync(row.TenantId, row.ProductId, 1).AsTask())
            .Should().ThrowAsync<MarketplaceCurrencyPolicyException>();
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("signature")]
    [InlineData("approval")]
    [InlineData("wallet")]
    public async Task PolicyReader_RejectsUnsignedUnapprovedOrIncompletePolicy(string invalid)
    {
        await using var context = await MarketplacePolicyContext.CreateAsync();
        var row = ValidPolicy();
        if (invalid == "hash") row.PayloadHash = "wrong";
        if (invalid == "approval") row.ApprovedBy = row.ProposedBy;
        if (invalid == "wallet") row.PlatformFeeWalletId = Guid.Empty;
        context.Add(row);
        await context.SaveChangesAsync();
        var reader = new PostgreSqlDurableMarketplacePolicyReader(
            context, new SignatureVerifier(invalid != "signature"));

        await FluentActions.Awaiting(() => reader.GetVersionAsync(row.TenantId, row.ProductId, row.Version).AsTask())
            .Should().ThrowAsync<MarketplaceCurrencyPolicyException>();
    }

    [Fact]
    public void PolicyReader_RejectsInvalidDependencies()
    {
        var context = new StubApplicationDbContext();
        var verifier = new SignatureVerifier(true);

        FluentActions.Invoking(() => new PostgreSqlDurableMarketplacePolicyReader(null!, verifier))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlDurableMarketplacePolicyReader(context, null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlDurableMarketplacePolicyReader(context, verifier))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task AuthoritativeOrderReader_ReturnsCanonicalPendingAndProcessingSnapshots()
    {
        await using var context = await CommerceSnapshotContext.CreateAsync();
        var first = SeedCommerceOrder(context);
        var second = SeedCommerceOrder(context);
        second.Order.StartPaymentProcessing();
        await context.SaveChangesAsync();
        var reader = new EfAuthoritativeMarketplaceOrderReader(context);

        var pending = await reader.ReadAsync(first.TenantId, first.BuyerId, first.Order.Id);
        var processing = await reader.ReadAsync(second.TenantId, second.BuyerId, second.Order.Id);

        pending.TenantId.Should().Be(first.TenantId);
        pending.OrderId.Should().Be(first.Order.Id);
        pending.OrderLineItemId.Should().Be(first.Line.Id);
        pending.BuyerId.Should().Be(first.BuyerId);
        pending.ProductId.Should().Be(first.Product.Id);
        pending.SellerId.Should().Be(first.SellerId);
        pending.ProductPricingVersionId.Should().Be(first.Line.ProductPricingVersionId);
        pending.PriceVersionSnapshot.Should().Be(3);
        pending.Quantity.Should().Be(2);
        pending.UnitPriceSnapshot.Should().Be(5.5m);
        pending.FiatCurrencySnapshot.Should().Be("USD");
        pending.SnapshotHash.Should().HaveLength(64);
        processing.SnapshotHash.Should().HaveLength(64);
        processing.OrderId.Should().Be(second.Order.Id);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("buyer")]
    [InlineData("order")]
    public async Task AuthoritativeOrderReader_RejectsEmptyIdentifiers(string invalid)
    {
        await using var context = await CommerceSnapshotContext.CreateAsync();
        var seeded = SeedCommerceOrder(context);
        await context.SaveChangesAsync();
        var reader = new EfAuthoritativeMarketplaceOrderReader(context);

        await FluentActions.Awaiting(() => reader.ReadAsync(
                invalid == "tenant" ? Guid.Empty : seeded.TenantId,
                invalid == "buyer" ? Guid.Empty : seeded.BuyerId,
                invalid == "order" ? Guid.Empty : seeded.Order.Id).AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AuthoritativeOrderReader_RejectsMissingIneligibleAndWrongLineCount()
    {
        await using var context = await CommerceSnapshotContext.CreateAsync();
        var missing = SeedCommerceOrder(context);
        var ineligible = SeedCommerceOrder(context);
        ineligible.Order.MarkAsPaid();
        var empty = Order.Create(Guid.NewGuid(), Guid.NewGuid().ToString("N"), Guid.NewGuid());
        context.Add(empty);
        await context.SaveChangesAsync();
        var reader = new EfAuthoritativeMarketplaceOrderReader(context);

        await FluentActions.Awaiting(() => reader.ReadAsync(missing.TenantId, Guid.NewGuid(), missing.Order.Id).AsTask())
            .Should().ThrowAsync<MarketplaceOrderSnapshotException>();
        await FluentActions.Awaiting(() => reader.ReadAsync(ineligible.TenantId, ineligible.BuyerId, ineligible.Order.Id).AsTask())
            .Should().ThrowAsync<MarketplaceOrderSnapshotException>();
        await FluentActions.Awaiting(() => reader.ReadAsync(empty.TenantId!.Value, empty.UserId, empty.Id).AsTask())
            .Should().ThrowAsync<MarketplaceOrderSnapshotException>();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("tenant-null")]
    [InlineData("quantity")]
    [InlineData("pricing")]
    [InlineData("version")]
    [InlineData("price")]
    [InlineData("currency")]
    public async Task AuthoritativeOrderReader_RejectsIncompleteImmutableLine(string invalid)
    {
        await using var context = await CommerceSnapshotContext.CreateAsync();
        var seeded = SeedCommerceOrder(context);
        var entry = context.Entry(seeded.Line);
        if (invalid == "tenant") entry.Property(nameof(OrderLineItem.TenantId)).CurrentValue = Guid.NewGuid();
        if (invalid == "tenant-null") entry.Property(nameof(OrderLineItem.TenantId)).CurrentValue = null;
        if (invalid == "quantity") entry.Property(nameof(OrderLineItem.Quantity)).CurrentValue = 0;
        if (invalid == "pricing") entry.Property(nameof(OrderLineItem.ProductPricingVersionId)).CurrentValue = Guid.Empty;
        if (invalid == "version") entry.Property(nameof(OrderLineItem.PriceVersionSnapshot)).CurrentValue = 0;
        if (invalid == "price") entry.Property(nameof(OrderLineItem.UnitPriceSnapshot)).CurrentValue = -1m;
        if (invalid == "currency") entry.Property(nameof(OrderLineItem.CurrencySnapshot)).CurrentValue = " ";
        await context.SaveChangesAsync();
        var reader = new EfAuthoritativeMarketplaceOrderReader(context);

        await FluentActions.Awaiting(() => reader.ReadAsync(seeded.TenantId, seeded.BuyerId, seeded.Order.Id).AsTask())
            .Should().ThrowAsync<MarketplaceOrderSnapshotException>();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("creator-null")]
    [InlineData("creator-empty")]
    [InlineData("self")]
    public async Task AuthoritativeOrderReader_RejectsMissingProductOrInvalidSeller(string invalid)
    {
        await using var context = await CommerceSnapshotContext.CreateAsync();
        var seeded = SeedCommerceOrder(context);
        if (invalid == "missing") context.Remove(seeded.Product);
        if (invalid == "creator-null") seeded.Product.CreatorId = null;
        if (invalid == "creator-empty") seeded.Product.CreatorId = Guid.Empty;
        if (invalid == "self") seeded.Product.CreatorId = seeded.BuyerId;
        await context.SaveChangesAsync();
        var reader = new EfAuthoritativeMarketplaceOrderReader(context);

        await FluentActions.Awaiting(() => reader.ReadAsync(seeded.TenantId, seeded.BuyerId, seeded.Order.Id).AsTask())
            .Should().ThrowAsync<MarketplaceOrderSnapshotException>();
    }

    [Fact]
    public void AuthoritativeOrderReader_RejectsInvalidContext()
    {
        FluentActions.Invoking(() => new EfAuthoritativeMarketplaceOrderReader(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new EfAuthoritativeMarketplaceOrderReader(new StubApplicationDbContext()))
            .Should().Throw<InvalidOperationException>();
    }

    private static MarketplaceCurrencyPolicyVersionRow ValidPolicy()
    {
        const string canonical = "{\"marketplace\":\"policy\"}";
        return new MarketplaceCurrencyPolicyVersionRow
        {
            TenantId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Version = 7,
            SellerId = Guid.NewGuid(),
            Mode = ProductCurrencyMode.HardOnly,
            HardPriceUnits = 100,
            SoftPriceUnits = 0,
            PlatformFeePpm = 100_000,
            EffectiveAt = Now.AddDays(-1),
            ExpiresAt = Now.AddDays(1),
            PlatformFeeWalletId = Guid.NewGuid(),
            RefundHoldTicks = TimeSpan.FromDays(7).Ticks,
            CanonicalPayload = canonical,
            PayloadHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))),
            KeyId = "kms-key",
            Signature = "signature",
            ProposedBy = Guid.NewGuid(),
            ApprovedBy = Guid.NewGuid(),
            PublishedAt = Now.AddDays(-2)
        };
    }

    private static SeededOrder SeedCommerceOrder(CommerceSnapshotContext context)
    {
        var tenantId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var product = Product.Create("Product", creatorId: sellerId, tenantId: tenantId);
        var order = Order.Create(buyerId, Guid.NewGuid().ToString("N"), tenantId);
        var line = order.AddLineItem(product.Id, product.Name,
            new OrderLineItemPricingSnapshot(Guid.NewGuid(), Guid.NewGuid(), 3, 6m, 5.5m, 5.5m, "USD"),
            quantity: 2, discountAmount: 0.5m);
        context.AddRange(product, order);
        return new SeededOrder(tenantId, buyerId, sellerId, product, order, line);
    }

    private sealed record SeededOrder(
        Guid TenantId,
        Guid BuyerId,
        Guid SellerId,
        Product Product,
        Order Order,
        OrderLineItem Line);

    private sealed class MarketplacePolicyContext : DbContext, IApplicationDbContext
    {
        private MarketplacePolicyContext(DbContextOptions<MarketplacePolicyContext> options) : base(options) { }

        public static async Task<MarketplacePolicyContext> CreateAsync()
        {
            var context = new MarketplacePolicyContext(new DbContextOptionsBuilder<MarketplacePolicyContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
            await context.Database.EnsureCreatedAsync();
            return context;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new MarketplaceModelConfiguration().Configure(modelBuilder);

        Task<IDbContextTransaction> IApplicationDbContext.BeginTransactionAsync(CancellationToken cancellationToken) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class CommerceSnapshotContext : DbContext, IApplicationDbContext
    {
        private CommerceSnapshotContext(DbContextOptions<CommerceSnapshotContext> options) : base(options) { }

        public static async Task<CommerceSnapshotContext> CreateAsync()
        {
            var context = new CommerceSnapshotContext(new DbContextOptionsBuilder<CommerceSnapshotContext>()
                .UseSqlite("Data Source=:memory:").Options);
            await context.Database.OpenConnectionAsync();
            await context.Database.EnsureCreatedAsync();
            return context;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(builder =>
            {
                builder.HasKey(item => item.Id);
                builder.Ignore(item => item.User);
                builder.HasMany(item => item.LineItems).WithOne(item => item.Order)
                    .HasForeignKey(item => item.OrderId);
            });
            modelBuilder.Entity<OrderLineItem>(builder =>
            {
                builder.HasKey(item => item.Id);
                builder.Ignore(item => item.Product);
                builder.Ignore(item => item.UserProduct);
            });
            modelBuilder.Entity<Product>(builder =>
            {
                builder.HasKey(item => item.Id);
                builder.Ignore(item => item.Creator);
                builder.Ignore(item => item.Pricing);
                builder.Ignore(item => item.SubscriptionPlans);
                builder.Ignore(item => item.UserProducts);
                builder.Ignore(item => item.PromoCodes);
                builder.Ignore(item => item.CommissionConfig);
                builder.Ignore(item => item.BundleItems);
                builder.Ignore(item => item.IncludedInBundles);
            });
        }

        Task<IDbContextTransaction> IApplicationDbContext.BeginTransactionAsync(CancellationToken cancellationToken) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class SignatureVerifier(bool result) : ICapabilityPolicySignatureVerifier
    {
        public int Calls { get; private set; }

        public ValueTask<bool> VerifyAsync(string canonicalPayload, string keyId, string signature,
            CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubApplicationDbContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
