using System.Text.Json;
using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.UnitTests.Ledger;

public sealed class RegisteredPostingGatewayTests
{
    [Fact]
    public void AuthorityRejectsMissingSecurityBindings()
    {
        var create = () => new RegisteredPostingAuthority(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "operation", 1);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AllocationRequiresRootRange()
    {
        var create = () => new RegisteredPostingAllocation(
            1, new CreditLotId(Guid.NewGuid()), 100, []);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequestRejectsAllocationsForUnknownLines()
    {
        var allocation = new RegisteredPostingAllocation(
            3, new CreditLotId(Guid.NewGuid()), 100,
            [new RootTraceRange(new SourceStampId(Guid.NewGuid()), 0, 100, 1)]);

        var create = () => new RegisteredPostingRequest(Authority(), Posting(), [allocation]);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PayloadUsesGatewayOwnedIdsAndSerializesTheDatabaseContract()
    {
        var allocation = new RegisteredPostingAllocation(
            1,
            new CreditLotId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            100,
            [new RootTraceRange(new SourceStampId(Guid.Parse("20000000-0000-0000-0000-000000000001")), 0, 100, 4)]);
        var request = new RegisteredPostingRequest(Authority(), Posting(), [allocation]);
        var ids = new Queue<Guid>([
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            Guid.Parse("30000000-0000-0000-0000-000000000002"),
            Guid.Parse("30000000-0000-0000-0000-000000000003"),
            Guid.Parse("30000000-0000-0000-0000-000000000004")
        ]);

        var payload = RegisteredPostingPayloadFactory.Create(
            request,
            new Dictionary<int, Guid>
            {
                [1] = Guid.Parse("40000000-0000-0000-0000-000000000001"),
                [2] = Guid.Parse("40000000-0000-0000-0000-000000000002")
            },
            ids.Dequeue);

        using var lines = JsonDocument.Parse(payload.Lines);
        using var allocations = JsonDocument.Parse(payload.Allocations);
        using var ranges = JsonDocument.Parse(payload.RootRanges);
        using var epochs = JsonDocument.Parse(payload.ExpectedReversalEpochs);

        lines.RootElement.GetArrayLength().Should().Be(2);
        lines.RootElement[0].GetProperty("id").GetGuid().Should().Be(Guid.Parse("30000000-0000-0000-0000-000000000001"));
        lines.RootElement[0].GetProperty("account_id").GetGuid().Should().Be(Guid.Parse("40000000-0000-0000-0000-000000000001"));
        lines.RootElement[0].GetProperty("account_code").GetInt32().Should().Be((int)EconomyAccountCode.SoftCoinLiability);
        allocations.RootElement[0].GetProperty("journal_line_id").GetGuid().Should().Be(Guid.Parse("30000000-0000-0000-0000-000000000001"));
        ranges.RootElement[0].GetProperty("entry_allocation_id").GetGuid().Should().Be(Guid.Parse("30000000-0000-0000-0000-000000000003"));
        epochs.RootElement[0].GetProperty("expected_epoch").GetInt64().Should().Be(4);
    }

    [Fact]
    public void PayloadUsesStableIdentifiersForAnIdempotentRetry()
    {
        var request = new RegisteredPostingRequest(Authority(), Posting());
        var accounts = new Dictionary<int, Guid>
        {
            [1] = Guid.Parse("40000000-0000-0000-0000-000000000001"),
            [2] = Guid.Parse("40000000-0000-0000-0000-000000000002")
        };

        var original = RegisteredPostingPayloadFactory.Create(request, accounts);
        var retry = RegisteredPostingPayloadFactory.Create(request, accounts);

        retry.Should().Be(original);
    }
    [Fact]
    public void PayloadRejectsConflictingReversalEpochsForTheSameRoot()
    {
        var root = new SourceStampId(Guid.NewGuid());
        var allocation = new RegisteredPostingAllocation(
            1, new CreditLotId(Guid.NewGuid()), 100,
            [
                new RootTraceRange(root, 0, 50, 1),
                new RootTraceRange(root, 50, 50, 2)
            ]);
        var request = new RegisteredPostingRequest(Authority(), Posting(), [allocation]);

        var create = () => RegisteredPostingPayloadFactory.Create(
            request,
            new Dictionary<int, Guid> { [1] = Guid.NewGuid(), [2] = Guid.NewGuid() });

        create.Should().Throw<RegisteredPostingRejectedException>();
    }

    [Fact]
    public void PayloadOrdersExpectedReversalEpochsByRoot()
    {
        var firstRoot = new SourceStampId(Guid.Parse("21000000-0000-0000-0000-000000000001"));
        var secondRoot = new SourceStampId(Guid.Parse("21000000-0000-0000-0000-000000000002"));
        var request = new RegisteredPostingRequest(
            Authority(),
            Posting(),
            [
                new RegisteredPostingAllocation(1, CreditLotId.New(), 100,
                    [new RootTraceRange(secondRoot, 0, 100, 2)]),
                new RegisteredPostingAllocation(2, CreditLotId.New(), 100,
                    [new RootTraceRange(firstRoot, 0, 100, 1)])
            ]);

        var payload = RegisteredPostingPayloadFactory.Create(
            request,
            new Dictionary<int, Guid> { [1] = Guid.NewGuid(), [2] = Guid.NewGuid() });
        using var epochs = JsonDocument.Parse(payload.ExpectedReversalEpochs);

        epochs.RootElement[0].GetProperty("root_source_stamp_id").GetGuid().Should().Be(firstRoot.Value);
        epochs.RootElement[1].GetProperty("root_source_stamp_id").GetGuid().Should().Be(secondRoot.Value);
    }

    [Fact]
    public void ReceiptRejectsMissingWriterAcknowledgement()
    {
        var create = () => new RegisteredPostingReceipt(
            new PostingId(Guid.NewGuid()), 0, string.Empty, false);

        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AuthorityAndRequest_TrimAndExposeSecurityBindings()
    {
        var capability = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var risk = Guid.NewGuid();
        var authority = new RegisteredPostingAuthority(capability, actor, tenant, risk, " operation ", 2);
        var allocation = new RegisteredPostingAllocation(
            1, CreditLotId.New(), 100,
            [
                new RootTraceRange(new SourceStampId(Guid.Parse("30000000-0000-0000-0000-000000000002")), 100, 100, 1),
                new RootTraceRange(new SourceStampId(Guid.Parse("30000000-0000-0000-0000-000000000001")), 0, 100, 1)
            ]);
        var request = new RegisteredPostingRequest(authority, Posting(), [allocation], " snapshot ");

        authority.CapabilityId.Should().Be(capability);
        authority.ActorId.Should().Be(actor);
        authority.TenantId.Should().Be(tenant);
        authority.RiskDecisionId.Should().Be(risk);
        authority.RiskOperationFingerprint.Should().Be("operation");
        authority.ExpectedCounterVersion.Should().Be(2);
        allocation.LineSequence.Should().Be(1);
        allocation.AmountUnits.Should().Be(100);
        allocation.RootRanges.Select(range => range.Root.Value)
            .Should().BeInAscendingOrder();
        request.Authority.Should().Be(authority);
        request.Allocations.Should().ContainSingle().Which.Should().Be(allocation);
        request.DispatchSnapshotHash.Should().Be("snapshot");

        Action missingActor = () => new RegisteredPostingAuthority(capability, Guid.Empty, tenant, risk, "operation", 1);
        Action missingTenant = () => new RegisteredPostingAuthority(capability, actor, Guid.Empty, risk, "operation", 1);
        Action missingRisk = () => new RegisteredPostingAuthority(capability, actor, tenant, Guid.Empty, "operation", 1);
        Action blankFingerprint = () => new RegisteredPostingAuthority(capability, actor, tenant, risk, " ", 1);
        Action invalidCounter = () => new RegisteredPostingAuthority(capability, actor, tenant, risk, "operation", 0);
        missingActor.Should().Throw<ArgumentException>();
        missingTenant.Should().Throw<ArgumentException>();
        missingRisk.Should().Throw<ArgumentException>();
        blankFingerprint.Should().Throw<ArgumentException>();
        invalidCounter.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PayloadRejectsMissingOrEmptyProvisionedAccount()
    {
        var request = new RegisteredPostingRequest(Authority(), Posting());

        Action missing = () => RegisteredPostingPayloadFactory.Create(request, new Dictionary<int, Guid>());
        Action empty = () => RegisteredPostingPayloadFactory.Create(request, new Dictionary<int, Guid>
        {
            [1] = Guid.Empty,
            [2] = Guid.Empty
        });

        missing.Should().Throw<RegisteredPostingRejectedException>();
        empty.Should().Throw<RegisteredPostingRejectedException>();
    }
    private static RegisteredPostingAuthority Authority() => new(
        Guid.Parse("50000000-0000-0000-0000-000000000001"),
        Guid.Parse("50000000-0000-0000-0000-000000000002"),
        Guid.Parse("50000000-0000-0000-0000-000000000003"),
        Guid.Parse("50000000-0000-0000-0000-000000000004"),
        "spend:soft:100",
        1);

    private static PostingRequest Posting() => new(
        new PostingId(Guid.Parse("60000000-0000-0000-0000-000000000001")),
        new PostingTemplate(PostingTemplateKind.Spend, PostingTemplate.CurrentVersion),
        new IdempotencyKey("registered-posting-test"),
        PostingAuthority.WalletOwner,
        new ReserveVersion(1),
        new PolicyVersion(1),
        null,
        new DateTimeOffset(2026, 8, 9, 0, 0, 0, TimeSpan.Zero),
        [
            new PostingLine(1, EntrySide.Debit, EconomyAccountCode.SoftCoinLiability,
                new CoinAmount(CurrencyCode.SoftCoin, 100),
                new WalletId(Guid.Parse("70000000-0000-0000-0000-000000000001")), null, null),
            new PostingLine(2, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability,
                new CoinAmount(CurrencyCode.SoftCoin, 100),
                new WalletId(Guid.Parse("70000000-0000-0000-0000-000000000002")), null, null)
        ]);
}
