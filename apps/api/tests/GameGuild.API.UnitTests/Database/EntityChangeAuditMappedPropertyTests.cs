using System.Text.Json;
using GameGuild.API.Database;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.API.UnitTests.Database;

public sealed class EntityChangeAuditMappedPropertyTests
{
    [Theory]
    [InlineData(EntityState.Added)]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public void MappedHiddenAndInheritedSensitivePropertiesAreRedactedWithoutLosingTheRecord(EntityState state)
    {
        using var context = NewContext();
        var entity = new MappedEntity { Id = Guid.NewGuid(), HiddenValue = Guid.NewGuid(),
            InheritedSecret = Guid.NewGuid().ToString("N"), VisibleValue = "visible-before" };
        var entry = context.Attach(entity);
        entry.Property("ShadowValue").CurrentValue = "shadow-before";
        entry.Property("ShadowValue").OriginalValue = "shadow-before";

        if (state == EntityState.Modified)
        {
            entity.HiddenValue = Guid.NewGuid();
            entity.InheritedSecret = Guid.NewGuid().ToString("N");
            entity.VisibleValue = "visible-after";
            entry.Property("ShadowValue").CurrentValue = "shadow-after";
        }
        else
        {
            entry.State = state;
        }

        Assert.Equal(1, EntityChangeAuditCapture.Capture(context, null, null));
        var record = Assert.Single(context.Set<EntityChangeAuditRecord>().Local);
        Assert.Equal(state.ToString(), record.ChangeKind);
        Assert.Equal(entity.Id.ToString(), record.KeyId);
        if (state != EntityState.Added)
        {
            AssertSnapshot(record.BeforeValues, "visible-before", "shadow-before");
        }
        else
        {
            Assert.Null(record.BeforeValues);
        }

        if (state != EntityState.Deleted)
        {
            AssertSnapshot(record.AfterValues, state == EntityState.Modified ? "visible-after" : "visible-before",
                state == EntityState.Modified ? "shadow-after" : "shadow-before");
        }
        else
        {
            Assert.Null(record.AfterValues);
        }
    }

    [Fact]
    public void HiddenUnmappedSensitivePropertyDoesNotChangeTheMappedNonSensitiveProperty()
    {
        using var context = NewContext();
        var entity = new PublicMappedEntity { Id = Guid.NewGuid(), Value = Guid.NewGuid() };
        context.Add(entity);

        Assert.Equal(1, EntityChangeAuditCapture.Capture(context, null, null));
        var record = Assert.Single(context.Set<EntityChangeAuditRecord>().Local);
        using var snapshot = JsonDocument.Parse(record.AfterValues!);
        Assert.Equal(entity.Value, snapshot.RootElement.GetProperty(nameof(entity.Value)).GetGuid());
    }

    private static void AssertSnapshot(string? snapshot, string visible, string shadow)
    {
        Assert.NotNull(snapshot);
        using var document = JsonDocument.Parse(snapshot);
        var values = document.RootElement;
        Assert.Equal(EntityChangeAuditCapture.RedactedValue, values.GetProperty(nameof(MappedEntity.HiddenValue)).GetString());
        Assert.Equal(EntityChangeAuditCapture.RedactedValue, values.GetProperty(nameof(MappedEntity.InheritedSecret)).GetString());
        Assert.Equal(visible, values.GetProperty(nameof(MappedEntity.VisibleValue)).GetString());
        Assert.Equal(shadow, values.GetProperty("ShadowValue").GetString());
    }

    private static MappingContext NewContext() => new(new DbContextOptionsBuilder<MappingContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private sealed class MappingContext(DbContextOptions<MappingContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MappedEntity>().HasBaseType((Type?)null);
            modelBuilder.Entity<MappedEntity>().Property(entity => entity.HiddenValue);
            modelBuilder.Entity<MappedEntity>().Property<string>("ShadowValue");
            modelBuilder.Entity<PublicMappedEntity>().HasBaseType((Type?)null);
            modelBuilder.Entity<PublicMappedEntity>().Property(entity => entity.Value);
            modelBuilder.Entity<EntityChangeAuditRecord>();
        }
    }

    private class MappedBase
    {
        public Guid Id { get; set; }
        public string HiddenValue { get; set; } = string.Empty;
        [AuditSensitive]
        public virtual string InheritedSecret { get; set; } = string.Empty;
    }

    private sealed class MappedEntity : MappedBase
    {
        [AuditSensitive]
        public new Guid HiddenValue { get; set; }
        public override string InheritedSecret { get; set; } = string.Empty;
        public string VisibleValue { get; set; } = string.Empty;
    }

    private class SensitiveUnmappedBase
    {
        public Guid Id { get; set; }
        [AuditSensitive]
        public string Value { get; set; } = string.Empty;
    }

    private sealed class PublicMappedEntity : SensitiveUnmappedBase
    {
        public new Guid Value { get; set; }
    }
}
