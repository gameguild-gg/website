using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Stripe;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Services;

public sealed class StripeCustomerLoggingSecurityTests
{
    private const string SensitiveEmail = "private-customer@example.invalid";
    private const string CustomerId = "cus_private\r\nforged\u2028entry";
    private const string PaymentMethodId = "pm_private\r\nforged\u2029entry";
    private const string SetupIntentId = "seti_private\r\nforged";
    private const string SubscriptionId = "sub_private\r\nforged";
    private const string ErrorCode = "provider_error\r\nforged\u2028entry";

    [Theory]
    [InlineData("customer", "success")]
    [InlineData("payment-method", "success")]
    [InlineData("setup-intent", "success")]
    [InlineData("default-method", "success")]
    [InlineData("cancellation", "success")]
    [InlineData("customer", "stripe-error")]
    [InlineData("payment-method", "stripe-error")]
    [InlineData("setup-intent", "stripe-error")]
    [InlineData("default-method", "stripe-error")]
    [InlineData("cancellation", "stripe-error")]
    [InlineData("customer", "unexpected-error")]
    [InlineData("payment-method", "unexpected-error")]
    [InlineData("setup-intent", "unexpected-error")]
    [InlineData("default-method", "unexpected-error")]
    [InlineData("cancellation", "unexpected-error")]
    public async Task Operation_ProtectsLogsWhilePreservingProviderRequestsAndResults(string operation, string outcome)
    {
        var clientSecret = Guid.NewGuid().ToString("N");
        var logger = new CapturingStripeLogger();
        var client = new Mock<IStripeClient>(MockBehavior.Strict);
        var sensitiveMessage = $"{SensitiveEmail} {clientSecret} private-name private-phone\r\nforged";
        Exception? failure = outcome switch
        {
            "success" => null,
            "stripe-error" => new StripeException(HttpStatusCode.BadRequest,
                new StripeError { Code = ErrorCode, Message = sensitiveMessage }, sensitiveMessage),
            "unexpected-error" => new InvalidOperationException(sensitiveMessage),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
        var requests = new List<(HttpMethod Method, string Path, BaseOptions Options, RequestOptions? RequestOptions, CancellationToken Cancellation)>();
        Configure(client, new Customer { Id = CustomerId }, failure, requests);
        Configure(client, new PaymentMethod { Id = PaymentMethodId }, failure, requests);
        Configure(client, new SetupIntent { Id = SetupIntentId, ClientSecret = clientSecret }, failure, requests);
        Configure(client, new Subscription { Id = SubscriptionId, Status = "canceled" }, failure, requests);
        var service = new StripeCustomerService(Options.Create(new StripeGatewayOptions { UseSimulation = false }), logger, client.Object);
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var expectedCode = outcome == "stripe-error" ? ErrorCode : outcome == "unexpected-error" ? "unexpected_error" : null;
        var expectedMessage = failure is null ? null : sensitiveMessage;

        switch (operation)
        {
            case "customer":
                var customer = await service.CreateCustomerAsync(new GatewayCustomerRequest(SensitiveEmail, "private-name", "private-phone"), token);
                customer.Success.Should().Be(failure is null);
                customer.ExternalCustomerId.Should().Be(failure is null ? CustomerId : null);
                customer.ErrorCode.Should().Be(expectedCode);
                customer.ErrorMessage.Should().Be(expectedMessage);
                if (failure is null)
                {
                    requests.Single().Options.Should().BeOfType<CustomerCreateOptions>().Which
                        .Should().BeEquivalentTo(new { Email = SensitiveEmail, Name = "private-name", Phone = "private-phone" });
                }
                break;
            case "payment-method":
                var method = await service.CreatePaymentMethodAsync(new GatewayPaymentMethodRequest(CustomerId, PaymentMethodId), token);
                method.Success.Should().Be(failure is null);
                method.ExternalPaymentMethodId.Should().Be(failure is null ? PaymentMethodId : null);
                method.ErrorCode.Should().Be(expectedCode);
                method.ErrorMessage.Should().Be(expectedMessage);
                if (failure is null)
                {
                    requests.Should().HaveCount(2);
                    ((PaymentMethodAttachOptions)requests[0].Options).Customer.Should().Be(CustomerId);
                    ((CustomerUpdateOptions)requests[1].Options).InvoiceSettings.DefaultPaymentMethod.Should().Be(PaymentMethodId);
                }
                break;
            case "setup-intent":
                var setup = await service.CreateSetupIntentAsync(new GatewaySetupIntentRequest(CustomerId), token);
                setup.Success.Should().Be(failure is null);
                setup.ExternalSetupIntentId.Should().Be(failure is null ? SetupIntentId : null);
                setup.ClientSecret.Should().Be(failure is null ? clientSecret : null);
                setup.CustomerId.Should().Be(failure is null || failure is StripeException ? CustomerId : null);
                setup.ErrorCode.Should().Be(expectedCode);
                setup.ErrorMessage.Should().Be(expectedMessage);
                if (failure is null)
                {
                    ((SetupIntentCreateOptions)requests.Single().Options).Customer.Should().Be(CustomerId);
                }
                break;
            case "default-method":
                var update = await service.SetDefaultPaymentMethodAsync(new GatewayDefaultPaymentMethodRequest(CustomerId, PaymentMethodId), token);
                update.Success.Should().Be(failure is null);
                update.ErrorCode.Should().Be(expectedCode);
                update.ErrorMessage.Should().Be(expectedMessage);
                if (failure is null)
                {
                    ((CustomerUpdateOptions)requests.Single().Options).InvoiceSettings.DefaultPaymentMethod.Should().Be(PaymentMethodId);
                }
                break;
            case "cancellation":
                var cancel = await service.CancelSubscriptionAsync(SubscriptionId, token);
                cancel.Success.Should().Be(failure is null);
                cancel.ErrorCode.Should().Be(expectedCode);
                cancel.ErrorMessage.Should().Be(expectedMessage);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }

        requests.Should().NotBeEmpty().And.OnlyContain(request => request.Cancellation == token);
        logger.Entries.Should().NotBeEmpty().And.OnlyContain(entry => entry.EventId.Id == 0);
        logger.Scopes.Should().BeEmpty();
        foreach (var entry in logger.Entries)
        {
            entry.Exception.Should().BeNull("provider exceptions can contain contact data, tokens and untrusted separators");
            var logged = entry.Rendered + "|" + string.Join("|", entry.Properties.Select(property => property.Value));
            foreach (var sensitive in new[] { SensitiveEmail, CustomerId, PaymentMethodId, SetupIntentId, SubscriptionId, clientSecret, "private-name", "private-phone" })
            {
                logged.Should().NotContain(sensitive);
            }
            logged.Should().NotContain("\r").And.NotContain("\n").And.NotContain("\u2028").And.NotContain("\u2029");
        }
        if (failure is not null)
        {
            logger.Entries.Last().Properties.Should().Contain(property =>
                property.Key == "ErrorType" && Equals(property.Value, failure.GetType().FullName));
        }
    }

    private static void Configure<T>(Mock<IStripeClient> client, T response, Exception? failure,
        List<(HttpMethod Method, string Path, BaseOptions Options, RequestOptions? RequestOptions, CancellationToken Cancellation)> requests)
        where T : IStripeEntity
    {
        client.Setup(value => value.RequestAsync<T>(It.IsAny<HttpMethod>(), It.IsAny<string>(), It.IsAny<BaseOptions>(),
                It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<HttpMethod, string, BaseOptions, RequestOptions, CancellationToken>((method, path, options, requestOptions, cancellation) =>
                requests.Add((method, path, options, requestOptions, cancellation)))
            .Returns(() => failure is null ? Task.FromResult(response) : Task.FromException<T>(failure));
    }

    private sealed class CapturingStripeLogger : ILogger<StripeCustomerService>
    {
        public List<LogEntry> Entries { get; } = [];
        public List<object> Scopes { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            Scopes.Add(state);
            return new Scope();
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                var properties = state is IEnumerable<KeyValuePair<string, object?>> values ? values.ToArray() : [];
                Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception), properties, exception));
            }
        }
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose()
        {
            // The capture-only scope owns no resources or ambient state to release.
        }
    }

    private sealed record LogEntry(LogLevel Level, EventId EventId, string Rendered,
        IReadOnlyList<KeyValuePair<string, object?>> Properties, Exception? Exception);
}
