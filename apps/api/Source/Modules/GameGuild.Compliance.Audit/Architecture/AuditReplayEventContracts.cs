[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.CaptureAuditReplayCommand),
    "compliance.audit.replay.capture",
    NoDomainEventReason = "This privileged read releases a safe retained-row snapshot without changing domain state; the durable operation event and required access audit record the capture.")]

[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.ReconstructAuditReplayCommand),
    "compliance.audit.replay.reconstruct",
    NoDomainEventReason = "This privileged investigation read verifies and summarizes an already-captured snapshot without changing domain state; the durable operation event and required access audit record the reconstruction.")]
