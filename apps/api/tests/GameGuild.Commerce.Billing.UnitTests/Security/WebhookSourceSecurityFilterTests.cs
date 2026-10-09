using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Security;

public class WebhookSourceSecurityFilterTests
{
    [Fact]
    public async Task Allowed_Source_Passes_Through()
    {
        var (filter, _, _, publisher) = CreateFilter(
            allowlistEntries: ["203.0.113.0/24"],
            remoteIp: IPAddress.Parse("203.0.113.9"),
            path: "/api/v1/billing/webhooks/stripe");

        var context = CreateContext(IPAddress.Parse("203.0.113.9"), "/api/v1/billing/webhooks/stripe");
        await filter.OnAuthorizationAsync(context);

        context.Result.Should().BeNull("the request should continue to the controller action");
        publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Disabled_Allowlist_Lets_Every_Source_Through()
    {
        var (filter, _, monitor, publisher) = CreateFilter(
            allowlistEntries: [],
            remoteIp: IPAddress.Parse("198.51.100.1"),
            path: "/api/v1/billing/webhooks/paypal");

        var context = CreateContext(IPAddress.Parse("198.51.100.1"), "/api/v1/billing/webhooks/paypal");
        await filter.OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        publisher.VerifyNoOtherCalls();
        monitor.Verify(m => m.IsBlocked("198.51.100.1", It.IsAny<DateTime>()), Times.Once);
        monitor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Disallowed_Source_Is_Rejected_With_403_Before_The_Payload_Is_Read()
    {
        var (filter, _, _, publisher) = CreateFilter(
            allowlistEntries: ["203.0.113.0/24"],
            remoteIp: IPAddress.Parse("198.51.100.1"),
            path: "/api/v1/billing/webhooks/stripe");

        var context = CreateContext(IPAddress.Parse("198.51.100.1"), "/api/v1/billing/webhooks/stripe");
        await filter.OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        publisher.Verify(p => p.PublishAsync(
            WebhookSecurityEventKind.SourceIpRejected,
            "stripe",
            "198.51.100.1",
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Currently_Blocked_Source_Is_Rejected_With_403_And_A_SourceBlocked_Event()
    {
        var (filter, _, _, publisher) = CreateFilter(
            allowlistEntries: [],
            remoteIp: IPAddress.Parse("198.51.100.1"),
            path: "/api/v1/billing/webhooks/apple-pay",
            isBlocked: true);

        var context = CreateContext(IPAddress.Parse("198.51.100.1"), "/api/v1/billing/webhooks/apple-pay");
        await filter.OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        publisher.Verify(p => p.PublishAsync(
            WebhookSecurityEventKind.SourceBlocked,
            "apple-pay",
            "198.51.100.1",
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Allowlist_Rejection_Registers_A_Failure_With_The_Suspicious_Activity_Monitor()
    {
        var (filter, _, monitor, _) = CreateFilter(
            allowlistEntries: ["203.0.113.0/24"],
            remoteIp: IPAddress.Parse("198.51.100.1"),
            path: "/api/v1/billing/webhooks/stripe");

        var context = CreateContext(IPAddress.Parse("198.51.100.1"), "/api/v1/billing/webhooks/stripe");
        await filter.OnAuthorizationAsync(context);

        monitor.Verify(m => m.RegisterFailure("198.51.100.1", It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task Publisher_Failures_Never_Change_The_Rejection_Decision()
    {
        var (filter, _, _, publisher) = CreateFilter(
            allowlistEntries: ["203.0.113.0/24"],
            remoteIp: IPAddress.Parse("198.51.100.1"),
            path: "/api/v1/billing/webhooks/stripe");
        publisher
            .Setup(p => p.PublishAsync(
                It.IsAny<WebhookSecurityEventKind>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("pipeline down"));

        var context = CreateContext(IPAddress.Parse("198.51.100.1"), "/api/v1/billing/webhooks/stripe");
        var act = () => filter.OnAuthorizationAsync(context);

        await act.Should().NotThrowAsync("security telemetry must never change the webhook response");
        context.Result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    private static (
        WebhookSourceSecurityFilter Filter,
        WebhookSourceIpAllowlist Allowlist,
        Mock<IWebhookSuspiciousActivityMonitor> Monitor,
        Mock<IWebhookSecurityEventPublisher> Publisher) CreateFilter(
        string[] allowlistEntries,
        IPAddress remoteIp,
        string path,
        bool isBlocked = false)
    {
        var allowlist = new WebhookSourceIpAllowlist(Options.Create(new BillingConfiguration
        {
            Webhook = new WebhookSettings
            {
                Security = new WebhookSecuritySettings { SourceIpAllowlist = allowlistEntries }
            }
        }));
        var monitor = new Mock<IWebhookSuspiciousActivityMonitor>();
        monitor
            .Setup(m => m.IsBlocked(It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(isBlocked);
        monitor
            .Setup(m => m.RegisterFailure(It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(false);
        var publisher = new Mock<IWebhookSecurityEventPublisher>();

        var filter = new WebhookSourceSecurityFilter(
            allowlist,
            monitor.Object,
            publisher.Object,
            NullLogger<WebhookSourceSecurityFilter>.Instance);

        return (filter, allowlist, monitor, publisher);
    }

    private static AuthorizationFilterContext CreateContext(IPAddress remoteIp, string path)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = remoteIp;
        httpContext.Request.Path = path;

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        return new AuthorizationFilterContext(actionContext, []);
    }
}
