using GameGuild.Finance.Contracts;
using Xunit;

namespace GameGuild.Finance.Contracts.UnitTests;

public sealed class FinanceIntegrationEventTests
{
    [Fact]
    public void EconomyPostingAccepted_IsVersionedAndContainsOnlyAccountingIdentifiers()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var postingId = Guid.NewGuid();
        var line = new FinancePostingLineV1(1, "Debit", "UserLiability", "SOFT", 400, Guid.NewGuid());

        IDurableIntegrationEvent integrationEvent = new EconomyPostingAcceptedEventV1(
            postingId,
            "sha256:posting",
            [line])
        {
            TenantId = tenantId,
            ActorId = actorId,
            AggregateType = "EconomyPosting",
            AggregateId = postingId.ToString(),
            CorrelationId = Guid.NewGuid()
        };

        Assert.Equal("finance.economy.posting-accepted.v1", integrationEvent.EventName);
        Assert.Equal("Finance.Economy", integrationEvent.SourceModule);
        Assert.Equal(1, integrationEvent.SchemaVersion);
        Assert.NotEqual(Guid.Empty, integrationEvent.EventId);
        Assert.Equal(tenantId, integrationEvent.TenantId);
        Assert.Equal(actorId, integrationEvent.ActorId);
    }

    [Fact]
    public void LedgerEntryPosted_IsVersionedAndNamesTheLedgerBoundedContext()
    {
        var ledgerId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        IDurableIntegrationEvent integrationEvent = new FinanceLedgerEntryPostedEventV1(
            ledgerId,
            entryId,
            "TXN-20260909-0001",
            "USD",
            12.50m,
            "Debit",
            new DateOnly(2026, 9, 9))
        {
            TenantId = Guid.NewGuid(),
            ActorId = Guid.NewGuid(),
            AggregateType = "LedgerEntry",
            AggregateId = entryId.ToString(),
            CorrelationId = Guid.NewGuid()
        };

        Assert.Equal("finance.ledger-entry.posted.v1", integrationEvent.EventName);
        Assert.Equal("Finance.Ledgers", integrationEvent.SourceModule);
        Assert.Equal(1, integrationEvent.SchemaVersion);
    }
}
