using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class EntityGraphAndLimitTests
{
    private static readonly DateTimeOffset Time = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GraphNormalizesEveryRequiredEntityDimensionIntoOneVersionedCluster()
    {
        var graph = new EntityRiskGraph();
        var nodes = Enum.GetValues<RiskEntityType>()
            .Select(type => new RiskEntityNode(type, $"hash-{type}"))
            .ToArray();
        for (var index = 1; index < nodes.Length; index++)
            graph.Link(nodes[0], nodes[index], $"evidence-{index}", Time);

        var cluster = graph.ClusterFor(nodes[^1]);

        cluster.Nodes.Should().BeEquivalentTo(nodes);
        cluster.Version.Should().Be(nodes.Length - 1);
        cluster.EvidenceHash.Should().NotBeNullOrWhiteSpace();
        graph.ClusterFor(new RiskEntityNode(RiskEntityType.Account, "unrelated")).Nodes.Should().ContainSingle();
    }

    [Fact]
    public void EntityReferencesCanBeDerivedWithHmacWithoutRetainingRawIdentifiers()
    {
        var node = RiskEntityNode.FromHmac(
            RiskEntityType.KycIdentity, "person@example.com", "secret"u8.ToArray());

        node.IdentifierHash.Should().HaveLength(64);
        node.IdentifierHash.Should().NotContain("person@example.com");
        FluentActions.Invoking(() => RiskEntityNode.FromHmac(
                RiskEntityType.KycIdentity, "person@example.com", []))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task AggregateLimitsSerializeConcurrentReservationsAndPreventOversubscription()
    {
        var store = new AggregateRiskLimitStore();
        var cluster = new EntityRiskCluster("cluster", 1, "evidence", []);
        var tasks = Enumerable.Range(0, 16).Select(index => Task.Run(() =>
        {
            try
            {
                store.Reserve(
                    Guid.NewGuid(), cluster, PostingTemplateKind.Spend,
                    new CoinAmount(CurrencyCode.HardCoin, 10), 50, Time, Time.AddHours(1));
                return true;
            }
            catch (AggregateRiskLimitExceededException)
            {
                return false;
            }
        }));

        var results = await Task.WhenAll(tasks);

        results.Count(success => success).Should().Be(5);
        store.Reservations.Sum(reservation => reservation.Amount.Units).Should().Be(50);
    }

    [Fact]
    public void AggregateLimitReplayIsIdempotentAndStaleClusterVersionFailsClosed()
    {
        var store = new AggregateRiskLimitStore();
        var id = Guid.NewGuid();
        var cluster = new EntityRiskCluster("cluster", 3, "evidence", []);
        var first = store.Reserve(
            id, cluster, PostingTemplateKind.PayoutReservation,
            new CoinAmount(CurrencyCode.HardCoin, 5), 10, Time, Time.AddHours(1));

        first.Id.Should().Be(id);
        first.ClusterVersion.Should().Be(3);
        first.ReservedAt.Should().Be(Time);
        store.Reserve(
            id, cluster, PostingTemplateKind.PayoutReservation,
            new CoinAmount(CurrencyCode.HardCoin, 5), 10, Time, Time.AddHours(1)).Should().Be(first);
        FluentActions.Invoking(() => store.Reserve(
                id, cluster with { Version = 4 }, PostingTemplateKind.PayoutReservation,
                new CoinAmount(CurrencyCode.HardCoin, 5), 10, Time, Time.AddHours(1)))
            .Should().Throw<RiskDecisionReuseException>();
        FluentActions.Invoking(() => store.Reserve(
                Guid.NewGuid(), cluster with { Version = 2 }, PostingTemplateKind.PayoutReservation,
                new CoinAmount(CurrencyCode.HardCoin, 1), 10, Time, Time.AddHours(1)))
            .Should().Throw<StaleEntityGraphException>();
    }
    [Fact]
    public void DifferentClusterOperationCurrencyAndExpiredReservationDoNotShareCapacity()
    {
        var store = new AggregateRiskLimitStore();
        var cluster = new EntityRiskCluster("cluster-a", 1, "evidence-a", []);

        store.Reserve(Guid.NewGuid(), cluster, PostingTemplateKind.Spend,
            new CoinAmount(CurrencyCode.HardCoin, 10), 10, Time, Time.AddHours(1));
        store.Reserve(Guid.NewGuid(), cluster, PostingTemplateKind.PayoutReservation,
            new CoinAmount(CurrencyCode.HardCoin, 10), 10, Time, Time.AddHours(1));
        store.Reserve(Guid.NewGuid(), cluster, PostingTemplateKind.Spend,
            new CoinAmount(CurrencyCode.SoftCoin, 10), 10, Time, Time.AddHours(1));
        store.Reserve(Guid.NewGuid(), new EntityRiskCluster("cluster-b", 1, "evidence-b", []),
            PostingTemplateKind.Spend, new CoinAmount(CurrencyCode.HardCoin, 10),
            10, Time, Time.AddHours(1));
        store.Reserve(Guid.NewGuid(), cluster, PostingTemplateKind.Spend,
            new CoinAmount(CurrencyCode.HardCoin, 10), 10, Time.AddHours(1), Time.AddHours(2));

        store.Reservations.Should().HaveCount(5);
    }

}
