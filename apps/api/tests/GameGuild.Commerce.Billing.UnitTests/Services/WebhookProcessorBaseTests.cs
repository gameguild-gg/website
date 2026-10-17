using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

/// <summary>
///     Unit tests for WebhookProcessorBase retry logic and tenant validation.
/// </summary>
public class WebhookProcessorBaseTests
{
    private readonly Mock<IBillingWebhookRepository> _repositoryMock;
    private readonly ILogger<TestWebhookProcessor> _logger;
    private readonly BillingConfiguration _billingConfiguration;
    private readonly TestWebhookProcessor _processor;

    public WebhookProcessorBaseTests()
    {
        _repositoryMock = new Mock<IBillingWebhookRepository>();
        _logger = NullLogger<TestWebhookProcessor>.Instance;
        _billingConfiguration = new BillingConfiguration
        {
            Webhook = new WebhookSettings
            {
                MaxRetryAttempts = 3,
                ProcessingTimeoutSeconds = 30,
                StorePayloads = true,
                RetryPolicy = new WebhookRetryPolicy
                {
                    Enabled = true,
                    InitialDelaySeconds = 1,
                    MaxDelaySeconds = 60,
                    BackoffMultiplier = 2.0
                }
            }
        };

        var options = Options.Create(_billingConfiguration);
        _processor = new TestWebhookProcessor(_repositoryMock.Object, options, _logger);
    }

    #region Tenant Validation Tests

    [Fact]
    public async Task ValidateTenantContext_WithMissingTenantId_ReturnsFailure()
    {
        // Act
        var result = await _processor.TestValidateTenantContextAsync(null, "sub_123");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Missing tenant context");
    }

    [Fact]
    public async Task ValidateTenantContext_WithEmptyTenantId_ReturnsFailure()
    {
        // Act
        var result = await _processor.TestValidateTenantContextAsync(Guid.Empty, "sub_123");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Missing tenant context");
    }

    [Fact]
    public async Task ValidateTenantContext_WithValidTenantId_ReturnsSuccess()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var result = await _processor.TestValidateTenantContextAsync(tenantId, null);

        // Assert
        result.IsValid.Should().BeTrue();
        result.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task ValidateTenantContext_WithValidOwnership_ReturnsSuccess()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subscriptionId = "sub_123";

        // Act
        var result = await _processor.TestValidateTenantContextAsync(
            tenantId,
            subscriptionId,
            (t, s) => Task.FromResult(true)); // Ownership valid

        // Assert
        result.IsValid.Should().BeTrue();
        result.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task ValidateTenantContext_WithInvalidOwnership_ReturnsFailure()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subscriptionId = "sub_123";

        // Act
        var result = await _processor.TestValidateTenantContextAsync(
            tenantId,
            subscriptionId,
            (t, s) => Task.FromResult(false)); // Ownership invalid

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not belong to tenant");
    }

    [Fact]
    public async Task ValidateTenantContext_WhenOwnershipCheckThrows_ReturnsFailure()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subscriptionId = "sub_123";

        // Act
        var result = await _processor.TestValidateTenantContextAsync(
            tenantId,
            subscriptionId,
            (_, _) => throw new Exception("Database error"));

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to validate");
    }

    #endregion

    #region Metadata Extraction Tests

    [Fact]
    public void ExtractTenantIdFromMetadata_WithValidTenantId_ReturnsTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var metadata = new Dictionary<string, string> { { "tenant_id", tenantId.ToString() } };

        // Act
        var result = _processor.TestExtractTenantIdFromMetadata(metadata);

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public void ExtractTenantIdFromMetadata_WithCamelCaseTenantId_ReturnsTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var metadata = new Dictionary<string, string> { { "tenantId", tenantId.ToString() } };

        // Act
        var result = _processor.TestExtractTenantIdFromMetadata(metadata);

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public void ExtractTenantIdFromMetadata_WithPascalCaseTenantId_ReturnsTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var metadata = new Dictionary<string, string> { { "TenantId", tenantId.ToString() } };

        // Act
        var result = _processor.TestExtractTenantIdFromMetadata(metadata);

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public void ExtractTenantIdFromMetadata_WithNullMetadata_ReturnsNull()
    {
        // Act
        var result = _processor.TestExtractTenantIdFromMetadata(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractTenantIdFromMetadata_WithMissingKey_ReturnsNull()
    {
        // Arrange
        var metadata = new Dictionary<string, string> { { "other_key", "value" } };

        // Act
        var result = _processor.TestExtractTenantIdFromMetadata(metadata);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractTenantIdFromMetadata_WithInvalidGuid_ReturnsNull()
    {
        // Arrange
        var metadata = new Dictionary<string, string> { { "tenant_id", "not-a-guid" } };

        // Act
        var result = _processor.TestExtractTenantIdFromMetadata(metadata);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Webhook Processing & Idempotency Tests

    [Fact]
    public async Task ProcessWebhook_WithDuplicateEvent_ReturnsAlreadyProcessed()
    {
        // Arrange
        var eventId = "evt_123";
        var existingEvent = new BillingWebhookEvent
        {
            ExternalEventId = eventId,
            Provider = "TestProvider",
            ProcessedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        _repositoryMock
            .Setup(r => r.GetByExternalEventIdAsync(eventId, "TestProvider", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEvent);

        // Act
        var result = await _processor.TestProcessWebhookAsync(eventId, "test.event", "{}");

        // Assert
        result.WasAlreadyProcessed.Should().BeTrue();
        result.EventId.Should().Be(eventId);
        _repositoryMock.Verify(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_WithNewEvent_ProcessesSuccessfully()
    {
        // Arrange
        var eventId = "evt_456";
        _processor.ShouldSucceed = true;

        _repositoryMock
            .Setup(r => r.GetByExternalEventIdAsync(eventId, "TestProvider", It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        // Act
        var result = await _processor.TestProcessWebhookAsync(eventId, "test.event", "{}");

        // Assert
        result.Processed.Should().BeTrue();
        result.EventId.Should().Be(eventId);
        _repositoryMock.Verify(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Retry Logic Tests

    [Fact]
    public async Task ProcessWebhook_WhenFirstAttemptFails_RetriesAndSucceeds()
    {
        // Arrange
        var eventId = "evt_retry";
        _processor.FailCount = 1; // Fail first attempt, succeed on second

        _repositoryMock
            .Setup(r => r.GetByExternalEventIdAsync(eventId, "TestProvider", It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        // Act
        var result = await _processor.TestProcessWebhookAsync(eventId, "test.event", "{}");

        // Assert
        result.Processed.Should().BeTrue();
        _processor.AttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task ProcessWebhook_WhenAllAttemptsFail_ThrowsWebhookProcessingException()
    {
        // Arrange
        var eventId = "evt_allfail";
        _processor.FailCount = 10; // Fail all attempts

        _repositoryMock
            .Setup(r => r.GetByExternalEventIdAsync(eventId, "TestProvider", It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        // Act
        var result = await _processor.TestProcessWebhookAsync(eventId, "test.event", "{}");

        // Assert
        result.Processed.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        _processor.AttemptCount.Should().Be(3); // Max attempts from config
    }

    [Fact]
    public async Task ProcessWebhook_WithDisabledRetry_OnlyAttemptsOnce()
    {
        // Arrange
        _billingConfiguration.Webhook.RetryPolicy.Enabled = false;

        var eventId = "evt_noretry";
        _processor.FailCount = 10; // Fail all attempts

        _repositoryMock
            .Setup(r => r.GetByExternalEventIdAsync(eventId, "TestProvider", It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        // Act
        var result = await _processor.TestProcessWebhookAsync(eventId, "test.event", "{}");

        // Assert
        result.Processed.Should().BeFalse();
        _processor.AttemptCount.Should().Be(1); // Only one attempt when retry disabled
    }

    #endregion

    #region Configuration Validation Tests

    [Fact]
    public void BillingConfiguration_Validate_WithValidConfig_ReturnsNoErrors()
    {
        // Arrange
        var config = new BillingConfiguration
        {
            Stripe = new StripeSettings
            {
                SecretKey = "sk_test_123",
                PublishableKey = "pk_test_123"
            },
            Webhook = new WebhookSettings
            {
                MaxRetryAttempts = 3,
                ProcessingTimeoutSeconds = 30,
                RetryPolicy = new WebhookRetryPolicy
                {
                    InitialDelaySeconds = 5,
                    BackoffMultiplier = 2.0
                }
            }
        };

        // Act
        var errors = config.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(config)).ToList();

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void BillingConfiguration_Validate_WithMissingStripePublishableKey_ReturnsError()
    {
        // Arrange
        var config = new BillingConfiguration
        {
            Stripe = new StripeSettings
            {
                SecretKey = "sk_test_123"
                // Missing PublishableKey
            }
        };

        // Act
        var errors = config.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(config)).ToList();

        // Assert
        errors.Should().HaveCount(1);
        errors[0].ErrorMessage.Should().Contain("PublishableKey");
    }

    [Fact]
    public void BillingConfiguration_Validate_WithNegativeMaxRetryAttempts_ReturnsError()
    {
        // Arrange
        var config = new BillingConfiguration
        {
            Webhook = new WebhookSettings { MaxRetryAttempts = -1 }
        };

        // Act
        var errors = config.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(config)).ToList();

        // Assert
        errors.Should().Contain(e => e.ErrorMessage!.Contains("MaxRetryAttempts"));
    }

    [Fact]
    public void BillingConfiguration_Validate_WithInvalidBackoffMultiplier_ReturnsError()
    {
        // Arrange
        var config = new BillingConfiguration
        {
            Webhook = new WebhookSettings
            {
                RetryPolicy = new WebhookRetryPolicy { BackoffMultiplier = 0.5 }
            }
        };

        // Act
        var errors = config.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(config)).ToList();

        // Assert
        errors.Should().Contain(e => e.ErrorMessage!.Contains("BackoffMultiplier"));
    }

    #endregion

    /// <summary>
    ///     Test implementation of WebhookProcessorBase for unit testing.
    /// </summary>
    private class TestWebhookProcessor : WebhookProcessorBase
    {
        public bool ShouldSucceed { get; set; } = true;
        public int FailCount { get; set; }
        public int AttemptCount { get; private set; }

        protected override string ProviderName => "TestProvider";

        public TestWebhookProcessor(
            IBillingWebhookRepository webhookRepository,
            IOptions<BillingConfiguration> billingConfiguration,
            ILogger<TestWebhookProcessor> logger)
            : base(webhookRepository, billingConfiguration, logger)
        {
        }

        protected override Task RouteEventAsync(string eventType, string payload, CancellationToken cancellationToken)
        {
            AttemptCount++;

            if (FailCount > 0)
            {
                FailCount--;
                throw new Exception("Simulated processing failure");
            }

            if (!ShouldSucceed)
            {
                throw new Exception("Processing failed");
            }

            return Task.CompletedTask;
        }

        // Expose protected methods for testing
        public Task<TenantValidationResult> TestValidateTenantContextAsync(
            Guid? tenantId,
            string? subscriptionExternalId,
            Func<Guid, string, Task<bool>>? validateOwnership = null)
            => ValidateTenantContextAsync(tenantId, subscriptionExternalId, validateOwnership);

        public Guid? TestExtractTenantIdFromMetadata(IDictionary<string, string>? metadata)
            => ExtractTenantIdFromMetadata(metadata);

        public Task<WebhookProcessingResult> TestProcessWebhookAsync(string eventId, string eventType, string payload)
            => ProcessWebhookAsync(eventId, eventType, payload);
    }
}
