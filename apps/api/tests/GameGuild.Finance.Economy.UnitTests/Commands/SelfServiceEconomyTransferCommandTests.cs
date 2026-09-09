using FluentAssertions;
using GameGuild.Finance.Economy.Commands;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Transfers;

namespace GameGuild.Finance.Economy.UnitTests.Commands;

public sealed class SelfServiceEconomyTransferCommandTests
{
    [Fact]
    public async Task Handler_ForwardsOnlyTheBusinessIntentToTheTransferService()
    {
        var request = Request();
        var receipt = new SelfServiceEconomyTransferReceipt(
            Guid.NewGuid(), request.TransferType, request.Currency, request.AmountUnits,
            request.RecipientUserId, 11, "journal-hash", false);
        var service = new CapturingService(receipt);
        var handler = new CreateMyEconomyTransferCommandHandler(service);

        var result = await handler.Handle(new CreateMyEconomyTransferCommand(request), CancellationToken.None);

        result.Should().Be(receipt);
        service.Request.Should().Be(request);
    }

    [Fact]
    public void PublicRequest_DoesNotExposeServerAuthorityFields()
    {
        var properties = typeof(SelfServiceEconomyTransferRequest).GetProperties()
            .Select(property => property.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        properties.Should().BeEquivalentTo(
            "RecipientUserId", "TransferType", "Currency", "AmountUnits", "IdempotencyKey");
        properties.Should().NotContain(new[]
        {
            "TenantId", "SourceWalletId", "DestinationWalletId", "RiskDecisionId",
            "OperationFingerprint", "JurisdictionCode", "PolicyVersion", "ReserveVersion",
            "ProviderHash", "DestinationHash", "Receipt"
        });
    }

    [Theory]
    [InlineData(false, SelfServiceEconomyTransferType.Tip, CurrencyCode.HardCoin, 1, "key")]
    [InlineData(true, SelfServiceEconomyTransferType.Tip, CurrencyCode.HardCoin, 1, "key")]
    [InlineData(true, (SelfServiceEconomyTransferType)0, CurrencyCode.HardCoin, 1, "key")]
    [InlineData(true, SelfServiceEconomyTransferType.Tip, (CurrencyCode)0, 1, "key")]
    [InlineData(true, SelfServiceEconomyTransferType.Tip, CurrencyCode.HardCoin, 0, "key")]
    [InlineData(true, SelfServiceEconomyTransferType.Tip, CurrencyCode.HardCoin, 1, "")]
    public void Validator_EnforcesTheClosedBusinessIntent(
        bool invalid,
        SelfServiceEconomyTransferType transferType,
        CurrencyCode currency,
        long units,
        string key)
    {
        var recipient = invalid && transferType == SelfServiceEconomyTransferType.Tip &&
                        currency == CurrencyCode.HardCoin && units == 1 && key == "key"
            ? Guid.Empty
            : Guid.NewGuid();
        var command = new CreateMyEconomyTransferCommand(
            new SelfServiceEconomyTransferRequest(recipient, transferType, currency, units, key));

        var result = new CreateMyEconomyTransferCommandValidator().Validate(command);

        result.IsValid.Should().Be(!invalid);
    }

    private static SelfServiceEconomyTransferRequest Request() => new(
        Guid.Parse("a3000000-0000-0000-0000-000000000001"),
        SelfServiceEconomyTransferType.Gift,
        CurrencyCode.SoftCoin,
        19,
        "transfer-command-key");

    private sealed class CapturingService(SelfServiceEconomyTransferReceipt receipt)
        : ISelfServiceEconomyTransferService
    {
        public SelfServiceEconomyTransferRequest? Request { get; private set; }

        public Task<SelfServiceEconomyTransferReceipt> TransferAsync(
            SelfServiceEconomyTransferRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(receipt);
        }
    }
}
