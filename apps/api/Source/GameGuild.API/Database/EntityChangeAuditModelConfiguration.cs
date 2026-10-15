using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.API.Database;

public sealed class EntityChangeAuditModelConfiguration : IModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<EntityChangeAuditRecord>();
        entity.ToTable("entity_change_audit_records", "gameguild.audit");
        entity.HasKey(record => record.Id);
        entity.Property(record => record.EntityName).HasMaxLength(300).IsRequired();
        entity.Property(record => record.KeyId).HasMaxLength(300).IsRequired();
        entity.Property(record => record.ChangeKind).HasMaxLength(20).IsRequired();
        entity.Property(record => record.BeforeValues).HasColumnType("jsonb");
        entity.Property(record => record.AfterValues).HasColumnType("jsonb");
        entity.Property(record => record.CorrelationId).HasMaxLength(100);
        entity.HasIndex(record => new { record.EntityName, record.KeyId, record.OccurredAtUtc })
            .HasDatabaseName("ix_entity_change_audit_entity");
        entity.HasIndex(record => record.OccurredAtUtc)
            .HasDatabaseName("ix_entity_change_audit_time");
        entity.HasIndex(record => record.CorrelationId)
            .HasDatabaseName("ix_entity_change_audit_correlation");
    }
}
