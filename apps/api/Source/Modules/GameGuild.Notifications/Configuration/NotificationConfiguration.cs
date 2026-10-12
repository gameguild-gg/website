using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Notifications.Configuration;

/// <summary>
/// EF Core configuration for the Notification entity
/// </summary>
public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(n => n.ActionUrl)
            .HasMaxLength(500);

        builder.Property(n => n.IconUrl)
            .HasMaxLength(500);

        builder.Property(n => n.ReferenceEntityType)
            .HasMaxLength(100);

        builder.Property(n => n.Metadata)
            .HasColumnType("text");

        builder.Property(n => n.Type)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(n => n.Channel)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(n => n.Priority)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(n => n.DeliveryStatus)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(n => n.LastError)
            .HasMaxLength(1000);

        builder.Property(n => n.RecipientEmail)
            .HasMaxLength(320);

        builder.Property(n => n.ProviderMessageId)
            .HasMaxLength(100);

        // Timeline join: EmailDeliveryEvent.ProviderMessageId -> Notifications.ProviderMessageId
        builder.HasIndex(n => n.ProviderMessageId);

        // Covers the dispatcher sweep: Channel == Email && DeliveryStatus == Pending && NextAttemptAt <= now
        builder.HasIndex(n => new { n.Channel, n.DeliveryStatus, n.NextAttemptAt });

        // Configure the relationship with NotificationTemplate
        builder.HasOne(n => n.Template)
            .WithMany()
            .HasForeignKey(n => n.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        // Global query filter for soft delete
        builder.HasQueryFilter(n => n.DeletedAt == null);
    }
}

/// <summary>
/// EF Core configuration for the NotificationTemplate entity
/// </summary>
public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(500);

        builder.Property(t => t.TitleTemplate)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.MessageTemplate)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(t => t.ActionUrlTemplate)
            .HasMaxLength(500);

        builder.Property(t => t.DefaultIconUrl)
            .HasMaxLength(500);

        builder.Property(t => t.Category)
            .HasMaxLength(50);

        builder.Property(t => t.SupportedPlaceholders)
            .HasMaxLength(1000);

        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.Channel)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.DefaultPriority)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Global query filter for soft delete
        builder.HasQueryFilter(t => t.DeletedAt == null);
    }
}

/// <summary>
/// EF Core configuration for the NotificationPreference entity
/// </summary>
public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Timezone)
            .HasMaxLength(50);

        builder.Property(p => p.MutedTypes)
            .HasMaxLength(500);

        builder.Property(p => p.EmailDigestFrequency)
            .HasConversion<string?>()
            .HasMaxLength(20);

        builder.Property(p => p.QuietHoursBypassPriority)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Global query filter for soft delete
        builder.HasQueryFilter(p => p.DeletedAt == null);
    }
}
