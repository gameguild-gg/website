using FluentAssertions;
using GameGuild.Commerce.Orders;
using GameGuild.Finance.Economy.Contracts;
using Xunit;

namespace GameGuild.Finance.Economy.Marketplace.UnitTests;

public sealed class CommerceOrderMarketplaceSettlementAuthorityTests
{
    [Theory]
    [InlineData(OrderMarketplaceCurrencyChoice.Hard, MarketplaceCurrencyChoice.Hard)]
    [InlineData(OrderMarketplaceCurrencyChoice.Soft, MarketplaceCurrencyChoice.Soft)]
    [InlineData(OrderMarketplaceCurrencyChoice.FixedMix, MarketplaceCurrencyChoice.FixedMix)]
    public async Task SettleAsync_MapsTheCommerceRequestToTheDurableEconomyWorkflow(
        OrderMarketplaceCurrencyChoice commerceChoice,
        MarketplaceCurrencyChoice economyChoice)
    {
        var now = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        var request = new OrderMarketplaceSettlementRequest(
            Guid.NewGuid(),
            commerceChoice,
            "order-idempotency");
        var settlementId = Guid.NewGuid();
        var durable = new RecordingSettlementService(new DurableMarketplaceSettlementResult(
                settlementId,
                request.OrderId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                MarketplaceSettlementStatus.Settled,
                MarketplaceEntitlementStatus.PendingGrant,
                [],
                PostingId.New(),
                1,
                "journal-hash",
                true,
                now));
        var authority = new CommerceOrderMarketplaceSettlementAuthority(
            durable,
            new FixedTimeProvider(now));

        var result = await authority.SettleAsync(request, CancellationToken.None);

        durable.Request.Should().NotBeNull();
        var mapped = durable.Request!;
        mapped.OrderId.Should().Be(request.OrderId);
        mapped.CurrencyChoice.Should().Be(economyChoice);
        mapped.IdempotencyKey.Should().Be(new IdempotencyKey(request.IdempotencyKey));
        mapped.SettledAt.Should().Be(now);
        result.IsAccepted.Should().BeTrue();
        result.SettlementId.Should().Be(settlementId);
        result.IsDuplicate.Should().BeTrue();
    }

    [Fact]
    public async Task SettleAsync_RejectsAnUnknownCommerceCurrencyChoice()
    {
        var authority = new CommerceOrderMarketplaceSettlementAuthority(
            new RecordingSettlementService(null!),
            new FixedTimeProvider(DateTimeOffset.UtcNow));
        var request = new OrderMarketplaceSettlementRequest(
            Guid.NewGuid(),
            (OrderMarketplaceCurrencyChoice)0,
            "idempotency");

        var action = async () => await authority.SettleAsync(request, CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingSettlementService(DurableMarketplaceSettlementResult result)
        : IDurableMarketplaceSettlementService
    {
        public SettleAuthoritativeMarketplaceOrderRequest? Request { get; private set; }

        public ValueTask<DurableMarketplaceSettlementResult> SettleAsync(
            SettleAuthoritativeMarketplaceOrderRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return ValueTask.FromResult(result);
        }
    }
}
