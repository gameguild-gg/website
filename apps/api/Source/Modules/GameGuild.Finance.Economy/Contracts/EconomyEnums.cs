namespace GameGuild.Finance.Economy.Contracts;

public enum CurrencyCode
{
    HardCoin = 1,
    SoftCoin = 2
}

public enum ProvenanceKind
{
    PurchasedHard = 1,
    EarnedHard = 2,
    ConvertedSoft = 3,
    AdRewardSoft = 4,
    SystemGrantSoft = 5,
    RefundRestoration = 6,
    EscrowReturn = 7,
    MarketplaceSoft = 8
}

public enum SourceConfirmationState
{
    Observed = 1,
    Confirmed = 2,
    Failed = 3,
    Expired = 4,
    Disputed = 5,
    Reversed = 6
}

public enum HoldReason
{
    RiskReview = 1,
    Dispute = 2,
    RefundWindow = 3,
    Compliance = 4,
    PayoutReservation = 5
}

public enum HoldStatus
{
    Active = 1,
    Released = 2,
    Consumed = 3
}

public enum EntrySide
{
    Debit = 1,
    Credit = 2
}

public enum EconomyAccountCode
{
    ExternalClearingHard = 1,
    PurchasedHardLiability = 2,
    EarnedHardLiability = 3,
    SoftCoinLiability = 4,
    HardCoinReserve = 5,
    SoftCoinReserve = 6,
    PlatformHardTreasury = 7,
    PlatformSoftTreasury = 8,
    HardCoinEscrow = 9,
    SoftCoinEscrow = 10,
    PayoutPayableHard = 11,
    AdminWithdrawalPayableHard = 12,
    RecoveryReceivableHard = 13,
    FeeRevenueHard = 14,
    ProviderLossHard = 15,
    RecoveryReceivableSoft = 16
}

public enum PostingAuthority
{
    ProviderConfirmation = 1,
    WalletOwner = 2,
    PlatformSystem = 3,
    EscrowCoordinator = 4,
    PayoutCoordinator = 5,
    Administrator = 6,
    MarketplaceCoordinator = 7
}

public enum PostingTemplateKind
{
    ConfirmedTopUpMint = 1,
    ProviderReversalFull = 2,
    ProviderReversalPartial = 3,
    Spend = 4,
    HardToSoftConversion = 5,
    SystemBackedGrant = 6,
    Burn = 7,
    Escrow = 8,
    Reclaim = 9,
    Refund = 10,
    PayoutReservation = 11,
    PayoutSuccess = 12,
    PayoutFailure = 13,
    AdminWithdrawalReservation = 14,
    AdminWithdrawalSuccess = 15,
    AdminWithdrawalFailure = 16,
    HardToSoftConversionFee = 17,
    ProviderConvertedSoftReversal = 18,
    ProviderReversalDebt = 19,
    ProviderReversalLoss = 20,
    AdRewardIssuance = 21,
    BountyEscrow = 22,
    BountyClaim = 23,
    BountyReclaim = 24,
    MarketplaceSettlement = 25,
    MarketplaceRefund = 26
}

public enum PostingStatus
{
    Accepted = 1,
    Rejected = 2,
    Duplicate = 3
}

public enum WalletLifecycleState
{
    Active = 1,
    Frozen = 2,
    Closed = 3,
    UnderReview = 4
}
