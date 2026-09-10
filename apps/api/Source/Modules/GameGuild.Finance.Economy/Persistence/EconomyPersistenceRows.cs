using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Policy;
using GameGuild.Finance.Economy.Projections;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Finance.Economy.Transfers;

namespace GameGuild.Finance.Economy.Persistence;

internal sealed class EconomyWalletRow
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid TenantId { get; set; }
    public WalletLifecycleState State { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class EconomyWalletBalanceProjectionRow
{
    public Guid WalletId { get; set; }
    public long PendingHard { get; set; }
    public long PendingSoft { get; set; }
    public long PurchasedHard { get; set; }
    public long EarnedHard { get; set; }
    public long RestrictedHard { get; set; }
    public long Soft { get; set; }
    public long ImmatureEarnedHard { get; set; }
    public long HeldHard { get; set; }
    public long HeldSoft { get; set; }
    public long AvailableHardToSpend { get; set; }
    public long AvailableSoftToSpend { get; set; }
    public long WithdrawableHard { get; set; }
    public WalletReviewState ReviewState { get; set; }
    public long SourceJournalSequence { get; set; }
    public string ProjectionHash { get; set; } = string.Empty;
    public DateTimeOffset RebuiltAt { get; set; }
}

internal sealed class EconomyProjectionReconciliationEventRow
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public string PreviousHash { get; set; } = string.Empty;
    public string RebuiltHash { get; set; } = string.Empty;
    public long SourceJournalSequence { get; set; }
    public DateTimeOffset DetectedAt { get; set; }
}

internal sealed class EconomyAccountRow
{
    public Guid Id { get; set; }
    public Guid? WalletId { get; set; }
    public EconomyAccountCode Code { get; set; }
    public CurrencyCode Currency { get; set; }
    public ProvenanceKind? Provenance { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class EconomySourceStampRow
{
    public Guid Id { get; set; }
    public string SourceKind { get; set; } = string.Empty;
    public string InternalSourceId { get; set; } = string.Empty;
    public string SourceLegId { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? ProviderReference { get; set; }
    public string EvidenceHash { get; set; } = string.Empty;
    public ProvenanceKind Provenance { get; set; }
    public SourceConfirmationState State { get; set; }
    public Guid ActorId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? PostingReferenceId { get; set; }
    public long PolicyVersion { get; set; }
    public long AuthoritativeUnits { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
}

internal sealed class EconomySourceStampEventRow
{
    public Guid Id { get; set; }
    public Guid SourceStampId { get; set; }
    public long Sequence { get; set; }
    public SourceConfirmationState State { get; set; }
    public string EvidenceHash { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}

internal sealed class EconomyFundingClaimRow
{
    public Guid SourceStampId { get; set; }
    public Guid WalletId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string ConnectedAccount { get; set; } = string.Empty;
    public string ProviderObject { get; set; } = string.Empty;
    public string ProviderMonetaryLeg { get; set; } = string.Empty;
    public long AuthoritativeUsdMinorUnits { get; set; }
    public SourceConfirmationState State { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset StateChangedAt { get; set; }
    public Guid? PostingGroupId { get; set; }
    public Guid? RootCreditLotId { get; set; }
    public long CumulativeProviderReversalUnits { get; set; }
    public long Version { get; set; }
}

internal sealed class EconomyTopUpIntentRow
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public Guid WalletId { get; set; }
    public long HardCoinUnits { get; set; }
    public long UsdMinorUnits { get; set; }
    public string JurisdictionCode { get; set; } = string.Empty;
    public long PolicyVersion { get; set; }
    public string PolicyHash { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string? ProviderEnvironment { get; set; }
    public string? ProviderAccountId { get; set; }
    public string? ProviderObjectId { get; set; }
    public string? ProviderObjectType { get; set; }
    public string? ProviderMonetaryLeg { get; set; }
    public EconomyTopUpProviderStatus Status { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? ProviderBoundAt { get; set; }
    public string? LastProviderEventId { get; set; }
    public DateTimeOffset? LastProviderEventAt { get; set; }
    public string? LastProviderEvidenceHash { get; set; }
    public Guid? PostingGroupId { get; set; }
    public string? FailureCode { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; }
}

internal sealed class EconomyProviderDisputeRow
{
    public string ProviderDisputeReference { get; set; } = string.Empty;
    public Guid SourceStampId { get; set; }
    public Guid ResponsibleWalletId { get; set; }
    public ProviderDisputeStatus Status { get; set; }
    public long LatestProviderSequence { get; set; }
    public long CumulativeDisputedHardUnits { get; set; }
    public long BaselineReversedHardUnits { get; set; }
    public long FrozenHardEquivalentUnits { get; set; }
    public string? ReversalIdempotencyKey { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; }
}

internal sealed class EconomyProviderDisputeEventRow
{
    public string ProviderEventId { get; set; } = string.Empty;
    public string ProviderDisputeReference { get; set; } = string.Empty;
    public Guid SourceStampId { get; set; }
    public long ProviderSequence { get; set; }
    public ProviderDisputeStatus Status { get; set; }
    public long CumulativeDisputedHardUnits { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}

internal sealed class EconomyDisputeFragmentFreezeRow
{
    public Guid Id { get; set; }
    public string ProviderDisputeReference { get; set; } = string.Empty;
    public Guid RootSourceStampId { get; set; }
    public Guid CreditLotId { get; set; }
    public Guid WalletId { get; set; }
    public CurrencyCode Currency { get; set; }
    public long AmountUnits { get; set; }
    public HoldStatus Status { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
    public DateTimeOffset? TerminalAt { get; set; }
}

internal sealed class EconomyDisputeFragmentRangeRow
{
    public Guid Id { get; set; }
    public Guid DisputeFragmentFreezeId { get; set; }
    public long StartInclusive { get; set; }
    public long EndExclusive { get; set; }
    public long ReversalEpoch { get; set; }
}

internal sealed class EconomyWalletDebtRow
{
    public Guid WalletId { get; set; }
    public long OutstandingHardUnits { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; }
}

internal sealed class EconomyWalletDebtEventRow
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public Guid SourceStampId { get; set; }
    public long Sequence { get; set; }
    public long DeltaHardUnits { get; set; }
    public long OutstandingHardUnits { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

internal sealed class EconomyPostingGroupRow
{
    public Guid Id { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public PostingTemplateKind TemplateKind { get; set; }
    public int TemplateVersion { get; set; }
    public PostingAuthority Authority { get; set; }
    public PostingStatus Status { get; set; }
    public Guid CapabilityId { get; set; }
    public Guid ActorId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? RiskDecisionId { get; set; }
    public long PolicyVersion { get; set; }
    public long ReserveVersion { get; set; }
    public long ReserveAuthorizationEpoch { get; set; }
    public Guid? SourceStampId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

internal sealed class EconomyJournalEntryRow
{
    public Guid Id { get; set; }
    public Guid PostingGroupId { get; set; }
    public long Sequence { get; set; }
    public string PreviousHash { get; set; } = string.Empty;
    public string? CanonicalPayloadHash { get; set; }
    public int HashAlgorithmVersion { get; set; }
    public string Hash { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
}

internal sealed class EconomyJournalLineRow
{
    public Guid Id { get; set; }
    public Guid JournalEntryId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? WalletId { get; set; }
    public Guid? CreditLotId { get; set; }
    public int Sequence { get; set; }
    public EntrySide Side { get; set; }
    public CurrencyCode Currency { get; set; }
    public long AmountUnits { get; set; }
    public ProvenanceKind? Provenance { get; set; }
}

internal sealed class EconomyCreditLotRow
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public Guid RootSourceStampId { get; set; }
    public CurrencyCode Currency { get; set; }
    public long AmountUnits { get; set; }
    public ProvenanceKind Provenance { get; set; }
    public DateTimeOffset CreditedAt { get; set; }
    public DateTimeOffset ConfirmedAt { get; set; }
    public DateTimeOffset OriginalMaturesAt { get; set; }
    public bool CashOutEligible { get; set; }
    public long JournalSequence { get; set; }
    public CreditLotState State { get; set; }
    public long ReversalEpoch { get; set; }
}

internal sealed class EconomyEntryAllocationRow
{
    public Guid Id { get; set; }
    public Guid JournalLineId { get; set; }
    public Guid ParentLotId { get; set; }
    public long AmountUnits { get; set; }
}

internal sealed class EconomyLotLineageEdgeRow
{
    public Guid Id { get; set; }
    public Guid ParentLotId { get; set; }
    public Guid ChildLotId { get; set; }
    public CurrencyCode Currency { get; set; }
    public long AmountUnits { get; set; }
}

internal sealed class EconomyFragmentRootRangeRow
{
    public Guid Id { get; set; }
    public Guid RootSourceStampId { get; set; }
    public Guid? CreditLotId { get; set; }
    public Guid? EntryAllocationId { get; set; }
    public long StartInclusive { get; set; }
    public long EndExclusive { get; set; }
    public long ReversalEpoch { get; set; }
}

internal sealed class EconomyRootReversalStateRow
{
    public Guid RootSourceStampId { get; set; }
    public long Epoch { get; set; }
    public long CumulativeProviderUnits { get; set; }
    public long ReversedUnits { get; set; }
    public string State { get; set; } = string.Empty;
    public string TargetedRanges { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class EconomyProviderFactAllocationRow
{
    public Guid Id { get; set; }
    public Guid SourceStampId { get; set; }
    public Guid JournalLineId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string ConnectedAccount { get; set; } = string.Empty;
    public string ProviderObject { get; set; } = string.Empty;
    public string ProviderMonetaryLeg { get; set; } = string.Empty;
    public CurrencyCode Currency { get; set; }
    public long AllocatedUnits { get; set; }
    public long CumulativeCreditedUnits { get; set; }
    public long AuthoritativeUnits { get; set; }
}

internal sealed class EconomyDispatchSnapshotRow
{
    public Guid Id { get; set; }
    public Guid PostingGroupId { get; set; }
    public string SnapshotHash { get; set; } = string.Empty;
    public CurrencyCode Currency { get; set; }
    public long AmountUnits { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string EligibilityPayload { get; set; } = string.Empty;
    public long ChainSequence { get; set; }
    public string ChainHash { get; set; } = string.Empty;
    public long ReserveVersion { get; set; }
    public long ReserveAuthorizationEpoch { get; set; }
    public long KillSwitchEpoch { get; set; }
    public long FencingToken { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class EconomyOutboxMessageRow
{
    public Guid Id { get; set; }
    public Guid PostingGroupId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}

internal sealed class EconomyIdempotencyRecordRow
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public Guid PostingGroupId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class EconomyChainHeadRow
{
    public short Id { get; set; }
    public long Sequence { get; set; }
    public string Hash { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class EconomyExternalAnchorRow
{
    public Guid Id { get; set; }
    public long JournalSequence { get; set; }
    public string JournalHash { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string WormReference { get; set; } = string.Empty;
    public string? DispatchSnapshotHash { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderReference { get; set; } = string.Empty;
    public DateTimeOffset AnchoredAt { get; set; }
}

internal sealed class EconomyReserveHeadRow
{
    public long Version { get; set; }
    public bool IsActive { get; set; }
    public long PolicyVersion { get; set; }
    public long AuthorizationEpoch { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public long HardFaceValueUsdMinor { get; set; }
    public long RequiredHardReserveUsdMinor { get; set; }
    public long SoftFaceValueUsdNanos { get; set; }
    public long StressedExpectedRedemptionCostUsdNanos { get; set; }
    public long RequiredSoftReserveUsdNanos { get; set; }
    public long HardBackingUsdNanos { get; set; }
    public long SoftBackingUsdNanos { get; set; }
    public ReserveCoverageState Coverage { get; set; }
    public string EvidenceHash { get; set; } = string.Empty;
    public DateTimeOffset ActivatedAt { get; set; }
}

internal sealed class EconomyReserveAssetAllocationRow
{
    public Guid Id { get; set; }
    public long ReserveVersion { get; set; }
    public string AssetKey { get; set; } = string.Empty;
    public ReserveBackingPurpose Purpose { get; set; }
    public long EligibleUsdNanos { get; set; }
}

internal sealed class EconomyRiskDecisionRow
{
    public Guid Id { get; set; }
    public RiskOutcome Outcome { get; set; }
    public string OperationFingerprint { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public string ActorHash { get; set; } = string.Empty;
    public PostingTemplateKind TemplateKind { get; set; }
    public Guid SourceWalletId { get; set; }
    public Guid DestinationWalletId { get; set; }
    public CurrencyCode Currency { get; set; }
    public long AmountUnits { get; set; }
    public string CurrencyLegs { get; set; } = string.Empty;
    public string SourceRoots { get; set; } = string.Empty;
    public string ProviderReferenceHash { get; set; } = string.Empty;
    public long PolicyVersion { get; set; }
    public long ReserveVersion { get; set; }
    public long ReserveAuthorizationEpoch { get; set; }
    public long FeatureVersion { get; set; }
    public long KillSwitchEpoch { get; set; }
    public long CounterVersion { get; set; }
    public long EntityGraphVersion { get; set; }
    public string EntityGraphEvidenceHash { get; set; } = string.Empty;
    public string ReasonCodes { get; set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

internal sealed class EconomyRegisteredCapabilityRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AllowedTemplateKinds { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

internal sealed class EconomyRiskDecisionConsumptionRow
{
    public Guid Id { get; set; }
    public Guid RiskDecisionId { get; set; }
    public Guid PostingGroupId { get; set; }
    public string OperationFingerprint { get; set; } = string.Empty;
    public DateTimeOffset ConsumedAt { get; set; }
}

internal sealed class EconomyRiskCounterRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public RiskLimitDimension Dimension { get; set; }
    public string SubjectHash { get; set; } = string.Empty;
    public PostingTemplateKind Operation { get; set; }
    public CurrencyCode Currency { get; set; }
    public DateTimeOffset WindowStartedAt { get; set; }
    public DateTimeOffset WindowEndsAt { get; set; }
    public long CounterVersion { get; set; }
    public long MaxUnits { get; set; }
    public long UsedUnits { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class EconomyRiskCounterReservationRow
{
    public Guid Id { get; set; }
    public Guid ReservationGroupId { get; set; }
    public Guid RiskDecisionId { get; set; }
    public Guid RiskCounterId { get; set; }
    public string InputFingerprint { get; set; } = string.Empty;
    public long AmountUnits { get; set; }
    public DateTimeOffset ReservedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public RiskCounterReservationStatus Status { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
}

internal sealed class EconomySelfServiceTransferIntentRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public Guid RecipientUserId { get; set; }
    public SelfServiceEconomyTransferType TransferType { get; set; }
    public CurrencyCode Currency { get; set; }
    public ProvenanceKind Provenance { get; set; }
    public long AmountUnits { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ProviderReferenceHash { get; set; } = string.Empty;
    public string DestinationHash { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
}

internal sealed class EconomyProtectedChangeCooldownRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SubjectId { get; set; }
    public ProtectedChangeKind Kind { get; set; }
    public string ValueHash { get; set; } = string.Empty;
    public long Version { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public DateTimeOffset AvailableAt { get; set; }
}

internal sealed class EconomyHoldRow
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public CurrencyCode Currency { get; set; }
    public long AmountUnits { get; set; }
    public HoldReason Reason { get; set; }
    public HoldStatus Status { get; set; }
    public DateTimeOffset EffectiveAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
}

internal sealed class EconomyHoldEventRow
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public long Sequence { get; set; }
    public HoldEventKind Kind { get; set; }
    public Guid ActorId { get; set; }
    public string EvidenceHash { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}

internal sealed class EconomyRiskReviewCaseRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RiskDecisionId { get; set; }
    public Guid SubmittedBy { get; set; }
    public RiskReviewStatus Status { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
    public string? Resolution { get; set; }
    public int RequiredApprovals { get; set; }
    public Guid? AppealOf { get; set; }
}

internal sealed class EconomyRiskReviewEventRow
{
    public Guid Id { get; set; }
    public Guid RiskReviewCaseId { get; set; }
    public long Sequence { get; set; }
    public RiskReviewEventKind Kind { get; set; }
    public Guid ActorId { get; set; }
    public string EvidenceHashes { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public RiskManualDecisionCode? DecisionCode { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

internal sealed class EconomyRiskAuditEvidenceRow
{
    public Guid Id { get; set; }
    public Guid RiskDecisionId { get; set; }
    public string EventKind { get; set; } = string.Empty;
    public string OperationFingerprint { get; set; } = string.Empty;
    public string EvidenceHash { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
}

internal sealed class EconomyCapabilityPolicyRow
{
    public Guid Id { get; set; }
    public string ScopeKey { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public EconomyValueMovementCapability Capability { get; set; }
    public string JurisdictionCode { get; set; } = string.Empty;
    public long Version { get; set; }
    public string CanonicalPayload { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public Guid ProposedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset ProposedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset EffectiveAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool ProviderReady { get; set; }
    public bool IsActive { get; set; }
}

internal sealed class EconomyCapabilityPolicyApprovalRow
{
    public Guid Id { get; set; }
    public Guid PolicyId { get; set; }
    public Guid ActorId { get; set; }
    public string ReauthenticationHash { get; set; } = string.Empty;
    public DateTimeOffset ApprovedAt { get; set; }
}

internal sealed class EconomyCapabilityReceiptRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public string SubjectReference { get; set; } = string.Empty;
    public string JurisdictionCode { get; set; } = string.Empty;
    public EconomyValueMovementCapability Capability { get; set; }
    public string OperationFingerprint { get; set; } = string.Empty;
    public long PolicyVersion { get; set; }
    public long ReserveVersion { get; set; }
    public Guid RiskDecisionId { get; set; }
    public long KillSwitchEpoch { get; set; }
    public string ProviderHash { get; set; } = string.Empty;
    public string DestinationHash { get; set; } = string.Empty;
    public string SourceRootHashes { get; set; } = string.Empty;
    public string EvidenceHashes { get; set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string ReceiptHash { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

internal sealed class EconomyCapabilityReceiptConsumptionRow
{
    public Guid Id { get; set; }
    public Guid ReceiptId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public string OperationFingerprint { get; set; } = string.Empty;
    public long KillSwitchEpoch { get; set; }
    public DateTimeOffset ConsumedAt { get; set; }
}

internal sealed class EconomyKillSwitchRow
{
    public Guid Id { get; set; }
    public string ScopeKey { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public EconomyValueMovementCapability? Capability { get; set; }
    public long Epoch { get; set; }
    public bool IsActive { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public Guid ActivatedBy { get; set; }
    public DateTimeOffset ActivatedAt { get; set; }
    public Guid? ReleaseProposedBy { get; set; }
    public string? ReleaseProposalReauthenticationHash { get; set; }
    public DateTimeOffset? ReleaseProposedAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
}

internal sealed class EconomyKillSwitchReleaseApprovalRow
{
    public Guid Id { get; set; }
    public Guid KillSwitchId { get; set; }
    public Guid ActorId { get; set; }
    public string ReauthenticationHash { get; set; } = string.Empty;
    public DateTimeOffset ApprovedAt { get; set; }
}

internal sealed class EconomyEntityGraphNodeRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public RiskEntityType Type { get; set; }
    public string IdentityHash { get; set; } = string.Empty;
    public long Version { get; set; }
    public string EvidenceHash { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset? SupersededAt { get; set; }
}

internal sealed class EconomyEntityGraphEdgeRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeftNodeId { get; set; }
    public Guid RightNodeId { get; set; }
    public string Relationship { get; set; } = string.Empty;
    public long Version { get; set; }
    public string EvidenceHash { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset? SupersededAt { get; set; }
}

internal sealed class EconomyComplianceEvidenceRow
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string ProviderEventId { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string SubjectHash { get; set; } = string.Empty;
    public string EvidenceKind { get; set; } = string.Empty;
    public string? JurisdictionCode { get; set; }
    public long Version { get; set; }
    public string Result { get; set; } = string.Empty;
    public long PolicyVersion { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public bool SignatureVerified { get; set; }
    public string RawObjectReference { get; set; } = string.Empty;
    public string EvidenceHash { get; set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}

internal sealed class EconomyComplianceInboxRow
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string ProviderEventId { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string RawObjectReference { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? ProcessingError { get; set; }
}

internal sealed class EconomyComplianceOutboxRow
{
    public Guid Id { get; set; }
    public Guid EvidenceId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
}

internal sealed class EconomyComplianceHoldRow
{
    public Guid Id { get; set; }
    public string ScopeKey { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string SubjectHash { get; set; } = string.Empty;
    public EconomyValueMovementCapability? Capability { get; set; }
    public string CaseReferenceHash { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public string EvidenceHash { get; set; } = string.Empty;
    public string IdempotencyKeyHash { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public Guid ActivatedBy { get; set; }
    public DateTimeOffset ActivatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid? ReleaseProposedBy { get; set; }
    public DateTimeOffset? ReleaseProposedAt { get; set; }
    public int? RequiredReleaseApprovals { get; set; }
    public string? ReleasePolicyEvidenceHash { get; set; }
    public Guid? ReleasedBy { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
}

internal sealed class EconomyComplianceHoldEventRow
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public int Sequence { get; set; }
    public string Kind { get; set; } = string.Empty;
    public Guid ActorId { get; set; }
    public string EvidenceHash { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}

internal sealed class EconomyCustodyObservationRow
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string AssetKey { get; set; } = string.Empty;
    public ReserveBackingPurpose Purpose { get; set; }
    public long Version { get; set; }
    public long EligibleUsdNanos { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

internal sealed class EconomyCustodyReconciliationRow
{
    public Guid Id { get; set; }
    public long ReserveVersion { get; set; }
    public string ObservationIds { get; set; } = string.Empty;
    public long LiabilityUsdNanos { get; set; }
    public long EligibleAssetUsdNanos { get; set; }
    public long VarianceUsdNanos { get; set; }
    public bool IsReconciled { get; set; }
    public string EvidenceHash { get; set; } = string.Empty;
    public Guid ReconciledBy { get; set; }
    public DateTimeOffset ReconciledAt { get; set; }
}

internal sealed class EconomyReserveProposalRow
{
    public Guid Id { get; set; }
    public long Version { get; set; }
    public long PolicyVersion { get; set; }
    public long? ExpectedActiveVersion { get; set; }
    public long AuthorizationEpoch { get; set; }
    public string SnapshotHash { get; set; } = string.Empty;
    public long LiabilityUsdNanos { get; set; }
    public long EligibleAssetUsdNanos { get; set; }
    public long HardFaceValueUsdMinor { get; set; }
    public long RequiredHardReserveUsdMinor { get; set; }
    public long SoftFaceValueUsdNanos { get; set; }
    public long StressedExpectedRedemptionCostUsdNanos { get; set; }
    public long RequiredSoftReserveUsdNanos { get; set; }
    public long HardBackingUsdNanos { get; set; }
    public long SoftBackingUsdNanos { get; set; }
    public ReserveCoverageState Coverage { get; set; }
    public string ObservationIds { get; set; } = string.Empty;
    public string AssetAllocations { get; set; } = string.Empty;
    public string EvidenceHash { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public Guid ProposedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public string? ApprovalReauthenticationHash { get; set; }
    public DateTimeOffset ProposedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

internal sealed class EconomyJournalVerificationCheckpointRow
{
    public Guid Id { get; set; }
    public long FromSequence { get; set; }
    public long ToSequence { get; set; }
    public string PreviousHash { get; set; } = string.Empty;
    public string CurrentHash { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string? FailureCode { get; set; }
    public long FencingToken { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
}

internal sealed class EconomyProjectionGenerationRow
{
    public Guid Id { get; set; }
    public long Generation { get; set; }
    public long FromSequence { get; set; }
    public long ToSequence { get; set; }
    public string ProjectionHash { get; set; } = string.Empty;
    public string JournalHash { get; set; } = string.Empty;
    public int MismatchCount { get; set; }
    public string State { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid ProposedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public Guid? SecondApprovedBy { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
}

internal sealed class EconomyWalletProjectionGenerationRow
{
    public long Generation { get; set; }
    public Guid WalletId { get; set; }
    public long PendingHard { get; set; }
    public long PendingSoft { get; set; }
    public long PurchasedHard { get; set; }
    public long EarnedHard { get; set; }
    public long RestrictedHard { get; set; }
    public long Soft { get; set; }
    public long ImmatureEarnedHard { get; set; }
    public long HeldHard { get; set; }
    public long HeldSoft { get; set; }
    public long AvailableHardToSpend { get; set; }
    public long AvailableSoftToSpend { get; set; }
    public long WithdrawableHard { get; set; }
    public long SourceJournalSequence { get; set; }
    public string ProjectionHash { get; set; } = string.Empty;
    public bool MatchesLive { get; set; }
    public DateTimeOffset RebuiltAt { get; set; }
}

internal sealed class EconomyProjectionGenerationApprovalRow
{
    public Guid Id { get; set; }
    public long Generation { get; set; }
    public Guid ActorId { get; set; }
    public string ReauthenticationHash { get; set; } = string.Empty;
    public DateTimeOffset ApprovedAt { get; set; }
}

internal sealed class EconomyAnchorVerificationRow
{
    public Guid Id { get; set; }
    public Guid ExternalAnchorId { get; set; }
    public string KeyId { get; set; } = string.Empty;
    public string ObjectVersion { get; set; } = string.Empty;
    public string ETag { get; set; } = string.Empty;
    public DateTimeOffset RetainUntil { get; set; }
    public string ObjectHash { get; set; } = string.Empty;
    public bool SignatureValid { get; set; }
    public bool ObjectMatches { get; set; }
    public DateTimeOffset VerifiedAt { get; set; }
}

internal sealed class EconomyWorkerLeaseRow
{
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public long FencingToken { get; set; }
    public DateTimeOffset AcquiredAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

internal enum EconomyLegacyShadowBatchState
{
    Captured = 1,
    Backfilling = 2,
    Backfilled = 3,
    Reconciled = 4,
    CutoverProposed = 5,
    CutoverActive = 6,
    RolledBack = 7,
    Failed = 8
}

internal enum EconomyLegacyShadowItemState
{
    Captured = 1,
    Observed = 2,
    Posted = 3,
    Reconciled = 4,
    Blocked = 5
}

internal enum EconomyLegacyCutoverState
{
    Proposed = 1,
    FirstApproved = 2,
    Active = 3,
    RolledBack = 4
}

internal sealed class EconomyLegacyShadowBatchRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RequestedBy { get; set; }
    public string JurisdictionCode { get; set; } = string.Empty;
    public long PolicyVersion { get; set; }
    public EconomyLegacyShadowBatchState State { get; set; }
    public int WalletCount { get; set; }
    public int TransactionCount { get; set; }
    public int FinancialLedgerEntryCount { get; set; }
    public long ExpectedHardUnits { get; set; }
    public long BackfilledHardUnits { get; set; }
    public long ReconciledHardUnits { get; set; }
    public string WalletSnapshotHash { get; set; } = string.Empty;
    public string TransactionSnapshotHash { get; set; } = string.Empty;
    public string FinancialLedgerSnapshotHash { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string? FailureCode { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; }
}

internal sealed class EconomyLegacyShadowWalletRow
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LegacyWalletId { get; set; }
    public Guid? EconomyWalletId { get; set; }
    public Guid OwnerId { get; set; }
    public long LegacyBalanceMinorUnits { get; set; }
    public long CompletedCreditsMinorUnits { get; set; }
    public long CompletedDebitsMinorUnits { get; set; }
    public int TransactionCount { get; set; }
    public string SnapshotHash { get; set; } = string.Empty;
    public Guid SourceStampId { get; set; }
    public Guid PostingId { get; set; }
    public Guid CreditLotId { get; set; }
    public EconomyLegacyShadowItemState State { get; set; }
    public long? JournalSequence { get; set; }
    public string? JournalHash { get; set; }
    public string? ReconciliationHash { get; set; }
    public string? FailureCode { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset? ObservedAt { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public DateTimeOffset? ReconciledAt { get; set; }
    public long Version { get; set; }
}

internal sealed class EconomyLegacyCutoverRow
{
    public Guid TenantId { get; set; }
    public Guid BatchId { get; set; }
    public EconomyLegacyCutoverState State { get; set; }
    public Guid ProposedBy { get; set; }
    public Guid? FirstApprovedBy { get; set; }
    public Guid? SecondApprovedBy { get; set; }
    public Guid? RolledBackBy { get; set; }
    public string ReauthenticationHash { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset ProposedAt { get; set; }
    public DateTimeOffset? FirstApprovedAt { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? RolledBackAt { get; set; }
    public long Epoch { get; set; }
    public long Version { get; set; }
}

internal sealed class EconomyLegacyCutoverAuditRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BatchId { get; set; }
    public long Sequence { get; set; }
    public EconomyLegacyCutoverState State { get; set; }
    public Guid ActorId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ReauthenticationHash { get; set; } = string.Empty;
    public string EvidenceHash { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
}

internal sealed class RegisteredPostingReceiptRow
{
    public Guid PostingId { get; set; }
    public long JournalSequence { get; set; }
    public string JournalHash { get; set; } = string.Empty;
    public bool Duplicate { get; set; }
}

internal sealed class HardToSoftConversionRiskDecisionReceiptRow
{
    public Guid RiskDecisionId { get; set; }
    public string SourceRoots { get; set; } = string.Empty;
}

internal sealed class ProviderReversalReceiptRow
{
    public Guid OperationId { get; set; }
    public long RecoveredHardUnits { get; set; }
    public long RecoveredConvertedSoftUnits { get; set; }
    public long ResponsibleDebtHardUnits { get; set; }
    public long PlatformLossHardUnits { get; set; }
    public bool Duplicate { get; set; }
}

internal sealed class FifoFragmentReservationReceiptRow
{
    public Guid ReservationId { get; set; }
    public Guid ParentLotId { get; set; }
    public Guid RootSourceStampId { get; set; }
    public long ReversalEpoch { get; set; }
    public long StartInclusive { get; set; }
    public long EndExclusive { get; set; }
    public long AmountUnits { get; set; }
}

internal sealed class MarketplaceFifoReservationReceiptRow
{
    public Guid ReservationId { get; set; }
    public Guid ParentLotId { get; set; }
    public Guid RootSourceStampId { get; set; }
    public long ReversalEpoch { get; set; }
    public long StartInclusive { get; set; }
    public long EndExclusive { get; set; }
    public CurrencyCode Currency { get; set; }
    public long AmountUnits { get; set; }
}
