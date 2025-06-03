using Microsoft.EntityFrameworkCore;
using cms.Common.Entities;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace cms.Common.Data;

/// <summary>
/// Extension methods for configuring base entity properties in Entity Framework
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Configures all entities that inherit from BaseEntity with common configurations
    /// </summary>
    /// <param name="modelBuilder">The model builder</param>
    public static void ConfigureBaseEntities(this ModelBuilder modelBuilder)
    {
        // Find all entity types that inherit from BaseEntity or BaseEntity<T>
        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => t.ClrType != null && IsBaseEntity(t.ClrType));

        foreach (IMutableEntityType entityType in entityTypes)
        {
            // Configure common properties
            modelBuilder.Entity(
                entityType.ClrType,
                builder =>
                {
                    // Id configuration (UUID)
                    builder.HasKey(nameof(BaseEntity.Id));
                    builder.Property(nameof(BaseEntity.Id))
                        .HasDefaultValueSql("gen_random_uuid()") // PostgreSQL UUID generation
                        .ValueGeneratedOnAdd();

                    // Version configuration for optimistic concurrency
                    builder.Property(nameof(BaseEntity.Version))
                        .IsRowVersion() // Maps to PostgreSQL's xmin column
                        .IsConcurrencyToken();

                    // CreatedAt configuration
                    builder.Property(nameof(BaseEntity.CreatedAt))
                        .IsRequired()
                        .HasDefaultValueSql("CURRENT_TIMESTAMP")
                        .ValueGeneratedOnAdd();

                    // UpdatedAt configuration
                    builder.Property(nameof(BaseEntity.UpdatedAt))
                        .IsRequired()
                        .HasDefaultValueSql("CURRENT_TIMESTAMP")
                        .ValueGeneratedOnAddOrUpdate();

                    // DeletedAt configuration (for soft delete)
                    builder.Property(nameof(BaseEntity.DeletedAt))
                        .IsRequired(false);

                    // Add indexes for performance
                    builder.HasIndex(nameof(BaseEntity.CreatedAt));
                    builder.HasIndex(nameof(BaseEntity.DeletedAt));
                }
            );
        }
    }

    /// <summary>
    /// Configures global query filters for soft delete
    /// </summary>
    /// <param name="modelBuilder">The model builder</param>
    public static void ConfigureSoftDelete(this ModelBuilder modelBuilder)
    {
        // Find all entity types that inherit from BaseEntity or BaseEntity<T>
        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => t.ClrType != null && IsBaseEntity(t.ClrType));

        foreach (IMutableEntityType entityType in entityTypes)
        {
            // Add global query filter to exclude soft-deleted entities
            ParameterExpression parameter = Expression.Parameter(entityType.ClrType, "e");
            MemberExpression deletedAtProperty = Expression.Property(parameter, nameof(BaseEntity.DeletedAt));
            BinaryExpression condition = Expression.Equal(deletedAtProperty, Expression.Constant(null, typeof(DateTime?)));
            LambdaExpression lambda = Expression.Lambda(condition, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }

    /// <summary>
    /// Checks if a type inherits from BaseEntity or BaseEntity&lt;T&gt;
    /// </summary>
    private static bool IsBaseEntity(Type type)
    {
        if (type == null) return false;

        // Check for direct inheritance from BaseEntity
        if (typeof(BaseEntity).IsAssignableFrom(type))
            return true;

        // Check for inheritance from BaseEntity<T>
        Type? baseType = type.BaseType;
        while (baseType != null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(BaseEntity<>))
                return true;
            baseType = baseType.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Sets up automatic UpdatedAt timestamp updates
    /// </summary>
    /// <param name="modelBuilder">The model builder</param>
    public static void ConfigureTimestamps(this ModelBuilder modelBuilder)
    {
        // This is handled by overriding SaveChanges in the DbContext
        // The configuration here is just for the database schema
    }
}
