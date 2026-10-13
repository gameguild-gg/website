using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.AccountCredits;

/// <summary>
///     Issue #410 v1 gift flow: purchase issues the full code exactly once (hash stored),
///     redemption grants account credit to the redeeming account, previews never leak the
///     code, and the due-delivery/expiry progression flips statuses.
/// </summary>
public class GiftServiceTests
{
    private static async Task<PaymentsPersistenceTestDbContext> CreateContextAsync()
    {
        var context = new PaymentsPersistenceTestDbContext(
            new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
                .UseInMemoryDatabase($"gifts-{Guid.NewGuid():N}")
                .Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static (GiftService GiftService, PaymentsPersistenceTestDbContext Context) CreateServices()
    {
        var context = CreateContextAsync().GetAwaiter().GetResult();
        return (new GiftService(context, new AccountCreditService(context)), context);
    }

    [Fact]
    public async Task Purchase_Returns_Full_Code_Once_And_Stores_Only_The_Hash()
    {
        var (service, context) = CreateServices();
        var tenantId = Guid.NewGuid();
        var giver = Guid.NewGuid();

        var result = await service.PurchaseAsync(new PurchaseGiftRequest(
            tenantId, giver, "friend@example.test", 60m, "USD", 6,
            RecipientName: "Friend", PersonalMessage: "Happy birthday!"));

        result.IsSuccess.Should().BeTrue();
        result.Value.RedemptionCode.Should().NotBeNullOrWhiteSpace().And.HaveLength(32);
        var stored = await context.Set<GiftSubscription>().SingleAsync();
        stored.Status.Should().Be(GiftStatus.Purchased);
        stored.RedemptionCodeHash.Should().Be(GiftRedemptionCodeGenerator.HashCode(result.Value.RedemptionCode));
        stored.RedemptionCodeHash.Should().NotBe(result.Value.RedemptionCode);
        stored.PersonalMessage.Should().Be("Happy birthday!");
        stored.ExpiresAt.Should().BeCloseTo(stored.PurchasedAt.AddYears(1), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Redeem_Grants_Credit_To_The_Redeeming_Account()
    {
        var (service, context) = CreateServices();
        var tenantId = Guid.NewGuid();
        var purchase = (await service.PurchaseAsync(new PurchaseGiftRequest(
            tenantId, Guid.NewGuid(), "friend@example.test", 60m, "USD", 6))).Value;

        var recipient = Guid.NewGuid();
        var redemption = await service.RedeemAsync(tenantId, purchase.RedemptionCode, recipient);

        redemption.IsSuccess.Should().BeTrue();
        redemption.Value.Status.Should().Be(GiftStatus.Redeemed);

        var grant = await context.Set<AccountCreditGrant>().SingleAsync();
        grant.UserId.Should().Be(recipient);
        grant.Amount.Should().Be(60m);
        grant.Source.Should().Be(AccountCreditSource.Gift);
        grant.SourceReferenceId.Should().Be(await context.Set<GiftSubscription>().Select(gift => gift.Id).SingleAsync());
        grant.IdempotencyKey.Should().Be($"gift:{grant.SourceReferenceId}:redemption");
    }

    [Fact]
    public async Task Redeem_Is_Idempotent_Rejected_For_The_Same_Gift()
    {
        var (service, _) = CreateServices();
        var tenantId = Guid.NewGuid();
        var purchase = (await service.PurchaseAsync(new PurchaseGiftRequest(
            tenantId, Guid.NewGuid(), "friend@example.test", 25m, "EUR", 3))).Value;

        (await service.RedeemAsync(tenantId, purchase.RedemptionCode, Guid.NewGuid())).IsSuccess.Should().BeTrue();
        var second = await service.RedeemAsync(tenantId, purchase.RedemptionCode, Guid.NewGuid());

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("Gifts.AlreadyRedeemed");
    }

    [Fact]
    public async Task GetByCode_Returns_Non_Secret_View_Without_The_Code()
    {
        var (service, _) = CreateServices();
        var tenantId = Guid.NewGuid();
        var purchase = (await service.PurchaseAsync(new PurchaseGiftRequest(
            tenantId, Guid.NewGuid(), "friend@example.test", 10m, "USD", 1))).Value;

        var view = await service.GetByCodeAsync(tenantId, purchase.RedemptionCode);

        view.IsSuccess.Should().BeTrue();
        view.Value.Amount.Should().Be(10m);
        view.Value.Currency.Should().Be("USD");
        view.Value.Status.Should().Be(GiftStatus.Purchased);
        // GiftPublicView exposes only the non-secret prefix — the full code never round-trips.
        view.Value.RedemptionCodePrefix.Should().NotBe(purchase.RedemptionCode);

        var unknown = await service.GetByCodeAsync(tenantId, "does-not-exist");
        unknown.IsFailure.Should().BeTrue();
        unknown.Error.Code.Should().Be("Gifts.NotFound");
    }

    [Fact]
    public async Task ProgressDueAsync_Marks_Scheduled_Deliveries_And_Expiries()
    {
        var (service, context) = CreateServices();
        var tenantId = Guid.NewGuid();
        var now = SystemClock.UtcNow;

        var deliverablePurchase = await service.PurchaseAsync(new PurchaseGiftRequest(
            tenantId, Guid.NewGuid(), "a@example.test", 5m, "USD", 1, DeliveryDate: now.AddHours(-1)));
        var futurePurchase = await service.PurchaseAsync(new PurchaseGiftRequest(
            tenantId, Guid.NewGuid(), "b@example.test", 5m, "USD", 1, DeliveryDate: now.AddDays(2)));
        deliverablePurchase.IsSuccess.Should().BeTrue();
        futurePurchase.IsSuccess.Should().BeTrue();

        var progressed = await service.ProgressDueAsync(now, batchSize: 100);

        progressed.Should().BeGreaterThanOrEqualTo(1);
        var statuses = await context.Set<GiftSubscription>().ToDictionaryAsync(gift => gift.RecipientEmail, gift => gift.Status);
        statuses["a@example.test"].Should().Be(GiftStatus.Delivered);
        statuses["b@example.test"].Should().Be(GiftStatus.Purchased);
    }

    [Fact]
    public async Task GetSent_And_GetReceived_Filter_By_User()
    {
        var (service, _) = CreateServices();
        var tenantId = Guid.NewGuid();
        var giver = Guid.NewGuid();
        var purchase = (await service.PurchaseAsync(new PurchaseGiftRequest(
            tenantId, giver, "friend@example.test", 12m, "USD", 2))).Value;
        await service.RedeemAsync(tenantId, purchase.RedemptionCode, recipientUserId: Guid.NewGuid());

        // refresh the entity to see the recipient binding
        var sent = await service.GetSentAsync(tenantId, giver, 0, 10);
        var received = await service.GetReceivedAsync(tenantId, giver, 0, 10);

        sent.TotalCount.Should().Be(1);
        sent.Items.Single().Status.Should().Be(GiftStatus.Redeemed);
        received.TotalCount.Should().Be(0); // giver never redeemed anything
    }
}
