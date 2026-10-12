[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.CreateScheduledAuditExportCommand),
    "compliance.audit.scheduled-export.create",
    NoDomainEventReason = "Scheduled export configuration changes are observed through the durable operation event.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.DisableScheduledAuditExportCommand),
    "compliance.audit.scheduled-export.disable",
    NoDomainEventReason = "Scheduled export configuration changes are observed through the durable operation event.")]
