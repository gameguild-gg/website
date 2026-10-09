using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Entities;

public sealed class PaymentProviderSecurityExpansionTests
{
    [Fact]
    public void BindProviderMapping_Binds_Provider_Identity_Once()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "USD", "provider-binding");

        payment.BindProviderMapping(
            "stripe",
            "live",
            "acct_merchant",
            "pi_bound",
            "payment_intent",
            "capture");

        payment.Provider.Should().Be("stripe");
        payment.ProviderEnvironment.Should().Be("live");
        payment.ProviderAccountId.Should().Be("acct_merchant");
        payment.ProviderObjectId.Should().Be("pi_bound");
        payment.ProviderObjectType.Should().Be("payment_intent");
        payment.ProviderMonetaryLeg.Should().Be("capture");
    }

    [Fact]
    public void BindProviderMapping_With_Identical_Identity_Is_Idempotent()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "USD", "provider-rebinding");
        payment.BindProviderMapping(
            "stripe",
            "test",
            "acct_merchant",
            "pi_replayed",
            "payment_intent",
            "capture");
        payment.UpdatedAt = DateTime.UnixEpoch;

        payment.BindProviderMapping(
            "stripe",
            "test",
            "acct_merchant",
            "pi_replayed",
            "payment_intent",
            "capture");

        payment.UpdatedAt.Should().Be(DateTime.UnixEpoch);
    }

    [Theory]
    [InlineData("paypal", "test", "acct_merchant", "pi_bound", "payment_intent", "capture")]
    [InlineData("stripe", "live", "acct_merchant", "pi_bound", "payment_intent", "capture")]
    [InlineData("stripe", "test", "acct_other", "pi_bound", "payment_intent", "capture")]
    [InlineData("stripe", "test", "acct_merchant", "pi_other", "payment_intent", "capture")]
    [InlineData("stripe", "test", "acct_merchant", "pi_bound", "charge", "capture")]
    [InlineData("stripe", "test", "acct_merchant", "pi_bound", "payment_intent", "refund")]
    public void BindProviderMapping_With_Mismatched_Identity_Is_Rejected(
        string provider,
        string environment,
        string accountId,
        string objectId,
        string objectType,
        string monetaryLeg)
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "USD", "provider-mismatch");
        payment.BindProviderMapping(
            "stripe",
            "test",
            "acct_merchant",
            "pi_bound",
            "payment_intent",
            "capture");

        var act = () => payment.BindProviderMapping(
            provider,
            environment,
            accountId,
            objectId,
            objectType,
            monetaryLeg);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*provider mapping*already bound*");
        payment.ProviderObjectId.Should().Be("pi_bound");
    }

    [Theory]
    [InlineData(null, "test", "acct_merchant", "pi_bound", "payment_intent", "capture")]
    [InlineData("stripe", "", "acct_merchant", "pi_bound", "payment_intent", "capture")]
    [InlineData("stripe", "test", "", "pi_bound", "payment_intent", "capture")]
    [InlineData("stripe", "test", "acct_merchant", "", "payment_intent", "capture")]
    [InlineData("stripe", "test", "acct_merchant", "pi_bound", "", "capture")]
    [InlineData("stripe", "test", "acct_merchant", "pi_bound", "payment_intent", " ")]
    public void BindProviderMapping_Requires_Every_Provider_Identity_Component(
        string? provider,
        string environment,
        string accountId,
        string objectId,
        string objectType,
        string monetaryLeg)
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "USD", "provider-required-values");

        var act = () => payment.BindProviderMapping(
            provider!,
            environment,
            accountId,
            objectId,
            objectType,
            monetaryLeg);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task GetByProviderMappingAsync_Requires_The_Complete_Provider_Scope()
    {
        var options = new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
            .UseInMemoryDatabase($"payment-provider-scope-{Guid.NewGuid()}")
            .Options;
        await using var context = new PaymentsPersistenceTestDbContext(options);
        var payment = Payment.Create(Guid.NewGuid(), 100m, "USD", "provider-lookup");
        payment.BindProviderMapping(
            "stripe",
            "live",
            "acct_merchant",
            "pi_scoped",
            "payment_intent",
            "capture");
        context.Set<Payment>().Add(payment);
        await context.SaveChangesAsync();
        var logger = new CapturingPaymentLogger();
        IPaymentRepository repository = new PaymentRepository(context, logger);

        var found = await repository.GetByProviderMappingAsync(
            "stripe",
            "live",
            "acct_merchant",
            "pi_scoped",
            "payment_intent",
            "capture");

        found.Should().BeSameAs(payment);
        logger.Entries.Should().ContainSingle().Which.Should()
            .NotContain("acct_merchant")
            .And.Contain(LogRedaction.RedactId("acct_merchant", "provider"));
        (await repository.GetByProviderMappingAsync("paypal", "live", "acct_merchant", "pi_scoped", "payment_intent", "capture"))
            .Should().BeNull();
        (await repository.GetByProviderMappingAsync("stripe", "test", "acct_merchant", "pi_scoped", "payment_intent", "capture"))
            .Should().BeNull();
        (await repository.GetByProviderMappingAsync("stripe", "live", "acct_other", "pi_scoped", "payment_intent", "capture"))
            .Should().BeNull();
        (await repository.GetByProviderMappingAsync("stripe", "live", "acct_merchant", "pi_other", "payment_intent", "capture"))
            .Should().BeNull();
        (await repository.GetByProviderMappingAsync("stripe", "live", "acct_merchant", "pi_scoped", "charge", "capture"))
            .Should().BeNull();
        (await repository.GetByProviderMappingAsync("stripe", "live", "acct_merchant", "pi_scoped", "payment_intent", "refund"))
            .Should().BeNull();
    }

    private sealed class CapturingPaymentLogger : ILogger<PaymentRepository>
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.None)
            {
                return;
            }
            Entries.Add(formatter(state, exception));
        }
    }

    [Fact]
    public void CapturingPaymentLogger_RespectsDisabledLogLevel()
    {
        var capturingLogger = new CapturingPaymentLogger();
        ILogger logger = capturingLogger;
        logger.IsEnabled(LogLevel.None).Should().BeFalse();
        logger.Log(LogLevel.None, default, "disabled", null, static (state, _) => state);
        logger.Log(LogLevel.Debug, default, "enabled", null, static (state, _) => state);
        capturingLogger.Entries.Should().ContainSingle().Which.Should().Be("enabled");
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(100, 100, 0)]
    [InlineData(100, 0, 100)]
    [InlineData(100, 40, 60)]
    public void ValidateProviderMonetaryBounds_Accepts_Values_Within_Payment_Bounds(
        decimal cumulativeConfirmedAmount,
        decimal cumulativeRefundedAmount,
        decimal cumulativeDisputedAmount)
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "USD", "provider-bounds-valid");

        payment.ValidateProviderMonetaryBounds(
            cumulativeConfirmedAmount,
            cumulativeRefundedAmount,
            cumulativeDisputedAmount);

        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.RefundedAmount.Should().Be(0m);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(100, -1, 0)]
    [InlineData(100, 0, -1)]
    public void ValidateProviderMonetaryBounds_Rejects_Negative_Cumulative_Values(
        decimal cumulativeConfirmedAmount,
        decimal cumulativeRefundedAmount,
        decimal cumulativeDisputedAmount)
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "USD", "provider-bounds-negative");

        var act = () => payment.ValidateProviderMonetaryBounds(
            cumulativeConfirmedAmount,
            cumulativeRefundedAmount,
            cumulativeDisputedAmount);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(101, 0, 0)]
    [InlineData(50, 51, 0)]
    [InlineData(50, 0, 51)]
    [InlineData(100, 60, 41)]
    public void ValidateProviderMonetaryBounds_Rejects_Cumulative_Values_Above_Authoritative_Bounds(
        decimal cumulativeConfirmedAmount,
        decimal cumulativeRefundedAmount,
        decimal cumulativeDisputedAmount)
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "USD", "provider-bounds-exceeded");

        var act = () => payment.ValidateProviderMonetaryBounds(
            cumulativeConfirmedAmount,
            cumulativeRefundedAmount,
            cumulativeDisputedAmount);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Payment_Model_Exposes_Nullable_Provider_Mapping_Fields_And_Scoped_Index()
    {
        var modelBuilder = new ModelBuilder();
        new PaymentsModelConfiguration().Configure(modelBuilder);

        var entity = modelBuilder.Model.FindEntityType(typeof(Payment))!;
        foreach (var propertyName in new[]
                 {
                     nameof(Payment.ProviderEnvironment),
                     nameof(Payment.ProviderAccountId),
                     nameof(Payment.ProviderObjectId),
                     nameof(Payment.ProviderObjectType),
                     nameof(Payment.ProviderMonetaryLeg)
                 })
        {
            entity.FindProperty(propertyName).Should().NotBeNull();
            entity.FindProperty(propertyName)!.IsNullable.Should().BeTrue();
        }

        entity.GetIndexes().Single(index => index.GetDatabaseName() == "ix_payments_provider_object_leg")
            .IsUnique.Should().BeTrue();

        entity.GetCheckConstraints().Select(constraint => constraint.Name).Should().Contain(
            "ck_payments_provider_mapping_complete");
        entity.GetCheckConstraints().Select(constraint => constraint.Name).Should().Contain(
            "ck_payments_provider_environment");
        entity.GetCheckConstraints().Select(constraint => constraint.Name).Should().Contain(
            "ck_payments_stripe_value_mapping_required");
    }

    [Fact]
    public void ResolveUnverifiedLegacyProviderObjectId_Returns_Existing_External_Payment_Id()
    {
        var payment = Payment.Create(Guid.NewGuid(), 10m, "USD", "provider-expand");
        payment.MarkAsProcessing("txn_legacy");
        payment.MarkAsSucceeded("pi_legacy");

        payment.ResolveUnverifiedLegacyProviderObjectId().Should().Be("pi_legacy");
    }
}
