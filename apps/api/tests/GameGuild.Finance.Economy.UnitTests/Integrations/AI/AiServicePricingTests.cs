using FluentAssertions;
using GameGuild.Finance.Economy.Integrations.AI;
using Xunit;

namespace GameGuild.Finance.Economy.UnitTests;

public sealed class AiServicePricingTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProviderRateCard_ComputesExactTokenCostWithCeilingArithmetic()
    {
        var rateCard = RateCard(inputCostPerMillion: 2_000_000_000, outputCostPerMillion: 8_000_000_000);

        var cost = rateCard.CalculateCost(inputTokens: 1_250, outputTokens: 750);

        cost.InputCostUsdNanos.Should().Be(2_500_000);
        cost.OutputCostUsdNanos.Should().Be(6_000_000);
        cost.TotalCostUsdNanos.Should().Be(8_500_000);
    }

    [Fact]
    public void ProviderRateCard_RoundsEachProviderCostLegUpToOneNano()
    {
        var rateCard = RateCard(inputCostPerMillion: 1, outputCostPerMillion: 1);

        var cost = rateCard.CalculateCost(inputTokens: 1, outputTokens: 1);

        cost.InputCostUsdNanos.Should().Be(1);
        cost.OutputCostUsdNanos.Should().Be(1);
        cost.TotalCostUsdNanos.Should().Be(2);
    }

    [Fact]
    public void ServicePriceSnapshot_AcceptsExactMinimumMarginEquality()
    {
        var snapshot = AiServicePriceSnapshot.Create(
            "ai.grade",
            RateCard(inputCostPerMillion: 400_000_000, outputCostPerMillion: 400_000_000),
            maximumInputTokens: 1_000_000,
            maximumOutputTokens: 1_000_000,
            trailingHighPercentileCostUsdNanos: 700_000_000,
            providerFxStressCostUsdNanos: 800_000_000,
            minimumGrossMarginPpm: 200_000,
            Now,
            Now.AddMinutes(10));

        snapshot.StressedProviderCostUsdNanos.Should().Be(800_000_000);
        snapshot.PriceSoftUnits.Should().Be(100_000);
        snapshot.MeetsMargin(100_000).Should().BeTrue();
    }

    [Fact]
    public void ServicePriceSnapshot_RejectsOneSoftCoinBelowMarginFloor()
    {
        var snapshot = AiServicePriceSnapshot.Create(
            "ai.grade",
            RateCard(inputCostPerMillion: 400_000_000, outputCostPerMillion: 400_000_000),
            1_000_000,
            1_000_000,
            700_000_000,
            800_000_000,
            200_000,
            Now,
            Now.AddMinutes(10));

        snapshot.MeetsMargin(99_999).Should().BeFalse();
    }

    [Fact]
    public void ServicePriceSnapshot_RequiresOneMoreSoftCoinWhenCostIsOneNanoAboveEquality()
    {
        var snapshot = AiServicePriceSnapshot.Create(
            "ai.grade",
            RateCard(inputCostPerMillion: 400_000_000, outputCostPerMillion: 400_000_000),
            1_000_000,
            1_000_000,
            700_000_000,
            800_000_001,
            200_000,
            Now,
            Now.AddMinutes(10));

        snapshot.PriceSoftUnits.Should().Be(100_001);
        snapshot.MeetsMargin(100_000).Should().BeFalse();
    }

    [Fact]
    public void ProviderRateCard_RejectsCostArithmeticOverflow()
    {
        var rateCard = RateCard(long.MaxValue, long.MaxValue);

        var action = () => rateCard.CalculateCost(int.MaxValue, int.MaxValue);

        action.Should().Throw<OverflowException>();
    }

    [Fact]
    public void ProviderRateCard_ValidatesMetadataRatesWindowAndUsage()
    {
        FluentActions.Invoking(() => new AiProviderRateCard(
                " ", AiProvider.OpenAi, "model", 1, 1, Now, Now.AddMinutes(1)))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new AiProviderRateCard(
                "v1", (AiProvider)999, "model", 1, 1, Now, Now.AddMinutes(1)))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new AiProviderRateCard(
                "v1", AiProvider.OpenAi, " ", 1, 1, Now, Now.AddMinutes(1)))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new AiProviderRateCard(
                "v1", AiProvider.OpenAi, "model", -1, 1, Now, Now.AddMinutes(1)))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new AiProviderRateCard(
                "v1", AiProvider.OpenAi, "model", 1, -1, Now, Now.AddMinutes(1)))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new AiProviderRateCard(
                "v1", AiProvider.OpenAi, "model", 0, 0, Now, Now.AddMinutes(1)))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new AiProviderRateCard(
                "v1", AiProvider.OpenAi, "model", 0, 1, Now, Now))
            .Should().Throw<ArgumentException>();

        var outputOnly = new AiProviderRateCard(
            " v1 ", AiProvider.OpenAi, " model ", 0, 1, Now, Now.AddMinutes(1));
        outputOnly.Version.Should().Be("v1");
        outputOnly.Model.Should().Be("model");
        outputOnly.ObservedAt.Should().Be(Now);
        outputOnly.ExpiresAt.Should().Be(Now.AddMinutes(1));
        outputOnly.CalculateCost(0, 1).TotalCostUsdNanos.Should().Be(1);
        FluentActions.Invoking(() => outputOnly.CalculateCost(-1, 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => outputOnly.CalculateCost(0, -1))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ServicePriceSnapshot_ValidatesEnvelopeCostsWindowAndZeroCost()
    {
        var rate = RateCard(1, 0);
        Action<string, AiProviderRateCard, int, int, long, long, int, DateTimeOffset, DateTimeOffset> create =
            (service, card, input, output, trailing, fx, margin, observed, expires) =>
                AiServicePriceSnapshot.Create(
                    service, card, input, output, trailing, fx, margin, observed, expires);

        FluentActions.Invoking(() => create(" ", rate, 1, 0, 1, 1, 0, Now, Now.AddMinutes(1)))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => create("ai", null!, 1, 0, 1, 1, 0, Now, Now.AddMinutes(1)))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => create("ai", rate, -1, 0, 1, 1, 0, Now, Now.AddMinutes(1)))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => create("ai", rate, 0, -1, 1, 1, 0, Now, Now.AddMinutes(1)))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => create("ai", rate, 0, 0, 1, 1, 0, Now, Now.AddMinutes(1)))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => create("ai", rate, 0, 1, -1, 1, 0, Now, Now.AddMinutes(1)))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => create("ai", rate, 0, 1, 1, -1, 0, Now, Now.AddMinutes(1)))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => create("ai", rate, 0, 1, 1, 1, 0, Now, Now))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => create("ai", rate, 0, 1, 0, 0, 0, Now, Now.AddMinutes(1)))
            .Should().Throw<AiProviderCostUnknownException>();

        var snapshot = AiServicePriceSnapshot.Create(
            " ai ", rate, 1, 0, 1, 1, 0, Now, Now.AddMinutes(1));
        snapshot.ServiceCode.Should().Be("ai");
        snapshot.MeetsMargin(0).Should().BeFalse();
        snapshot.MeetsMargin(-1).Should().BeFalse();
    }

    [Fact]
    public void RateCardCatalog_ValidatesPublishingAndLookupInputs()
    {
        var catalog = new AiServiceRateCardCatalog();
        var snapshot = Snapshot("v1", Now.AddMinutes(-1), Now.AddMinutes(1), 10);
        catalog.Snapshots.Should().BeEmpty();
        FluentActions.Invoking(() => catalog.Publish(null!)).Should().Throw<ArgumentNullException>();
        catalog.Publish(snapshot);
        catalog.Snapshots.Should().ContainSingle().Which.Should().BeSameAs(snapshot);
        FluentActions.Invoking(() => catalog.Publish(snapshot)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => catalog.Resolve(" ", AiProvider.OpenAi, "gpt-test", Now))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => catalog.Resolve("ai.grade", (AiProvider)999, "gpt-test", Now))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => catalog.Resolve("ai.grade", AiProvider.OpenAi, " ", Now))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => catalog.Resolve("ai.grade", AiProvider.Anthropic, "gpt-test", Now))
            .Should().Throw<AiProviderCostUnknownException>();
        FluentActions.Invoking(() => catalog.Resolve("ai.grade", AiProvider.OpenAi, "other", Now))
            .Should().Throw<AiProviderCostUnknownException>();
        FluentActions.Invoking(() => catalog.Resolve("ai.grade", AiProvider.OpenAi, "gpt-test", Now.AddMinutes(-2)))
            .Should().Throw<AiProviderCostUnknownException>();
    }

    [Fact]
    public void RateCardCatalog_AllowsIndependentServiceProviderModelAndVersionIdentities()
    {
        var catalog = new AiServiceRateCardCatalog();
        catalog.Publish(Snapshot("v1", Now.AddMinutes(-5), Now.AddMinutes(5), 10));
        catalog.Publish(AiServicePriceSnapshot.Create(
            "ai.other",
            new AiProviderRateCard(
                "v1", AiProvider.OpenAi, "gpt-test", 10, 10,
                Now.AddMinutes(-5), Now.AddMinutes(5)),
            100,
            100,
            10,
            10,
            100_000,
            Now.AddMinutes(-5),
            Now.AddMinutes(5)));
        catalog.Publish(AiServicePriceSnapshot.Create(
            "ai.grade",
            new AiProviderRateCard(
                "v1", AiProvider.Anthropic, "gpt-test", 10, 10,
                Now.AddMinutes(-5), Now.AddMinutes(5)),
            100,
            100,
            10,
            10,
            100_000,
            Now.AddMinutes(-5),
            Now.AddMinutes(5)));
        catalog.Publish(AiServicePriceSnapshot.Create(
            "ai.grade",
            new AiProviderRateCard(
                "v1", AiProvider.OpenAi, "gpt-other", 10, 10,
                Now.AddMinutes(-5), Now.AddMinutes(5)),
            100,
            100,
            10,
            10,
            100_000,
            Now.AddMinutes(-5),
            Now.AddMinutes(5)));
        catalog.Publish(Snapshot("v2", Now.AddMinutes(-5), Now.AddMinutes(5), 10));

        catalog.Snapshots.Should().HaveCount(5);
        catalog.Snapshots.Select(snapshot => (
                snapshot.ServiceCode,
                snapshot.RateCard.Provider,
                snapshot.RateCard.Model,
                snapshot.RateCard.Version))
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void RateCardCatalog_RejectsUnknownAndStaleCostFeeds()
    {
        var catalog = new AiServiceRateCardCatalog();
        var snapshot = AiServicePriceSnapshot.Create(
            "ai.grade",
            RateCard(10, 20),
            100,
            100,
            1,
            2,
            100_000,
            Now.AddMinutes(-10),
            Now);
        catalog.Publish(snapshot);

        FluentActions.Invoking(() => catalog.Resolve("ai.unknown", AiProvider.OpenAi, "gpt-test", Now))
            .Should().Throw<AiProviderCostUnknownException>();
        FluentActions.Invoking(() => catalog.Resolve("ai.grade", AiProvider.OpenAi, "gpt-test", Now))
            .Should().Throw<AiProviderCostStaleException>();
    }

    [Fact]
    public void RateCardCatalog_RejectsAStaleProviderRateInsideALiveServiceSnapshot()
    {
        var catalog = new AiServiceRateCardCatalog();
        var rate = new AiProviderRateCard(
            "provider-v1", AiProvider.OpenAi, "gpt-test", 10, 10,
            Now.AddMinutes(-10), Now.AddMinutes(-1));
        var snapshot = AiServicePriceSnapshot.Create(
            "ai.grade",
            rate,
            100,
            100,
            10,
            10,
            0,
            Now.AddMinutes(-10),
            Now.AddMinutes(10));
        catalog.Publish(snapshot);

        FluentActions.Invoking(() => catalog.Resolve(
                "ai.grade", AiProvider.OpenAi, "gpt-test", Now))
            .Should().Throw<AiProviderCostStaleException>();
    }

    [Fact]
    public void RateCardCatalog_ResolvesLatestEffectiveVersionWithoutChangingFixedParity()
    {
        var catalog = new AiServiceRateCardCatalog();
        var first = Snapshot("v1", Now.AddHours(-2), Now.AddHours(-1), 10);
        var current = Snapshot("v2", Now.AddMinutes(-5), Now.AddMinutes(5), 20);
        catalog.Publish(first);
        catalog.Publish(current);

        var resolved = catalog.Resolve("ai.grade", AiProvider.OpenAi, "gpt-test", Now);

        resolved.RateCard.Version.Should().Be("v2");
        Economy.Policy.EconomyParity.SoftCoinUnitsPerUsd.Should().Be(100_000);
    }

    private static AiServicePriceSnapshot Snapshot(
        string version,
        DateTimeOffset observedAt,
        DateTimeOffset expiresAt,
        long rate) => AiServicePriceSnapshot.Create(
        "ai.grade",
        new AiProviderRateCard(version, AiProvider.OpenAi, "gpt-test", rate, rate, observedAt, expiresAt),
        100,
        100,
        rate,
        rate,
        100_000,
        observedAt,
        expiresAt);

    private static AiProviderRateCard RateCard(long inputCostPerMillion, long outputCostPerMillion) =>
        new("rate-v1", AiProvider.OpenAi, "gpt-test", inputCostPerMillion, outputCostPerMillion, Now, Now.AddMinutes(10));
}
