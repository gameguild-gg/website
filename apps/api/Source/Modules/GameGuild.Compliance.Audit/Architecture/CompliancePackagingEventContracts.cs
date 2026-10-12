[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.UploadComplianceDocumentCommand),
    "compliance.audit.evidence-document.upload",
    NoDomainEventReason = "An immutable evidence upload is recorded by the durable operation event and the central audit trail.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.ReviewComplianceDocumentCommand),
    "compliance.audit.evidence-document.review",
    NoDomainEventReason = "The document review revision is recorded by the durable operation event and central audit trail; existing sealed packages remain unchanged.")]
[assembly: global::GameGuild.UseCaseEventContractAttribute(
    typeof(global::GameGuild.Compliance.Audit.PrepareCompliancePackageCommand),
    "compliance.audit.evidence-package.prepare",
    NoDomainEventReason = "A signed evidence snapshot is recorded by the durable operation event and the central audit trail without changing enforced compliance policy.")]
