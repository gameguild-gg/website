using System.Security.Cryptography;
using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.UnitTests.Funding;

namespace GameGuild.Finance.Economy.UnitTests.Ledger;

public sealed class ChainAnchorServiceTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-04-01T00:00:00Z");

    [Fact]
    public void PeriodicAnchor_SignsCurrentHeadAndQueuesImmutableStorageDispatch()
    {
        var (store, signer) = StoreWithPosting();
        var service = new ChainAnchorService(store, signer);

        var anchor = service.CreatePeriodic(Time.AddHours(1));

        anchor.Kind.Should().Be(ChainAnchorKind.Periodic);
        anchor.DispatchSnapshotHash.Should().BeNull();
        anchor.HeadSequence.Should().Be(1);
        anchor.HeadHash.Should().Be(store.JournalEntries[^1].Hash);
        anchor.CredentialId.Should().Be("anchor-key-1");
        signer.Verify(anchor.CanonicalPayload, anchor.Signature).Should().BeTrue();
        store.ChainAnchors.Should().ContainSingle().Which.Should().Be(anchor);
        store.OutboxMessages.Should().HaveCount(2);
        store.OutboxMessages[^1].Type.Should().Be("economy.chain-anchor.persist.v1");
    }

    [Fact]
    public void OnDemandAnchor_BindsCanonicalDispatchSnapshotHash()
    {
        var (store, signer) = StoreWithPosting();
        var service = new ChainAnchorService(store, signer);
        var firstSnapshot = Convert.ToHexStringLower(SHA256.HashData("dispatch-a"u8));
        var secondSnapshot = Convert.ToHexStringLower(SHA256.HashData("dispatch-b"u8));

        var first = service.CreateOnDemand(firstSnapshot, Time.AddHours(1));
        var second = service.CreateOnDemand(secondSnapshot, Time.AddHours(1));

        first.Kind.Should().Be(ChainAnchorKind.OnDemand);
        first.DispatchSnapshotHash.Should().Be(firstSnapshot);
        first.Signature.Should().NotBe(second.Signature);
        first.CanonicalPayload.Should().Contain(firstSnapshot);
        signer.Verify(first.CanonicalPayload, first.Signature).Should().BeTrue();
    }

    [Fact]
    public void Anchor_RejectsMissingHeadMalformedSnapshotAndWeakCredentials()
    {
        var empty = new InMemoryLedgerKernelStore();
        var signer = new HmacChainHeadSigner("anchor-key-1", RandomNumberGenerator.GetBytes(32));

        FluentActions.Invoking(() => new ChainAnchorService(empty, signer).CreatePeriodic(Time))
            .Should().Throw<InvalidOperationException>();
        var (store, _) = StoreWithPosting();
        FluentActions.Invoking(() => new ChainAnchorService(store, signer).CreateOnDemand("not-a-hash", Time))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new HmacChainHeadSigner("key", new byte[31]))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Anchor_RollsBackWhenImmutableStorageOutboxCreationFails()
    {
        var (store, signer) = StoreWithPosting();
        var service = new ChainAnchorService(store, signer, new ThrowingAnchorOutboxFactory());
        var beforeOutbox = store.OutboxMessages.Count;

        FluentActions.Invoking(() => service.CreatePeriodic(Time.AddHours(1)))
            .Should().Throw<InvalidOperationException>().WithMessage("anchor outbox failure");

        store.ChainAnchors.Should().BeEmpty();
        store.OutboxMessages.Should().HaveCount(beforeOutbox);
    }

    private static (InMemoryLedgerKernelStore Store, HmacChainHeadSigner Signer) StoreWithPosting()
    {
        var store = new InMemoryLedgerKernelStore();
        var posting = new TransactionalPostingService(store);
        var observed = FundingTestDriver.Observe(posting, Time, providerObject: "pi_anchor");
        FundingTestDriver.Confirm(posting, observed, Time.AddMinutes(1), "anchor-topup");
        return (store, new HmacChainHeadSigner("anchor-key-1", RandomNumberGenerator.GetBytes(32)));
    }

    private sealed class ThrowingAnchorOutboxFactory : IChainAnchorOutboxFactory
    {
        public ImmutableOutboxMessage Persist(ChainAnchor anchor) =>
            throw new InvalidOperationException("anchor outbox failure");
    }
}
