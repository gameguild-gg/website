[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.ExecuteAuditQueryCommand),
    "compliance.audit.query.execute",
    NoDomainEventReason = "Query execution is read-only over the audit stores; the execution itself is attributed through the central audit trail (AuditQueryExecuted).")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.ValidateAuditQueryCommand),
    "compliance.audit.query.validate",
    NoDomainEventReason = "Validation is a pure computation over the submitted DSL payload and persists nothing.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.CreateSavedAuditQueryCommand),
    "compliance.audit.saved-view.create",
    NoDomainEventReason = "A saved view is analyst tooling state; its creation is recorded by the durable operation event and the central audit trail (SavedAuditQueryCreated).")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.UpdateSavedAuditQueryCommand),
    "compliance.audit.saved-view.update",
    NoDomainEventReason = "Saved-view edits are captured as immutable SavedAuditQueryRevision snapshots plus the central audit trail; no downstream domain reacts to them.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.DeleteSavedAuditQueryCommand),
    "compliance.audit.saved-view.delete",
    NoDomainEventReason = "Deletion removes analyst tooling state only; revision history is retained and the action is recorded on the central audit trail.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.ExecuteSavedAuditQueryCommand),
    "compliance.audit.saved-view.execute",
    NoDomainEventReason = "Saved-view execution is read-only over the audit stores; usage tracking lives on the view row and the central audit trail (SavedAuditQueryExecuted).")]
