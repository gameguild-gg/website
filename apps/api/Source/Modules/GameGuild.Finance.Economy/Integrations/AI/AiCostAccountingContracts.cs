using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Integrations.AI;

public enum AiProvider
{
    OpenAi = 0,
    Anthropic = 1,
    Google = 2
}

public enum AiServiceChargeStatus
{
    Reserved = 1,
    Completed = 2,
    Released = 3,
    Compensated = 4
}

public sealed class AiFundingFragment
{
    public AiFundingFragment(CreditLot parentLot, FragmentSelection selection)
    {
        ArgumentNullException.ThrowIfNull(parentLot);
        ArgumentNullException.ThrowIfNull(selection);
        if (parentLot.Id != selection.ParentLotId ||
            parentLot.Amount.Currency != selection.Amount.Currency ||
            parentLot.TraceUnitsPerCoinUnit != selection.TraceUnitsPerCoinUnit)
            throw new ArgumentException("Funding selection is not bound to its parent lot.", nameof(selection));

        ParentLot = parentLot;
        Selection = selection;
    }

    public CreditLot ParentLot { get; }
    public FragmentSelection Selection { get; }
    public CoinAmount Amount => Selection.Amount;
    public IReadOnlyList<RootTraceRange> SelectedRanges => Selection.SelectedRanges;
}

public sealed record AiCostRiskApproval(
    RiskDecisionSnapshot Decision,
    ProtectedOperationContext Context,
    EntityRiskCluster EntityCluster,
    IReadOnlyList<AggregateRiskLimit> Limits,
    Guid CounterReservationId);

public sealed record AuthorizeAiServiceCommand(
    Guid RequestId,
    Guid TenantId,
    Guid ActorId,
    WalletId SourceWalletId,
    WalletId ServiceWalletId,
    string ServiceCode,
    AiProvider Provider,
    string Model,
    IReadOnlyList<CreditLot> AvailableFundingLots,
    AiCostRiskApproval Risk,
    DateTimeOffset AuthorizedAt,
    IdempotencyKey IdempotencyKey);

public sealed record CompleteAiServiceCommand(
    Guid AuthorizationId,
    string ProviderUsageId,
    AiProvider Provider,
    string Model,
    int InputTokens,
    int OutputTokens,
    int TotalTokens,
    DateTimeOffset CompletedAt,
    IdempotencyKey IdempotencyKey);

public sealed record FailAiServiceCommand(
    Guid AuthorizationId,
    string Reason,
    DateTimeOffset FailedAt,
    IdempotencyKey IdempotencyKey);

public sealed record AiSoftChargeReservationRequest(
    Guid AuthorizationId,
    CoinAmount Amount,
    IReadOnlyList<AiFundingFragment> FundingFragments,
    DateTimeOffset ReservedAt);

public sealed record AiSoftChargeReservation(
    Guid Id,
    Guid AuthorizationId,
    CoinAmount Amount,
    IReadOnlyList<AiFundingFragment> FundingFragments,
    DateTimeOffset ReservedAt);

public interface IAiSoftChargeGateway
{
    AiSoftChargeReservation Reserve(AiSoftChargeReservationRequest request);
    void Finalize(AiSoftChargeReservation reservation, DateTimeOffset finalizedAt);
    void Release(AiSoftChargeReservation reservation, string reason, DateTimeOffset releasedAt);
    void Compensate(AiSoftChargeReservation reservation, DateTimeOffset compensatedAt);
}

public sealed class AiServiceAuthorization
{
    internal AiServiceAuthorization(
        Guid id,
        AuthorizeAiServiceCommand command,
        AiServicePriceSnapshot price,
        IReadOnlyList<AiFundingFragment> fundingFragments,
        AiSoftChargeReservation reservation)
    {
        Id = id;
        RequestId = command.RequestId;
        TenantId = command.TenantId;
        ActorId = command.ActorId;
        SourceWalletId = command.SourceWalletId;
        ServiceWalletId = command.ServiceWalletId;
        ServiceCode = command.ServiceCode.Trim();
        Provider = command.Provider;
        Model = command.Model.Trim();
        Price = price;
        FundingFragments = Array.AsReadOnly(fundingFragments.ToArray());
        Reservation = reservation;
        AuthorizedAt = command.AuthorizedAt;
        Status = AiServiceChargeStatus.Reserved;
    }

    public Guid Id { get; }
    public Guid RequestId { get; }
    public Guid TenantId { get; }
    public Guid ActorId { get; }
    public WalletId SourceWalletId { get; }
    public WalletId ServiceWalletId { get; }
    public string ServiceCode { get; }
    public AiProvider Provider { get; }
    public string Model { get; }
    public AiServicePriceSnapshot Price { get; }
    public IReadOnlyList<AiFundingFragment> FundingFragments { get; }
    public AiSoftChargeReservation Reservation { get; }
    public DateTimeOffset AuthorizedAt { get; }
    public AiServiceChargeStatus Status { get; private set; }

    internal void ChangeStatus(AiServiceChargeStatus status) => Status = status;
}

public sealed record AiProviderCostFact(
    Guid Id,
    Guid AuthorizationId,
    Guid RequestId,
    Guid TenantId,
    Guid ActorId,
    string ServiceCode,
    AiProvider Provider,
    string Model,
    string ProviderUsageId,
    int InputTokens,
    int OutputTokens,
    int TotalTokens,
    long InputCostUsdNanos,
    long OutputCostUsdNanos,
    long ExactProviderCostUsdNanos,
    long ChargedSoftUnits,
    string RateCardVersion,
    DateTimeOffset CompletedAt);

public interface IAiProviderCostFactStore
{
    void Save(AiProviderCostFact fact);
}

public sealed record AiTreasuryServiceCostFact(
    Guid ProviderCostFactId,
    string ServiceCode,
    long CurrentServicePriceSoftUnits,
    long CurrentProviderCostUsdNanos,
    long TrailingHighPercentileCostUsdNanos,
    long ProviderFxStressCostUsdNanos,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt)
{
    public ReserveServiceObservation ToReserveObservation(long reservedSoftUnits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(reservedSoftUnits);
        return new ReserveServiceObservation(
            ServiceCode,
            CurrentServicePriceSoftUnits,
            CurrentProviderCostUsdNanos,
            TrailingHighPercentileCostUsdNanos,
            ProviderFxStressCostUsdNanos,
            reservedSoftUnits,
            true,
            ObservedAt,
            ExpiresAt);
    }
}

public interface IAiTreasuryCostPublisher
{
    void Publish(AiTreasuryServiceCostFact fact);
}

public sealed class AiCostAccountingIdempotencyException(string message) : InvalidOperationException(message);
public sealed class AiCostRiskExposureException(string message) : InvalidOperationException(message);
public sealed class AiProviderUsageReplayException(string message) : InvalidOperationException(message);
public sealed class AiProviderUsageExceededReservationException(string message) : InvalidOperationException(message);
public sealed class AiChargeCompensationException(string message, Exception innerException) : InvalidOperationException(message, innerException);
