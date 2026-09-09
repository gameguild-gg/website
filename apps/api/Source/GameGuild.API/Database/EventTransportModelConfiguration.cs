using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GameGuild.API.Eventing;

namespace GameGuild.API.Database;

public sealed class EventTransportModelConfiguration : IModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        ConfigureOutbox(modelBuilder.Entity<OutboxMessage>());
        ConfigureInbox(modelBuilder.Entity<InboxReceipt>());
    }

    private static void ConfigureOutbox(EntityTypeBuilder<OutboxMessage> entity)
    {
        entity.ToTable("outbox_messages", "gameguild.integration");
        entity.HasKey(message => message.EventId);
        entity.Property(message => message.EventName).HasMaxLength(200).IsRequired();
        entity.Property(message => message.EventType).HasMaxLength(1000).IsRequired();
        entity.Property(message => message.SourceModule).HasMaxLength(200).IsRequired();
        entity.Property(message => message.AggregateType).HasMaxLength(200).IsRequired();
        entity.Property(message => message.AggregateId).HasMaxLength(200).IsRequired();
        entity.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
        entity.HasIndex(message => new { message.CompletedAtUtc, message.DeadLetteredAtUtc, message.ClaimedUntilUtc })
            .HasDatabaseName("ix_outbox_dispatch");
        entity.HasIndex(message => new { message.AggregateType, message.AggregateId, message.OccurredAtUtc, message.EventId })
            .HasDatabaseName("ix_outbox_aggregate_order");
        entity.HasIndex(message => message.CorrelationId).HasDatabaseName("ix_outbox_correlation");
    }

    private static void ConfigureInbox(EntityTypeBuilder<InboxReceipt> entity)
    {
        entity.ToTable("inbox_receipts", "gameguild.integration");
        entity.HasKey(receipt => new { receipt.EventId, receipt.ConsumerName });
        entity.Property(receipt => receipt.ConsumerName).HasMaxLength(500).IsRequired();
        entity.Property(receipt => receipt.LastError).HasMaxLength(4000);
        entity.HasIndex(receipt => new { receipt.CompletedAtUtc, receipt.DeadLetteredAtUtc, receipt.NextAttemptAtUtc })
            .HasDatabaseName("ix_inbox_retry");
        entity.HasOne<OutboxMessage>()
            .WithMany()
            .HasForeignKey(receipt => receipt.EventId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
