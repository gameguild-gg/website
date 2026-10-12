using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using GameGuild.Commerce.Payments;

namespace GameGuild.Commerce.Payments.UnitTests;

public class EfConfigAndServicesTests
{
    // ── EF Configuration Classes (11 configs) ───────────────────────────
    [Fact]
    public void AuditTrailConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var cfg = new AuditTrailConfiguration();
        cfg.Configure(mb.Entity<AuditTrail>());
        mb.Model.Should().NotBeNull();
    }

    [Fact]
    public void DisputeEvidenceConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var cfg = new DisputeEvidenceConfiguration();
        cfg.Configure(mb.Entity<DisputeEvidence>());
        mb.Model.Should().NotBeNull();
    }

    [Fact]
    public void FinancialLedgerEntryConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var cfg = new FinancialLedgerEntryConfiguration();
        cfg.Configure(mb.Entity<FinancialLedgerEntry>());
        mb.Model.Should().NotBeNull();
    }

    [Fact]
    public void PaymentDisputeConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var cfg = new PaymentDisputeConfiguration();
        cfg.Configure(mb.Entity<PaymentDispute>());
        mb.Model.Should().NotBeNull();
    }

    [Fact]
    public void RevenueEventConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var cfg = new RevenueEventConfiguration();
        cfg.Configure(mb.Entity<RevenueEvent>());
        mb.Model.Should().NotBeNull();
    }

    [Fact]
    public void TaxJurisdictionConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var cfg = new TaxJurisdictionConfiguration();
        cfg.Configure(mb.Entity<TaxJurisdiction>());
        mb.Model.Should().NotBeNull();
    }

    [Fact]
    public void TaxRateConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var cfg = new TaxRateConfiguration();
        cfg.Configure(mb.Entity<TaxRate>());
        mb.Model.Should().NotBeNull();
    }

    [Fact]
    public void TaxRuleConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var cfg = new TaxRuleConfiguration();
        cfg.Configure(mb.Entity<TaxRule>());
        mb.Model.Should().NotBeNull();
    }

    [Fact]
    public void UserWalletConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var cfg = new UserWalletConfiguration();
        cfg.Configure(mb.Entity<UserWallet>());
        mb.Model.Should().NotBeNull();
    }

    [Fact]
    public void WalletTransactionConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var cfg = new WalletTransactionConfiguration();
        cfg.Configure(mb.Entity<WalletTransaction>());
        mb.Model.Should().NotBeNull();
    }

    // ── PaymentsModelConfiguration ──────────────────────────────────────
    [Fact]
    public void PaymentsModelConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var cfg = new PaymentsModelConfiguration();
        cfg.Configure(mb);
        mb.Model.Should().NotBeNull();
    }

    // ── Repository Constructors ─────────────────────────────────────────
    [Fact]
    public void AuditTrailRepository_CanBeCreated()
    {
        var repo = new AuditTrailRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void FinancialLedgerRepository_CanBeCreated()
    {
        var repo = new FinancialLedgerRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void PaymentRepository_CanBeCreated()
    {
        var repo = new PaymentRepository(
            Mock.Of<IApplicationDbContext>(),
            NullLogger<PaymentRepository>.Instance);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void RevenueEventRepository_CanBeCreated()
    {
        var repo = new RevenueEventRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void WalletRepository_CanBeCreated()
    {
        var repo = new WalletRepository(
            Mock.Of<IApplicationDbContext>(),
            NullLogger<WalletRepository>.Instance);
        repo.Should().NotBeNull();
    }

    // ── Service Constructors ────────────────────────────────────────────
    [Fact]
    public void DisputeService_CanBeCreated()
    {
        var svc = new DisputeService(
            Mock.Of<IApplicationDbContext>(),
            NullLogger<DisputeService>.Instance);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void RevenueAuditService_CanBeCreated()
    {
        var svc = new RevenueAuditService(
            Mock.Of<IRevenueEventRepository>(),
            Mock.Of<IFinancialLedgerRepository>(),
            Mock.Of<IAuditTrailRepository>(),
            NullLogger<RevenueAuditService>.Instance);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void TaxCalculationService_CanBeCreated()
    {
        var svc = new TaxCalculationService(
            Mock.Of<IApplicationDbContext>(),
            NullLogger<TaxCalculationService>.Instance,
            new MemoryCache(new MemoryCacheOptions()));
        svc.Should().NotBeNull();
    }

    [Fact]
    public void WalletService_CanBeCreated()
    {
        var svc = new WalletService(
            Mock.Of<IWalletRepository>(),
            NullLogger<WalletService>.Instance);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void StripePaymentService_CanBeCreated()
    {
        var opts = Options.Create(new StripeGatewayOptions());
        var svc = new StripePaymentService(opts, NullLogger<StripePaymentService>.Instance);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void StripeCustomerService_CanBeCreated()
    {
        var opts = Options.Create(new StripeGatewayOptions());
        var svc = new StripeCustomerService(opts, NullLogger<StripeCustomerService>.Instance);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void StripePaymentGateway_CanBeCreated()
    {
        var opts = Options.Create(new StripeGatewayOptions());
        var gateway = new StripePaymentGateway(
            opts,
            Mock.Of<IStripePaymentService>(),
            Mock.Of<IStripeCustomerService>());
        gateway.Should().NotBeNull();
    }
}
