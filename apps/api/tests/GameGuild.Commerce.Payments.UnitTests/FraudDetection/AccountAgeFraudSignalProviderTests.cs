using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.FraudDetection;

public class AccountAgeFraudSignalProviderTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _paymentId = Guid.NewGuid();
    private readonly Guid _subscriptionId = Guid.NewGuid();

    private AccountAgeFraudSignalProvider CreateProvider() =>
        new(NullLogger<AccountAgeFraudSignalProvider>.Instance);

    private static FraudAssessmentOptions Options() => new();

    [Fact]
    public async Task EvaluateAsync_Should_Flag_FirstCycle_HighValue_Charge()
    {
        var provider = CreateProvider();

        var signals = await provider.EvaluateAsync(
            Context(amount: 999m, billingCycleNumber: 1),
            Options(),
            CancellationToken.None);

        var signal = signals.SingleOrDefault(item => item.SignalType == "AccountAge.NewCustomerHighValue");
        signal.Should().NotBeNull();
        signal!.Weight.Should().Be(30); // NewCustomerHighValueWeight default
    }

    [Fact]
    public async Task EvaluateAsync_Should_Not_Flag_FirstCycle_Charge_Below_Threshold()
    {
        var provider = CreateProvider();

        var signals = await provider.EvaluateAsync(
            Context(amount: 25m, billingCycleNumber: 1),
            Options(),
            CancellationToken.None);

        signals.Should().BeEmpty("ordinary first subscriptions stay below the signal");
    }

    [Fact]
    public async Task EvaluateAsync_Should_Not_Flag_HighValue_Renewal_Of_Established_Relationship()
    {
        var provider = CreateProvider();

        var signals = await provider.EvaluateAsync(
            Context(amount: 999m, billingCycleNumber: 7),
            Options(),
            CancellationToken.None);

        signals.Should().BeEmpty("an established customer's renewal is not a new-relationship signal");
    }

    [Fact]
    public async Task EvaluateAsync_Should_Honor_The_Configured_Threshold()
    {
        var options = new FraudAssessmentOptions { NewCustomerHighValueAmountThreshold = 10m };
        var provider = CreateProvider();

        var signals = await provider.EvaluateAsync(
            Context(amount: 25m, billingCycleNumber: 1),
            options,
            CancellationToken.None);

        signals.Should().Contain(item => item.SignalType == "AccountAge.NewCustomerHighValue");
    }

    private FraudSignalContext Context(decimal amount, int billingCycleNumber) => new(
        _tenantId,
        _paymentId,
        _subscriptionId,
        "cus_age",
        null,
        amount,
        "USD",
        billingCycleNumber);
}
