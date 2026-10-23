[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.PreserveEvidenceCommand),
    "compliance.audit.chain-of-custody.preserve",
    NoDomainEventReason = "Preservation appends an immutable custody record; the durable operation event and the central audit trail document it.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.ReleaseEvidenceCommand),
    "compliance.audit.chain-of-custody.release",
    NoDomainEventReason = "Release advances the custody state on the evidence record and appends a custody event; the durable operation event documents it.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.BreakEvidenceSealCommand),
    "compliance.audit.chain-of-custody.break-seal",
    NoDomainEventReason = "Seal breaking advances the custody state and appends a custody event; the durable operation event documents it.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.RegisterCustodianCommand),
    "compliance.audit.chain-of-custody.register-custodian",
    NoDomainEventReason = "Custodian registration changes the authorization registry; the durable operation event and the mutation audit log record the change.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.RevokeCustodianCommand),
    "compliance.audit.chain-of-custody.revoke-custodian",
    NoDomainEventReason = "Custodian revocation changes the authorization registry; the durable operation event and the mutation audit log record the change.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.InitiateEvidenceTransferCommand),
    "compliance.audit.chain-of-custody.initiate-transfer",
    NoDomainEventReason = "Transfer initiation appends a signed custody event and moves evidence to transfer-pending; the durable operation event documents it.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.AcknowledgeEvidenceTransferCommand),
    "compliance.audit.chain-of-custody.acknowledge-transfer",
    NoDomainEventReason = "Acknowledgment counter-signs the transfer and reassigns custody with an appended event; the durable operation event documents it.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.CreateLegalHoldCommand),
    "compliance.audit.chain-of-custody.create-legal-hold",
    NoDomainEventReason = "Hold creation preserves evidence under custody and records notifications; the durable operation event and the central audit trail document it.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.ReleaseLegalHoldCommand),
    "compliance.audit.chain-of-custody.release-legal-hold",
    NoDomainEventReason = "Hold release only lifts the preservation block; preserved evidence and custody chains are retained and the audit trail documents the release.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.DispatchLegalHoldNotificationsCommand),
    "compliance.audit.chain-of-custody.dispatch-hold-notifications",
    NoDomainEventReason = "Notification dispatch stamps delivery intent on existing notification rows and writes an audit entry; no domain state changes.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.BuildCustodyPackageCommand),
    "compliance.audit.chain-of-custody.build-package",
    NoDomainEventReason = "The signed custody package is an immutable artifact recorded by the durable operation event and the central audit trail.")]
