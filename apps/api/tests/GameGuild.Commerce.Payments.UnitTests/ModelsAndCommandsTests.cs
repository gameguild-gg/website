using FluentAssertions;
using GameGuild.Commerce.Payments;
using GameGuild.Commerce.Payments.Commands.CloseWallet;
using GameGuild.Commerce.Payments.Commands.FreezeWallet;
using GameGuild.Commerce.Payments.Commands.UnfreezeWallet;
using GameGuild.Commerce.Payments.Models;
using GameGuild.Commerce.Payments.Queries.GetWalletAuditLog;
using GameGuild.Commerce.Payments.Queries.GetWalletById;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests;

#region Enum Tests

public class AuditActionEnumTests
{
    [Theory]
    [InlineData(AuditAction.Created, 0)]
    [InlineData(AuditAction.Updated, 1)]
    [InlineData(AuditAction.Deleted, 2)]
    [InlineData(AuditAction.Restored, 3)]
    [InlineData(AuditAction.StatusChanged, 4)]
    [InlineData(AuditAction.PermissionChanged, 5)]
    [InlineData(AuditAction.ConfigurationChanged, 6)]
    [InlineData(AuditAction.Other, 7)]
    public void AuditAction_ShouldHaveExpectedValues(AuditAction action, int expected) =>
        ((int)action).Should().Be(expected);
}

public class CustomerTypeEnumTests
{
    [Theory]
    [InlineData(CustomerType.B2C, 0)]
    [InlineData(CustomerType.B2B, 1)]
    public void CustomerType_ShouldHaveExpectedValues(CustomerType type, int expected) =>
        ((int)type).Should().Be(expected);
}

public class DiscountTypeEnumTests
{
    [Theory]
    [InlineData(DiscountType.Percentage, 0)]
    [InlineData(DiscountType.FixedAmount, 1)]
    [InlineData(DiscountType.FreeMonths, 2)]
    public void DiscountType_ShouldHaveExpectedValues(DiscountType type, int expected) =>
        ((int)type).Should().Be(expected);
}

public class DisputeResolutionEnumTests
{
    [Theory]
    [InlineData(DisputeResolution.Won, 0)]
    [InlineData(DisputeResolution.Lost, 1)]
    [InlineData(DisputeResolution.PartialRefund, 2)]
    [InlineData(DisputeResolution.MerchantCredit, 3)]
    [InlineData(DisputeResolution.Replacement, 4)]
    [InlineData(DisputeResolution.MutualAgreement, 5)]
    public void DisputeResolution_ShouldHaveExpectedValues(DisputeResolution res, int expected) =>
        ((int)res).Should().Be(expected);
}

public class DisputeStatusEnumTests
{
    [Theory]
    [InlineData(DisputeStatus.Submitted, 0)]
    [InlineData(DisputeStatus.UnderReview, 1)]
    [InlineData(DisputeStatus.PendingCustomerResponse, 2)]
    [InlineData(DisputeStatus.PendingMerchantResponse, 3)]
    [InlineData(DisputeStatus.Resolved, 4)]
    [InlineData(DisputeStatus.Won, 5)]
    [InlineData(DisputeStatus.Lost, 6)]
    [InlineData(DisputeStatus.Cancelled, 7)]
    public void DisputeStatus_ShouldHaveExpectedValues(DisputeStatus status, int expected) =>
        ((int)status).Should().Be(expected);
}

public class DisputeTypeEnumTests
{
    [Theory]
    [InlineData(DisputeType.Fraudulent, 0)]
    [InlineData(DisputeType.ProductNotReceived, 1)]
    [InlineData(DisputeType.ProductNotAsDescribed, 2)]
    [InlineData(DisputeType.Duplicate, 3)]
    [InlineData(DisputeType.IncorrectAmount, 4)]
    [InlineData(DisputeType.ServiceNotProvided, 5)]
    [InlineData(DisputeType.CreditNotProcessed, 6)]
    [InlineData(DisputeType.Other, 7)]
    public void DisputeType_ShouldHaveExpectedValues(DisputeType type, int expected) =>
        ((int)type).Should().Be(expected);
}

public class EvidenceTypeEnumTests
{
    [Theory]
    [InlineData(EvidenceType.Receipt, 0)]
    [InlineData(EvidenceType.Communication, 1)]
    [InlineData(EvidenceType.Photo, 2)]
    [InlineData(EvidenceType.Video, 3)]
    [InlineData(EvidenceType.ShippingInfo, 4)]
    [InlineData(EvidenceType.Contract, 5)]
    [InlineData(EvidenceType.BankStatement, 6)]
    [InlineData(EvidenceType.Documentation, 7)]
    [InlineData(EvidenceType.Other, 8)]
    public void EvidenceType_ShouldHaveExpectedValues(EvidenceType type, int expected) =>
        ((int)type).Should().Be(expected);
}

public class LedgerEntryTypeEnumTests
{
    [Theory]
    [InlineData(LedgerEntryType.Revenue, 0)]
    [InlineData(LedgerEntryType.Expense, 1)]
    [InlineData(LedgerEntryType.Refund, 2)]
    [InlineData(LedgerEntryType.Fee, 3)]
    [InlineData(LedgerEntryType.Transfer, 4)]
    [InlineData(LedgerEntryType.Adjustment, 5)]
    [InlineData(LedgerEntryType.Credit, 6)]
    [InlineData(LedgerEntryType.Debit, 7)]
    public void LedgerEntryType_ShouldHaveExpectedValues(LedgerEntryType type, int expected) =>
        ((int)type).Should().Be(expected);
}

public class PricingRuleTypeEnumTests
{
    [Theory]
    [InlineData(PricingRuleType.Percentage, 0)]
    [InlineData(PricingRuleType.FixedAmount, 1)]
    [InlineData(PricingRuleType.BuyXGetY, 2)]
    [InlineData(PricingRuleType.VolumeDiscount, 3)]
    [InlineData(PricingRuleType.TieredPricing, 4)]
    [InlineData(PricingRuleType.Bundle, 5)]
    public void PricingRuleType_ShouldHaveExpectedValues(PricingRuleType type, int expected) =>
        ((int)type).Should().Be(expected);
}

public class RevenueEventStatusEnumTests
{
    [Theory]
    [InlineData(RevenueEventStatus.Pending, 0)]
    [InlineData(RevenueEventStatus.Processed, 1)]
    [InlineData(RevenueEventStatus.Failed, 2)]
    [InlineData(RevenueEventStatus.Cancelled, 3)]
    public void RevenueEventStatus_ShouldHaveExpectedValues(RevenueEventStatus status, int expected) =>
        ((int)status).Should().Be(expected);
}

public class RevenueEventTypeEnumTests
{
    [Theory]
    [InlineData(RevenueEventType.PaymentReceived, 0)]
    [InlineData(RevenueEventType.SubscriptionStarted, 1)]
    [InlineData(RevenueEventType.SubscriptionRenewed, 2)]
    [InlineData(RevenueEventType.SubscriptionCancelled, 3)]
    [InlineData(RevenueEventType.RefundProcessed, 4)]
    [InlineData(RevenueEventType.Chargeback, 5)]
    [InlineData(RevenueEventType.FeeCharged, 6)]
    [InlineData(RevenueEventType.CreditIssued, 7)]
    [InlineData(RevenueEventType.Adjustment, 8)]
    [InlineData(RevenueEventType.Other, 9)]
    public void RevenueEventType_ShouldHaveExpectedValues(RevenueEventType type, int expected) =>
        ((int)type).Should().Be(expected);
}

public class RevenueSourceEnumTests
{
    [Theory]
    [InlineData(RevenueSource.Subscription, 0)]
    [InlineData(RevenueSource.OneTimePayment, 1)]
    [InlineData(RevenueSource.AddOn, 2)]
    [InlineData(RevenueSource.ServiceFee, 3)]
    [InlineData(RevenueSource.TransactionFee, 4)]
    [InlineData(RevenueSource.SetupFee, 5)]
    [InlineData(RevenueSource.UsageFee, 6)]
    [InlineData(RevenueSource.Other, 7)]
    public void RevenueSource_ShouldHaveExpectedValues(RevenueSource source, int expected) =>
        ((int)source).Should().Be(expected);
}

public class TaxJurisdictionTypeEnumTests
{
    [Theory]
    [InlineData(TaxJurisdictionType.Country, 0)]
    [InlineData(TaxJurisdictionType.State, 1)]
    [InlineData(TaxJurisdictionType.Province, 2)]
    [InlineData(TaxJurisdictionType.Region, 3)]
    [InlineData(TaxJurisdictionType.City, 4)]
    [InlineData(TaxJurisdictionType.County, 5)]
    [InlineData(TaxJurisdictionType.District, 6)]
    public void TaxJurisdictionType_ShouldHaveExpectedValues(TaxJurisdictionType type, int expected) =>
        ((int)type).Should().Be(expected);
}

public class TaxRuleTypeEnumTests
{
    [Theory]
    [InlineData(TaxRuleType.Standard, 0)]
    [InlineData(TaxRuleType.Reduced, 1)]
    [InlineData(TaxRuleType.ZeroRated, 2)]
    [InlineData(TaxRuleType.Exempt, 3)]
    [InlineData(TaxRuleType.ReverseCharge, 4)]
    [InlineData(TaxRuleType.WithholdingTax, 5)]
    [InlineData(TaxRuleType.Compound, 6)]
    [InlineData(TaxRuleType.Custom, 7)]
    public void TaxRuleType_ShouldHaveExpectedValues(TaxRuleType type, int expected) =>
        ((int)type).Should().Be(expected);
}

public class TaxTypeEnumTests
{
    [Theory]
    [InlineData(TaxType.VAT, 0)]
    [InlineData(TaxType.GST, 1)]
    [InlineData(TaxType.SalesTax, 2)]
    [InlineData(TaxType.ServiceTax, 3)]
    [InlineData(TaxType.WithholdingTax, 4)]
    [InlineData(TaxType.ExciseTax, 5)]
    [InlineData(TaxType.CustomsDuty, 6)]
    [InlineData(TaxType.Other, 7)]
    public void TaxType_ShouldHaveExpectedValues(TaxType type, int expected) =>
        ((int)type).Should().Be(expected);
}

public class TransactionStatusEnumTests
{
    [Theory]
    [InlineData(TransactionStatus.Pending, 0)]
    [InlineData(TransactionStatus.Processing, 1)]
    [InlineData(TransactionStatus.Completed, 2)]
    [InlineData(TransactionStatus.Failed, 3)]
    [InlineData(TransactionStatus.Cancelled, 4)]
    [InlineData(TransactionStatus.Reversed, 5)]
    public void TransactionStatus_ShouldHaveExpectedValues(TransactionStatus status, int expected) =>
        ((int)status).Should().Be(expected);
}

public class WalletTransactionTypeEnumTests
{
    [Theory]
    [InlineData(WalletTransactionType.Credit, 0)]
    [InlineData(WalletTransactionType.Debit, 1)]
    [InlineData(WalletTransactionType.TransferIn, 2)]
    [InlineData(WalletTransactionType.TransferOut, 3)]
    [InlineData(WalletTransactionType.Refund, 4)]
    [InlineData(WalletTransactionType.Fee, 5)]
    [InlineData(WalletTransactionType.Adjustment, 6)]
    public void WalletTransactionType_ShouldHaveExpectedValues(WalletTransactionType type, int expected) =>
        ((int)type).Should().Be(expected);
}

#endregion

#region PaymentResult Factory Tests

public class PaymentResultTests
{
    [Fact]
    public void CreateSuccess_ShouldSetProperties()
    {
        var amount = new Money(99.99m, "USD");
        var invoiceId = Guid.NewGuid();
        var result = PaymentResult.CreateSuccess(amount, "pay-1", "txn-1", invoiceId);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(PaymentStatus.Succeeded);
        result.Amount.Should().Be(amount);
        result.PaymentId.Should().Be("pay-1");
        result.TransactionId.Should().Be("txn-1");
        result.InvoiceId.Should().Be(invoiceId);
        result.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public void Failed_ShouldSetProperties()
    {
        var invoiceId = Guid.NewGuid();
        var result = PaymentResult.Failed("Insufficient funds", invoiceId);

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Failed);
        result.FailureReason.Should().Be("Insufficient funds");
        result.InvoiceId.Should().Be(invoiceId);
    }

    [Fact]
    public void Pending_ShouldSetProperties()
    {
        var amount = new Money(50.00m, "EUR");
        var result = PaymentResult.Pending(amount, "pay-2");

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Pending);
        result.Amount.Should().Be(amount);
        result.PaymentId.Should().Be("pay-2");
    }
}

#endregion

#region PaymentCancellationResult Tests

public class PaymentCancellationResultTests
{
    [Fact]
    public void ShouldSetRequiredAndOptionalProperties()
    {
        var paymentId = Guid.NewGuid();
        var canceledBy = Guid.NewGuid();
        var result = new PaymentCancellationResult
        {
            PaymentId = paymentId,
            CancellationReason = "Customer requested",
            CanceledAt = DateTime.UtcNow,
            CanceledBy = canceledBy,
            Success = true,
            RefundProcessed = true,
            RefundAmount = 25.00m
        };

        result.PaymentId.Should().Be(paymentId);
        result.CancellationReason.Should().Be("Customer requested");
        result.CanceledBy.Should().Be(canceledBy);
        result.Success.Should().BeTrue();
        result.RefundProcessed.Should().BeTrue();
        result.RefundAmount.Should().Be(25.00m);
        result.ErrorMessage.Should().BeNull();
    }
}

#endregion

#region PaymentRetryResult Tests

public class PaymentRetryResultTests
{
    [Fact]
    public void Defaults_ShouldBeCorrect()
    {
        var result = new PaymentRetryResult();
        result.Success.Should().BeFalse();
        result.RetryAttempt.Should().Be(0);
        result.NextRetryAt.Should().BeNull();
        result.PaymentResult.Should().BeNull();
        result.MaxRetriesReached.Should().BeFalse();
        result.FailureReason.Should().BeNull();
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var result = new PaymentRetryResult
        {
            Success = true,
            RetryAttempt = 3,
            MaxRetriesReached = true,
            FailureReason = "Gateway timeout"
        };

        result.Success.Should().BeTrue();
        result.RetryAttempt.Should().Be(3);
        result.MaxRetriesReached.Should().BeTrue();
        result.FailureReason.Should().Be("Gateway timeout");
    }
}

#endregion

#region TaxCalculationRequest/Result Tests

public class TaxCalculationRequestTests
{
    [Fact]
    public void ShouldSetRequiredAndDefaults()
    {
        var req = new TaxCalculationRequest
        {
            JurisdictionCode = "US-CA",
            Amount = 100.00m,
            Currency = "USD",
            CustomerType = CustomerType.B2C
        };

        req.JurisdictionCode.Should().Be("US-CA");
        req.Amount.Should().Be(100.00m);
        req.Currency.Should().Be("USD");
        req.CustomerType.Should().Be(CustomerType.B2C);
        req.ProductCategory.Should().BeNull();
        req.CustomerVatNumber.Should().BeNull();
        req.IsTaxInclusive.Should().BeFalse();
        req.ApplicableExemptions.Should().BeEmpty();
    }
}

public class TaxCalculationResultTests
{
    [Fact]
    public void ShouldSetProperties()
    {
        var result = new TaxCalculationResult
        {
            SubtotalAmount = 100.00m,
            TaxAmount = 8.50m,
            TotalAmount = 108.50m,
            EffectiveTaxRate = 0.085m,
            JurisdictionCode = "US-CA"
        };

        result.SubtotalAmount.Should().Be(100.00m);
        result.TaxAmount.Should().Be(8.50m);
        result.TotalAmount.Should().Be(108.50m);
        result.EffectiveTaxRate.Should().Be(0.085m);
    }

    [Fact]
    public void Defaults_ShouldBeCorrect()
    {
        var result = new TaxCalculationResult();
        result.SubtotalAmount.Should().Be(0m);
        result.JurisdictionCode.Should().BeEmpty();
    }
}

#endregion

#region WalletDtos Tests

public class WalletDtosTests
{
    [Fact]
    public void PatchWalletRequest_ShouldSetDefaults()
    {
        var req = new PatchWalletRequest();
        req.Currency.Should().BeNull();
        req.DailyLimit.Should().BeNull();
        req.MonthlyLimit.Should().BeNull();
    }

    [Fact]
    public void PatchWalletRequest_ShouldSetValues()
    {
        var req = new PatchWalletRequest("EUR", 500m, 10000m);
        req.Currency.Should().Be("EUR");
        req.DailyLimit.Should().Be(500m);
        req.MonthlyLimit.Should().Be(10000m);
    }

    [Fact]
    public void FreezeWalletRequest_ShouldSetReason()
    {
        var req = new FreezeWalletRequest("Suspicious activity");
        req.Reason.Should().Be("Suspicious activity");
    }

    [Fact]
    public void WalletAuditEntry_ShouldSetProperties()
    {
        var entry = new WalletAuditEntry(
            Guid.NewGuid(), Guid.NewGuid(), "Deposit",
            "Added funds", 100m, 500m, DateTime.UtcNow, "admin");

        entry.Action.Should().Be("Deposit");
        entry.Amount.Should().Be(100m);
        entry.BalanceAfter.Should().Be(500m);
        entry.PerformedBy.Should().Be("admin");
    }

    [Fact]
    public void WalletAuditLogResponse_ShouldSetProperties()
    {
        var entries = new List<WalletAuditEntry>();
        var response = new WalletAuditLogResponse(entries, 0, 1, 10, 0);
        response.Items.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(10);
        response.TotalPages.Should().Be(0);
    }

    [Fact]
    public void WalletListResponse_ShouldSetProperties()
    {
        var items = new List<WalletSummary>();
        var response = new WalletListResponse(items, 0, 1, 20, 0);
        response.Items.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
    }

    [Fact]
    public void WalletSummary_ShouldSetProperties()
    {
        var summary = new WalletSummary(
            Guid.NewGuid(), Guid.NewGuid(), "USD", 250.50m, false, DateTime.UtcNow, null);
        summary.Currency.Should().Be("USD");
        summary.Balance.Should().Be(250.50m);
        summary.IsFrozen.Should().BeFalse();
        summary.LastTransactionAt.Should().BeNull();
    }
}

#endregion

#region Command Record Tests

public class PaymentCommandRecordTests
{
    [Fact]
    public void AddFundsCommand_ShouldSetProperties()
    {
        var userId = Guid.NewGuid();
        var cmd = new AddFundsCommand(userId, 100m, "Deposit", "ref-1");
        cmd.UserId.Should().Be(userId);
        cmd.Amount.Should().Be(100m);
        cmd.Description.Should().Be("Deposit");
        cmd.ReferenceId.Should().Be("ref-1");
    }

    [Fact]
    public void AddFundsCommand_Defaults()
    {
        var cmd = new AddFundsCommand(Guid.NewGuid(), 50m, "Top up");
        cmd.ReferenceId.Should().BeNull();
    }

    [Fact]
    public void CancelPaymentCommand_ShouldSetProperties()
    {
        var paymentId = Guid.NewGuid();
        var canceledBy = Guid.NewGuid();
        var cmd = new CancelPaymentCommand(paymentId, "No longer needed", canceledBy);
        cmd.PaymentId.Should().Be(paymentId);
        cmd.CancellationReason.Should().Be("No longer needed");
        cmd.CanceledBy.Should().Be(canceledBy);
    }

    [Fact]
    public void CancelDisputeCommand_ShouldSetProperties()
    {
        var cmd = new CancelDisputeCommand(Guid.NewGuid(), "Resolved externally");
        cmd.Reason.Should().Be("Resolved externally");
    }

    [Fact]
    public void CloseWalletCommand_ShouldSetWalletId()
    {
        var walletId = Guid.NewGuid();
        var cmd = new CloseWalletCommand(walletId);
        cmd.WalletId.Should().Be(walletId);
    }

    [Fact]
    public void CreateDisputeCommand_ShouldSetProperties()
    {
        var cmd = new CreateDisputeCommand(
            Guid.NewGuid(), Guid.NewGuid(), DisputeType.Fraudulent,
            50.00m, "Unauthorized charge", "I did not make this purchase");
        cmd.Type.Should().Be(DisputeType.Fraudulent);
        cmd.Amount.Should().Be(50.00m);
        cmd.Reason.Should().Be("Unauthorized charge");
    }

    [Fact]
    public void CreateWalletCommand_ShouldSetDefaults()
    {
        var cmd = new CreateWalletCommand(Guid.NewGuid());
        cmd.Currency.Should().Be("USD");
    }

    [Fact]
    public void DeductFundsCommand_ShouldSetProperties()
    {
        var cmd = new DeductFundsCommand(Guid.NewGuid(), 25m, "Purchase", "ref-2");
        cmd.Amount.Should().Be(25m);
        cmd.Description.Should().Be("Purchase");
        cmd.ReferenceId.Should().Be("ref-2");
    }

    [Fact]
    public void FreezeWalletCommand_ShouldSetProperties()
    {
        var cmd = new FreezeWalletCommand(Guid.NewGuid(), "Fraud detected");
        cmd.Reason.Should().Be("Fraud detected");
    }

    [Fact]
    public void LockWalletCommand_ShouldSetProperties()
    {
        var cmd = new LockWalletCommand(Guid.NewGuid(), "Account review");
        cmd.Reason.Should().Be("Account review");
    }

    [Fact]
    public void ProcessPaymentCommand_ShouldSetProperties()
    {
        var cmd = new ProcessPaymentCommand(Guid.NewGuid(), Guid.NewGuid(), 99.99m, "pm_1234");
        cmd.Amount.Should().Be(99.99m);
        cmd.PaymentMethodId.Should().Be("pm_1234");
    }

    [Fact]
    public void ProcessRefundCommand_ShouldSetProperties()
    {
        var paymentId = Guid.NewGuid();
        var cmd = new ProcessRefundCommand(paymentId, 25.00m, "Defective product");
        cmd.PaymentId.Should().Be(paymentId);
        cmd.Amount.Should().Be(25.00m);
        cmd.Reason.Should().Be("Defective product");
    }

    [Fact]
    public void RecordRevenueEventCommand_ShouldSetProperties()
    {
        var userId = Guid.NewGuid();
        var cmd = new RecordRevenueEventCommand(
            RevenueEventType.PaymentReceived, 500m, "USD",
            RevenueSource.Subscription, "sub-123", userId, "{\"plan\":\"pro\"}");
        cmd.EventType.Should().Be(RevenueEventType.PaymentReceived);
        cmd.Amount.Should().Be(500m);
        cmd.Currency.Should().Be("USD");
        cmd.Source.Should().Be(RevenueSource.Subscription);
        cmd.ReferenceId.Should().Be("sub-123");
        cmd.UserId.Should().Be(userId);
    }

    [Fact]
    public void ResolveDisputeCommand_ShouldSetProperties()
    {
        var cmd = new ResolveDisputeCommand(
            Guid.NewGuid(), DisputeResolution.MerchantCredit, "Offered store credit", Guid.NewGuid());
        cmd.Resolution.Should().Be(DisputeResolution.MerchantCredit);
        cmd.Notes.Should().Be("Offered store credit");
    }

    [Fact]
    public void RetryPaymentCommand_ShouldSetPaymentId()
    {
        var paymentId = Guid.NewGuid();
        var cmd = new RetryPaymentCommand(paymentId);
        cmd.PaymentId.Should().Be(paymentId);
    }

    [Fact]
    public void TransferFundsCommand_ShouldSetProperties()
    {
        var cmd = new TransferFundsCommand(
            Guid.NewGuid(), Guid.NewGuid(), 75.00m, "Gift transfer", "gift-ref");
        cmd.Amount.Should().Be(75.00m);
        cmd.Description.Should().Be("Gift transfer");
        cmd.ReferenceId.Should().Be("gift-ref");
    }

    [Fact]
    public void UnfreezeWalletCommand_ShouldSetWalletId()
    {
        var walletId = Guid.NewGuid();
        var cmd = new UnfreezeWalletCommand(walletId);
        cmd.WalletId.Should().Be(walletId);
    }

    [Fact]
    public void UnlockWalletCommand_ShouldSetUserId()
    {
        var userId = Guid.NewGuid();
        var cmd = new UnlockWalletCommand(userId);
        cmd.UserId.Should().Be(userId);
    }

    [Fact]
    public void UpdateDisputeStatusCommand_ShouldSetProperties()
    {
        var dueDate = DateTime.UtcNow.AddDays(7);
        var cmd = new UpdateDisputeStatusCommand(Guid.NewGuid(), DisputeStatus.UnderReview, dueDate);
        cmd.NewStatus.Should().Be(DisputeStatus.UnderReview);
        cmd.DueDate.Should().Be(dueDate);
    }

    [Fact]
    public void UpdatePaymentStatusCommand_ShouldSetProperties()
    {
        var cmd = new UpdatePaymentStatusCommand(Guid.NewGuid(), PaymentStatus.Succeeded, "txn-123");
        cmd.Status.Should().Be(PaymentStatus.Succeeded);
        cmd.TransactionId.Should().Be("txn-123");
    }
}

#endregion

#region Query Record Tests

public class PaymentQueryRecordTests
{
    [Fact]
    public void GetAllPaymentsQuery_Defaults()
    {
        var q = new GetAllPaymentsQuery();
        q.TenantId.Should().BeNull();
        q.Status.Should().BeNull();
        q.StartDate.Should().BeNull();
        q.EndDate.Should().BeNull();
        q.Page.Should().Be(1);
        q.PageSize.Should().Be(20);
    }

    [Fact]
    public void GetPaymentByIdQuery_ShouldSetId()
    {
        var id = Guid.NewGuid();
        var q = new GetPaymentByIdQuery(id);
        q.PaymentId.Should().Be(id);
    }

    [Fact]
    public void GetFailedPaymentsQuery_Defaults()
    {
        var q = new GetFailedPaymentsQuery();
        q.TenantId.Should().BeNull();
    }

    [Fact]
    public void GetDisputeByIdQuery_ShouldSetId()
    {
        var id = Guid.NewGuid();
        var q = new GetDisputeByIdQuery(id);
        q.DisputeId.Should().Be(id);
    }

    [Fact]
    public void GetDisputesByPaymentIdQuery_ShouldSetId()
    {
        var id = Guid.NewGuid();
        var q = new GetDisputesByPaymentIdQuery(id);
        q.PaymentId.Should().Be(id);
    }

    [Fact]
    public void GetDisputesByStatusQuery_ShouldSetDefaults()
    {
        var q = new GetDisputesByStatusQuery(DisputeStatus.Submitted);
        q.Status.Should().Be(DisputeStatus.Submitted);
        q.Skip.Should().Be(0);
        q.Take.Should().Be(50);
    }

    [Fact]
    public void GetDisputesByUserIdQuery_ShouldSetProperties()
    {
        var userId = Guid.NewGuid();
        var q = new GetDisputesByUserIdQuery(userId, 10, 25);
        q.UserId.Should().Be(userId);
        q.Skip.Should().Be(10);
        q.Take.Should().Be(25);
    }

    [Fact]
    public void GetDisputeEvidenceQuery_ShouldSetId()
    {
        var id = Guid.NewGuid();
        var q = new GetDisputeEvidenceQuery(id);
        q.DisputeId.Should().Be(id);
    }

    [Fact]
    public void GetRevenueEventByIdQuery_ShouldSetId()
    {
        var id = Guid.NewGuid();
        var q = new GetRevenueEventByIdQuery(id);
        q.EventId.Should().Be(id);
    }

    [Fact]
    public void GetRevenueEventsByDateRangeQuery_ShouldSetProperties()
    {
        var start = DateTime.UtcNow.AddDays(-30);
        var end = DateTime.UtcNow;
        var q = new GetRevenueEventsByDateRangeQuery(start, end, 5, 25);
        q.StartDate.Should().Be(start);
        q.EndDate.Should().Be(end);
        q.Skip.Should().Be(5);
        q.Take.Should().Be(25);
    }

    [Fact]
    public void GetRevenueEventsByReferenceIdQuery_ShouldSetId()
    {
        var q = new GetRevenueEventsByReferenceIdQuery("ref-123");
        q.ReferenceId.Should().Be("ref-123");
    }

    [Fact]
    public void GetWalletByIdQuery_ShouldSetId()
    {
        var id = Guid.NewGuid();
        var q = new GetWalletByIdQuery(id);
        q.WalletId.Should().Be(id);
    }

    [Fact]
    public void GetWalletByUserIdQuery_ShouldSetId()
    {
        var userId = Guid.NewGuid();
        var q = new GetWalletByUserIdQuery(userId);
        q.UserId.Should().Be(userId);
    }

    [Fact]
    public void GetWalletBalanceQuery_ShouldSetId()
    {
        var userId = Guid.NewGuid();
        var q = new GetWalletBalanceQuery(userId);
        q.UserId.Should().Be(userId);
    }

    [Fact]
    public void GetTransactionHistoryQuery_ShouldSetDefaults()
    {
        var userId = Guid.NewGuid();
        var q = new GetTransactionHistoryQuery(userId);
        q.UserId.Should().Be(userId);
        q.Skip.Should().Be(0);
        q.Take.Should().Be(50);
        q.TypeFilter.Should().BeNull();
        q.StatusFilter.Should().BeNull();
    }

    [Fact]
    public void GetWalletAuditLogQuery_ShouldSetProperties()
    {
        var q = new GetWalletAuditLogQuery(Guid.NewGuid(), 2, 10);
        q.Page.Should().Be(2);
        q.PageSize.Should().Be(10);
    }

    [Fact]
    public void GetLedgerEntriesByAccountQuery_ShouldSetDefaults()
    {
        var q = new GetLedgerEntriesByAccountQuery("Revenue");
        q.Account.Should().Be("Revenue");
        q.Skip.Should().Be(0);
        q.Take.Should().Be(50);
    }

    [Fact]
    public void GetUnreconciledLedgerEntriesQuery_ShouldSetDefaults()
    {
        var q = new GetUnreconciledLedgerEntriesQuery();
        q.Skip.Should().Be(0);
        q.Take.Should().Be(50);
    }

    [Fact]
    public void GetAuditTrailQuery_ShouldSetProperties()
    {
        var q = new GetAuditTrailQuery("Payment", Guid.NewGuid(), 10, 25);
        q.EntityType.Should().Be("Payment");
        q.Skip.Should().Be(10);
        q.Take.Should().Be(25);
    }

    [Fact]
    public void GetCanceledPaymentsQuery_Defaults()
    {
        var q = new GetCanceledPaymentsQuery();
        q.TenantId.Should().BeNull();
        q.CancellationReason.Should().BeNull();
    }

    [Fact]
    public void GetOverduePaymentsQuery_Defaults()
    {
        var q = new GetOverduePaymentsQuery();
        q.TenantId.Should().BeNull();
        q.OverdueThreshold.Should().Be(30);
    }

    [Fact]
    public void GetScheduledPaymentsQuery_Defaults()
    {
        var q = new GetScheduledPaymentsQuery();
        q.TenantId.Should().BeNull();
        q.ScheduledDate.Should().BeNull();
    }

    [Fact]
    public void GetRefundedPaymentsQuery_Defaults()
    {
        var q = new GetRefundedPaymentsQuery();
        q.TenantId.Should().BeNull();
        q.RefundReason.Should().BeNull();
    }

    [Fact]
    public void CalculatePricingQuery_ShouldSetProperties()
    {
        var planId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var q = new CalculatePricingQuery(planId, tenantId, "SAVE20");
        q.PlanId.Should().Be(planId);
        q.TenantId.Should().Be(tenantId);
        q.DiscountCode.Should().Be("SAVE20");
    }

    [Fact]
    public void GetApplicableTaxRulesQuery_ShouldSetProperties()
    {
        var q = new GetApplicableTaxRulesQuery("US-NY", CustomerType.B2B);
        q.JurisdictionCode.Should().Be("US-NY");
        q.CustomerType.Should().Be(CustomerType.B2B);
        q.EffectiveDate.Should().BeNull();
    }

    [Fact]
    public void GetTaxJurisdictionByIdQuery_ShouldSetId()
    {
        var id = Guid.NewGuid();
        var q = new GetTaxJurisdictionByIdQuery(id);
        q.JurisdictionId.Should().Be(id);
    }

    [Fact]
    public void GetTaxRuleByIdQuery_ShouldSetId()
    {
        var id = Guid.NewGuid();
        var q = new GetTaxRuleByIdQuery(id);
        q.RuleId.Should().Be(id);
    }
}

#endregion
