using FluentAssertions;
using GameGuild.Commerce.Payments;
using GameGuild.Commerce.Payments.Models;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests;

#region Payment Entity Tests

public class PaymentCreateTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreatePayment()
    {
        var tenantId = Guid.NewGuid();
        var payment = Payment.Create(tenantId, 100.00m, "USD", "idem-key-1");

        payment.Should().NotBeNull();
        payment.Amount.Should().Be(100.00m);
        payment.Currency.Should().Be("USD");
        payment.IdempotencyKey.Should().Be("idem-key-1");
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.Provider.Should().Be("stripe");
    }

    [Fact]
    public void Create_WithAllOptionalParams_ShouldSetAllValues()
    {
        var tenantId = Guid.NewGuid();
        var subId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        var payment = Payment.Create(tenantId, 50m, "eur", "key-2", "paypal",
            subId, orderId, invoiceId, "cust-123", "pm-456", "Test payment");

        payment.Currency.Should().Be("EUR"); // uppercased
        payment.Provider.Should().Be("paypal");
        payment.SubscriptionId.Should().Be(subId);
        payment.OrderId.Should().Be(orderId);
        payment.InvoiceId.Should().Be(invoiceId);
        payment.ExternalCustomerId.Should().Be("cust-123");
        payment.PaymentMethodId.Should().Be("pm-456");
        payment.Description.Should().Be("Test payment");
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrow()
    {
        var act = () => Payment.Create(Guid.Empty, 100m, "USD", "key");
        act.Should().Throw<ArgumentException>().WithParameterName("tenantId");
    }

    [Fact]
    public void Create_WithZeroAmount_ShouldThrow()
    {
        var act = () => Payment.Create(Guid.NewGuid(), 0m, "USD", "key");
        act.Should().Throw<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrow()
    {
        var act = () => Payment.Create(Guid.NewGuid(), -5m, "USD", "key");
        act.Should().Throw<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public void Create_WithEmptyIdempotencyKey_ShouldThrow()
    {
        var act = () => Payment.Create(Guid.NewGuid(), 100m, "USD", "");
        act.Should().Throw<ArgumentException>().WithParameterName("idempotencyKey");
    }

    [Fact]
    public void Create_WithWhitespaceIdempotencyKey_ShouldThrow()
    {
        var act = () => Payment.Create(Guid.NewGuid(), 100m, "USD", "   ");
        act.Should().Throw<ArgumentException>().WithParameterName("idempotencyKey");
    }
}

public class PaymentTransitionTests
{
    private static Payment CreatePendingPayment() =>
        Payment.Create(Guid.NewGuid(), 100m, "USD", Guid.NewGuid().ToString());

    [Fact]
    public void CanTransitionTo_FromPending_ToProcessing_ShouldBeTrue()
    {
        var p = CreatePendingPayment();
        p.CanTransitionTo(PaymentStatus.Processing).Should().BeTrue();
    }

    [Fact]
    public void CanTransitionTo_FromPending_ToCancelled_ShouldBeTrue()
    {
        var p = CreatePendingPayment();
        p.CanTransitionTo(PaymentStatus.Cancelled).Should().BeTrue();
    }

    [Fact]
    public void CanTransitionTo_FromPending_ToFailed_ShouldBeTrue()
    {
        var p = CreatePendingPayment();
        p.CanTransitionTo(PaymentStatus.Failed).Should().BeTrue();
    }

    [Fact]
    public void CanTransitionTo_FromPending_ToSucceeded_ShouldBeFalse()
    {
        var p = CreatePendingPayment();
        p.CanTransitionTo(PaymentStatus.Succeeded).Should().BeFalse();
    }

    [Fact]
    public void MarkAsProcessing_FromPending_ShouldSucceed()
    {
        var p = CreatePendingPayment();
        p.MarkAsProcessing("ext-txn-1");
        p.Status.Should().Be(PaymentStatus.Processing);
        p.ExternalTransactionId.Should().Be("ext-txn-1");
    }

    [Fact]
    public void MarkAsProcessing_WithoutTransactionId_ShouldSucceed()
    {
        var p = CreatePendingPayment();
        p.MarkAsProcessing();
        p.Status.Should().Be(PaymentStatus.Processing);
        p.ExternalTransactionId.Should().BeNull();
    }

    [Fact]
    public void MarkAsSucceeded_FromProcessing_ShouldSucceed()
    {
        var p = CreatePendingPayment();
        p.MarkAsProcessing();
        p.MarkAsSucceeded("ext-pay-1", "ext-txn-2");

        p.Status.Should().Be(PaymentStatus.Succeeded);
        p.ExternalPaymentId.Should().Be("ext-pay-1");
        p.ExternalTransactionId.Should().Be("ext-txn-2");
        p.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsSucceeded_KeepsExistingTransactionId_WhenNullPassed()
    {
        var p = CreatePendingPayment();
        p.MarkAsProcessing("original-txn");
        p.MarkAsSucceeded("ext-pay-1");

        p.ExternalTransactionId.Should().Be("original-txn");
    }

    [Fact]
    public void MarkAsFailed_FromProcessing_ShouldSetFailureDetails()
    {
        var p = CreatePendingPayment();
        p.MarkAsProcessing();
        p.MarkAsFailed("Card declined", "card_declined");

        p.Status.Should().Be(PaymentStatus.Failed);
        p.FailureReason.Should().Be("Card declined");
        p.ErrorCode.Should().Be("card_declined");
        p.ProcessedAt.Should().NotBeNull();
        p.NextRetryAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsRequiresAction_FromProcessing_ShouldSucceed()
    {
        var p = CreatePendingPayment();
        p.MarkAsProcessing();
        p.MarkAsRequiresAction("3ds-txn");

        p.Status.Should().Be(PaymentStatus.RequiresAction);
        p.ExternalTransactionId.Should().Be("3ds-txn");
    }

    [Fact]
    public void Cancel_FromPending_ShouldSetCancellationDetails()
    {
        var p = CreatePendingPayment();
        var userId = Guid.NewGuid();
        p.Cancel("User requested", userId);

        p.Status.Should().Be(PaymentStatus.Cancelled);
        p.CancellationReason.Should().Be("User requested");
        p.CancelledByUserId.Should().Be(userId);
        p.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_WithoutUserId_ShouldSucceed()
    {
        var p = CreatePendingPayment();
        p.Cancel("System timeout");

        p.CancelledByUserId.Should().BeNull();
    }

    [Fact]
    public void MarkAsProcessing_FromCancelledState_ShouldThrow()
    {
        var p = CreatePendingPayment();
        p.Cancel("done");

        var act = () => p.MarkAsProcessing();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void PrepareForRetry_FromFailed_ShouldResetToPending()
    {
        var p = CreatePendingPayment();
        p.MarkAsProcessing();
        p.MarkAsFailed("timeout");

        p.PrepareForRetry();

        p.Status.Should().Be(PaymentStatus.Pending);
        p.RetryCount.Should().Be(1);
        p.FailureReason.Should().BeNull();
        p.ErrorCode.Should().BeNull();
        p.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public void PrepareForRetry_ShouldReleaseTheDefinitivelyFailedProviderAttempt()
    {
        var payment = CreatePendingPayment();
        payment.MarkAsProcessing("pi_declined");
        payment.MarkAsFailed("Card declined", "card_declined");

        payment.PrepareForRetry("pm_replacement");

        payment.ExternalTransactionId.Should().BeNull();
        payment.PaymentMethodId.Should().Be("pm_replacement");
    }

    [Fact]
    public void PrepareForRetry_WhenNotFailed_ShouldThrow()
    {
        var p = CreatePendingPayment();
        var act = () => p.PrepareForRetry();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void PrepareForRetry_WhenMaxRetriesReached_ShouldThrow()
    {
        var p = CreatePendingPayment();
        // Exhaust all retries
        for (var i = 0; i < 3; i++)
        {
            p.MarkAsProcessing();
            p.MarkAsFailed("fail");
            if (i < 2)
            {
                p.PrepareForRetry();
            }
        }

        // At this point RetryCount == 2, one more retry allowed
        p.PrepareForRetry();
        p.MarkAsProcessing();
        p.MarkAsFailed("fail again");

        // Now RetryCount == 3 == MaxRetries
        var act = () => p.PrepareForRetry();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CanRetry_WhenFailedAndRetriesRemaining_ShouldBeTrue()
    {
        var p = CreatePendingPayment();
        p.MarkAsProcessing();
        p.MarkAsFailed("err");

        p.CanRetry.Should().BeTrue();
    }

    [Fact]
    public void CanRetry_WhenNotFailed_ShouldBeFalse()
    {
        var p = CreatePendingPayment();
        p.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void MaxRetriesReached_Initially_ShouldBeFalse()
    {
        var p = CreatePendingPayment();
        p.MaxRetriesReached.Should().BeFalse();
    }

    [Fact]
    public void IsTerminal_WhenCancelled_ShouldBeTrue()
    {
        var p = CreatePendingPayment();
        p.Cancel("done");
        p.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void IsTerminal_WhenPending_ShouldBeFalse()
    {
        var p = CreatePendingPayment();
        p.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void NetAmount_AfterPartialRefund_ShouldReflectDifference()
    {
        var p = CreatePendingPayment();
        p.MarkAsProcessing();
        p.MarkAsSucceeded("pay-1");
        p.ProcessRefund(30m, "ref-1", "partial refund");

        p.NetAmount.Should().Be(70m);
        p.RefundedAmount.Should().Be(30m);
    }

    [Fact]
    public void ProcessRefund_FullRefund_ShouldTransitionToRefunded()
    {
        var p = CreatePendingPayment();
        p.MarkAsProcessing();
        p.MarkAsSucceeded("pay-1");
        p.ProcessRefund(100m, "ref-1", "full refund");

        p.Status.Should().Be(PaymentStatus.Refunded);
        p.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void ProcessRefund_ExceedingAmount_ShouldThrow()
    {
        var p = CreatePendingPayment();
        p.MarkAsProcessing();
        p.MarkAsSucceeded("pay-1");

        var act = () => p.ProcessRefund(150m, "ref-1", "too much");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ProcessRefund_ZeroAmount_ShouldThrow()
    {
        var p = CreatePendingPayment();
        p.MarkAsProcessing();
        p.MarkAsSucceeded("pay-1");

        var act = () => p.ProcessRefund(0m, "ref-1", "zero");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ProcessRefund_FromPending_ShouldThrow()
    {
        var p = CreatePendingPayment();
        var act = () => p.ProcessRefund(10m, "ref-1", "nope");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsDisputed_FromSucceeded_ShouldSucceed()
    {
        var p = CreatePendingPayment();
        p.MarkAsProcessing();
        p.MarkAsSucceeded("pay-1");
        p.MarkAsDisputed();

        p.Status.Should().Be(PaymentStatus.Disputed);
    }

    [Fact]
    public void ProcessRefund_FromDisputed_ShouldSucceed()
    {
        var p = CreatePendingPayment();
        p.MarkAsProcessing();
        p.MarkAsSucceeded("pay-1");
        p.MarkAsDisputed();
        p.ProcessRefund(100m, "ref-1", "dispute resolution");

        p.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public void SetMetadata_ShouldUpdateMetadata()
    {
        var p = CreatePendingPayment();
        p.SetMetadata("{\"key\":\"value\"}");

        p.Metadata.Should().Be("{\"key\":\"value\"}");
    }

    [Fact]
    public void MarkAsSucceeded_FromRequiresAction_ShouldSucceed()
    {
        var p = CreatePendingPayment();
        p.MarkAsProcessing();
        p.MarkAsRequiresAction();
        p.MarkAsSucceeded("pay-1");

        p.Status.Should().Be(PaymentStatus.Succeeded);
    }

    [Fact]
    public void MarkAsProcessing_FromRequiresAction_ShouldSucceed()
    {
        var p = CreatePendingPayment();
        p.MarkAsProcessing();
        p.MarkAsRequiresAction();
        p.MarkAsProcessing();

        p.Status.Should().Be(PaymentStatus.Processing);
    }

    [Fact]
    public void MarkAsFailed_FromRequiresAction_ShouldSucceed()
    {
        var p = CreatePendingPayment();
        p.MarkAsProcessing();
        p.MarkAsRequiresAction();
        p.MarkAsFailed("expired");

        p.Status.Should().Be(PaymentStatus.Failed);
    }
}

#endregion

#region UserWallet Entity Tests

public class UserWalletTests
{
    private static UserWallet CreateActiveWallet(decimal balance = 100m)
    {
        return new UserWallet
        {
            UserId = Guid.NewGuid(),
            Balance = balance,
            Currency = "USD",
            IsActive = true,
            IsLocked = false
        };
    }

    [Fact]
    public void AddFunds_ShouldIncreaseBalance()
    {
        var w = CreateActiveWallet(50m);
        w.AddFunds(25m, "deposit", "ref-1");

        w.Balance.Should().Be(75m);
        w.LastTransactionAt.Should().NotBeNull();
        w.Transactions.Should().HaveCount(1);
    }

    [Fact]
    public void AddFunds_TransactionShouldHaveCorrectProperties()
    {
        var w = CreateActiveWallet(50m);
        w.AddFunds(25m, "deposit", "ref-1");

        var txn = w.Transactions.First();
        txn.Type.Should().Be(WalletTransactionType.Credit);
        txn.Amount.Should().Be(25m);
        txn.BalanceAfter.Should().Be(75m);
        txn.Description.Should().Be("deposit");
        txn.ReferenceId.Should().Be("ref-1");
        txn.Status.Should().Be(TransactionStatus.Completed);
    }

    [Fact]
    public void AddFunds_WhenInactive_ShouldThrow()
    {
        var w = CreateActiveWallet();
        w.IsActive = false;

        var act = () => w.AddFunds(10m, "test");
        act.Should().Throw<InvalidOperationException>().WithMessage("*not active*");
    }

    [Fact]
    public void AddFunds_WhenLocked_ShouldThrow()
    {
        var w = CreateActiveWallet();
        w.Lock("fraud");

        var act = () => w.AddFunds(10m, "test");
        act.Should().Throw<InvalidOperationException>().WithMessage("*locked*");
    }

    [Fact]
    public void AddFunds_ZeroAmount_ShouldThrow()
    {
        var w = CreateActiveWallet();
        var act = () => w.AddFunds(0m, "test");
        act.Should().Throw<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public void AddFunds_NegativeAmount_ShouldThrow()
    {
        var w = CreateActiveWallet();
        var act = () => w.AddFunds(-5m, "test");
        act.Should().Throw<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public void DeductFunds_ShouldDecreaseBalance()
    {
        var w = CreateActiveWallet(100m);
        w.DeductFunds(30m, "purchase", "ref-2");

        w.Balance.Should().Be(70m);
        w.Transactions.Should().HaveCount(1);
    }

    [Fact]
    public void DeductFunds_TransactionShouldHaveCorrectProperties()
    {
        var w = CreateActiveWallet(100m);
        w.DeductFunds(30m, "purchase");

        var txn = w.Transactions.First();
        txn.Type.Should().Be(WalletTransactionType.Debit);
        txn.Amount.Should().Be(30m);
        txn.BalanceAfter.Should().Be(70m);
    }

    [Fact]
    public void DeductFunds_WhenInactive_ShouldThrow()
    {
        var w = CreateActiveWallet();
        w.IsActive = false;
        var act = () => w.DeductFunds(10m, "test");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DeductFunds_WhenLocked_ShouldThrow()
    {
        var w = CreateActiveWallet();
        w.Lock("security");
        var act = () => w.DeductFunds(10m, "test");
        act.Should().Throw<InvalidOperationException>().WithMessage("*locked*");
    }

    [Fact]
    public void DeductFunds_InsufficientBalance_ShouldThrow()
    {
        var w = CreateActiveWallet(10m);
        var act = () => w.DeductFunds(50m, "test");
        act.Should().Throw<InvalidOperationException>().WithMessage("*Insufficient*");
    }

    [Fact]
    public void DeductFunds_ZeroAmount_ShouldThrow()
    {
        var w = CreateActiveWallet();
        var act = () => w.DeductFunds(0m, "test");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Lock_ShouldSetLockedState()
    {
        var w = CreateActiveWallet();
        w.Lock("fraud investigation");

        w.IsLocked.Should().BeTrue();
        w.LockReason.Should().Be("fraud investigation");
    }

    [Fact]
    public void Unlock_ShouldClearLockedState()
    {
        var w = CreateActiveWallet();
        w.Lock("test");
        w.Unlock();

        w.IsLocked.Should().BeFalse();
        w.LockReason.Should().BeNull();
    }

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var w = new UserWallet();
        w.IsActive.Should().BeTrue();
        w.IsLocked.Should().BeFalse();
        w.Currency.Should().Be("USD");
        w.Balance.Should().Be(0);
        w.Transactions.Should().BeEmpty();
    }
}

#endregion

#region PaymentDispute Entity Tests

public class PaymentDisputeTests
{
    private static PaymentDispute CreateSubmittedDispute()
    {
        return new PaymentDispute
        {
            PaymentId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Type = DisputeType.Fraudulent,
            Status = DisputeStatus.Submitted,
            Amount = 100m,
            Reason = "Unauthorized charge"
        };
    }

    [Fact]
    public void Submit_ShouldSetStatusAndDueDate()
    {
        var d = new PaymentDispute();
        var dueDate = DateTime.UtcNow.AddDays(30);
        d.Submit(dueDate);

        d.Status.Should().Be(DisputeStatus.Submitted);
        d.DueDate.Should().Be(dueDate);
    }

    [Fact]
    public void MoveToReview_FromSubmitted_ShouldSucceed()
    {
        var d = CreateSubmittedDispute();
        d.MoveToReview();
        d.Status.Should().Be(DisputeStatus.UnderReview);
    }

    [Fact]
    public void MoveToReview_FromNonSubmitted_ShouldThrow()
    {
        var d = CreateSubmittedDispute();
        d.MoveToReview();
        var act = () => d.MoveToReview(); // now UnderReview
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RequestCustomerResponse_FromUnderReview_ShouldSucceed()
    {
        var d = CreateSubmittedDispute();
        d.MoveToReview();
        var dueDate = DateTime.UtcNow.AddDays(7);
        d.RequestCustomerResponse(dueDate);

        d.Status.Should().Be(DisputeStatus.PendingCustomerResponse);
        d.DueDate.Should().Be(dueDate);
    }

    [Fact]
    public void RequestCustomerResponse_FromNonReview_ShouldThrow()
    {
        var d = CreateSubmittedDispute();
        var act = () => d.RequestCustomerResponse(DateTime.UtcNow.AddDays(7));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RequestMerchantResponse_FromUnderReview_ShouldSucceed()
    {
        var d = CreateSubmittedDispute();
        d.MoveToReview();
        var dueDate = DateTime.UtcNow.AddDays(5);
        d.RequestMerchantResponse(dueDate);

        d.Status.Should().Be(DisputeStatus.PendingMerchantResponse);
        d.DueDate.Should().Be(dueDate);
    }

    [Fact]
    public void RequestMerchantResponse_FromNonReview_ShouldThrow()
    {
        var d = CreateSubmittedDispute();
        var act = () => d.RequestMerchantResponse(DateTime.UtcNow.AddDays(5));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Resolve_ShouldSetResolutionDetails()
    {
        var d = CreateSubmittedDispute();
        d.MoveToReview();
        var resolvedBy = Guid.NewGuid();
        d.Resolve(DisputeResolution.Won, "Customer was right", resolvedBy);

        d.Status.Should().Be(DisputeStatus.Resolved);
        d.Resolution.Should().Be(DisputeResolution.Won);
        d.ResolutionNotes.Should().Be("Customer was right");
        d.ResolvedBy.Should().Be(resolvedBy);
        d.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_WhenAlreadyResolved_ShouldThrow()
    {
        var d = CreateSubmittedDispute();
        d.MoveToReview();
        d.Resolve(DisputeResolution.Won, "ok", Guid.NewGuid());

        var act = () => d.Resolve(DisputeResolution.Lost, "nope", Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsWon_ShouldSetStatusToWon()
    {
        var d = CreateSubmittedDispute();
        d.MoveToReview();
        var resolvedBy = Guid.NewGuid();
        d.MarkAsWon("Customer wins", resolvedBy);

        d.Status.Should().Be(DisputeStatus.Won);
        d.Resolution.Should().Be(DisputeResolution.Won);
    }

    [Fact]
    public void MarkAsLost_ShouldSetStatusToLost()
    {
        var d = CreateSubmittedDispute();
        d.MoveToReview();
        d.MarkAsLost("Merchant wins", Guid.NewGuid());

        d.Status.Should().Be(DisputeStatus.Lost);
        d.Resolution.Should().Be(DisputeResolution.Lost);
    }

    [Fact]
    public void Cancel_ShouldSetStatusToCancelled()
    {
        var d = CreateSubmittedDispute();
        d.Cancel("Customer withdrew");

        d.Status.Should().Be(DisputeStatus.Cancelled);
        d.ResolutionNotes.Should().Be("Customer withdrew");
        d.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_WhenAlreadyResolved_ShouldThrow()
    {
        var d = CreateSubmittedDispute();
        d.MoveToReview();
        d.Resolve(DisputeResolution.Won, "done", Guid.NewGuid());

        var act = () => d.Cancel("too late");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_WhenWon_ShouldThrow()
    {
        var d = CreateSubmittedDispute();
        d.MoveToReview();
        d.MarkAsWon("won", Guid.NewGuid());

        var act = () => d.Cancel("nope");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_WhenLost_ShouldThrow()
    {
        var d = CreateSubmittedDispute();
        d.MoveToReview();
        d.MarkAsLost("lost", Guid.NewGuid());

        var act = () => d.Cancel("nope");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var d = new PaymentDispute();
        d.Status.Should().Be(DisputeStatus.Submitted);
        d.Evidence.Should().BeEmpty();
        d.Reason.Should().Be(string.Empty);
    }
}

#endregion

#region WalletTransaction Entity Tests

public class WalletTransactionTests
{
    [Fact]
    public void Complete_ShouldSetStatusAndProcessedAt()
    {
        var txn = new WalletTransaction();
        txn.Complete();

        txn.Status.Should().Be(TransactionStatus.Completed);
        txn.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_ShouldSetStatusAndNotes()
    {
        var txn = new WalletTransaction();
        txn.Fail("Insufficient funds");

        txn.Status.Should().Be(TransactionStatus.Failed);
        txn.Notes.Should().Be("Insufficient funds");
        txn.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var txn = new WalletTransaction();
        txn.Status.Should().Be(TransactionStatus.Pending);
        txn.Description.Should().Be(string.Empty);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var walletId = Guid.NewGuid();
        var txn = new WalletTransaction
        {
            WalletId = walletId,
            Type = WalletTransactionType.Credit,
            Amount = 50m,
            BalanceAfter = 150m,
            Description = "test credit",
            ReferenceId = "ref-123",
            Metadata = "{\"source\":\"api\"}",
            Notes = "test note"
        };

        txn.WalletId.Should().Be(walletId);
        txn.Type.Should().Be(WalletTransactionType.Credit);
        txn.Amount.Should().Be(50m);
        txn.BalanceAfter.Should().Be(150m);
        txn.Description.Should().Be("test credit");
        txn.ReferenceId.Should().Be("ref-123");
        txn.Metadata.Should().Be("{\"source\":\"api\"}");
        txn.Notes.Should().Be("test note");
    }
}

#endregion

#region AuditTrail Entity Tests

public class AuditTrailTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var at = new AuditTrail();
        at.EntityType.Should().Be(string.Empty);
        at.Action.Should().Be(AuditAction.Created);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var entityId = Guid.NewGuid();
        var changedBy = Guid.NewGuid();

        var at = new AuditTrail
        {
            EntityType = "Payment",
            EntityId = entityId,
            Action = AuditAction.Updated,
            OldValue = "{\"status\":\"Pending\"}",
            NewValue = "{\"status\":\"Processing\"}",
            ChangedBy = changedBy,
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            Metadata = "{\"field\":\"status\"}",
            Reason = "Status change"
        };

        at.EntityType.Should().Be("Payment");
        at.EntityId.Should().Be(entityId);
        at.Action.Should().Be(AuditAction.Updated);
        at.OldValue.Should().Be("{\"status\":\"Pending\"}");
        at.NewValue.Should().Be("{\"status\":\"Processing\"}");
        at.ChangedBy.Should().Be(changedBy);
        at.IpAddress.Should().Be("192.168.1.1");
        at.UserAgent.Should().Be("Mozilla/5.0");
        at.Metadata.Should().Be("{\"field\":\"status\"}");
        at.Reason.Should().Be("Status change");
    }
}

#endregion

#region FinancialLedgerEntry Entity Tests

public class FinancialLedgerEntryTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var entry = new FinancialLedgerEntry();
        entry.DebitAccount.Should().Be(string.Empty);
        entry.CreditAccount.Should().Be(string.Empty);
        entry.Currency.Should().Be("USD");
        entry.Description.Should().Be(string.Empty);
        entry.IsReconciled.Should().BeFalse();
    }

    [Fact]
    public void Reconcile_ShouldSetReconciledFields()
    {
        var entry = new FinancialLedgerEntry();
        var userId = Guid.NewGuid();
        entry.Reconcile(userId, "Month-end reconciliation");

        entry.IsReconciled.Should().BeTrue();
        entry.ReconciledBy.Should().Be(userId);
        entry.ReconciledAt.Should().NotBeNull();
        entry.Notes.Should().Be("Month-end reconciliation");
    }

    [Fact]
    public void Reconcile_WithoutNotes_ShouldNotOverwriteExistingNotes()
    {
        var entry = new FinancialLedgerEntry { Notes = "existing" };
        entry.Reconcile(Guid.NewGuid());

        entry.Notes.Should().Be("existing");
    }

    [Fact]
    public void Reconcile_WhenAlreadyReconciled_ShouldThrow()
    {
        var entry = new FinancialLedgerEntry();
        entry.Reconcile(Guid.NewGuid());

        var act = () => entry.Reconcile(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>().WithMessage("*already reconciled*");
    }

    [Fact]
    public void LedgerAccountProperties_ShouldBeSettable()
    {
        var entry = new FinancialLedgerEntry
        {
            DebitLedgerAccount = LedgerAccount.Cash,
            CreditLedgerAccount = LedgerAccount.DeferredRevenue,
            EntryType = LedgerEntryType.Revenue,
            Amount = 500m,
            FiscalYear = 2025,
            FiscalPeriod = 6
        };

        entry.DebitLedgerAccount.Should().Be(LedgerAccount.Cash);
        entry.CreditLedgerAccount.Should().Be(LedgerAccount.DeferredRevenue);
        entry.FiscalYear.Should().Be(2025);
        entry.FiscalPeriod.Should().Be(6);
    }
}

#endregion

#region RevenueEvent Entity Tests

public class RevenueEventTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var e = new RevenueEvent();
        e.Currency.Should().Be("USD");
        e.ReferenceId.Should().Be(string.Empty);
        e.Status.Should().Be(RevenueEventStatus.Pending);
    }

    [Fact]
    public void MarkAsProcessed_ShouldSetStatusAndTimestamp()
    {
        var e = new RevenueEvent();
        var ledgerEntryId = Guid.NewGuid();
        e.MarkAsProcessed(ledgerEntryId);

        e.Status.Should().Be(RevenueEventStatus.Processed);
        e.ProcessedAt.Should().NotBeNull();
        e.LedgerEntryId.Should().Be(ledgerEntryId);
    }

    [Fact]
    public void MarkAsProcessed_WithoutLedgerEntry_ShouldSucceed()
    {
        var e = new RevenueEvent();
        e.MarkAsProcessed();

        e.Status.Should().Be(RevenueEventStatus.Processed);
        e.LedgerEntryId.Should().BeNull();
    }

    [Fact]
    public void MarkAsFailed_ShouldSetStatusAndNotes()
    {
        var e = new RevenueEvent();
        e.MarkAsFailed("Processing error");

        e.Status.Should().Be(RevenueEventStatus.Failed);
        e.ProcessedAt.Should().NotBeNull();
        e.ProcessingNotes.Should().Be("Processing error");
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var userId = Guid.NewGuid();
        var e = new RevenueEvent
        {
            EventType = RevenueEventType.PaymentReceived,
            Amount = 99.99m,
            Currency = "EUR",
            Source = RevenueSource.Subscription,
            ReferenceId = "ref-123",
            Metadata = "{\"plan\":\"premium\"}",
            UserId = userId
        };

        e.EventType.Should().Be(RevenueEventType.PaymentReceived);
        e.Amount.Should().Be(99.99m);
        e.Currency.Should().Be("EUR");
        e.Source.Should().Be(RevenueSource.Subscription);
        e.UserId.Should().Be(userId);
    }
}

#endregion

#region TaxRule Entity Tests

public class TaxRuleTests
{
    [Fact]
    public void IsEffective_WhenActiveAndWithinRange_ShouldReturnTrue()
    {
        var rule = new TaxRule
        {
            IsActive = true,
            EffectiveFrom = new DateTime(2024, 1, 1),
            EffectiveTo = new DateTime(2025, 12, 31)
        };

        rule.IsEffective(new DateTime(2024, 6, 15)).Should().BeTrue();
    }

    [Fact]
    public void IsEffective_WhenInactive_ShouldReturnFalse()
    {
        var rule = new TaxRule { IsActive = false };
        rule.IsEffective(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsEffective_WhenBeforeEffectiveFrom_ShouldReturnFalse()
    {
        var rule = new TaxRule
        {
            IsActive = true,
            EffectiveFrom = new DateTime(2025, 1, 1)
        };

        rule.IsEffective(new DateTime(2024, 6, 15)).Should().BeFalse();
    }

    [Fact]
    public void IsEffective_WhenAfterEffectiveTo_ShouldReturnFalse()
    {
        var rule = new TaxRule
        {
            IsActive = true,
            EffectiveFrom = new DateTime(2024, 1, 1),
            EffectiveTo = new DateTime(2024, 12, 31)
        };

        rule.IsEffective(new DateTime(2025, 6, 15)).Should().BeFalse();
    }

    [Fact]
    public void IsEffective_WithNoDateRange_ShouldReturnTrue()
    {
        var rule = new TaxRule { IsActive = true };
        rule.IsEffective(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void AppliesToTransaction_WhenAllConditionsMet_ShouldReturnTrue()
    {
        var rule = new TaxRule
        {
            IsActive = true,
            CustomerTypeFilter = CustomerType.B2C,
            MinimumAmount = 10m,
            MaximumAmount = 1000m
        };

        rule.AppliesToTransaction(100m, CustomerType.B2C).Should().BeTrue();
    }

    [Fact]
    public void AppliesToTransaction_WhenInactive_ShouldReturnFalse()
    {
        var rule = new TaxRule { IsActive = false };
        rule.AppliesToTransaction(100m, CustomerType.B2C).Should().BeFalse();
    }

    [Fact]
    public void AppliesToTransaction_WhenCustomerTypeMismatch_ShouldReturnFalse()
    {
        var rule = new TaxRule
        {
            IsActive = true,
            CustomerTypeFilter = CustomerType.B2B
        };

        rule.AppliesToTransaction(100m, CustomerType.B2C).Should().BeFalse();
    }

    [Fact]
    public void AppliesToTransaction_WhenBelowMinimum_ShouldReturnFalse()
    {
        var rule = new TaxRule
        {
            IsActive = true,
            MinimumAmount = 50m
        };

        rule.AppliesToTransaction(10m, CustomerType.B2C).Should().BeFalse();
    }

    [Fact]
    public void AppliesToTransaction_WhenAboveMaximum_ShouldReturnFalse()
    {
        var rule = new TaxRule
        {
            IsActive = true,
            MaximumAmount = 500m
        };

        rule.AppliesToTransaction(1000m, CustomerType.B2C).Should().BeFalse();
    }

    [Fact]
    public void AppliesToTransaction_WithNoFilters_ShouldReturnTrue()
    {
        var rule = new TaxRule { IsActive = true };
        rule.AppliesToTransaction(100m, CustomerType.B2C).Should().BeTrue();
    }

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var rule = new TaxRule();
        rule.Name.Should().Be(string.Empty);
        rule.IsActive.Should().BeTrue();
    }
}

#endregion

#region TaxRate Entity Tests

public class TaxRateTests
{
    [Fact]
    public void IsEffective_WhenActiveAndWithinRange_ShouldReturnTrue()
    {
        var rate = new TaxRate
        {
            IsActive = true,
            EffectiveFrom = new DateTime(2024, 1, 1),
            EffectiveTo = new DateTime(2025, 12, 31)
        };

        rate.IsEffective(new DateTime(2024, 6, 15)).Should().BeTrue();
    }

    [Fact]
    public void IsEffective_WhenInactive_ShouldReturnFalse()
    {
        var rate = new TaxRate { IsActive = false };
        rate.IsEffective(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsEffective_WhenBeforeEffectiveFrom_ShouldReturnFalse()
    {
        var rate = new TaxRate
        {
            IsActive = true,
            EffectiveFrom = new DateTime(2030, 1, 1)
        };

        rate.IsEffective(new DateTime(2024, 6, 15)).Should().BeFalse();
    }

    [Fact]
    public void IsEffective_WhenAfterEffectiveTo_ShouldReturnFalse()
    {
        var rate = new TaxRate
        {
            IsActive = true,
            EffectiveFrom = new DateTime(2024, 1, 1),
            EffectiveTo = new DateTime(2024, 6, 30)
        };

        rate.IsEffective(new DateTime(2025, 1, 1)).Should().BeFalse();
    }

    [Fact]
    public void IsEffective_WithNoEndDate_ShouldReturnTrue()
    {
        var rate = new TaxRate
        {
            IsActive = true,
            EffectiveFrom = new DateTime(2024, 1, 1)
        };

        rate.IsEffective(new DateTime(2030, 1, 1)).Should().BeTrue();
    }

    [Fact]
    public void AppliesToAmount_WhenWithinRange_ShouldReturnTrue()
    {
        var rate = new TaxRate
        {
            MinimumTaxableAmount = 10m,
            MaximumTaxableAmount = 1000m
        };

        rate.AppliesToAmount(500m).Should().BeTrue();
    }

    [Fact]
    public void AppliesToAmount_WhenBelowMinimum_ShouldReturnFalse()
    {
        var rate = new TaxRate { MinimumTaxableAmount = 100m };
        rate.AppliesToAmount(50m).Should().BeFalse();
    }

    [Fact]
    public void AppliesToAmount_WhenAboveMaximum_ShouldReturnFalse()
    {
        var rate = new TaxRate { MaximumTaxableAmount = 500m };
        rate.AppliesToAmount(1000m).Should().BeFalse();
    }

    [Fact]
    public void AppliesToAmount_WithNoLimits_ShouldReturnTrue()
    {
        var rate = new TaxRate();
        rate.AppliesToAmount(99999m).Should().BeTrue();
    }

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var rate = new TaxRate();
        rate.IsActive.Should().BeTrue();
    }
}

#endregion

#region DisputeEvidence Entity Tests

public class DisputeEvidenceTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var e = new DisputeEvidence();
        e.Title.Should().Be(string.Empty);
        e.IsFromMerchant.Should().BeFalse();
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var disputeId = Guid.NewGuid();
        var submittedBy = Guid.NewGuid();

        var e = new DisputeEvidence
        {
            DisputeId = disputeId,
            EvidenceType = EvidenceType.Receipt,
            Title = "Payment receipt",
            Description = "Original payment receipt",
            FileUrl = "https://example.com/receipt.pdf",
            FileName = "receipt.pdf",
            FileSize = 102400,
            MimeType = "application/pdf",
            SubmittedBy = submittedBy,
            IsFromMerchant = true,
            Metadata = "{\"source\":\"upload\"}"
        };

        e.DisputeId.Should().Be(disputeId);
        e.EvidenceType.Should().Be(EvidenceType.Receipt);
        e.Title.Should().Be("Payment receipt");
        e.FileUrl.Should().Be("https://example.com/receipt.pdf");
        e.FileName.Should().Be("receipt.pdf");
        e.FileSize.Should().Be(102400);
        e.MimeType.Should().Be("application/pdf");
        e.IsFromMerchant.Should().BeTrue();
    }
}

#endregion

#region PromoStackingRule Entity Tests

public class PromoStackingRuleTests
{
    [Fact]
    public void GetAllowedPromoCodeIds_WhenNull_ShouldReturnEmpty()
    {
        var rule = new PromoStackingRule();
        rule.GetAllowedPromoCodeIds().Should().BeEmpty();
    }

    [Fact]
    public void SetAllowedPromoCodeIds_ShouldSerializeToJson()
    {
        var rule = new PromoStackingRule();
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        rule.SetAllowedPromoCodeIds(ids);

        rule.AllowedPromoCodeIds.Should().NotBeNull();
        rule.GetAllowedPromoCodeIds().Should().HaveCount(2);
        rule.GetAllowedPromoCodeIds().Should().BeEquivalentTo(ids);
    }

    [Fact]
    public void SetAllowedPromoCodeIds_EmptyList_ShouldSetNull()
    {
        var rule = new PromoStackingRule();
        rule.SetAllowedPromoCodeIds(new List<Guid>());

        rule.AllowedPromoCodeIds.Should().BeNull();
    }

    [Fact]
    public void GetExcludedPromoCodeIds_WhenNull_ShouldReturnEmpty()
    {
        var rule = new PromoStackingRule();
        rule.GetExcludedPromoCodeIds().Should().BeEmpty();
    }

    [Fact]
    public void SetExcludedPromoCodeIds_ShouldSerializeToJson()
    {
        var rule = new PromoStackingRule();
        var ids = new List<Guid> { Guid.NewGuid() };
        rule.SetExcludedPromoCodeIds(ids);

        rule.ExcludedPromoCodeIds.Should().NotBeNull();
        rule.GetExcludedPromoCodeIds().Should().HaveCount(1);
    }

    [Fact]
    public void SetExcludedPromoCodeIds_EmptyList_ShouldSetNull()
    {
        var rule = new PromoStackingRule();
        rule.SetExcludedPromoCodeIds(new List<Guid>());
        rule.ExcludedPromoCodeIds.Should().BeNull();
    }

    [Fact]
    public void GetPromoCodeTypes_WhenNull_ShouldReturnEmpty()
    {
        var rule = new PromoStackingRule();
        rule.GetPromoCodeTypes().Should().BeEmpty();
    }

    [Fact]
    public void SetPromoCodeTypes_ShouldSerializeToJson()
    {
        var rule = new PromoStackingRule();
        rule.SetPromoCodeTypes(new List<string> { "percent_off", "fixed_amount" });

        rule.PromoCodeTypes.Should().NotBeNull();
        rule.GetPromoCodeTypes().Should().HaveCount(2);
        rule.GetPromoCodeTypes().Should().Contain("percent_off");
    }

    [Fact]
    public void SetPromoCodeTypes_EmptyList_ShouldSetNull()
    {
        var rule = new PromoStackingRule();
        rule.SetPromoCodeTypes(new List<string>());
        rule.PromoCodeTypes.Should().BeNull();
    }

    [Fact]
    public void GetAllowedPromoCodeIds_InvalidJson_ShouldReturnEmpty()
    {
        var rule = new PromoStackingRule { AllowedPromoCodeIds = "not-json" };
        rule.GetAllowedPromoCodeIds().Should().BeEmpty();
    }

    [Fact]
    public void GetExcludedPromoCodeIds_InvalidJson_ShouldReturnEmpty()
    {
        var rule = new PromoStackingRule { ExcludedPromoCodeIds = "not-json" };
        rule.GetExcludedPromoCodeIds().Should().BeEmpty();
    }

    [Fact]
    public void GetPromoCodeTypes_InvalidJson_ShouldReturnEmpty()
    {
        var rule = new PromoStackingRule { PromoCodeTypes = "not-json" };
        rule.GetPromoCodeTypes().Should().BeEmpty();
    }

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var rule = new PromoStackingRule();
        rule.StackBehavior.Should().Be(StackBehavior.Allow);
        rule.Priority.Should().Be(0);
    }
}

#endregion

#region CustomerTaxExemption Entity Tests

public class CustomerTaxExemptionTests
{
    private static CustomerTaxExemption CreateExemption(
        DateTime? validFrom = null,
        DateTime? validUntil = null)
    {
        return CustomerTaxExemption.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "US-CA",
            TaxExemptionType.NonProfit,
            "CERT-12345",
            validFrom ?? new DateTime(2024, 1, 1),
            validUntil ?? new DateTime(2026, 12, 31),
            "IRS",
            "Test exemption");
    }

    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var e = CreateExemption();

        e.JurisdictionCode.Should().Be("US-CA");
        e.ExemptionType.Should().Be(TaxExemptionType.NonProfit);
        e.CertificateNumber.Should().Be("CERT-12345");
        e.IssuingAuthority.Should().Be("IRS");
        e.Notes.Should().Be("Test exemption");
        e.Status.Should().Be(TaxExemptionStatus.Active);
        e.VerificationStatus.Should().Be(ExemptionVerificationStatus.Pending);
    }

    [Fact]
    public void Create_ShouldUppercaseJurisdictionCode()
    {
        var e = CustomerTaxExemption.Create(
            Guid.NewGuid(), Guid.NewGuid(), "us-ca",
            TaxExemptionType.Educational, "CERT-1",
            DateTime.UtcNow, null);

        e.JurisdictionCode.Should().Be("US-CA");
    }

    [Fact]
    public void Create_WithEmptyJurisdictionCode_ShouldThrow()
    {
        var act = () => CustomerTaxExemption.Create(
            Guid.NewGuid(), Guid.NewGuid(), "",
            TaxExemptionType.NonProfit, "CERT-1",
            DateTime.UtcNow, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyCertificateNumber_ShouldThrow()
    {
        var act = () => CustomerTaxExemption.Create(
            Guid.NewGuid(), Guid.NewGuid(), "US-CA",
            TaxExemptionType.NonProfit, "",
            DateTime.UtcNow, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ValidUntilBeforeValidFrom_ShouldThrow()
    {
        var act = () => CustomerTaxExemption.Create(
            Guid.NewGuid(), Guid.NewGuid(), "US-CA",
            TaxExemptionType.NonProfit, "CERT-1",
            new DateTime(2025, 1, 1), new DateTime(2024, 1, 1));

        act.Should().Throw<ArgumentException>().WithParameterName("validUntil");
    }

    [Fact]
    public void IsValidOn_WhenActiveAndVerifiedAndInRange_ShouldReturnTrue()
    {
        var e = CreateExemption();
        e.MarkVerified("admin");

        e.IsValidOn(new DateTime(2025, 6, 15)).Should().BeTrue();
    }

    [Fact]
    public void IsValidOn_WhenNotVerified_ShouldReturnFalse()
    {
        var e = CreateExemption();
        // Still Pending verification
        e.IsValidOn(new DateTime(2025, 6, 15)).Should().BeFalse();
    }

    [Fact]
    public void IsValidOn_WhenInactive_ShouldReturnFalse()
    {
        var e = CreateExemption();
        e.MarkVerified("admin");
        e.MarkRejected("admin2", "invalid");

        e.IsValidOn(new DateTime(2025, 6, 15)).Should().BeFalse();
    }

    [Fact]
    public void IsValidOn_WhenBeforeValidFrom_ShouldReturnFalse()
    {
        var e = CreateExemption(validFrom: new DateTime(2025, 1, 1));
        e.MarkVerified("admin");

        e.IsValidOn(new DateTime(2024, 6, 15)).Should().BeFalse();
    }

    [Fact]
    public void IsValidOn_WhenAfterValidUntil_ShouldReturnFalse()
    {
        var e = CreateExemption(validUntil: new DateTime(2024, 12, 31));
        e.MarkVerified("admin");

        e.IsValidOn(new DateTime(2025, 6, 15)).Should().BeFalse();
    }

    [Fact]
    public void MarkVerified_ShouldSetVerificationDetails()
    {
        var e = CreateExemption();
        e.MarkVerified("admin-user");

        e.VerificationStatus.Should().Be(ExemptionVerificationStatus.Verified);
        e.VerifiedBy.Should().Be("admin-user");
        e.LastVerifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkVerified_WithEmptyVerifier_ShouldThrow()
    {
        var e = CreateExemption();
        var act = () => e.MarkVerified("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkRejected_ShouldSetStatusAndNotes()
    {
        var e = CreateExemption();
        e.MarkRejected("admin", "Certificate expired");

        e.VerificationStatus.Should().Be(ExemptionVerificationStatus.Rejected);
        e.Status.Should().Be(TaxExemptionStatus.Inactive);
        e.Notes.Should().Contain("Rejection reason: Certificate expired");
    }

    [Fact]
    public void MarkRejected_WithEmptyRejector_ShouldThrow()
    {
        var e = CreateExemption();
        var act = () => e.MarkRejected("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Revoke_ShouldSetStatusToRevoked()
    {
        var e = CreateExemption();
        e.Revoke("compliance-officer", "Policy violation");

        e.Status.Should().Be(TaxExemptionStatus.Revoked);
        e.Notes.Should().Contain("Revoked by compliance-officer: Policy violation");
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ShouldThrow()
    {
        var e = CreateExemption();
        e.Revoke("admin", "first revoke");

        var act = () => e.Revoke("admin", "second revoke");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Revoke_WithEmptyRevoker_ShouldThrow()
    {
        var e = CreateExemption();
        var act = () => e.Revoke("", "reason");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Revoke_WithEmptyReason_ShouldThrow()
    {
        var e = CreateExemption();
        var act = () => e.Revoke("admin", "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetCertificateDocument_ShouldUpdatePath()
    {
        var e = CreateExemption();
        e.SetCertificateDocument("/docs/cert.pdf");

        e.CertificateDocumentPath.Should().Be("/docs/cert.pdf");
    }

    [Fact]
    public void ExtendValidity_ShouldUpdateValidUntil()
    {
        var e = CreateExemption();
        var newDate = new DateTime(2030, 12, 31);
        e.ExtendValidity(newDate);

        e.ValidUntil.Should().Be(newDate);
    }

    [Fact]
    public void ExtendValidity_WhenInactive_ShouldThrow()
    {
        var e = CreateExemption();
        e.MarkRejected("admin", "invalid");

        var act = () => e.ExtendValidity(new DateTime(2030, 1, 1));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ExtendValidity_BeforeValidFrom_ShouldThrow()
    {
        var e = CreateExemption(validFrom: new DateTime(2025, 1, 1));

        var act = () => e.ExtendValidity(new DateTime(2024, 6, 1));
        act.Should().Throw<ArgumentException>();
    }
}

public class TaxExemptionEnumTests
{
    [Theory]
    [InlineData(TaxExemptionType.NonProfit, 1)]
    [InlineData(TaxExemptionType.Educational, 2)]
    [InlineData(TaxExemptionType.Government, 3)]
    [InlineData(TaxExemptionType.Reseller, 4)]
    [InlineData(TaxExemptionType.Agricultural, 5)]
    [InlineData(TaxExemptionType.Manufacturing, 6)]
    [InlineData(TaxExemptionType.Diplomatic, 7)]
    [InlineData(TaxExemptionType.Medical, 8)]
    [InlineData(TaxExemptionType.Other, 99)]
    public void TaxExemptionType_ShouldHaveExpectedValues(TaxExemptionType t, int expected) =>
        ((int)t).Should().Be(expected);

    [Theory]
    [InlineData(TaxExemptionStatus.Active, 1)]
    [InlineData(TaxExemptionStatus.Inactive, 2)]
    [InlineData(TaxExemptionStatus.Revoked, 3)]
    public void TaxExemptionStatus_ShouldHaveExpectedValues(TaxExemptionStatus s, int expected) =>
        ((int)s).Should().Be(expected);

    [Theory]
    [InlineData(ExemptionVerificationStatus.Pending, 1)]
    [InlineData(ExemptionVerificationStatus.Verified, 2)]
    [InlineData(ExemptionVerificationStatus.Rejected, 3)]
    public void ExemptionVerificationStatus_ShouldHaveExpectedValues(ExemptionVerificationStatus s, int expected) =>
        ((int)s).Should().Be(expected);
}

#endregion

#region TaxJurisdiction Entity Tests

public class TaxJurisdictionTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var j = new TaxJurisdiction();
        j.Code.Should().Be(string.Empty);
        j.Name.Should().Be(string.Empty);
        j.IsActive.Should().BeTrue();
        j.IsReverseChargeApplicable.Should().BeFalse();
        j.TaxRules.Should().BeEmpty();
        j.ChildJurisdictions.Should().BeEmpty();
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var parentId = Guid.NewGuid();
        var j = new TaxJurisdiction
        {
            Code = "US-CA",
            Name = "California",
            Type = TaxJurisdictionType.State,
            ParentJurisdictionId = parentId,
            IsActive = true,
            TaxRegistrationNumber = "TAX-12345",
            IsReverseChargeApplicable = true
        };

        j.Code.Should().Be("US-CA");
        j.Name.Should().Be("California");
        j.Type.Should().Be(TaxJurisdictionType.State);
        j.ParentJurisdictionId.Should().Be(parentId);
        j.TaxRegistrationNumber.Should().Be("TAX-12345");
        j.IsReverseChargeApplicable.Should().BeTrue();
    }
}

#endregion

#region TaxBreakdown and Service Models Tests

public class TaxBreakdownTests
{
    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var tb = new TaxBreakdown
        {
            TaxType = TaxType.SalesTax,
            Description = "State sales tax",
            Rate = 0.0875m,
            TaxableAmount = 100m,
            TaxAmount = 8.75m,
            JurisdictionCode = "US-CA"
        };

        tb.TaxType.Should().Be(TaxType.SalesTax);
        tb.Description.Should().Be("State sales tax");
        tb.Rate.Should().Be(0.0875m);
        tb.TaxableAmount.Should().Be(100m);
        tb.TaxAmount.Should().Be(8.75m);
        tb.JurisdictionCode.Should().Be("US-CA");
    }

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var tb = new TaxBreakdown();
        tb.Description.Should().Be(string.Empty);
        tb.JurisdictionCode.Should().Be(string.Empty);
    }
}

#endregion

#region PaymentStatus Enum Tests

public class PaymentStatusEnumTests
{
    [Theory]
    [InlineData(PaymentStatus.Pending, 0)]
    [InlineData(PaymentStatus.Processing, 1)]
    [InlineData(PaymentStatus.Succeeded, 2)]
    [InlineData(PaymentStatus.Failed, 3)]
    [InlineData(PaymentStatus.Cancelled, 4)]
    [InlineData(PaymentStatus.RequiresAction, 5)]
    [InlineData(PaymentStatus.Refunded, 6)]
    [InlineData(PaymentStatus.Disputed, 7)]
    public void PaymentStatus_ShouldHaveExpectedValues(PaymentStatus status, int expected) =>
        ((int)status).Should().Be(expected);
}

#endregion

#region LedgerAccount Enum Tests

public class LedgerAccountEnumTests
{
    [Theory]
    [InlineData(LedgerAccount.Cash, 1000)]
    [InlineData(LedgerAccount.AccountsReceivable, 1100)]
    [InlineData(LedgerAccount.PrepaidExpenses, 1200)]
    [InlineData(LedgerAccount.UserWalletDeposits, 1300)]
    [InlineData(LedgerAccount.PaymentGatewayPending, 1400)]
    [InlineData(LedgerAccount.AccountsPayable, 2000)]
    [InlineData(LedgerAccount.DeferredRevenue, 2100)]
    [InlineData(LedgerAccount.UserWalletLiability, 2200)]
    [InlineData(LedgerAccount.RefundsPayable, 2300)]
    public void LedgerAccount_ShouldHaveExpectedValues(LedgerAccount account, int expected) =>
        ((int)account).Should().Be(expected);
}

#endregion

#region ProcessRefundResult Tests

public class ProcessRefundResultTests
{
    [Fact]
    public void ShouldSetRequiredProperties()
    {
        var refundId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var result = new ProcessRefundResult
        {
            RefundId = refundId,
            PaymentId = paymentId,
            RefundedAmount = 50m,
            Currency = "USD",
            Status = TransactionStatus.Completed,
            Reason = "Customer request",
            ProcessedAt = now,
            ReferenceNumber = "REF-001",
            EstimatedCompletionDate = now.AddDays(5),
            ProcessingFee = 1.50m
        };

        result.RefundId.Should().Be(refundId);
        result.PaymentId.Should().Be(paymentId);
        result.RefundedAmount.Should().Be(50m);
        result.Currency.Should().Be("USD");
        result.Status.Should().Be(TransactionStatus.Completed);
        result.Reason.Should().Be("Customer request");
        result.ReferenceNumber.Should().Be("REF-001");
        result.ProcessingFee.Should().Be(1.50m);
    }
}

#endregion

#region PaymentHistoryResult Tests

public class PaymentHistoryResultTests
{
    [Fact]
    public void ShouldSetRequiredProperties()
    {
        var paymentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var result = new PaymentHistoryResult
        {
            PaymentId = paymentId,
            UserId = userId,
            Amount = 99.99m,
            Currency = "EUR",
            Status = PaymentStatus.Succeeded,
            PaymentMethod = "credit_card",
            Description = "Course purchase",
            CreatedAt = now,
            UpdatedAt = now,
            TransactionReference = "TXN-123"
        };

        result.PaymentId.Should().Be(paymentId);
        result.UserId.Should().Be(userId);
        result.Amount.Should().Be(99.99m);
        result.Currency.Should().Be("EUR");
        result.Status.Should().Be(PaymentStatus.Succeeded);
        result.PaymentMethod.Should().Be("credit_card");
        result.TransactionReference.Should().Be("TXN-123");
    }
}

#endregion
