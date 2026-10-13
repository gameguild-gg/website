using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.GatewayRouting;

/// <summary>
///     Selection matrix for the config-driven fail-closed gateway selector (issue #413):
///     priority ordering, currency/country/payment-method/amount filters, independent
///     enable/disable (adapter configuration and routing entry), and the fail-closed
///     no-eligible-gateway behaviour.
/// </summary>
public class GatewaySelectionTests
{
    private static ConfigPaymentGatewaySelector CreateSelector(
        IReadOnlyList<IPaymentGateway> gateways,
        GatewayRoutingOptions? routing = null) =>
        new(
            new FakeGatewayRegistry([.. gateways]),
            Options.Create(routing ?? new GatewayRoutingOptions()),
            NullLogger<ConfigPaymentGatewaySelector>.Instance);

    private static GatewayPaymentRequest ChargeRequest(
        decimal amount = 25m,
        string currency = "USD",
        string? paymentMethodId = "pm_card_visa",
        string? country = null) =>
        new(
            IdempotencyKey: "idem-1",
            Amount: amount,
            Currency: currency,
            CustomerId: "cus_1",
            PaymentMethodId: paymentMethodId,
            Description: "test charge",
            Metadata: country is null
                ? null
                : new Dictionary<string, string> { [GatewayRouteContext.CountryMetadataKey] = country });

    [Fact]
    public void Select_OrdersByWellKnownDefaultPriority_WhenNoRoutingEntriesExist()
    {
        var payPal = new FakePaymentGateway("paypal");
        var stripe = new FakePaymentGateway("stripe");
        var selector = CreateSelector([payPal, stripe]);

        var candidates = selector.Select(new GatewayRouteContext("USD"));

        candidates.Select(candidate => candidate.Gateway.ProviderId)
            .Should().Equal("stripe", "paypal");
    }

    [Fact]
    public void Select_OrdersByConfiguredPriority_DescendingValuesFirst()
    {
        var stripe = new FakePaymentGateway("stripe");
        var payPal = new FakePaymentGateway("paypal");
        var routing = new GatewayRoutingOptions
        {
            Gateways =
            [
                new GatewayRouteEntry { ProviderId = "stripe", Priority = 300 },
                new GatewayRouteEntry { ProviderId = "paypal", Priority = 100 }
            ]
        };
        var selector = CreateSelector([stripe, payPal], routing);

        var candidates = selector.Select(new GatewayRouteContext("USD"));

        candidates.Select(candidate => candidate.Gateway.ProviderId)
            .Should().Equal("paypal", "stripe");
    }

    [Fact]
    public void Select_ExcludesGatewaysDisabledAtTheAdapter()
    {
        var stripe = new FakePaymentGateway("stripe") { IsEnabled = false };
        var payPal = new FakePaymentGateway("paypal");
        var selector = CreateSelector([stripe, payPal]);

        var candidates = selector.Select(new GatewayRouteContext("USD"));

        candidates.Should().ContainSingle();
        candidates[0].Gateway.ProviderId.Should().Be("paypal");
    }

    [Fact]
    public void Select_ExcludesGatewaysDisabledByRoutingEntry_EvenWhenAdapterIsEnabled()
    {
        var stripe = new FakePaymentGateway("stripe");
        var payPal = new FakePaymentGateway("paypal");
        var routing = new GatewayRoutingOptions
        {
            Gateways = [new GatewayRouteEntry { ProviderId = "stripe", Enabled = false }]
        };
        var selector = CreateSelector([stripe, payPal], routing);

        var candidates = selector.Select(new GatewayRouteContext("USD"));

        candidates.Should().ContainSingle();
        candidates[0].Gateway.ProviderId.Should().Be("paypal");
    }

    [Fact]
    public void Select_ExcludesGatewaysDisabledAtTheAdapter_EvenWhenRoutingEntryIsEnabled()
    {
        // Fail-closed conjunction: both the adapter disposition and the routing entry
        // must allow the gateway.
        var stripe = new FakePaymentGateway("stripe") { IsEnabled = false };
        var routing = new GatewayRoutingOptions
        {
            Gateways = [new GatewayRouteEntry { ProviderId = "stripe", Enabled = true }]
        };
        var selector = CreateSelector([stripe], routing);

        selector.Select(new GatewayRouteContext("USD")).Should().BeEmpty();
    }

    [Fact]
    public void Select_FiltersByCurrency_OnlyWhenDeclared()
    {
        var stripe = new FakePaymentGateway("stripe");
        var payPal = new FakePaymentGateway("paypal");
        var routing = new GatewayRoutingOptions
        {
            Gateways = [new GatewayRouteEntry { ProviderId = "payPal", SupportedCurrencies = ["EUR"] }]
        };
        var selector = CreateSelector([stripe, payPal], routing);

        var usdCandidates = selector.Select(new GatewayRouteContext("USD"));
        usdCandidates.Select(candidate => candidate.Gateway.ProviderId).Should().Equal("stripe");

        var eurCandidates = selector.Select(new GatewayRouteContext("EUR"));
        eurCandidates.Select(candidate => candidate.Gateway.ProviderId).Should().Equal("stripe", "paypal");
    }

    [Fact]
    public void Select_FiltersByCountry_WhenRequestCarriesOne()
    {
        var stripe = new FakePaymentGateway("stripe");
        var payPal = new FakePaymentGateway("paypal");
        var routing = new GatewayRoutingOptions
        {
            Gateways =
            [
                new GatewayRouteEntry { ProviderId = "paypal", SupportedCountries = ["DE", "FR"] },
                new GatewayRouteEntry { ProviderId = "stripe", SupportedCountries = ["US"] }
            ]
        };
        var selector = CreateSelector([stripe, payPal], routing);

        selector.Select(new GatewayRouteContext("EUR", Country: "de"))
            .Select(candidate => candidate.Gateway.ProviderId)
            .Should().Equal("paypal");

        selector.Select(new GatewayRouteContext("EUR", Country: "BR"))
            .Should().BeEmpty("neither gateway serves BR");
    }

    [Fact]
    public void Select_FiltersByPaymentMethodType()
    {
        var stripe = new FakePaymentGateway("stripe");
        var payPal = new FakePaymentGateway("paypal");
        var routing = new GatewayRoutingOptions
        {
            Gateways = [new GatewayRouteEntry { ProviderId = "stripe", SupportedPaymentMethods = ["card"] }]
        };
        var selector = CreateSelector([stripe, payPal], routing);

        // ba_ prefix infers the paypal method type: stripe's card-only route rejects it.
        selector.Select(new GatewayRouteContext("USD", PaymentMethodType: "paypal"))
            .Select(candidate => candidate.Gateway.ProviderId)
            .Should().Equal("paypal");

        selector.Select(new GatewayRouteContext("USD", PaymentMethodType: "card"))
            .Select(candidate => candidate.Gateway.ProviderId)
            .Should().Equal("stripe", "paypal");
    }

    [Fact]
    public void Select_AppliesAmountBoundsInclusively()
    {
        var stripe = new FakePaymentGateway("stripe");
        var routing = new GatewayRoutingOptions
        {
            Gateways = [new GatewayRouteEntry { ProviderId = "stripe", MinAmount = 10m, MaxAmount = 100m }]
        };
        var selector = CreateSelector([stripe], routing);

        selector.Select(new GatewayRouteContext("USD", Amount: 10m)).Should().HaveCount(1);
        selector.Select(new GatewayRouteContext("USD", Amount: 100m)).Should().HaveCount(1);
        selector.Select(new GatewayRouteContext("USD", Amount: 9.99m)).Should().BeEmpty();
        selector.Select(new GatewayRouteContext("USD", Amount: 100.01m)).Should().BeEmpty();
    }

    [Fact]
    public void RouteContext_DerivesCountryAndPaymentMethodType_FromChargeRequest()
    {
        var context = GatewayRouteContext.FromRequest(ChargeRequest(country: "DE", paymentMethodId: "ba-9XY"));

        context.Country.Should().Be("DE");
        context.PaymentMethodType.Should().Be("paypal");
        context.Amount.Should().Be(25m);
        context.Currency.Should().Be("USD");
    }

    [Fact]
    public void SelectPrimary_FailsClosed_WhenNoGatewaysAreRegistered()
    {
        var selector = CreateSelector([]);

        var act = () => selector.SelectPrimary();

        act.Should().Throw<GatewaySelectionFailedException>()
            .WithMessage("*No payment gateway adapters are registered*");
    }

    [Fact]
    public void SelectPrimary_FailsClosed_WhenEveryGatewayIsFilteredOut()
    {
        var stripe = new FakePaymentGateway("stripe");
        var routing = new GatewayRoutingOptions
        {
            Gateways = [new GatewayRouteEntry { ProviderId = "stripe", SupportedCurrencies = ["EUR"] }]
        };
        var selector = CreateSelector([stripe], routing);

        var act = () => selector.SelectPrimary(new GatewayRouteContext("JPY"));

        act.Should().Throw<GatewaySelectionFailedException>()
            .WithMessage("*currency JPY*not supported*");
    }

    [Fact]
    public void SelectPrimary_ReturnsHighestPriorityCandidate()
    {
        var selector = CreateSelector([new FakePaymentGateway("paypal"), new FakePaymentGateway("stripe")]);

        selector.SelectPrimary().Gateway.ProviderId.Should().Be("stripe");
    }

    [Fact]
    public void Select_ReturnsEmptyList_NotNull_WhenNothingIsEligible()
    {
        var selector = CreateSelector([new FakePaymentGateway("stripe") { IsEnabled = false }]);

        var candidates = selector.Select(new GatewayRouteContext("USD"));

        candidates.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void RoutingOptionsValidator_RejectsDuplicateAndInconsistentEntries()
    {
        var validator = new GatewayRoutingOptionsValidator();
        var options = new GatewayRoutingOptions
        {
            Gateways =
            [
                new GatewayRouteEntry { ProviderId = "stripe" },
                new GatewayRouteEntry { ProviderId = "STRIPE", MinAmount = 200m, MaxAmount = 100m }
            ]
        };

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(failure => failure.Contains("Duplicate"));
        result.Failures.Should().Contain(failure => failure.Contains("MinAmount cannot exceed MaxAmount"));
    }
}
