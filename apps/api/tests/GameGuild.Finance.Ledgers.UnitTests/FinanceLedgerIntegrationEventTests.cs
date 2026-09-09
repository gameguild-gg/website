using GameGuild.Finance.Contracts;
using GameGuild.Finance.Ledgers.Abstractions;
using GameGuild.Finance.Ledgers.Entities;
using GameGuild.Finance.Ledgers.Enums;
using GameGuild.Finance.Ledgers.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Finance.Ledgers.UnitTests;

public sealed class FinanceLedgerIntegrationEventTests
{
    [Fact]
    public async Task PostEntry_QueuesVersionedDurableEventWithOperationCorrelation()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var ledger = Ledger.CreateRoot(tenantId, "OPERATING", "Operating", "USD", actorId);
        var ledgers = new Mock<ILedgerRepository>();
        ledgers.Setup(repository => repository.GetByIdAsync(ledger.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ledger);
        var closures = new Mock<ILedgerClosureRepository>();
        closures.Setup(repository => repository.GetAncestorIdsAsync(ledger.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ledger.Id]);
        var entries = new Mock<ILedgerEntryRepository>();
        entries.Setup(repository => repository.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var operationAccessor = new UseCaseOperationContextAccessor();
        using var operation = operationAccessor.Begin(new UseCaseOperationContext(
            "finance.ledgers.post-entry",
            "PostEntryCommand",
            tenantId,
            actorId,
            correlationId,
            null));
        var service = new LedgerEntryService(
            ledgers.Object,
            closures.Object,
            entries.Object,
            NullLogger<LedgerEntryService>.Instance,
            operationAccessor);

        var posted = await service.PostEntryAsync(
            ledger.Id,
            EntryType.Debit,
            EntryCategory.Expense,
            12.50m,
            new DateOnly(2026, 9, 10),
            "Cloud operation",
            actorId,
            "economy:test");

        var durableEvent = Assert.IsType<FinanceLedgerEntryPostedEventV1>(
            Assert.Single(((IHasIntegrationEvents)posted).IntegrationEvents));
        Assert.Equal(posted.Id, durableEvent.EntryId);
        Assert.Equal(ledger.Id, durableEvent.LedgerId);
        Assert.Equal(tenantId, durableEvent.TenantId);
        Assert.Equal(actorId, durableEvent.ActorId);
        Assert.Equal(correlationId, durableEvent.CorrelationId);
        Assert.Equal("finance.ledger-entry.posted.v1", durableEvent.EventName);
    }
}
