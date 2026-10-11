using FluentAssertions;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Entities;

/// <summary>
///     Issue #410 v1 entity state machines: account credit grants (consume/revoke/expire),
///     gift subscriptions (Purchased → Delivered → Redeemed/Expired with delivery gating and
///     the one-year window) and referral funnel transitions.
/// </summary>
public class AccountCreditEntitiesTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    private static AccountCreditGrant Grant(
        decimal amount = 50m,
        DateTime? expiresAt = null,
        string idempotencyKey = "key-1") =>
        new(Guid.NewGuid(), Guid.NewGuid(), amount, "USD", AccountCreditSource.Manual,
            AccountCreditType.General, "test grant", expiresAt: expiresAt, idempotencyKey: idempotencyKey);

    [Fact]
    public void Grant_Consume_Drains_Bounded_By_Remaining()
    {
        var grant = Grant(30m);

        var first = grant.Consume(20m, Now);
        var second = grant.Consume(20m, Now);

        first.Should().Be(20m);
        second.Should().Be(10m);
        grant.RemainingAmount.Should().Be(0m);
        grant.Status.Should().Be(AccountCreditGrantStatus.FullyUsed);
        grant.FullyUsedAt.Should().Be(Now);
    }

    [Fact]
    public void Grant_Revoke_Requires_Reason_And_Clears_Balance()
    {
        var grant = Grant(25m);

        grant.Invoking(g => g.Revoke("  ", Now)).Should().Throw<ArgumentException>();
        grant.Revoke("goodwill reversal", Now);

        grant.Status.Should().Be(AccountCreditGrantStatus.Revoked);
        grant.RemainingAmount.Should().Be(0m);
        grant.RevocationReason.Should().Be("goodwill reversal");
        grant.IsSpendable.Should().BeFalse();
    }

    [Fact]
    public void Grant_Revoke_Rejects_Terminal_States()
    {
        var grant = Grant(25m);
        grant.Consume(25m, Now);

        grant.Invoking(g => g.Revoke("late", Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Grant_MarkExpired_Only_From_Active()
    {
        var grant = Grant(40m);
        grant.MarkExpired(Now);
        grant.Status.Should().Be(AccountCreditGrantStatus.Expired);

        grant.MarkExpired(Now); // idempotent no-op
        grant.Status.Should().Be(AccountCreditGrantStatus.Expired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Grant_Rejects_NonPositive_Amounts(decimal amount)
    {
        var act = () => Grant(amount);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Grant_Rejects_Unknown_Tenant_Or_User()
    {
        var act = () => new AccountCreditGrant(Guid.Empty, Guid.NewGuid(), 10m, "USD",
            AccountCreditSource.Manual, AccountCreditType.General, "d");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Gift_Redeem_Grants_Status_And_Links_Recipient()
    {
        var giver = Guid.NewGuid();
        var recipient = Guid.NewGuid();
        var gift = NewGift(giver);

        gift.Redeem(recipient, Now);

        gift.Status.Should().Be(GiftStatus.Redeemed);
        gift.RecipientUserId.Should().Be(recipient);
        gift.RedeemedAt.Should().Be(Now);
        gift.DeliveredAt.Should().Be(Now);
    }

    [Fact]
    public void Gift_Redeem_Blocks_Giver_And_Second_Redeem()
    {
        var giver = Guid.NewGuid();
        var gift = NewGift(giver);

        gift.Invoking(g => g.Redeem(giver, Now)).Should().Throw<InvalidOperationException>()
            .WithMessage("*giver*");

        gift.Redeem(Guid.NewGuid(), Now);
        gift.Invoking(g => g.Redeem(Guid.NewGuid(), Now)).Should().Throw<InvalidOperationException>()
            .WithMessage("*already*");
    }

    [Fact]
    public void Gift_Delivery_Schedule_Blocks_Early_Redemption()
    {
        var gift = NewGift(Guid.NewGuid(), deliveryDate: Now.AddDays(3));

        gift.IsRedeemableAt(Now).Should().BeFalse();
        gift.Invoking(g => g.Redeem(Guid.NewGuid(), Now)).Should().Throw<InvalidOperationException>()
            .WithMessage("*delivery date*");

        gift.MarkDelivered(Now); // delivery not due yet: stays Purchased
        gift.Status.Should().Be(GiftStatus.Purchased);

        var due = Now.AddDays(3);
        gift.MarkDelivered(due);
        gift.Status.Should().Be(GiftStatus.Delivered);
        gift.IsRedeemableAt(due).Should().BeTrue();
    }

    [Fact]
    public void Gift_Expires_After_One_Year_Window()
    {
        var purchasedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var gift = NewGift(Guid.NewGuid(), purchasedAt: purchasedAt);

        gift.ExpiresAt.Should().Be(purchasedAt.AddYears(1));
        gift.IsRedeemableAt(purchasedAt.AddYears(1).AddDays(-1)).Should().BeTrue();

        var pastWindow = purchasedAt.AddYears(1).AddDays(1);
        gift.MarkExpired(pastWindow);
        gift.Status.Should().Be(GiftStatus.Expired);
        gift.Invoking(g => g.Redeem(Guid.NewGuid(), pastWindow)).Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(37)]
    public void Gift_Rejects_Invalid_Durations(int months)
    {
        var act = () => NewGift(Guid.NewGuid(), durationMonths: months);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Gift_Rejects_Oversized_Personal_Message()
    {
        var act = () => NewGift(Guid.NewGuid(), personalMessage: new string('x', 501));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Referral_Follows_Click_Signup_Conversion_Rewards()
    {
        var referral = NewReferral();
        var referee = Guid.NewGuid();

        referral.RecordClick("referee@example.test", "ip-hash", Now);
        referral.Status.Should().Be(ReferralStatus.Clicked);
        referral.ClickedAt.Should().Be(Now);

        referral.RecordSignup(referee, "ip-hash", Now);
        referral.Status.Should().Be(ReferralStatus.SignedUp);
        referral.RefereeUserId.Should().Be(referee);

        var subscriptionId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        referral.RecordConversion(subscriptionId, invoiceId, Now);
        referral.Status.Should().Be(ReferralStatus.Converted);
        referral.ConversionInvoiceId.Should().Be(invoiceId);

        // idempotent conversion replay
        referral.RecordConversion(subscriptionId, invoiceId, Now.AddMinutes(1));
        referral.Status.Should().Be(ReferralStatus.Converted);

        referral.AttachRewards(Guid.NewGuid(), Guid.NewGuid(), Now);
        referral.Status.Should().Be(ReferralStatus.RewardIssued);
    }

    [Fact]
    public void Referral_Self_Referral_Is_Flagged_Fraudulent()
    {
        var referral = NewReferral();

        referral.RecordSignup(referral.ReferrerUserId, null, Now);

        referral.Status.Should().Be(ReferralStatus.Fraudulent);
        referral.RefereeUserId.Should().BeNull();
        referral.FraudReason.Should().Contain("Self-referral");
    }

    [Fact]
    public void Referral_Conversion_Requires_Signup_First()
    {
        var referral = NewReferral();
        referral.Invoking(r => r.RecordConversion(Guid.NewGuid(), Guid.NewGuid(), Now))
            .Should().Throw<InvalidOperationException>();
    }

    private static GiftSubscription NewGift(
        Guid giver,
        DateTime? deliveryDate = null,
        string? personalMessage = null,
        int durationMonths = 3,
        DateTime? purchasedAt = null) =>
        new(Guid.NewGuid(), giver, "friend@example.test", 45m, "USD", durationMonths,
            GiftRedemptionCodeGenerator.HashCode("code"), "ABCD12",
            deliveryDate: deliveryDate, personalMessage: personalMessage, purchasedAt: purchasedAt);

    private static Referral NewReferral() => new(Guid.NewGuid(), Guid.NewGuid(), "ABC12345");
}
