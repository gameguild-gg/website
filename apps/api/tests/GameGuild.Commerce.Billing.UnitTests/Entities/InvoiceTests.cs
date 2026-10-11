using FluentAssertions;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Entities;

/// <summary>
///     Tests for Invoice entity immutability and single-payment invariant.
///     These tests verify:
///     - Invariant #2: Invoice never changes value after issuance
///     - Invariant #3: Payment never applied to multiple invoices (unique PaymentId index)
/// </summary>
public class InvoiceTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidTenantId_ShouldCreateInvoice()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();

        // Act
        var invoice = new Invoice(tenantId, subscriptionId, 29.99m);

        // Assert
        invoice.TenantId.Should().Be(tenantId);
        invoice.SubscriptionId.Should().Be(subscriptionId);
        invoice.Subtotal.Should().Be(29.99m);
        invoice.Total.Should().Be(29.99m);
        invoice.Status.Should().Be(InvoiceStatus.Draft);
        invoice.IsImmutable.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithEmptyTenantId_ShouldThrow()
    {
        // Arrange - Invariant #1: No financial entity exists without valid TenantId
        var emptyTenantId = Guid.Empty;
        var subscriptionId = Guid.NewGuid();

        // Act & Assert
        var act = () => new Invoice(emptyTenantId, subscriptionId, 29.99m);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*TenantId*required*");
    }

    [Fact]
    public void Constructor_WithIdempotencyKey_ShouldStoreIt()
    {
        var tenantId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var idempotencyKey = $"subscription:{subscriptionId}:cycle:1:invoice";

        var invoice = new Invoice(tenantId, subscriptionId, 29.99m, "USD", idempotencyKey);

        invoice.IdempotencyKey.Should().Be(idempotencyKey);
    }

    [Fact]
    public void Constructor_WithOverlongIdempotencyKey_ShouldThrow()
    {
        var act = () => new Invoice(Guid.NewGuid(), Guid.NewGuid(), 29.99m, "USD", new string('k', 201));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Idempotency key*");
    }

    [Fact]
    public void SetMetadata_Draft_ShouldStoreJson()
    {
        var invoice = CreateDraftInvoice();

        invoice.SetMetadata("""{"billingCycleNumber":1}""");

        invoice.Metadata.Should().Contain("billingCycleNumber");
    }

    [Fact]
    public void SetMetadata_OnIssuedInvoice_ShouldThrow()
    {
        var invoice = CreateDraftInvoice();
        invoice.Issue();

        var act = () => invoice.SetMetadata("""{"billingCycleNumber":1}""");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SetDescription_Draft_ShouldStoreLineLabel()
    {
        var invoice = CreateDraftInvoice();

        invoice.SetDescription("Subscription billing cycle 1");

        invoice.Description.Should().Be("Subscription billing cycle 1");
    }

    [Fact]
    public void SetDescription_OnIssuedInvoice_ShouldThrow()
    {
        var invoice = CreateDraftInvoice();
        invoice.Issue();

        var act = () => invoice.SetDescription("Subscription billing cycle 1");

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Immutability Tests

    [Fact]
    public void Issue_ShouldMakeInvoiceImmutable()
    {
        // Arrange - Invariant #2: Invoice never changes value after issuance
        var invoice = CreateDraftInvoice();

        // Act
        invoice.Issue();

        // Assert
        invoice.Status.Should().Be(InvoiceStatus.Open);
        invoice.IsImmutable.Should().BeTrue();
        invoice.IssuedAt.Should().NotBeNull();
    }

    [Fact]
    public void SetBillingPeriod_AfterIssue_ShouldThrow()
    {
        // Arrange
        var invoice = CreateIssuedInvoice();

        // Act & Assert
        var act = () => invoice.SetBillingPeriod(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*immutable*");
    }

    [Fact]
    public void ApplyDiscount_AfterIssue_ShouldThrow()
    {
        // Arrange
        var invoice = CreateIssuedInvoice();

        // Act & Assert
        var act = () => invoice.ApplyDiscount(5.00m);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*immutable*");
    }

    [Fact]
    public void SetTax_AfterIssue_ShouldThrow()
    {
        // Arrange
        var invoice = CreateIssuedInvoice();

        // Act & Assert
        var act = () => invoice.SetTax(2.50m);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*immutable*");
    }

    [Fact]
    public void Draft_AllowsModifications()
    {
        // Arrange
        var invoice = CreateDraftInvoice();

        // Act - All these should work on draft
        invoice.SetBillingPeriod(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        invoice.SetTax(2.50m);
        invoice.ApplyDiscount(5.00m);

        // Assert
        invoice.TaxAmount.Should().Be(2.50m);
        invoice.DiscountAmount.Should().Be(5.00m);
        invoice.PeriodStart.Should().NotBeNull();
        invoice.PeriodEnd.Should().NotBeNull();
    }

    [Fact]
    public void Issue_WithZeroTotal_ShouldThrow()
    {
        // Arrange
        var invoice = CreateDraftInvoice(0m);

        // Act & Assert
        var act = () => invoice.Issue();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*zero or negative*");
    }

    #endregion

    #region Single Payment Invariant Tests

    [Fact]
    public void RecordPayment_OnIssuedInvoice_ShouldSucceed()
    {
        // Arrange
        var invoice = CreateIssuedInvoice();
        var paymentId = Guid.NewGuid();

        // Act
        invoice.RecordPayment(paymentId, 29.99m, DateTime.UtcNow);

        // Assert
        invoice.PaymentId.Should().Be(paymentId);
        invoice.AmountPaid.Should().Be(29.99m);
        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public void RecordPayment_WithDifferentPaymentId_ShouldThrow()
    {
        // Arrange - Invariant #3: Payment never applied to multiple invoices
        var invoice = CreateIssuedInvoice();
        var firstPaymentId = Guid.NewGuid();
        var secondPaymentId = Guid.NewGuid();

        // Record first payment (partial)
        invoice.RecordPayment(firstPaymentId, 15.00m, DateTime.UtcNow);

        // Act & Assert - Try to record with different payment ID
        var act = () => invoice.RecordPayment(secondPaymentId, 14.99m, DateTime.UtcNow);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already has payment*Single payment*");
    }

    [Fact]
    public void RecordPayment_WithSamePaymentId_ShouldUpdateAmount()
    {
        // Arrange - Idempotent update with same payment ID
        var invoice = CreateIssuedInvoice();
        var paymentId = Guid.NewGuid();

        // Act
        invoice.RecordPayment(paymentId, 15.00m, DateTime.UtcNow);
        invoice.RecordPayment(paymentId, 29.99m, DateTime.UtcNow);

        // Assert
        invoice.AmountPaid.Should().Be(29.99m);
        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public void RecordPayment_OnDraftInvoice_ShouldThrow()
    {
        // Arrange
        var invoice = CreateDraftInvoice();
        var paymentId = Guid.NewGuid();

        // Act & Assert
        var act = () => invoice.RecordPayment(paymentId, 29.99m, DateTime.UtcNow);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*draft*");
    }

    [Fact]
    public void RecordPayment_OnPaidInvoice_ShouldThrow()
    {
        // Arrange
        var invoice = CreateIssuedInvoice();
        var paymentId = Guid.NewGuid();
        invoice.RecordPayment(paymentId, 29.99m, DateTime.UtcNow);

        // Act & Assert
        var act = () => invoice.RecordPayment(paymentId, 10.00m, DateTime.UtcNow);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already paid*");
    }

    [Fact]
    public void RecordPayment_OnVoidedInvoice_ShouldThrow()
    {
        // Arrange
        var invoice = CreateIssuedInvoice();
        invoice.Void("Test void");

        var paymentId = Guid.NewGuid();

        // Act & Assert
        var act = () => invoice.RecordPayment(paymentId, 29.99m, DateTime.UtcNow);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*voided*");
    }

    #endregion

    #region Invoice Status Tests

    [Fact]
    public void Void_ShouldChangeStatusToVoid()
    {
        // Arrange
        var invoice = CreateIssuedInvoice();

        // Act
        invoice.Void("Customer requested cancellation");

        // Assert
        invoice.Status.Should().Be(InvoiceStatus.Void);
        invoice.VoidedAt.Should().NotBeNull();
        invoice.VoidReason.Should().Be("Customer requested cancellation");
    }

    [Fact]
    public void MarkUncollectible_ShouldChangeStatusToUncollectible()
    {
        // Arrange
        var invoice = CreateIssuedInvoice();

        // Act
        invoice.MarkUncollectible();

        // Assert
        invoice.Status.Should().Be(InvoiceStatus.Uncollectible);
    }

    [Fact]
    public void MarkPastDue_ShouldChangeStatusToPastDue()
    {
        // Arrange
        var invoice = CreateIssuedInvoice();

        // Act
        invoice.MarkPastDue();

        // Assert
        invoice.Status.Should().Be(InvoiceStatus.PastDue);
    }

    #endregion

    #region Amount Calculation Tests

    [Fact]
    public void Total_ShouldBeCalculatedCorrectly()
    {
        // Arrange
        var invoice = CreateDraftInvoice(100m);

        // Act
        invoice.SetTax(10m);
        invoice.ApplyDiscount(20m);

        // Assert
        // Total = Subtotal - Discount + Tax = 100 - 20 + 10 = 90
        invoice.Total.Should().Be(90m);
    }

    [Fact]
    public void AmountRemaining_ShouldCalculateCorrectly()
    {
        // Arrange
        var invoice = CreateIssuedInvoice();
        var paymentId = Guid.NewGuid();

        // Act
        invoice.RecordPayment(paymentId, 10.00m, DateTime.UtcNow);

        // Assert
        invoice.AmountRemaining.Should().Be(19.99m);
    }

    #endregion

    #region Helper Methods

    private static Invoice CreateDraftInvoice(decimal amount = 29.99m)
    {
        return new Invoice(
            tenantId: Guid.NewGuid(),
            subscriptionId: Guid.NewGuid(),
            amount: amount,
            currency: "USD"
        );
    }

    private static Invoice CreateIssuedInvoice(decimal amount = 29.99m)
    {
        var invoice = CreateDraftInvoice(amount);
        invoice.Issue();
        return invoice;
    }

    #endregion
}
