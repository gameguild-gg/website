using GameGuild;
using GameGuild.Assets.Commands;

[assembly: UseCaseEventContract(typeof(BulkUploadAssetsCommand), "assets.bulk-upload", NoDomainEventReason = "Bulk upload completion is observed through the durable generic operation event; file contents are never copied into event payloads.")]
[assembly: UseCaseEventContract(typeof(ReviewReportCommand), "assets.review-report", NoDomainEventReason = "Moderation review is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(RunAssetRetentionCommand), "assets.run-retention", NoDomainEventReason = "Retention execution is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(SetAssetLegalHoldCommand), "assets.set-legal-hold", NoDomainEventReason = "Legal-hold state changes are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(UpdateAssetCommand), "assets.update", NoDomainEventReason = "Asset metadata changes are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(BulkDeleteAssetsCommand), "assets.bulk-delete", NoDomainEventReason = "Bulk deletion is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ReportAssetCommand), "assets.report", NoDomainEventReason = "Asset reports are observed through the durable generic operation event.")]
