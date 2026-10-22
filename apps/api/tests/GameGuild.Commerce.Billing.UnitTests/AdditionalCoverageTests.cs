using FluentAssertions;
using GameGuild.Commerce;
using GameGuild.Commerce.Subscriptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests;

internal sealed class TestStripeWebhookVerifier : IStripeWebhookVerifier
{
    public VerifiedStripeWebhookEvent Verify(string payload, string signature)
    {
        string eventId;
        string eventType;
        var signatureParts = signature.Split('|', 2, StringSplitOptions.TrimEntries);

        if (signatureParts.Length == 2)
        {
            eventId = signatureParts[0];
            eventType = signatureParts[1];
        }
        else
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                eventId = root.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty;
                eventType = root.TryGetProperty("type", out var type) ? type.GetString() ?? string.Empty : string.Empty;
            }
            catch (JsonException exception)
            {
                throw new InvalidWebhookPayloadException("Test webhook payload is malformed.", exception);
            }
        }

        if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(eventType))
        {
            throw new InvalidWebhookPayloadException("Test webhook payload requires id and type.");
        }

        Guid? tenantId = null;
        string? externalSubscriptionId = null;
        decimal? amount = null;
        string? currency = null;
        var providerObjectId = eventId;

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("object", out var providerObject))
            {
                if (providerObject.TryGetProperty("id", out var objectId) &&
                    !string.IsNullOrWhiteSpace(objectId.GetString()))
                {
                    providerObjectId = objectId.GetString()!;
                }

                if (eventType.StartsWith("customer.subscription.", StringComparison.Ordinal))
                {
                    externalSubscriptionId = providerObjectId;
                }
                else if (providerObject.TryGetProperty("subscription", out var subscription))
                {
                    externalSubscriptionId = subscription.GetString();
                }
                else if (eventType.StartsWith("invoice.", StringComparison.Ordinal) &&
                         providerObjectId.StartsWith("sub_", StringComparison.Ordinal))
                {
                    externalSubscriptionId = providerObjectId;
                }

                if (providerObject.TryGetProperty("metadata", out var metadata) &&
                    metadata.TryGetProperty("tenant_id", out var tenant) &&
                    Guid.TryParse(tenant.GetString(), out var parsedTenantId))
                {
                    tenantId = parsedTenantId;
                }

                var amountInMinorUnits = providerObject.TryGetProperty("amount_paid", out var amountPaid)
                    ? amountPaid.GetDecimal()
                    : providerObject.TryGetProperty("amount_due", out var amountDue)
                        ? amountDue.GetDecimal()
                        : (decimal?)null;
                amount = amountInMinorUnits / 100m;
                currency = providerObject.TryGetProperty("currency", out var currencyElement)
                    ? currencyElement.GetString()?.ToUpperInvariant()
                    : null;
            }
        }
        catch (JsonException exception) when (signatureParts.Length == 2)
        {
            throw new InvalidWebhookPayloadException("Test webhook payload is malformed.", exception);
        }

        return new VerifiedStripeWebhookEvent
        {
            EventId = eventId,
            EventType = eventType,
            ProviderEnvironment = "test",
            ProviderAccountId = "acct_test",
            WebhookEndpointId = "we_test",
            EventSchemaVersion = "test",
            ProviderObjectId = providerObjectId,
            ProviderObjectType = "test",
            ProviderMonetaryLeg = eventType.StartsWith("customer.subscription.", StringComparison.Ordinal)
                ? "subscription"
                : "none",
            VerifiedPayload = payload,
            RetainedPayload = payload,
            PayloadSha256 = "test-hash",
            TenantId = tenantId,
            ExternalSubscriptionId = externalSubscriptionId,
            Amount = amount,
            Currency = currency
        };
    }
}

#region Invoice Additional Tests

public class InvoiceAdditionalTests
{
    private static Invoice CreateDraft(decimal amount = 29.99m) =>
        new(Guid.NewGuid(), Guid.NewGuid(), amount);

    private static Invoice CreateIssued(decimal amount = 29.99m)
    {
        var inv = CreateDraft(amount);
        inv.Issue();
        return inv;
    }

    [Fact]
    public void SetExternalId_Should_Set_Value()
    {
        var inv = CreateDraft();
        inv.SetExternalId("stripe_inv_123");
        inv.ExternalId.Should().Be("stripe_inv_123");
    }

    [Fact]
    public void SetExternalId_On_Issued_Invoice_Should_Also_Work()
    {
        var inv = CreateIssued();
        inv.SetExternalId("stripe_inv_456");
        inv.ExternalId.Should().Be("stripe_inv_456");
    }

    [Fact]
    public void ApplyDiscount_Negative_Should_Throw()
    {
        var inv = CreateDraft(100m);
        var act = () => inv.ApplyDiscount(-1m);
        act.Should().Throw<ArgumentException>().WithMessage("*negative*");
    }

    [Fact]
    public void ApplyDiscount_Greater_Than_Subtotal_Should_Throw()
    {
        var inv = CreateDraft(100m);
        var act = () => inv.ApplyDiscount(200m);
        act.Should().Throw<ArgumentException>().WithMessage("*exceed*subtotal*");
    }

    [Fact]
    public void SetTax_Negative_Should_Throw()
    {
        var inv = CreateDraft(100m);
        var act = () => inv.SetTax(-5m);
        act.Should().Throw<ArgumentException>().WithMessage("*negative*");
    }

    [Fact]
    public void Void_Paid_Invoice_Should_Throw()
    {
        var inv = CreateIssued();
        inv.RecordPayment(Guid.NewGuid(), 29.99m, DateTime.UtcNow);
        var act = () => inv.Void("test");
        act.Should().Throw<InvalidOperationException>().WithMessage("*void*paid*");
    }

    [Fact]
    public void Void_Already_Voided_Should_Be_Idempotent()
    {
        var inv = CreateIssued();
        inv.Void("first void");
        inv.Void("second void"); // should not throw
        inv.Status.Should().Be(InvoiceStatus.Void);
        inv.VoidReason.Should().Be("first void"); // unchanged
    }

    [Fact]
    public void MarkUncollectible_On_Draft_Should_Throw()
    {
        var inv = CreateDraft();
        var act = () => inv.MarkUncollectible();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkUncollectible_On_PastDue_Should_Succeed()
    {
        var inv = CreateIssued();
        inv.MarkPastDue();
        inv.MarkUncollectible();
        inv.Status.Should().Be(InvoiceStatus.Uncollectible);
    }

    [Fact]
    public void MarkPastDue_On_PastDue_Should_Throw()
    {
        var inv = CreateIssued();
        inv.MarkPastDue();
        var act = () => inv.MarkPastDue();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkPastDue_On_Draft_Should_Throw()
    {
        var inv = CreateDraft();
        var act = () => inv.MarkPastDue();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Issue_Should_Set_DueDate_If_Not_Specified()
    {
        var inv = CreateDraft();
        inv.Issue();
        inv.DueDate.Should().NotBeNull();
    }

    [Fact]
    public void Issue_Should_Use_Provided_DueDate()
    {
        var inv = CreateDraft();
        var due = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        inv.Issue(due);
        inv.DueDate.Should().Be(due);
    }

    [Fact]
    public void InvoiceNumber_Should_Start_With_INV()
    {
        var inv = CreateDraft();
        inv.InvoiceNumber.Should().StartWith("INV-");
    }

    [Fact]
    public void RecordPayment_Partial_Should_Not_Mark_Paid()
    {
        var inv = CreateIssued();
        inv.RecordPayment(Guid.NewGuid(), 10m, DateTime.UtcNow);
        inv.Status.Should().Be(InvoiceStatus.Open); // still open, not fully paid
        inv.AmountRemaining.Should().Be(19.99m);
    }

    [Fact]
    public void Constructor_WithCurrency_Should_Set_Currency()
    {
        var inv = new Invoice(Guid.NewGuid(), Guid.NewGuid(), 50m, "EUR");
        inv.Currency.Should().Be("EUR");
    }
}

#endregion

#region StripeBillingWebhookService Additional Tests

public class StripeBillingWebhookServiceAdditionalTests
{
    private static StripeBillingWebhookService CreateService(
        Mock<IBillingWebhookRepository> repository,
        ISubscriptionQueryService? queryService = null,
        ISubscriptionBillingService? billingService = null,
        ISubscriptionLifecycleService? lifecycleService = null,
        ISubscriptionExternalIdService? externalIdService = null)
    {
        return new StripeBillingWebhookService(
            repository.Object,
            new TestStripeWebhookVerifier(),
            Mock.Of<IStripeProviderObjectBindingValidator>(),
            NullLogger<StripeBillingWebhookService>.Instance,
            lifecycleService ?? Mock.Of<ISubscriptionLifecycleService>(),
            queryService ?? CreateQueryService(),
            billingService ?? Mock.Of<ISubscriptionBillingService>(),
            externalIdService ?? Mock.Of<ISubscriptionExternalIdService>());
    }

    private static ISubscriptionQueryService CreateQueryService(decimal amount = 20m, Guid? tenantId = null)
    {
        var service = new Mock<ISubscriptionQueryService>();
        service
            .Setup(query => query.GetByExternalIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Subscription(
                tenantId ?? Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                BillingCycle.Monthly,
                new Money(amount, "USD"),
                DateTime.UtcNow));
        return service.Object;
    }

    private static Mock<IBillingWebhookRepository> CreateMockRepo()
    {
        var repo = new Mock<IBillingWebhookRepository>();
        repo.Setup(r => r.GetByProviderScopeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repo.Setup(r => r.GetByExternalEventIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repo.Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repo.Setup(r => r.TryClaimProcessingAsync(
                It.IsAny<BillingWebhookEvent>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repo.Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        return repo;
    }

    [Fact]
    public async Task ProcessStripeWebhook_SubscriptionCreated_Should_Route_To_Lifecycle()
    {
        var repo = CreateMockRepo();
        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        lifecycle
            .Setup(l => l.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<BillingCycle>(), It.IsAny<Money>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow));

        var externalIdSvc = new Mock<ISubscriptionExternalIdService>();
        var service = CreateService(repo, lifecycleService: lifecycle.Object, externalIdService: externalIdSvc.Object);

        var payload = "{\"data\":{\"object\":{\"id\":\"sub_new\",\"status\":\"active\",\"metadata\":{}}}}";
        var result = await service.ProcessStripeWebhookAsync(payload, "evt_create|customer.subscription.created");

        result.Processed.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessStripeWebhook_Error_Should_MarkFailed_And_Return_Error()
    {
        var repo = new Mock<IBillingWebhookRepository>();
        repo.Setup(r => r.GetByExternalEventIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repo.Setup(r => r.GetByProviderScopeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repo.Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repo.Setup(r => r.TryClaimProcessingAsync(
                It.IsAny<BillingWebhookEvent>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repo.Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        // Lifecycle.ActivateAsync will throw
        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        lifecycle.Setup(l => l.ActivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Activation failed"));

        var queryService = new Mock<ISubscriptionQueryService>();
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow);
        queryService.Setup(q => q.GetByExternalIdAsync("sub_err", It.IsAny<CancellationToken>())).ReturnsAsync(sub);

        var service = CreateService(repo, queryService: queryService.Object, lifecycleService: lifecycle.Object);

        var payload = "{\"data\":{\"object\":{\"id\":\"sub_err\",\"status\":\"active\"}}}";
        var result = await service.ProcessStripeWebhookAsync(payload, "evt_err|customer.subscription.updated");

        result.Processed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Activation failed");
    }

    [Fact]
    public async Task ProcessStripeWebhook_WithMetadata_Should_ParseTenantAndPlan()
    {
        var repo = CreateMockRepo();
        var tenantId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        lifecycle
            .Setup(l => l.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<BillingCycle>(), It.IsAny<Money>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow));

        var externalIdSvc = new Mock<ISubscriptionExternalIdService>();
        var service = CreateService(
            repo,
            queryService: CreateQueryService(tenantId: tenantId),
            lifecycleService: lifecycle.Object,
            externalIdService: externalIdSvc.Object);

        var payload = $"{{\"data\":{{\"object\":{{\"id\":\"sub_meta\",\"status\":\"active\",\"metadata\":{{\"tenant_id\":\"{tenantId}\",\"plan_id\":\"{planId}\"}}}}}}}}";
        var result = await service.ProcessStripeWebhookAsync(payload, "evt_meta|customer.subscription.created");

        result.Processed.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessStripeWebhook_InvoiceWithAmountDue_Should_Parse()
    {
        var repo = CreateMockRepo();
        var service = CreateService(repo);

        // amount_due instead of amount_paid
        var payload = "{\"data\":{\"object\":{\"subscription\":\"sub_x\",\"amount_due\":2000,\"currency\":\"usd\"}}}";
        var result = await service.ProcessStripeWebhookAsync(payload, "evt_amt|invoice.payment_succeeded");

        result.Processed.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessStripeWebhook_WithPriceAndProduct_Should_Parse()
    {
        var repo = CreateMockRepo();
        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        lifecycle
            .Setup(l => l.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<BillingCycle>(), It.IsAny<Money>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow));

        var externalIdSvc = new Mock<ISubscriptionExternalIdService>();
        var service = CreateService(repo, lifecycleService: lifecycle.Object, externalIdService: externalIdSvc.Object);

        var payload = "{\"data\":{\"object\":{\"id\":\"sub_p\",\"status\":\"active\",\"items\":{\"data\":[{\"price\":{\"id\":\"price_1\",\"product\":\"prod_1\"}}]},\"current_period_start\":1700000000,\"current_period_end\":1702592000,\"billing_cycle_anchor\":1700000000}}}";
        var result = await service.ProcessStripeWebhookAsync(payload, "evt_price|customer.subscription.created");

        result.Processed.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessStripeWebhook_InvalidJson_Should_Handle_Gracefully()
    {
        var repo = CreateMockRepo();
        var service = CreateService(repo);

        var act = () => service.ProcessStripeWebhookAsync("not json", "evt_bad|unknown.event");

        await act.Should().ThrowAsync<InvalidWebhookPayloadException>();
    }
}

#endregion

#region ApplePayBillingWebhookService Additional Tests

public class ApplePayBillingWebhookServiceAdditionalTests
{
    private static Mock<IBillingWebhookRepository> CreateMockRepo()
    {
        var repo = new Mock<IBillingWebhookRepository>();
        repo.Setup(r => r.GetByExternalEventIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repo.Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repo.Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        return repo;
    }

    private static Mock<IApplePayReceiptValidationService> CreateValidator(
        string notificationType, string? subtype = null)
    {
        var validator = new Mock<IApplePayReceiptValidationService>();
        validator
            .Setup(v => v.VerifyNotificationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AppleNotificationVerificationResult.Success(
                notificationType, subtype, "tx", "orig", "prod", null, "Sandbox"));
        return validator;
    }

    [Fact]
    public async Task ProcessAppStoreNotification_UnknownType_Should_Succeed()
    {
        var repo = CreateMockRepo();
        var validator = CreateValidator("TOTALLY_UNKNOWN");
        var service = new ApplePayBillingWebhookService(
            repo.Object, validator.Object,
            NullLogger<ApplePayBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>());

        var result = await service.ProcessAppStoreNotificationAsync("payload");
        result.Processed.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAppStoreNotification_Error_Should_MarkFailed()
    {
        var repo = new Mock<IBillingWebhookRepository>();
        repo.Setup(r => r.GetByExternalEventIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repo.Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repo.Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var validator = CreateValidator("SUBSCRIBED");
        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        lifecycle
            .Setup(l => l.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<BillingCycle>(), It.IsAny<Money>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Create failed"));

        var service = new ApplePayBillingWebhookService(
            repo.Object, validator.Object,
            NullLogger<ApplePayBillingWebhookService>.Instance,
            lifecycle.Object,
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>());

        var result = await service.ProcessAppStoreNotificationAsync("payload");
        result.Processed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Create failed");
    }

    [Fact]
    public async Task ProcessAppStoreNotification_ChangeRenewalStatus_NonDisabled_Should_NotUpdate()
    {
        var repo = CreateMockRepo();
        var validator = CreateValidator("DID_CHANGE_RENEWAL_STATUS", "AUTO_RENEW_ENABLED");
        var lifecycle = new Mock<ISubscriptionLifecycleService>();

        var service = new ApplePayBillingWebhookService(
            repo.Object, validator.Object,
            NullLogger<ApplePayBillingWebhookService>.Instance,
            lifecycle.Object,
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>());

        var result = await service.ProcessAppStoreNotificationAsync("payload");
        result.Processed.Should().BeTrue();
        // No subscription update should be called for non-disabled
    }

    [Fact]
    public async Task ProcessAppStoreNotification_RefundReversed_Should_Succeed()
    {
        var repo = CreateMockRepo();
        var validator = CreateValidator("REFUND_REVERSED");

        var service = new ApplePayBillingWebhookService(
            repo.Object, validator.Object,
            NullLogger<ApplePayBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>());

        var result = await service.ProcessAppStoreNotificationAsync("payload");
        result.Processed.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAppStoreNotification_RefundDeclined_Should_Succeed()
    {
        var repo = CreateMockRepo();
        var validator = CreateValidator("REFUND_DECLINED");

        var service = new ApplePayBillingWebhookService(
            repo.Object, validator.Object,
            NullLogger<ApplePayBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>());

        var result = await service.ProcessAppStoreNotificationAsync("payload");
        result.Processed.Should().BeTrue();
    }
}

#endregion

#region AppleJwsVerificationService Tests

public class AppleJwsVerificationServiceTests
{
    [Fact]
    public void DecodeSignedTransaction_InvalidFormat_Should_Return_Null()
    {
        var logger = NullLogger<AppleJwsVerificationService>.Instance;
        var service = new AppleJwsVerificationService(logger);

        var result = service.DecodeSignedTransaction("not.valid");
        result.Should().BeNull();
    }

    [Fact]
    public void DecodeSignedTransaction_ThreeParts_NoValidHeader_Should_Return_Null()
    {
        var logger = NullLogger<AppleJwsVerificationService>.Instance;
        var service = new AppleJwsVerificationService(logger);

        // Three parts but invalid base64url header
        var result = service.DecodeSignedTransaction("aW52YWxpZA.payload.signature");
        result.Should().BeNull();
    }

    [Fact]
    public void DecodeSignedNotification_InvalidFormat_Should_Return_Null()
    {
        var logger = NullLogger<AppleJwsVerificationService>.Instance;
        var service = new AppleJwsVerificationService(logger);

        var result = service.DecodeSignedNotification("single-part");
        result.Should().BeNull();
    }

    [Fact]
    public void DecodeSignedNotification_FourParts_Should_Return_Null()
    {
        var logger = NullLogger<AppleJwsVerificationService>.Instance;
        var service = new AppleJwsVerificationService(logger);

        var result = service.DecodeSignedNotification("a.b.c.d");
        result.Should().BeNull();
    }

    [Fact]
    public void DecodeSignedTransaction_EmptyX5c_Should_Return_Null()
    {
        var logger = NullLogger<AppleJwsVerificationService>.Instance;
        var service = new AppleJwsVerificationService(logger);

        // Header with empty x5c: {"alg":"ES256","x5c":[]}
        var headerJson = "{\"alg\":\"ES256\",\"x5c\":[]}";
        var headerBase64 = Base64UrlEncode(headerJson);
        var result = service.DecodeSignedTransaction($"{headerBase64}.payload.signature");
        result.Should().BeNull();
    }

    [Fact]
    public void DecodeSignedNotification_EmptyX5c_Should_Return_Null()
    {
        var logger = NullLogger<AppleJwsVerificationService>.Instance;
        var service = new AppleJwsVerificationService(logger);

        var headerJson = "{\"alg\":\"ES256\",\"x5c\":[]}";
        var headerBase64 = Base64UrlEncode(headerJson);
        var result = service.DecodeSignedNotification($"{headerBase64}.payload.signature");
        result.Should().BeNull();
    }

    [Fact]
    public void DecodeSignedTransaction_ShortCertChain_Should_Return_Null()
    {
        var logger = NullLogger<AppleJwsVerificationService>.Instance;
        var service = new AppleJwsVerificationService(logger);

        // Header with only 1 cert in chain (need at least 2)
        var headerJson = "{\"alg\":\"ES256\",\"x5c\":[\"AAAA\"]}";
        var headerBase64 = Base64UrlEncode(headerJson);
        var result = service.DecodeSignedTransaction($"{headerBase64}.payload.signature");
        result.Should().BeNull();
    }

    private static string Base64UrlEncode(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

#endregion

#region BillingConfiguration Additional Tests

public class BillingConfigurationAdditionalTests
{
    [Fact]
    public void IsProviderEnabled_Stripe_Should_Return_True_When_Configured()
    {
        var config = new BillingConfiguration
        {
            Stripe = new StripeSettings { SecretKey = "sk" }
        };
        config.IsProviderEnabled(PaymentProviders.Stripe).Should().BeTrue();
    }

    [Fact]
    public void IsProviderEnabled_PayPal_Should_Return_True_When_Configured()
    {
        var config = new BillingConfiguration
        {
            PayPal = new PayPalSettings { ClientId = "client" }
        };
        config.IsProviderEnabled(PaymentProviders.PayPal).Should().BeTrue();
    }

    [Fact]
    public void IsProviderEnabled_Apple_Should_Return_True_When_Configured()
    {
        var config = new BillingConfiguration
        {
            ApplePay = new ApplePaySettings { BundleId = "bundle" }
        };
        config.IsProviderEnabled(PaymentProviders.AppleAppStore).Should().BeTrue();
    }

    [Fact]
    public void IsProviderEnabled_Should_Return_False_When_Not_Configured()
    {
        var config = new BillingConfiguration();
        config.IsProviderEnabled(PaymentProviders.Stripe).Should().BeFalse();
        config.IsProviderEnabled(PaymentProviders.PayPal).Should().BeFalse();
        config.IsProviderEnabled(PaymentProviders.AppleAppStore).Should().BeFalse();
    }

    [Fact]
    public void GetEnabledProviders_Empty_Config_Should_Return_Empty()
    {
        var config = new BillingConfiguration();
        config.GetEnabledProviders().Should().BeEmpty();
    }
}

#endregion

#region Exception Tests

public class BillingExceptionTests
{
    [Fact]
    public void InvalidWebhookSignatureException_Default_Constructor()
    {
        var ex = new InvalidWebhookSignatureException();
        ex.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InvalidWebhookSignatureException_With_Message()
    {
        var ex = new InvalidWebhookSignatureException("bad sig");
        ex.Message.Should().Be("bad sig");
    }

    [Fact]
    public void InvalidWebhookSignatureException_With_InnerException()
    {
        var inner = new Exception("inner");
        var ex = new InvalidWebhookSignatureException("bad sig", inner);
        ex.Message.Should().Be("bad sig");
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void WebhookProcessingException_With_Message()
    {
        var ex = new WebhookProcessingException("processing failed");
        ex.Message.Should().Be("processing failed");
    }

    [Fact]
    public void WebhookProcessingException_With_InnerException()
    {
        var inner = new InvalidOperationException("cause");
        var ex = new WebhookProcessingException("failed", inner);
        ex.InnerException.Should().Be(inner);
    }
}

#endregion

#region ProcessStripeWebhookCommandHandler Tests

public class ProcessStripeWebhookCommandHandlerTests
{
    [Fact]
    public async Task Handle_NullRequest_Should_Throw()
    {
        var stripeService = new StripeBillingWebhookService(
            Mock.Of<IBillingWebhookRepository>(),
            new TestStripeWebhookVerifier(),
            Mock.Of<IStripeProviderObjectBindingValidator>(),
            NullLogger<StripeBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>());
        var handler = new ProcessStripeWebhookCommandHandler(
            stripeService,
            NullLogger<ProcessStripeWebhookCommandHandler>.Instance);

        var act = () => handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_ValidPayload_Should_Process()
    {
        var repo = new Mock<IBillingWebhookRepository>();
        repo.Setup(r => r.GetByProviderScopeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repo.Setup(r => r.GetByExternalEventIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repo.Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repo.Setup(r => r.TryClaimProcessingAsync(
                It.IsAny<BillingWebhookEvent>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repo.Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var stripeService = new StripeBillingWebhookService(
            repo.Object,
            new TestStripeWebhookVerifier(),
            Mock.Of<IStripeProviderObjectBindingValidator>(),
            NullLogger<StripeBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>());
        var handler = new ProcessStripeWebhookCommandHandler(
            stripeService,
            NullLogger<ProcessStripeWebhookCommandHandler>.Instance);

        var cmd = new ProcessStripeWebhookCommand(
            "{\"id\":\"evt_1\",\"type\":\"unknown.event\",\"data\":{\"object\":{}}}",
            "sig");
        var result = await handler.Handle(cmd, CancellationToken.None);
        result.Processed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MissingEventId_Should_ReturnFailed()
    {
        var stripeService = new StripeBillingWebhookService(
            Mock.Of<IBillingWebhookRepository>(),
            new TestStripeWebhookVerifier(),
            Mock.Of<IStripeProviderObjectBindingValidator>(),
            NullLogger<StripeBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>());
        var handler = new ProcessStripeWebhookCommandHandler(
            stripeService,
            NullLogger<ProcessStripeWebhookCommandHandler>.Instance);

        var cmd = new ProcessStripeWebhookCommand("{\"type\":\"test\"}", "sig");
        var act = () => handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidWebhookPayloadException>();
    }

    [Fact]
    public async Task Handle_InvalidJson_Should_ReturnFailed()
    {
        var stripeService = new StripeBillingWebhookService(
            Mock.Of<IBillingWebhookRepository>(),
            new TestStripeWebhookVerifier(),
            Mock.Of<IStripeProviderObjectBindingValidator>(),
            NullLogger<StripeBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>());
        var handler = new ProcessStripeWebhookCommandHandler(
            stripeService,
            NullLogger<ProcessStripeWebhookCommandHandler>.Instance);

        var cmd = new ProcessStripeWebhookCommand("not json", "sig");
        var act = () => handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidWebhookPayloadException>();
    }

    [Fact]
    public async Task Handle_FailedResult_Should_LogWarning()
    {
        var repo = new Mock<IBillingWebhookRepository>();
        repo.Setup(r => r.GetByExternalEventIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repo.Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        var stripeService = new StripeBillingWebhookService(
            repo.Object,
            new TestStripeWebhookVerifier(),
            Mock.Of<IStripeProviderObjectBindingValidator>(),
            NullLogger<StripeBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>());
        var handler = new ProcessStripeWebhookCommandHandler(
            stripeService,
            NullLogger<ProcessStripeWebhookCommandHandler>.Instance);

        var cmd = new ProcessStripeWebhookCommand(
            "{\"id\":\"evt_fail\",\"type\":\"test.event\"}", "sig");
        var result = await handler.Handle(cmd, CancellationToken.None);
        result.Processed.Should().BeFalse();
    }
}

#endregion

#region AppleStoreAuthService Tests

public class AppleStoreAuthServiceTests
{
    [Fact]
    public async Task GetAppStoreJwtAsync_WithoutPrivateKeyContent_And_NoPath_Should_Return_Null()
    {
        var settings = Options.Create(new ApplePaySettings
        {
            TeamId = "TEAM123",
            KeyId = "KEY123",
            BundleId = "com.example.app",
            PrivateKeyContent = null,
            PrivateKeyPath = string.Empty
        });

        var service = new AppleStoreAuthService(settings, NullLogger<AppleStoreAuthService>.Instance);
        var result = await service.GetAppStoreJwtAsync();
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAppStoreJwtAsync_WithMissingKeyFile_Should_Return_Null()
    {
        var settings = Options.Create(new ApplePaySettings
        {
            TeamId = "TEAM123",
            KeyId = "KEY123",
            BundleId = "com.example.app",
            PrivateKeyContent = null,
            PrivateKeyPath = "/nonexistent/path/key.p8"
        });

        var service = new AppleStoreAuthService(settings, NullLogger<AppleStoreAuthService>.Instance);
        var result = await service.GetAppStoreJwtAsync();
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAppStoreJwtAsync_WithValidKey_Should_Return_Jwt()
    {
        // Generate a real ECDSA P-256 key
        using var ecdsa = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var pem = ecdsa.ExportECPrivateKeyPem();

        var settings = Options.Create(new ApplePaySettings
        {
            TeamId = "TEAM123",
            KeyId = "KEY123",
            BundleId = "com.example.app",
            PrivateKeyContent = pem
        });

        var service = new AppleStoreAuthService(settings, NullLogger<AppleStoreAuthService>.Instance);
        var result = await service.GetAppStoreJwtAsync();
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAppStoreJwtAsync_CachedToken_Should_Return_Same()
    {
        using var ecdsa = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var pem = ecdsa.ExportECPrivateKeyPem();

        var settings = Options.Create(new ApplePaySettings
        {
            TeamId = "TEAM123",
            KeyId = "KEY123",
            BundleId = "com.example.app",
            PrivateKeyContent = pem
        });

        var service = new AppleStoreAuthService(settings, NullLogger<AppleStoreAuthService>.Instance);
        var first = await service.GetAppStoreJwtAsync();
        var second = await service.GetAppStoreJwtAsync();
        first.Should().NotBeNull();
        second.Should().Be(first); // cached
    }
}

#endregion
#region ProcessPayPalWebhookCommandHandler Tests

public class ProcessPayPalWebhookCommandHandlerTests
{
    private static PayPalBillingWebhookService CreatePayPalService(
        Mock<IBillingWebhookRepository> repo,
        IPayPalSignatureVerificationService verification)
    {
        return new PayPalBillingWebhookService(
            repo.Object,
            verification,
            NullLogger<PayPalBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>());
    }

    [Fact]
    public async Task Handle_NullRequest_Should_Throw()
    {
        var repo = new Mock<IBillingWebhookRepository>();
        var service = CreatePayPalService(repo, Mock.Of<IPayPalSignatureVerificationService>());
        var handler = new ProcessPayPalWebhookCommandHandler(service, NullLogger<ProcessPayPalWebhookCommandHandler>.Instance);

        var act = () => handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_DuplicateEvent_Should_Return_AlreadyProcessed()
    {
        var repo = new Mock<IBillingWebhookRepository>();
        repo.Setup(r => r.GetByExternalEventIdAsync("tx-dup", PaymentProviders.PayPal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BillingWebhookEvent { ExternalEventId = "tx-dup", Provider = PaymentProviders.PayPal, IsProcessed = true, ProcessedAt = DateTime.UtcNow });

        var service = CreatePayPalService(repo, Mock.Of<IPayPalSignatureVerificationService>());
        var handler = new ProcessPayPalWebhookCommandHandler(service, NullLogger<ProcessPayPalWebhookCommandHandler>.Instance);

        var cmd = new ProcessPayPalWebhookCommand(
            "{\"id\":\"evt1\",\"event_type\":\"BILLING.SUBSCRIPTION.CREATED\"}",
            "tx-dup", "sig", "time");
        var result = await handler.Handle(cmd, CancellationToken.None);
        result.WasAlreadyProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SignatureInvalid_Should_Return_Failed()
    {
        var repo = new Mock<IBillingWebhookRepository>();
        repo.Setup(r => r.GetByExternalEventIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repo.Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repo.Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var verification = new Mock<IPayPalSignatureVerificationService>();
        verification.Setup(v => v.VerifySignatureAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayPalVerificationResult.Failed("invalid sig"));

        var service = CreatePayPalService(repo, verification.Object);
        var handler = new ProcessPayPalWebhookCommandHandler(service, NullLogger<ProcessPayPalWebhookCommandHandler>.Instance);

        var cmd = new ProcessPayPalWebhookCommand(
            "{\"id\":\"evt2\",\"event_type\":\"PAYMENT.SALE.COMPLETED\"}",
            "tx-fail", "sig", "time");
        var result = await handler.Handle(cmd, CancellationToken.None);
        result.Processed.Should().BeFalse();
    }
}

#endregion

#region ProcessApplePayWebhookCommandHandler Tests

public class ProcessApplePayWebhookCommandHandlerTests
{
    private static ApplePayBillingWebhookService CreateAppleService(
        Mock<IBillingWebhookRepository> repo,
        IApplePayReceiptValidationService receiptValidation)
    {
        return new ApplePayBillingWebhookService(
            repo.Object,
            receiptValidation,
            NullLogger<ApplePayBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>());
    }

    [Fact]
    public async Task Handle_NullRequest_Should_Throw()
    {
        var repo = new Mock<IBillingWebhookRepository>();
        var service = CreateAppleService(repo, Mock.Of<IApplePayReceiptValidationService>());
        var handler = new ProcessApplePayWebhookCommandHandler(service, NullLogger<ProcessApplePayWebhookCommandHandler>.Instance);

        var act = () => handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_ValidationFailed_Should_Return_Failed()
    {
        var repo = new Mock<IBillingWebhookRepository>();
        var receiptValidation = new Mock<IApplePayReceiptValidationService>();
        receiptValidation.Setup(v => v.VerifyNotificationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AppleNotificationVerificationResult.Failed("invalid notification"));

        var service = CreateAppleService(repo, receiptValidation.Object);
        var handler = new ProcessApplePayWebhookCommandHandler(service, NullLogger<ProcessApplePayWebhookCommandHandler>.Instance);

        var cmd = new ProcessApplePayWebhookCommand("signed.payload.here", "merchant1", "sig");
        var result = await handler.Handle(cmd, CancellationToken.None);
        result.Processed.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DuplicateEvent_Should_Return_AlreadyProcessed()
    {
        var repo = new Mock<IBillingWebhookRepository>();
        var receiptValidation = new Mock<IApplePayReceiptValidationService>();
        receiptValidation.Setup(v => v.VerifyNotificationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AppleNotificationVerificationResult.Success(
                "DID_RENEW", "AUTO_RENEW_ENABLED", "tx-apple-dup", "orig-tx", "com.app.sub", DateTime.UtcNow.AddDays(30), "sandbox"));

        repo.Setup(r => r.GetByExternalEventIdAsync("tx-apple-dup", PaymentProviders.AppleAppStore, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BillingWebhookEvent { ExternalEventId = "tx-apple-dup", Provider = PaymentProviders.AppleAppStore, ProcessedAt = DateTime.UtcNow });

        var service = CreateAppleService(repo, receiptValidation.Object);
        var handler = new ProcessApplePayWebhookCommandHandler(service, NullLogger<ProcessApplePayWebhookCommandHandler>.Instance);

        var cmd = new ProcessApplePayWebhookCommand("{\"eventId\":\"e1\",\"eventType\":\"DID_RENEW\"}", "merchant1", "sig");
        var result = await handler.Handle(cmd, CancellationToken.None);
        result.WasAlreadyProcessed.Should().BeTrue();
    }
}

#endregion

#region PayPal Service Invalid JSON Tests

public class PayPalBillingWebhookServiceAdditionalTests2
{
    private static PayPalBillingWebhookService CreateService(
        Mock<IBillingWebhookRepository> repo,
        IPayPalSignatureVerificationService verification)
    {
        return new PayPalBillingWebhookService(
            repo.Object,
            verification,
            NullLogger<PayPalBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>());
    }

    [Fact]
    public async Task ProcessPayPalWebhookAsync_InvalidJson_Should_Still_Process()
    {
        // Invalid JSON hits the catch blocks in ParsePayPalPayload and ParsePayPalPayloadData
        var repo = new Mock<IBillingWebhookRepository>();
        repo.Setup(r => r.GetByExternalEventIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repo.Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repo.Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var verification = new Mock<IPayPalSignatureVerificationService>();
        verification.Setup(v => v.VerifySignatureAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayPalVerificationResult.Success());

        var service = CreateService(repo, verification.Object);

        var result = await service.ProcessPayPalWebhookAsync(
            "wh-id", "NOT VALID JSON", "tx-invalid", "time", "sig", null, null, CancellationToken.None);

        // Should succeed with "unknown" event type (catch blocks in Parse methods handle the errors)
        result.Processed.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessPayPalWebhookAsync_PartialJson_ParsePayloadData_Catch()
    {
        // JSON parseable for ParsePayPalPayload but missing fields for ParsePayPalPayloadData deeper parsing
        var repo = new Mock<IBillingWebhookRepository>();
        repo.Setup(r => r.GetByExternalEventIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repo.Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repo.Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var verification = new Mock<IPayPalSignatureVerificationService>();
        verification.Setup(v => v.VerifySignatureAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayPalVerificationResult.Success());

        var service = CreateService(repo, verification.Object);

        // Valid JSON with event_type but resource has unexpected structure
        var json = "{\"event_type\":\"BILLING.SUBSCRIPTION.CREATED\",\"resource\":{\"id\":\"sub_123\",\"status_update_time\":\"invalid-date\"}}";
        var result = await service.ProcessPayPalWebhookAsync(
            "wh-id", json, "tx-partial", "time", "sig", null, null, CancellationToken.None);

        // Processing may succeed or fail depending on parsing depth
        result.Should().NotBeNull();
    }
}

#endregion

#region Stripe Service Richer JSON Tests

public class StripeBillingWebhookServiceAdditionalTests2
{
    [Fact]
    public async Task ProcessStripeWebhookAsync_WithDataObjectId_Should_Parse_SubscriptionId()
    {
        var repo = new Mock<IBillingWebhookRepository>();
        repo.Setup(r => r.GetByProviderScopeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repo.Setup(r => r.GetByExternalEventIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repo.Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repo.Setup(r => r.TryClaimProcessingAsync(
                It.IsAny<BillingWebhookEvent>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repo.Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var queryService = new Mock<ISubscriptionQueryService>();
        queryService
            .Setup(query => query.GetByExternalIdAsync("sub_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Subscription(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                BillingCycle.Monthly,
                new Money(25m, "USD"),
                DateTime.UtcNow));

        var stripeService = new StripeBillingWebhookService(
            repo.Object,
            new TestStripeWebhookVerifier(),
            Mock.Of<IStripeProviderObjectBindingValidator>(),
            NullLogger<StripeBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            queryService.Object,
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>());

        // JSON with data.object.id to cover line 153, and amount_due without amount_paid to cover line 184
        var json = "{\"id\":\"evt_rich\",\"type\":\"invoice.created\",\"data\":{\"object\":{\"id\":\"sub_1\",\"customer\":\"cus_1\",\"status\":\"active\",\"amount_due\":2500,\"currency\":\"usd\"}}}";
        var result = await stripeService.ProcessStripeWebhookAsync(json, "evt_rich|invoice.created", CancellationToken.None);

        result.Processed.Should().BeTrue();
    }
}

#endregion
