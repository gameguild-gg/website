using System.Text.Json;
using FluentAssertions;
using GameGuild.Commerce.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.IntegrationTests;

/// <summary>
///     Stripe dispute webhook reconciliation (issue #419): payload parsing, gateway status/reason
///     mapping and the create/update path of <see cref="StripeDisputeEventConsumer"/>.
/// </summary>
public class StripeDisputeWebhookTests
{
    [Theory]
    [InlineData("needs_response", DisputeStatus.Submitted)]
    [InlineData("warning_needs_response", DisputeStatus.Submitted)]
    [InlineData("under_review", DisputeStatus.UnderReview)]
    [InlineData("warning_under_review", DisputeStatus.UnderReview)]
    [InlineData("won", DisputeStatus.Won)]
    [InlineData("lost", DisputeStatus.Lost)]
    [InlineData("charge_refunded", DisputeStatus.Resolved)]
    [InlineData("unknown_future_status", DisputeStatus.UnderReview)]
    public void MapStatus_MapsStripeDisputeStatuses(string stripeStatus, DisputeStatus expected)
    {
        StripeDisputeEventConsumer.MapStatus(stripeStatus).Should().Be(expected);
    }

    [Theory]
    [InlineData("fraudulent", DisputeType.Fraudulent)]
    [InlineData("product_not_received", DisputeType.ProductNotReceived)]
    [InlineData("duplicate", DisputeType.Duplicate)]
    [InlineData("credit_not_processed", DisputeType.CreditNotProcessed)]
    [InlineData("something_new", DisputeType.Other)]
    public void MapType_MapsStripeDisputeReasons(string stripeReason, DisputeType expected)
    {
        StripeDisputeEventConsumer.MapType(stripeReason).Should().Be(expected);
    }

    [Fact]
    public void ParseDisputePayload_ExtractsGatewayFields()
    {
        var payload = DisputeEventPayload(
            eventType: "charge.dispute.created",
            disputeId: "dp_1",
            status: "needs_response",
            reason: "fraudulent",
            amountCents: 12345,
            evidenceDueBy: 1760000000,
            paymentIntent: "pi_1");

        var parsed = StripeDisputeEventConsumer.ParseDisputePayload(payload);

        parsed.Should().NotBeNull();
        parsed!.DisputeId.Should().Be("dp_1");
        parsed.Status.Should().Be("needs_response");
        parsed.Reason.Should().Be("fraudulent");
        parsed.Amount.Should().Be(123.45m);
        parsed.EvidenceDueBy.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1760000000).UtcDateTime);
    }

    [Fact]
    public void ParseDisputePayload_RejectsMalformedPayloads()
    {
        StripeDisputeEventConsumer.ParseDisputePayload("{ not json").Should().BeNull();
        StripeDisputeEventConsumer.ParseDisputePayload("""{"data":{"object":{"object":"dispute"}}}""").Should().BeNull();
    }

    [Fact]
    public async Task TryConsumeAsync_NonDisputeEvents_AreIgnored()
    {
        var consumer = CreateConsumer(out _, out _);

        var handled = await consumer.TryConsumeAsync(VerifiedEvent("invoice.payment_succeeded"), CancellationToken.None);

        handled.Should().BeFalse();
    }

    [Fact]
    public async Task TryConsumeAsync_CreatedEvent_CreatesDisputeLinkedToTheVerifiedPayment()
    {
        var consumer = CreateConsumer(out var db, out var paymentRepository);
        var payment = SucceededBoundPayment();
        paymentRepository
            .Setup(repo => repo.GetByProviderMappingAsync("stripe", "test", "acct_platform", "pi_dispute", "payment_intent", "capture", It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        await using var context = db;

        var handled = await consumer.TryConsumeAsync(
            VerifiedEvent("charge.dispute.created", disputeId: "dp_created", status: "needs_response", amountCents: 5000, evidenceDueBy: 1760000000),
            CancellationToken.None);

        handled.Should().BeTrue();
        var dispute = await context.Set<PaymentDispute>().SingleAsync();
        dispute.PaymentId.Should().Be(payment.Id);
        dispute.GatewayDisputeId.Should().Be("dp_created");
        dispute.GatewayName.Should().Be("stripe");
        dispute.Status.Should().Be(DisputeStatus.Submitted);
        dispute.Amount.Should().Be(50m);
        dispute.EvidenceDueBy.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1760000000).UtcDateTime);
        dispute.DueDate.Should().Be(dispute.EvidenceDueBy);
        payment.Status.Should().Be(PaymentStatus.Disputed);
    }

    [Fact]
    public async Task TryConsumeAsync_UnknownPayment_FailsClosed()
    {
        var consumer = CreateConsumer(out var db, out var paymentRepository);
        paymentRepository
            .Setup(repo => repo.GetByProviderMappingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        paymentRepository
            .Setup(repo => repo.GetByExternalPaymentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        await using var context = db;

        async Task Act() => await consumer.TryConsumeAsync(
            VerifiedEvent("charge.dispute.created", disputeId: "dp_unknown"),
            CancellationToken.None);

        (await Assert.ThrowsAsync<InvalidWebhookPayloadException>(Act)).Should().NotBeNull();
        (await context.Set<PaymentDispute>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task TryConsumeAsync_ClosedEvent_UpdatesTheExistingDispute()
    {
        var consumer = CreateConsumer(out var db, out var paymentRepository);
        var payment = SucceededBoundPayment();
        paymentRepository
            .Setup(repo => repo.GetByProviderMappingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        await using var context = db;
        context.Add(new PaymentDispute
        {
            PaymentId = payment.Id,
            UserId = Guid.NewGuid(),
            Type = DisputeType.Fraudulent,
            Amount = 50m,
            Reason = "fraudulent",
            Status = DisputeStatus.Submitted
        }.Also(d => d.BindGatewayIdentity("stripe", "dp_update", SystemClock.UtcNow.AddDays(7))));
        await context.SaveChangesAsync();

        var handled = await consumer.TryConsumeAsync(
            VerifiedEvent("charge.dispute.closed", disputeId: "dp_update", status: "lost"),
            CancellationToken.None);

        handled.Should().BeTrue();
        (await context.Set<PaymentDispute>().CountAsync()).Should().Be(1);
        var dispute = await context.Set<PaymentDispute>().SingleAsync();
        dispute.Status.Should().Be(DisputeStatus.Lost);
        dispute.ResolvedAt.Should().NotBeNull();
    }

    private static StripeDisputeEventConsumer CreateConsumer(
        out StripeDisputeWebhookTestDbContext db,
        out Mock<IPaymentRepository> paymentRepository)
    {
        var options = new DbContextOptionsBuilder<StripeDisputeWebhookTestDbContext>()
            .UseInMemoryDatabase($"webhook-{Guid.NewGuid():N}")
            .Options;
        db = new StripeDisputeWebhookTestDbContext(options);
        paymentRepository = new Mock<IPaymentRepository>();
        var methodRepository = new Mock<IPaymentMethodRepository>();
        return new StripeDisputeEventConsumer(
            paymentRepository.Object,
            methodRepository.Object,
            db,
            NullLogger<StripeDisputeEventConsumer>.Instance);
    }

    private static Payment SucceededBoundPayment()
    {
        var payment = Payment.Create(Guid.NewGuid(), 50m, "USD", $"idem-{Guid.NewGuid():N}");
        payment.MarkAsProcessing();
        payment.MarkAsSucceeded("pi_dispute", "ch_dispute");
        return payment;
    }

    private static VerifiedStripeWebhookEvent VerifiedEvent(
        string eventType,
        string disputeId = "dp_1",
        string status = "needs_response",
        long amountCents = 5000,
        long? evidenceDueBy = null,
        string paymentIntent = "pi_dispute") =>
        new()
        {
            EventId = $"evt_{Guid.NewGuid():N}",
            EventType = eventType,
            ProviderEnvironment = "test",
            ProviderAccountId = "acct_platform",
            WebhookEndpointId = "we_1",
            EventSchemaVersion = "2024-06-20",
            ProviderObjectId = paymentIntent,
            ProviderObjectType = "dispute",
            ProviderMonetaryLeg = "dispute",
            VerifiedPayload = DisputeEventPayload(eventType, disputeId, status, "fraudulent", amountCents, evidenceDueBy, paymentIntent),
            RetainedPayload = "{}",
            PayloadSha256 = "sha",
            OccurredAt = DateTimeOffset.UtcNow,
            IsLiveMode = false,
            Amount = amountCents / 100m,
            Currency = "USD"
        };

    private static string DisputeEventPayload(
        string eventType,
        string disputeId,
        string status,
        string reason,
        long amountCents,
        long? evidenceDueBy,
        string paymentIntent)
    {
        var dispute = new Dictionary<string, object?>
        {
            ["id"] = disputeId,
            ["object"] = "dispute",
            ["status"] = status,
            ["reason"] = reason,
            ["amount"] = amountCents,
            ["currency"] = "usd",
            ["payment_intent"] = paymentIntent
        };
        if (evidenceDueBy.HasValue)
        {
            dispute["evidence_due_by"] = evidenceDueBy.Value;
        }

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = $"evt_{Guid.NewGuid():N}",
            ["type"] = eventType,
            ["data"] = new Dictionary<string, object?> { ["object"] = dispute }
        });
    }
}

file static class DisputeTestExtensions
{
    public static T Also<T>(this T value, Action<T> apply)
    {
        apply(value);
        return value;
    }
}

internal sealed class StripeDisputeWebhookTestDbContext(
    Microsoft.EntityFrameworkCore.DbContextOptions<StripeDisputeWebhookTestDbContext> options)
    : Microsoft.EntityFrameworkCore.DbContext(options), IApplicationDbContext
{
    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>();
        modelBuilder.Entity<PaymentMethod>();
        modelBuilder.Entity<PaymentDispute>(builder =>
        {
            builder.Property(dispute => dispute.Status).HasConversion<string>().IsRequired();
            builder.Property(dispute => dispute.Type).HasConversion<string>().IsRequired();
            builder.Property(dispute => dispute.Resolution).HasConversion<string>();
        });
        modelBuilder.Entity<DisputeEvidence>();
        base.OnModelCreating(modelBuilder);
    }
}
