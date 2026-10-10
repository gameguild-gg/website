using System.Reflection;
using Asp.Versioning;
using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Controllers;

public sealed class BillingEventsControllerTests
{
    private readonly Mock<ISender> _sender = new();

    private BillingEventsController CreateController() => new(_sender.Object)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    // ── Authorization and routing surface (fail-closed admin monitoring) ──────────

    [Fact]
    public void Controller_RequiresSystemAdminPolicy()
    {
        var authorizeAttribute = typeof(BillingEventsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        authorizeAttribute.Should().NotBeNull(
            "every billing events endpoint must declare an authorization decision");
        authorizeAttribute!.Policy.Should().Be(Policies.SystemAdmin,
            "billing event monitoring matches the sibling billing webhook inspection policy");
    }

    [Fact]
    public void Controller_IsVersionedOnTheBillingEventsRoute()
    {
        typeof(BillingEventsController)
            .GetCustomAttribute<ApiVersionAttribute>()!
            .Versions.Select(version => version.ToString()).Should().Contain("1.0");
        typeof(BillingEventsController)
            .GetCustomAttribute<RouteAttribute>()!
            .Template.Should().Be("api/v{version:apiVersion}/billing/events");
    }

    [Fact]
    public void AllActions_AreCoveredByTheControllerLevelAuthorizationDecision()
    {
        var httpMethods = new[] { "HttpGet", "HttpPost", "HttpPut", "HttpPatch", "HttpDelete" };

        var publicActions = typeof(BillingEventsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes()
                .Any(attribute => httpMethods.Any(name => attribute.GetType().Name.StartsWith(name, StringComparison.Ordinal))))
            .ToArray();

        publicActions.Should().NotBeEmpty();
        foreach (var action in publicActions)
        {
            action.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull(
                "no billing events endpoint is anonymous");
        }
    }

    // ── Webhook inbox feed ────────────────────────────────────────────────────────

    [Fact]
    public async Task ListEvents_ReturnsPagedFeedFromQuery()
    {
        var items = new[]
        {
            new BillingWebhookEventListItemDto(
                Guid.NewGuid(), PaymentProviders.Stripe, "evt_1", "invoice.payment_succeeded",
                IsProcessed: true, IsFailed: false, ProcessingAttempts: 1,
                ErrorMessage: null, ProcessedAt: SystemClock.UtcNow, CreatedAt: SystemClock.UtcNow,
                TenantId: Guid.NewGuid(), SubscriptionId: Guid.NewGuid())
        };
        var paged = new PagedResult<BillingWebhookEventListItemDto>(items, 42, skip: 20, take: 20);
        _sender
            .Setup(sender => sender.Send(
                It.Is<ListBillingEventsQuery>(query =>
                    query.Status == "failed"
                    && query.Provider == "stripe"
                    && query.EventType == "invoice.payment_failed"
                    && query.Skip == 20
                    && query.Take == 20),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await CreateController().ListEvents(
            status: "failed", provider: "stripe", eventType: "invoice.payment_failed",
            skip: 20, take: 20);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeAssignableTo<PagedResult<BillingWebhookEventListItemDto>>().Subject;
        body.TotalCount.Should().Be(42);
        body.PageNumber.Should().Be(2);
        body.Items.Should().ContainSingle().Which.Provider.Should().Be(PaymentProviders.Stripe);
        _sender.VerifyAll();
    }

    [Fact]
    public async Task GetEvent_ReturnsNotFound_WhenInboxEventIsUnknown()
    {
        _sender
            .Setup(sender => sender.Send(It.IsAny<GetWebhookEventQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEventDto?)null);

        var result = await CreateController().GetEvent(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetEvent_ReturnsInboxEvent_WhenFound()
    {
        var dto = new BillingWebhookEventDto
        {
            Id = Guid.NewGuid(),
            Provider = PaymentProviders.Stripe,
            ExternalEventId = "evt_1"
        };
        _sender
            .Setup(sender => sender.Send(It.IsAny<GetWebhookEventQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await CreateController().GetEvent(dto.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(dto);
    }

    // ── Durable outbox feed ───────────────────────────────────────────────────────

    [Fact]
    public async Task ListOutboxEvents_ReturnsPagedFeedFromQuery()
    {
        var items = new[]
        {
            new BillingOutboxEventDto(
                Guid.NewGuid(), "commerce.billing.invoice-paid.v1",
                "GameGuild.Commerce.Billing.BillingInvoicePaidV1",
                "Subscription", Guid.NewGuid().ToString(), Guid.NewGuid(), Guid.NewGuid(),
                BillingOutboxEventStatus.Completed, DateTimeOffset.UtcNow, 1)
        };
        var paged = new PagedResult<BillingOutboxEventDto>(items, 7, skip: 0, take: 20);
        _sender
            .Setup(sender => sender.Send(
                It.Is<ListBillingOutboxEventsQuery>(query =>
                    query.EventName == "commerce.billing.invoice-paid.v1"
                    && query.Status == "completed"
                    && query.Skip == 0
                    && query.Take == 20),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await CreateController().ListOutboxEvents(
            eventName: "commerce.billing.invoice-paid.v1", status: "completed");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeAssignableTo<PagedResult<BillingOutboxEventDto>>().Subject;
        body.TotalCount.Should().Be(7);
        body.Items.Should().ContainSingle().Which.Status.Should().Be(BillingOutboxEventStatus.Completed);
        _sender.VerifyAll();
    }

    [Fact]
    public async Task GetOutboxEvent_ReturnsNotFound_WhenEventIsUnknown()
    {
        _sender
            .Setup(sender => sender.Send(It.IsAny<GetBillingOutboxEventQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingOutboxEventDto?)null);

        var result = await CreateController().GetOutboxEvent(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetOutboxEvent_ReturnsEvent_WhenFound()
    {
        var dto = new BillingOutboxEventDto(
            Guid.NewGuid(), "commerce.billing.webhook-processed.v1",
            "GameGuild.Commerce.Billing.BillingWebhookProcessedV1",
            "BillingWebhookEvent", Guid.NewGuid().ToString(), Guid.NewGuid(), Guid.NewGuid(),
            BillingOutboxEventStatus.Pending, DateTimeOffset.UtcNow, 1);
        _sender
            .Setup(sender => sender.Send(It.IsAny<GetBillingOutboxEventQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await CreateController().GetOutboxEvent(dto.EventId, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(dto);
    }
}
