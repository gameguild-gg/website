[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.CaptureAuditReplayCommand),
    "compliance.audit.replay.capture",
    NoDomainEventReason = "This privileged read releases a safe retained-row snapshot without changing domain state; the durable operation event and required access audit record the capture.")]
