using FluentAssertions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Queries;

public sealed class ListBillingEventsQueryHandlerTests
{
    private readonly Mock<IBillingWebhookRepository> _webhookRepository = new();
    private readonly ListBillingEventsQueryHandler _handler;

    public ListBillingEventsQueryHandlerTests()
    {
        _handler = new ListBillingEventsQueryHandler(_webhookRepository.Object);
    }

    [Fact]
    public async Task Handle_ReturnsPagedResultWithMetadata()
    {
        var tenantId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var events = new[]
        {
            new BillingWebhookEvent
            {
                Provider = PaymentProviders.Stripe,
                ExternalEventId = "evt_1",
                EventType = "invoice.payment_succeeded",
                TenantId = tenantId,
                SubscriptionId = subscriptionId
            }
        };
        _webhookRepository
            .Setup(repository => repository.SearchEventsAsync(
                It.IsAny<BillingWebhookEventSearchCriteria>(), 20, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((events, 45));

        var result = await _handler.Handle(new ListBillingEventsQuery(Skip: 20, Take: 20), CancellationToken.None);

        result.TotalCount.Should().Be(45);
        result.Skip.Should().Be(20);
        result.Take.Should().Be(20);
        result.PageNumber.Should().Be(2);
        result.TotalPages.Should().Be(3);
        var item = result.Items.Should().ContainSingle().Subject;
        item.Provider.Should().Be(PaymentProviders.Stripe);
        item.ExternalEventId.Should().Be("evt_1");
        item.TenantId.Should().Be(tenantId);
        item.SubscriptionId.Should().Be(subscriptionId);
    }

    [Fact]
    public async Task Handle_NormalizesStatusProviderAndEventTypeFilters()
    {
        _webhookRepository
            .Setup(repository => repository.SearchEventsAsync(
                It.Is<BillingWebhookEventSearchCriteria>(criteria =>
                    criteria.Status == BillingEventStatusFilter.Failed
                    && criteria.Provider == PaymentProviders.Stripe
                    && criteria.EventType == "invoice.payment_failed"),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(([], 0));

        await _handler.Handle(new ListBillingEventsQuery(
            Status: "FAILED", Provider: "Stripe", EventType: " invoice.payment_failed "), CancellationToken.None);

        _webhookRepository.VerifyAll();
    }

    [Fact]
    public async Task Handle_RejectsInvalidStatusFilter()
    {
        var validator = new ListBillingEventsQueryValidator();
        var validationResult = validator.Validate(new ListBillingEventsQuery(Status: "bogus"));

        validationResult.IsValid.Should().BeFalse(
            "unknown status filters must fail validation instead of silently returning everything");
    }

    [Fact]
    public async Task Handle_RejectsPagingOutsideBounds()
    {
        var validator = new ListBillingEventsQueryValidator();

        validator.Validate(new ListBillingEventsQuery(Skip: -1)).IsValid.Should().BeFalse();
        validator.Validate(new ListBillingEventsQuery(Take: 0)).IsValid.Should().BeFalse();
        validator.Validate(new ListBillingEventsQuery(Take: 101)).IsValid.Should().BeFalse();
        validator.Validate(new ListBillingEventsQuery(Skip: 0, Take: 100)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_RejectsInvertedDateRange()
    {
        var validator = new ListBillingEventsQueryValidator();
        var validationResult = validator.Validate(new ListBillingEventsQuery(
            FromUtc: DateTimeOffset.UtcNow,
            ToUtc: DateTimeOffset.UtcNow.AddHours(-1)));

        validationResult.IsValid.Should().BeFalse("toUtc must not precede fromUtc");
    }
}

public sealed class ListBillingOutboxEventsQueryHandlerTests
{
    private readonly Mock<IBillingOutboxEventReader> _outboxReader = new();
    private readonly ListBillingOutboxEventsQueryHandler _handler;

    public ListBillingOutboxEventsQueryHandlerTests()
    {
        _handler = new ListBillingOutboxEventsQueryHandler(_outboxReader.Object);
    }

    [Fact]
    public async Task Handle_PagesThroughTheOutboxReadModel()
    {
        var items = new[]
        {
            new BillingOutboxEventDto(
                Guid.NewGuid(), "commerce.billing.webhook-failed.v1",
                "GameGuild.Commerce.Billing.BillingWebhookFailedV1",
                "BillingWebhookEvent", Guid.NewGuid().ToString(), Guid.NewGuid(), Guid.NewGuid(),
                BillingOutboxEventStatus.DeadLettered, DateTimeOffset.UtcNow, 1)
        };
        _outboxReader
            .Setup(reader => reader.CountAsync(
                "commerce.billing.webhook-failed.v1",
                BillingOutboxEventStatus.DeadLettered,
                It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _outboxReader
            .Setup(reader => reader.ListAsync(
                0, 20,
                "commerce.billing.webhook-failed.v1",
                BillingOutboxEventStatus.DeadLettered,
                It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var result = await _handler.Handle(new ListBillingOutboxEventsQuery(
            EventName: "commerce.billing.webhook-failed.v1", Status: "deadlettered"), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle()
            .Which.Status.Should().Be(BillingOutboxEventStatus.DeadLettered);
        _outboxReader.VerifyAll();
    }

    [Fact]
    public async Task Handle_RecognizesAlternateDeadLetterSpelling()
    {
        _outboxReader
            .Setup(reader => reader.ListAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(),
                BillingOutboxEventStatus.DeadLettered,
                It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _handler.Handle(new ListBillingOutboxEventsQuery(Status: "dead-lettered"), CancellationToken.None);

        _outboxReader.VerifyAll();
    }

    [Fact]
    public async Task Handle_IgnoresUnknownStatusAsNoFilter()
    {
        _outboxReader
            .Setup(reader => reader.ListAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(),
                null,
                It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _handler.Handle(new ListBillingOutboxEventsQuery(Status: "bogus"), CancellationToken.None);

        _outboxReader.VerifyAll();
    }

    [Fact]
    public async Task GetBillingOutboxEvent_DelegatesToTheReader()
    {
        var eventId = Guid.NewGuid();
        var expected = new BillingOutboxEventDto(
            eventId, "commerce.billing.invoice-paid.v1",
            "GameGuild.Commerce.Billing.BillingInvoicePaidV1",
            "Subscription", Guid.NewGuid().ToString(), Guid.NewGuid(), Guid.NewGuid(),
            BillingOutboxEventStatus.Pending, DateTimeOffset.UtcNow, 1);
        _outboxReader
            .Setup(reader => reader.GetByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetBillingOutboxEventQueryHandler(_outboxReader.Object);
        var result = await handler.Handle(new GetBillingOutboxEventQuery(eventId), CancellationToken.None);

        result.Should().Be(expected);
    }
}
