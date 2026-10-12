[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.ConfigureAuditRetentionCommand),
    "compliance.audit.retention-simulation.configure",
    NoDomainEventReason = "Simulation assumptions do not change enforced retention policies; the durable operation event records configuration changes.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.RunAuditRetentionSimulationCommand),
    "compliance.audit.retention-simulation.run",
    NoDomainEventReason = "The simulation saves an evidence snapshot without deleting or moving records; the durable operation event records its creation.")]
