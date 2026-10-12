[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.ConfigureSecurityLogRetentionCommand),
    "compliance.audit.security-events.retention.configure",
    NoDomainEventReason = "Retention policy changes are recorded through the durable security event pipeline; the durable operation event records the configuration change.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.EnforceSecurityLogRetentionCommand),
    "compliance.audit.security-events.retention.enforce",
    NoDomainEventReason = "Enforcement writes an auditable execution record per tenant; no cross-module state changes.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.AcknowledgeSecurityAlertCommand),
    "compliance.audit.security-events.alerts.acknowledge",
    NoDomainEventReason = "Acknowledgement only changes the workflow state of the persisted alert; the durable operation event records the transition.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.ResolveSecurityAlertCommand),
    "compliance.audit.security-events.alerts.resolve",
    NoDomainEventReason = "Resolution only changes the workflow state of the persisted alert; the resolution is audited as a security event and needs no cross-module state change.")]
