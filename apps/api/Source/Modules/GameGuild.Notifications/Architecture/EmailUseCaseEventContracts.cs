using GameGuild;
using GameGuild.Notifications;
using GameGuild.Notifications.Controllers;

[assembly: UseCaseEventContract(typeof(SetMutedNotificationTypesCommand), "notifications.set-muted-types", NoDomainEventReason = "Preference changes are internal; the durable operation event records the mutation without disclosing notification preferences.")]
[assembly: UseCaseEventContract(typeof(SetNotificationDigestFrequencyCommand), "notifications.set-digest-frequency", NoDomainEventReason = "Digest preference changes are internal and observed through the durable operation event.")]
[assembly: UseCaseEventContract(typeof(ReleaseEmailSuppressionCommand), "notifications.release-email-suppression", NoDomainEventReason = "Delivery administration is observed through the durable operation event; recipient addresses are not event payloads.")]
[assembly: UseCaseEventContract(typeof(RequeueEmailNotificationCommand), "notifications.requeue-email", NoDomainEventReason = "Requeue updates the existing delivery queue; the durable operation event provides administrative observability.")]
[assembly: UseCaseEventContract(typeof(IngestEmailDeliveryEventCommand), "notifications.ingest-email-delivery-event", NoDomainEventReason = "Provider delivery events are durably persisted and observed without copying their private payload into the operation event.")]
[assembly: UseCaseEventContract(typeof(ReceiveEmailEventsCommand), "notifications.receive-email-events", NoDomainEventReason = "Provider delivery events are persisted and processed transactionally; the generic operation event excludes the private webhook body.")]
[assembly: UseCaseEventContract(typeof(UnsubscribeNotificationCommand), "notifications.unsubscribe", NoDomainEventReason = "Signed-token preference changes are observed through the durable operation event without storing the token.")]
