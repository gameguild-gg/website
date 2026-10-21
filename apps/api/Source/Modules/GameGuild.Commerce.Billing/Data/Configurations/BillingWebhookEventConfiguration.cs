using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Entity Type Configuration for BillingWebhookEvent
/// </summary>
public class BillingWebhookEventConfiguration : IEntityTypeConfiguration<BillingWebhookEvent>
{
    public void Configure(EntityTypeBuilder<BillingWebhookEvent> builder)
    {
        // Configure table
        builder.ToTable("billing_webhook_events", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_billing_webhook_events_provider_scope_complete",
                """(("ProviderEnvironment" IS NULL AND "ProviderAccountId" IS NULL AND "WebhookEndpointId" IS NULL AND "IsLiveMode" IS NULL) OR ("ProviderEnvironment" IS NOT NULL AND "ProviderAccountId" IS NOT NULL AND "WebhookEndpointId" IS NOT NULL AND "IsLiveMode" IS NOT NULL))""");
            tableBuilder.HasCheckConstraint(
                "ck_billing_webhook_events_provider_environment",
                """("ProviderEnvironment" IS NULL OR ("ProviderEnvironment" = 'live' AND "IsLiveMode" = true) OR ("ProviderEnvironment" = 'test' AND "IsLiveMode" = false))""");
            tableBuilder.HasCheckConstraint(
                "ck_billing_webhook_events_provider_object_complete",
                """(("ProviderObjectId" IS NULL AND "ProviderObjectType" IS NULL AND "ProviderMonetaryLeg" IS NULL) OR ("ProviderObjectId" IS NOT NULL AND "ProviderObjectType" IS NOT NULL AND "ProviderMonetaryLeg" IS NOT NULL AND "ProviderEnvironment" IS NOT NULL AND "ProviderAccountId" IS NOT NULL))""");
        });

        // Configure primary key
        builder.HasKey(x => x.Id);

        // Property configurations
        builder.Property(x => x.Provider)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ExternalEventId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.ProviderEnvironment)
            .HasMaxLength(32);

        builder.Property(x => x.ProviderAccountId)
            .HasMaxLength(255);

        builder.Property(x => x.WebhookEndpointId)
            .HasMaxLength(255);

        builder.Property(x => x.ProviderObjectId)
            .HasMaxLength(255);

        builder.Property(x => x.ProviderObjectType)
            .HasMaxLength(100);

        builder.Property(x => x.ProviderMonetaryLeg)
            .HasMaxLength(100);

        builder.Property(x => x.IsLiveMode);

        builder.Property(x => x.EventSchemaVersion)
            .HasMaxLength(50);

        builder.Property(x => x.EventType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Payload)
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.DeadLetterReason)
            .HasMaxLength(1000);

        builder.Property(x => x.ProcessingAttempts)
            .IsConcurrencyToken();

        // Configure indexes for performance and idempotency
        builder.HasIndex(x => new { x.ExternalEventId, x.Provider })
            .IsUnique()
            .HasFilter("""("ProviderEnvironment" IS NULL AND "ProviderAccountId" IS NULL AND "WebhookEndpointId" IS NULL)""")
            .HasDatabaseName("ix_billing_webhook_events_external_id_provider");

        builder.HasIndex(x => new
            {
                x.Provider,
                x.ProviderEnvironment,
                x.ProviderAccountId,
                x.WebhookEndpointId,
                x.ExternalEventId
            })
            .HasFilter("\"ProviderEnvironment\" IS NOT NULL AND \"ProviderAccountId\" IS NOT NULL AND \"WebhookEndpointId\" IS NOT NULL")
            .IsUnique()
            .IsCreatedConcurrently()
            .HasDatabaseName("ix_billing_webhook_events_provider_scope_event");

        builder.HasIndex(x => new
            {
                x.Provider,
                x.ProviderEnvironment,
                x.ProviderAccountId,
                x.ProviderObjectId,
                x.ProviderMonetaryLeg
            })
            .HasFilter("\"ProviderEnvironment\" IS NOT NULL AND \"ProviderAccountId\" IS NOT NULL AND \"ProviderObjectId\" IS NOT NULL AND \"ProviderMonetaryLeg\" IS NOT NULL")
            .IsCreatedConcurrently()
            .HasDatabaseName("ix_billing_webhook_events_provider_object_leg");
        
        builder.HasIndex(x => x.EventType)
            .HasDatabaseName("ix_billing_webhook_events_event_type");
        
        builder.HasIndex(x => x.IsProcessed)
            .HasDatabaseName("ix_billing_webhook_events_is_processed");
        
        builder.HasIndex(x => x.IsFailed)
            .HasDatabaseName("ix_billing_webhook_events_is_failed");

        // Dead-letter inspection queue (issue #408): exhausted events ordered by the
        // moment they left the retry pipeline.
        builder.HasIndex(x => x.DeadLetteredAt)
            .HasDatabaseName("ix_billing_webhook_events_dead_lettered_at");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("ix_billing_webhook_events_tenant_id");
        
        builder.HasIndex(x => x.SubscriptionId)
            .HasDatabaseName("ix_billing_webhook_events_subscription_id");
        
        builder.HasIndex(x => x.CreatedAt)
            .HasDatabaseName("ix_billing_webhook_events_created_at");
    }
}
