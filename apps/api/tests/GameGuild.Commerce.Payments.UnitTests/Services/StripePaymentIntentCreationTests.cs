using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;
using Xunit;

using GameGuild.Commerce.Payments.UnitTests;

namespace GameGuild.Commerce.Payments.UnitTests.Services;

// Constructs StripePaymentService with real (non-simulated) options, which
// mutates the process-wide StripeConfiguration via EnsureApiKey; must run in
// the serialized collection so it cannot race other configuration assertions.
[Collection(StripeProcessConfigurationCollection.Name)]
public sealed class StripePaymentIntentCreationTests
{
    [Fact]
    public async Task CreatePaymentIntentAsync_SendsAnUnconfirmedServerPricedIntent()
    {
        PaymentIntentCreateOptions? capturedCreate = null;
        RequestOptions? capturedRequest = null;
        var service = Service((create, request, _) =>
        {
            capturedCreate = create;
            capturedRequest = request;
            return Task.FromResult(Intent("requires_action"));
        });
        var request = Request();

        var result = await service.CreatePaymentIntentAsync(request);

        capturedCreate.Should().BeEquivalentTo(new
        {
            Amount = 250L,
            Currency = "usd",
            Confirm = false,
            Description = "Economy HardCoin top-up",
            Metadata = request.Metadata
        });
        capturedCreate!.AutomaticPaymentMethods!.Enabled.Should().BeTrue();
        capturedRequest!.IdempotencyKey.Should().Be("economy-top-up:key");
        capturedRequest.StripeAccount.Should().BeNull();
        result.Should().Be(new GatewayPaymentIntentSetupResult(
            "pi_topup",
            PaymentStatus.RequiresAction,
            "pi_topup_secret",
            new GatewayProviderMapping("test", "acct_platform", "pi_topup", "payment_intent", "capture")));
        result.TransactionId.Should().Be("pi_topup");
        result.ClientSecret.Should().Be("pi_topup_secret");
        result.ProviderMapping.Should().Be(new GatewayProviderMapping(
            "test",
            "acct_platform",
            "pi_topup",
            "payment_intent",
            "capture"));
    }

    [Theory]
    [InlineData("requires_payment_method", PaymentStatus.RequiresAction)]
    [InlineData("requires_confirmation", PaymentStatus.RequiresAction)]
    [InlineData("requires_action", PaymentStatus.RequiresAction)]
    [InlineData("processing", PaymentStatus.Processing)]
    public async Task CreatePaymentIntentAsync_MapsEverySupportedInitialStatus(
        string providerStatus,
        PaymentStatus expected)
    {
        var result = await Service((_, _, _) => Task.FromResult(Intent(providerStatus)))
            .CreatePaymentIntentAsync(Request());

        result.Status.Should().Be(expected);
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_ClassifiesAmbiguousAndDefinitiveStripeFailures()
    {
        var ambiguous = Service((_, _, _) => throw new StripeException("Connection reset"));
        var ambiguousResult = await ambiguous.CreatePaymentIntentAsync(Request());
        ambiguousResult.OutcomeUnknown.Should().BeTrue();
        ambiguousResult.Status.Should().Be(PaymentStatus.Processing);
        ambiguousResult.ErrorCode.Should().Be("stripe_error");
        ambiguousResult.ErrorMessage.Should().Be("Connection reset");

        var providerError = new StripeException(
            HttpStatusCode.BadRequest,
            new StripeError { Type = "card_error", Code = "card_declined", Message = "Declined" },
            "Request failed");
        var definitive = Service((_, _, _) => throw providerError);
        var definitiveResult = await definitive.CreatePaymentIntentAsync(Request());
        definitiveResult.OutcomeUnknown.Should().BeFalse();
        definitiveResult.Status.Should().Be(PaymentStatus.Failed);
        definitiveResult.ErrorCode.Should().Be("card_declined");
        definitiveResult.ErrorMessage.Should().Be("Declined");
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_RejectsSimulationAndEveryMalformedRequest()
    {
        var simulation = new StripePaymentService(
            Microsoft.Extensions.Options.Options.Create(new StripeGatewayOptions { UseSimulation = true }),
            NullLogger<StripePaymentService>.Instance);
        await FluentActions.Awaiting(() => simulation.CreatePaymentIntentAsync(Request()))
            .Should().ThrowAsync<InvalidOperationException>();

        var service = Service((_, _, _) => Task.FromResult(Intent("requires_action")));
        GatewayPaymentIntentSetupRequest[] invalid =
        [
            Request() with { IdempotencyKey = "" },
            Request() with { Amount = 0 },
            Request() with { Currency = "" },
            Request() with { Description = "" },
            Request() with { Metadata = null! }
        ];
        await FluentActions.Awaiting(() => service.CreatePaymentIntentAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
        foreach (var request in invalid)
            await FluentActions.Awaiting(() => service.CreatePaymentIntentAsync(request))
                .Should().ThrowAsync<ArgumentException>();
        FluentActions.Invoking(() => new StripePaymentService(
                Microsoft.Extensions.Options.Options.Create(Options()),
                NullLogger<StripePaymentService>.Instance,
                null!))
            .Should().Throw<ArgumentNullException>();
    }

    private static StripePaymentService Service(
        Func<PaymentIntentCreateOptions, RequestOptions, CancellationToken, Task<PaymentIntent>> create) => new(
        Microsoft.Extensions.Options.Options.Create(Options()),
        NullLogger<StripePaymentService>.Instance,
        create);

    private static StripeGatewayOptions Options() => new()
    {
        IsEnabled = true,
        UseSimulation = false,
        ApiKey = "sk_test",
        PublishableKey = "pk_test",
        AccountId = "acct_platform"
    };

    private static GatewayPaymentIntentSetupRequest Request() => new(
        "economy-top-up:key",
        2.50m,
        "USD",
        "Economy HardCoin top-up",
        new Dictionary<string, string> { ["purpose"] = "economy_hard_coin_top_up" });

    private static PaymentIntent Intent(string status) => new()
    {
        Id = "pi_topup",
        Status = status,
        ClientSecret = "pi_topup_secret"
    };
}
