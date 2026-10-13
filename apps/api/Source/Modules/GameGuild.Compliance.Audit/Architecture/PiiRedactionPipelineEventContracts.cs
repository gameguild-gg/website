[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.CreatePiiRedactionRuleCommand),
    "compliance.audit.pii-redaction.create-rule",
    NoDomainEventReason = "Rule creation only reconfigures the in-process redaction pipeline; the durable operation event and the mutation audit log record the change.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.UpdatePiiRedactionRuleCommand),
    "compliance.audit.pii-redaction.update-rule",
    NoDomainEventReason = "Rule updates only reconfigure the in-process redaction pipeline; the durable operation event and the mutation audit log record the change.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.DeletePiiRedactionRuleCommand),
    "compliance.audit.pii-redaction.delete-rule",
    NoDomainEventReason = "Rule deletion only reconfigures the in-process redaction pipeline; the durable operation event and the mutation audit log record the change.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.TestPiiRedactionPipelineCommand),
    "compliance.audit.pii-redaction.test",
    NoDomainEventReason = "The test run is a read-only dry run over sample values; nothing is mutated or persisted.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.ResolvePiiTokenCommand),
    "compliance.audit.pii-redaction.resolve-token",
    NoDomainEventReason = "Token resolution reads the vault and writes a critical-risk audit entry; no domain state changes.")]
