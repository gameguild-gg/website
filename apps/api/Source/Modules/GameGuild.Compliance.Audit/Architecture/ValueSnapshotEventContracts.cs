[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.ConfigureValueSnapshotRetentionCommand),
    "compliance.audit.value-snapshots.configure-retention",
    NoDomainEventReason = "Retention configuration only changes the policy row; the durable operation event and the mutation audit log record the change.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.EnforceValueSnapshotRetentionCommand),
    "compliance.audit.value-snapshots.enforce-retention",
    NoDomainEventReason = "Retention enforcement deletes or compacts audit-only snapshot rows and records its own execution row; no business domain state changes.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.CreateValueSnapshotRollbackPlanCommand),
    "compliance.audit.value-snapshots.create-rollback-plan",
    NoDomainEventReason = "Plan creation is a read-only reconstruction of recorded history into a proposed plan; no domain state changes.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.ExecuteValueSnapshotRollbackCommand),
    "compliance.audit.value-snapshots.execute-rollback",
    NoDomainEventReason = "The restoring writes themselves flow through the standard entity-change capture and the durable operation event, producing fresh audited value snapshots of the recovery.")]
