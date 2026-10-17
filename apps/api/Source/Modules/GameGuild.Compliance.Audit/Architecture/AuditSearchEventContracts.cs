[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.SearchAuditLogsWithConditionsCommand),
    "compliance.audit.search-filter",
    NoDomainEventReason = "Read-only filtered search over existing audit entries; the durable operation event records the query.")]
