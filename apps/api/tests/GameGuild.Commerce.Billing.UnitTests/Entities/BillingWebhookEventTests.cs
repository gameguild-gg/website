using FluentAssertions;
using GameGuild.Commerce.Billing;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Entities;

/// <summary>
/// Unit tests for BillingWebhookEvent entity
/// </summary>
public class BillingWebhookEventEntityTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrectlySet()
    {
        // Arrange & Act
        var webhookEvent = new BillingWebhookEvent();

        // Assert
        webhookEvent.Provider.Should().BeEmpty();
        webhookEvent.ExternalEventId.Should().BeEmpty();
        webhookEvent.EventType.Should().BeEmpty();
        webhookEvent.Payload.Should().BeEmpty();
        webhookEvent.Headers.Should().BeNull();
        webhookEvent.IsProcessed.Should().BeFalse();
        webhookEvent.IsFailed.Should().BeFalse();
        webhookEvent.ProcessingAttempts.Should().Be(0);
        webhookEvent.ErrorMessage.Should().BeNull();
        webhookEvent.ProcessedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("stripe")]
    [InlineData("paypal")]
    [InlineData("square")]
    public void Provider_ShouldAcceptValidProviders(string provider)
    {
        // Arrange
        var webhookEvent = new BillingWebhookEvent();

        // Act
        webhookEvent.Provider = provider;

        // Assert
        webhookEvent.Provider.Should().Be(provider);
    }

    [Theory]
    [InlineData("subscription.created")]
    [InlineData("payment.succeeded")]
    [InlineData("invoice.paid")]
    [InlineData("customer.subscription.deleted")]
    public void EventType_ShouldAcceptCommonEventTypes(string eventType)
    {
        // Arrange
        var webhookEvent = new BillingWebhookEvent();

        // Act
        webhookEvent.EventType = eventType;

        // Assert
        webhookEvent.EventType.Should().Be(eventType);
    }

    [Fact]
    public void MarkAsProcessed_ShouldSetCorrectState()
    {
        // Arrange
        var webhookEvent = new BillingWebhookEvent
        {
            IsFailed = true,
            ProcessingAttempts = 2,
            ErrorMessage = "Previous error"
        };

        // Act
        webhookEvent.MarkAsProcessed();

        // Assert
        webhookEvent.IsProcessed.Should().BeTrue();
        webhookEvent.IsFailed.Should().BeFalse();
        webhookEvent.ProcessedAt.Should().NotBeNull();
        webhookEvent.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkAsFailed_ShouldSetCorrectState()
    {
        // Arrange
        var webhookEvent = new BillingWebhookEvent
        {
            ProcessingAttempts = 1
        };
        var errorMessage = "Connection timeout";

        // Act
        webhookEvent.MarkAsFailed(errorMessage);

        // Assert
        webhookEvent.IsFailed.Should().BeTrue();
        webhookEvent.ErrorMessage.Should().Be(errorMessage);
        webhookEvent.ProcessingAttempts.Should().Be(1);
    }

    [Fact]
    public void MarkAsFailed_CalledMultipleTimes_ShouldNotDoubleCountAttempts()
    {
        // Arrange
        var webhookEvent = new BillingWebhookEvent { ProcessingAttempts = 1 };

        // Act
        webhookEvent.MarkAsFailed("Error 1");
        webhookEvent.MarkAsFailed("Error 2");
        webhookEvent.MarkAsFailed("Error 3");

        // Assert
        webhookEvent.ProcessingAttempts.Should().Be(1);
        webhookEvent.ErrorMessage.Should().Be("Error 3");
    }

    [Fact]
    public void TryBeginProcessing_Should_Reject_Processed_Or_Active_Lease()
    {
        var processed = new BillingWebhookEvent { IsProcessed = true };
        var active = new BillingWebhookEvent
        {
            ProcessingAttempts = 1,
            UpdatedAt = DateTime.UtcNow
        };

        processed.TryBeginProcessing(DateTime.UtcNow.AddMinutes(-1)).Should().BeFalse();
        active.TryBeginProcessing(DateTime.UtcNow.AddMinutes(-1)).Should().BeFalse();
    }

    [Fact]
    public void TryBeginProcessing_Should_Reclaim_Failed_Or_Stale_Lease()
    {
        var failed = new BillingWebhookEvent
        {
            ProcessingAttempts = 1,
            IsFailed = true,
            ErrorMessage = "temporary",
            UpdatedAt = DateTime.UtcNow
        };
        var stale = new BillingWebhookEvent
        {
            ProcessingAttempts = 2,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        failed.TryBeginProcessing(DateTime.UtcNow.AddMinutes(-1)).Should().BeTrue();
        stale.TryBeginProcessing(DateTime.UtcNow.AddMinutes(-1)).Should().BeTrue();

        failed.ProcessingAttempts.Should().Be(2);
        failed.IsFailed.Should().BeFalse();
        failed.ErrorMessage.Should().BeNull();
        stale.ProcessingAttempts.Should().Be(3);
    }

    [Fact]
    public void Payload_ShouldAcceptJsonPayload()
    {
        // Arrange
        var webhookEvent = new BillingWebhookEvent();
        var jsonPayload = "{\"id\":\"evt_123\",\"type\":\"payment.succeeded\",\"data\":{\"amount\":1000}}";

        // Act
        webhookEvent.Payload = jsonPayload;

        // Assert
        webhookEvent.Payload.Should().Be(jsonPayload);
    }

    [Fact]
    public void Headers_ShouldAcceptJsonHeaders()
    {
        // Arrange
        var webhookEvent = new BillingWebhookEvent();
        var headers = "{\"Stripe-Signature\":\"t=123,v1=abc\"}";

        // Act
        webhookEvent.Headers = headers;

        // Assert
        webhookEvent.Headers.Should().Be(headers);
    }

    [Fact]
    public void SubscriptionId_WhenSet_ShouldRetainValue()
    {
        // Arrange
        var webhookEvent = new BillingWebhookEvent();
        var subscriptionId = Guid.NewGuid();

        // Act
        webhookEvent.SubscriptionId = subscriptionId;

        // Assert
        webhookEvent.SubscriptionId.Should().Be(subscriptionId);
    }

    [Fact]
    public void MarkSignatureVerified_Should_Set_The_Immutable_Verification_Proof()
    {
        var webhookEvent = new BillingWebhookEvent();
        webhookEvent.IsSignatureVerified.Should().BeNull("legacy rows start unverified");

        webhookEvent.MarkSignatureVerified();

        webhookEvent.IsSignatureVerified.Should().BeTrue();
        webhookEvent.SignatureVerifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verification is immutable once established.
        var firstVerifiedAt = webhookEvent.SignatureVerifiedAt;
        webhookEvent.MarkSignatureVerified();
        webhookEvent.SignatureVerifiedAt.Should().Be(firstVerifiedAt);
    }

    [Fact]
    public void MarkAsDeadLettered_Should_Record_The_Terminal_Reason()
    {
        var webhookEvent = new BillingWebhookEvent { ProcessingAttempts = 5 };

        webhookEvent.MarkAsDeadLettered("Maximum retry attempts (5) exceeded");

        webhookEvent.DeadLetteredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        webhookEvent.DeadLetterReason.Should().Be("Maximum retry attempts (5) exceeded");
        webhookEvent.IsProcessed.Should().BeFalse();
        webhookEvent.IsFailed.Should().BeFalse("the failure evidence stays on the flags caller set");
    }

    [Fact]
    public void MarkAsDeadLettered_Should_Truncate_Overlong_Reasons()
    {
        var webhookEvent = new BillingWebhookEvent();

        webhookEvent.MarkAsDeadLettered(new string('x', 2500));

        webhookEvent.DeadLetterReason.Should().NotBeNull();
        webhookEvent.DeadLetterReason!.Length.Should().Be(1000);
    }

    [Fact]
    public void MarkAsDeadLettered_Should_Default_The_Reason_When_Missing()
    {
        var webhookEvent = new BillingWebhookEvent();

        webhookEvent.MarkAsDeadLettered("  ");

        webhookEvent.DeadLetterReason.Should().Be("Retry budget exhausted");
    }
}
