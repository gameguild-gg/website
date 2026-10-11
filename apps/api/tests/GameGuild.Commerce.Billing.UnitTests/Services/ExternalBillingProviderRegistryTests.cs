using FluentAssertions;
using GameGuild.Commerce.Billing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

public class ExternalBillingProviderRegistryTests
{
    private readonly Mock<IBillingProviderStateRepository> _stateRepository = new();
    private readonly BillingConfiguration _configuration = new();

    private ExternalBillingProviderRegistry CreateRegistry() => new(
        _stateRepository.Object,
        new OptionsWrapper<BillingConfiguration>(_configuration));

    [Fact]
    public async Task ListAsync_Should_Return_Every_Supported_Provider_Defaulting_To_Enabled()
    {
        _stateRepository
            .Setup(repository => repository.GetAllStatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var providers = await CreateRegistry().ListAsync(CancellationToken.None);

        providers.Select(provider => provider.ProviderKey)
            .Should()
            .BeEquivalentTo(PaymentProviders.All);
        providers.Should().OnlyContain(provider => provider.Enabled);
    }

    [Fact]
    public async Task ListAsync_Should_Reflect_Persisted_Disable_Overrides()
    {
        var disabled = new BillingProviderState(PaymentProviders.Stripe, isEnabled: false, changedByUserId: Guid.NewGuid());
        _stateRepository
            .Setup(repository => repository.GetAllStatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([disabled]);

        var providers = await CreateRegistry().ListAsync(CancellationToken.None);

        providers.Single(provider => provider.ProviderKey == PaymentProviders.Stripe).Enabled.Should().BeFalse();
        providers.Single(provider => provider.ProviderKey == PaymentProviders.PayPal).Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_Should_Return_Null_For_Unknown_Provider_Keys_Fail_Closed()
    {
        var provider = await CreateRegistry().GetAsync("not-a-provider", CancellationToken.None);

        provider.Should().BeNull();
        _stateRepository.Verify(repository => repository.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_Should_Normalize_And_Return_Configured_Health()
    {
        _configuration.Stripe.SecretKey = "sk_test";
        _configuration.Stripe.PublishableKey = "pk_test";
        _configuration.Stripe.WebhookSecret = "whsec_test";
        _configuration.Stripe.WebhookEndpointId = "we_test";
        _stateRepository
            .Setup(repository => repository.GetByProviderKeyAsync(PaymentProviders.Stripe, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingProviderState?)null);

        var provider = await CreateRegistry().GetAsync("Stripe", CancellationToken.None);

        provider.Should().NotBeNull();
        provider!.ProviderKey.Should().Be(PaymentProviders.Stripe);
        provider.Configured.Should().BeTrue();
        provider.ConfigValid.Should().BeTrue();
        provider.WebhookEndpointConfigured.Should().BeTrue();
        provider.Health.Should().Be("healthy");
        provider.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_Should_Report_Degraded_When_Webhook_Verification_Is_Incomplete()
    {
        _configuration.Stripe.SecretKey = "sk_test";
        _configuration.Stripe.PublishableKey = "pk_test";
        // WebhookSecret left empty: credentials exist but the callback endpoint cannot verify events.
        _stateRepository
            .Setup(repository => repository.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingProviderState?)null);

        var provider = await CreateRegistry().GetAsync(PaymentProviders.Stripe, CancellationToken.None);

        provider!.Configured.Should().BeTrue();
        provider.WebhookEndpointConfigured.Should().BeFalse();
        provider.Health.Should().Be("degraded");
    }

    [Fact]
    public async Task GetAsync_Should_Report_NotConfigured_When_Credentials_Are_Absent()
    {
        var provider = await CreateRegistry().GetAsync(PaymentProviders.GooglePay, CancellationToken.None);

        provider!.Configured.Should().BeFalse();
        provider.Health.Should().Be("not-configured");
        provider.WebhookEndpointConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task SetEnabledAsync_Should_Fail_Closed_For_Unknown_Provider_Keys()
    {
        var act = () => CreateRegistry().SetEnabledAsync("not-a-provider", enabled: true, Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _stateRepository.Verify(repository => repository.CreateAsync(It.IsAny<BillingProviderState>(), It.IsAny<CancellationToken>()), Times.Never);
        _stateRepository.Verify(repository => repository.UpdateAsync(It.IsAny<BillingProviderState>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetEnabledAsync_Should_Create_State_Row_When_None_Exists()
    {
        _stateRepository
            .Setup(repository => repository.GetByProviderKeyAsync(PaymentProviders.Stripe, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingProviderState?)null);

        var status = await CreateRegistry().SetEnabledAsync(PaymentProviders.Stripe, enabled: false, Guid.NewGuid(), CancellationToken.None);

        status.Enabled.Should().BeFalse();
        _stateRepository.Verify(
            repository => repository.CreateAsync(
                It.Is<BillingProviderState>(state => state.ProviderKey == PaymentProviders.Stripe && !state.IsEnabled),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetEnabledAsync_EnableDisableRoundTrip_Should_Update_The_Same_Row()
    {
        var state = new BillingProviderState(PaymentProviders.Stripe, isEnabled: false, changedByUserId: Guid.NewGuid());
        _stateRepository
            .Setup(repository => repository.GetByProviderKeyAsync(PaymentProviders.Stripe, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        var registry = CreateRegistry();

        var disabled = await registry.SetEnabledAsync(PaymentProviders.Stripe, enabled: false, Guid.NewGuid(), CancellationToken.None);
        var enabled = await registry.SetEnabledAsync(PaymentProviders.Stripe, enabled: true, Guid.NewGuid(), CancellationToken.None);

        disabled.Enabled.Should().BeFalse();
        enabled.Enabled.Should().BeTrue();
        _stateRepository.Verify(repository => repository.UpdateAsync(state, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _stateRepository.Verify(repository => repository.CreateAsync(It.IsAny<BillingProviderState>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(PaymentProviders.Stripe, true, true)]
    [InlineData(PaymentProviders.PayPal, false, false)]
    [InlineData(PaymentProviders.ApplePay, true, false)] // credentials but no shared secret
    [InlineData(PaymentProviders.AppleAppStore, true, false)]
    [InlineData(PaymentProviders.GooglePay, false, false)]
    [InlineData(PaymentProviders.GooglePlayStore, false, false)]
    [InlineData("unknown", false, false)]
    public void EvaluateHealth_Should_Classify_Configuration_State(string providerKey, bool expectedConfigured, bool expectedWebhookEndpoint)
    {
        _configuration.Stripe.SecretKey = "sk_test";
        _configuration.Stripe.PublishableKey = "pk_test";
        _configuration.Stripe.WebhookSecret = "whsec_test";
        _configuration.Stripe.WebhookEndpointId = "we_test";
        _configuration.PayPal.ClientId = string.Empty;
        _configuration.ApplePay.BundleId = "com.example.app";
        _configuration.ApplePay.SharedSecret = string.Empty;

        var (configured, configValid, webhookEndpointConfigured, errors) =
            ExternalBillingProviderRegistry.EvaluateHealth(providerKey, _configuration);

        configured.Should().Be(expectedConfigured);
        webhookEndpointConfigured.Should().Be(expectedWebhookEndpoint);

        if (expectedConfigured && !configValid)
        {
            errors.Should().NotBeEmpty();
        }
    }
}
