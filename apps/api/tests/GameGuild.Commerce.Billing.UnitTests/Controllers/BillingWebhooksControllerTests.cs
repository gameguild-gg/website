using System.Reflection;
using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Controllers;

public class BillingWebhooksControllerTests
{
    [Fact]
    public async Task HandleGooglePayWebhook_Should_Reject_Missing_Authorization()
    {
        var sender = new Mock<ISender>();
        var controller = CreateController(sender.Object, "{}", new Dictionary<string, string>
        {
            ["Google-Cloud-Project-Id"] = "project"
        });

        var result = await controller.HandleGooglePayWebhook(CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task HandleGooglePayWebhook_Should_Reject_Missing_ProjectId()
    {
        var sender = new Mock<ISender>();
        var controller = CreateController(sender.Object, "{}", new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer token"
        });

        var result = await controller.HandleGooglePayWebhook(CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task HandleGooglePayWebhook_Should_Return_Ok_When_Processed()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<ProcessGooglePayWebhookCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookProcessingResult { Processed = true });

        var controller = CreateController(sender.Object, "{}", new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer token",
            ["Google-Cloud-Project-Id"] = "project"
        });

        var result = await controller.HandleGooglePayWebhook(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task HandleApplePayWebhook_Should_Reject_Missing_MerchantId()
    {
        var sender = new Mock<ISender>();
        var controller = CreateController(sender.Object, "{}", new Dictionary<string, string>
        {
            ["Apple-Pay-Signature"] = "sig"
        });

        var result = await controller.HandleApplePayWebhook(CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task HandleApplePayWebhook_Should_Return_Ok_When_Processed()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<ProcessApplePayWebhookCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebhookProcessingResult.Success("evt"));

        var controller = CreateController(sender.Object, "{}", new Dictionary<string, string>
        {
            ["Apple-Pay-Merchant-Id"] = "merchant",
            ["Apple-Pay-Signature"] = "sig"
        });

        var result = await controller.HandleApplePayWebhook(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task HandleStripeWebhook_Should_Reject_Missing_Signature()
    {
        var sender = new Mock<ISender>();
        var controller = CreateController(sender.Object, "{}", new Dictionary<string, string>());

        var result = await controller.HandleStripeWebhook(CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task HandleStripeWebhook_Should_Return_BadRequest_When_Signature_Invalid()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<ProcessStripeWebhookCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidWebhookSignatureException("bad"));

        var controller = CreateController(sender.Object, "{}", new Dictionary<string, string>
        {
            ["Stripe-Signature"] = "sig"
        });

        var result = await controller.HandleStripeWebhook(CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task HandleStripeWebhook_Should_Return_BadRequest_When_Verified_Payload_Is_Invalid()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<ProcessStripeWebhookCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidWebhookPayloadException("mismatch"));
        var controller = CreateController(sender.Object, "{}", new Dictionary<string, string>
        {
            ["Stripe-Signature"] = "sig"
        });

        var result = await controller.HandleStripeWebhook(CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task HandleStripeWebhook_Should_Return_500_When_Inbox_Or_Processing_Fails()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<ProcessStripeWebhookCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebhookProcessingResult.Failed("evt", "retry"));
        var controller = CreateController(sender.Object, "{}", new Dictionary<string, string>
        {
            ["Stripe-Signature"] = "sig"
        });

        var result = await controller.HandleStripeWebhook(CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task HandlePayPalWebhook_Should_Reject_Missing_Headers()
    {
        var sender = new Mock<ISender>();
        var controller = CreateController(sender.Object, "{}", new Dictionary<string, string>());

        var result = await controller.HandlePayPalWebhook(CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task HandlePayPalWebhook_Should_Return_Ok_When_Processed()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<ProcessPayPalWebhookCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebhookProcessingResult.Success("evt"));

        var controller = CreateController(sender.Object, "{}", new Dictionary<string, string>
        {
            ["PayPal-Transmission-Id"] = "tx",
            ["PayPal-Transmission-Time"] = "time",
            ["PayPal-Transmission-Sig"] = "sig"
        });

        var result = await controller.HandlePayPalWebhook(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task HandlePayPalWebhook_Should_Return_500_When_Processing_Fails()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<ProcessPayPalWebhookCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebhookProcessingResult.Failed("evt", "oops"));

        var controller = CreateController(sender.Object, "{}", new Dictionary<string, string>
        {
            ["PayPal-Transmission-Id"] = "tx",
            ["PayPal-Transmission-Time"] = "time",
            ["PayPal-Transmission-Sig"] = "sig"
        });

        var result = await controller.HandlePayPalWebhook(CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetWebhookEvent_Should_Return_NotFound_When_Missing()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<GetWebhookEventQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEventDto?)null);

        var controller = CreateController(sender.Object, "{}", new Dictionary<string, string>());

        var result = await controller.GetWebhookEvent("evt", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task RetryWebhookEvent_Should_Return_Ok_When_Success()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<RetryWebhookEventCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookRetryResult { Success = true });

        var controller = CreateController(sender.Object, "{}", new Dictionary<string, string>());

        var result = await controller.RetryWebhookEvent("evt", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetWebhookSecuritySummary_Should_Return_The_Summary()
    {
        var summary = new BillingWebhookSecuritySummaryDto
        {
            SourceIpAllowlist = new BillingWebhookAllowlistStatusDto
            {
                IsEnabled = true,
                ConfiguredNetworkCount = 1,
                ConfiguredNetworks = ["203.0.113.0/24"]
            }
        };
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<GetBillingWebhookSecuritySummaryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        var controller = CreateController(sender.Object, "{}", new Dictionary<string, string>());

        var result = await controller.GetWebhookSecuritySummary(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Which;
        ok.Value.Should().BeSameAs(summary);
    }

    // ── Endpoint authorization metadata (regression guard for the anonymous split) ──

    [Fact]
    public void Only_The_Provider_Callbacks_Are_Anonymous()
    {
        var controllerType = typeof(BillingWebhooksController);

        controllerType.IsDefined(typeof(AllowAnonymousAttribute), true).Should().BeFalse(
            "the controller must not be class-level anonymous: inspection, retry, and the security surface are admin-only");

        var anonymousActions = controllerType
            .GetMethods()
            .Where(method => method.IsDefined(typeof(AllowAnonymousAttribute), true))
            .Select(method => method.Name)
            .ToList();

        anonymousActions.Should().BeEquivalentTo(
            [nameof(BillingWebhooksController.HandleGooglePayWebhook),
             nameof(BillingWebhooksController.HandleApplePayWebhook),
             nameof(BillingWebhooksController.HandleStripeWebhook),
             nameof(BillingWebhooksController.HandlePayPalWebhook)]);
    }

    [Fact]
    public void Inspection_Retry_And_Security_Surface_Require_The_SystemAdmin_Policy()
    {
        var controllerType = typeof(BillingWebhooksController);

        var adminActions = controllerType
            .GetMethods()
            .Where(method => method.IsDefined(typeof(AuthorizeAttribute), true))
            .Select(method => method.Name)
            .ToList();

        adminActions.Should().BeEquivalentTo(
            [nameof(BillingWebhooksController.GetWebhookEvent),
             nameof(BillingWebhooksController.RetryWebhookEvent),
             nameof(BillingWebhooksController.GetWebhookSecuritySummary)]);

        foreach (var method in controllerType.GetMethods()
                     .Where(method => method.IsDefined(typeof(AuthorizeAttribute), true)))
        {
            var authorize = method.GetCustomAttribute<AuthorizeAttribute>();
            authorize!.Policy.Should().Be("SystemAdmin", $"{method.Name} must be SystemAdmin-guarded");
        }
    }

    [Fact]
    public void Provider_Callbacks_Use_The_Webhook_Rate_Limit_Policy_And_The_Source_Security_Filter()
    {
        var controllerType = typeof(BillingWebhooksController);
        var callbacks = new[]
        {
            nameof(BillingWebhooksController.HandleGooglePayWebhook),
            nameof(BillingWebhooksController.HandleApplePayWebhook),
            nameof(BillingWebhooksController.HandleStripeWebhook),
            nameof(BillingWebhooksController.HandlePayPalWebhook)
        };

        foreach (var callback in callbacks)
        {
            var method = controllerType.GetMethod(callback)!;
            var rateLimit = method.GetCustomAttribute<EnableRateLimitingAttribute>();
            rateLimit.Should().NotBeNull($"{callback} must carry a rate limit policy");
            rateLimit!.PolicyName.Should().Be(RateLimitPolicies.Webhook);

            var serviceFilter = method.GetCustomAttribute<ServiceFilterAttribute>();
            serviceFilter.Should().NotBeNull($"{callback} must enforce the webhook source security filter");
            serviceFilter!.ServiceType.Should().Be(typeof(WebhookSourceSecurityFilter));
        }
    }

    private static BillingWebhooksController CreateController(ISender sender, string body, IDictionary<string, string> headers)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        foreach (var header in headers)
        {
            context.Request.Headers[header.Key] = header.Value;
        }

        return new BillingWebhooksController(sender, NullLogger<BillingWebhooksController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }
}
