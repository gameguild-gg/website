using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using GameGuild.Common.Entities;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GameGuild.Common.Data;

/// <summary>
/// Extension methods for configuring base entity properties in Entity Framework
/// </summary>
public static class ModelBuilderExtensions
{    /// <summary>
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
                    // In TPC inheritance, each concrete type gets its own complete table
                    // We need to configure base properties for all concrete types, not just root types
                    bool isAbstractType = entityType.ClrType.IsAbstract;
                    bool isTPCInheritanceType = IsTPCInheritanceEntity(entityType.ClrType);

                    if (!isAbstractType)
                    {
                        // Skip key configuration for TPC inheritance entities - EF handles it automatically
                        if (!isTPCInheritanceType)
                        {
                            // Id configuration (UUID) - for entities not using TPC inheritance
                            builder.HasKey(nameof(BaseEntity.Id));
                            builder.Property(nameof(BaseEntity.Id))
                                .HasDefaultValueSql("gen_random_uuid()") // PostgreSQL UUID generation
                                .ValueGeneratedOnAdd();
                        }

                        // Version configuration for optimistic concurrency
                        // Use ConcurrencyCheck instead of IsRowVersion for cross-database compatibility
                        // No database default - application controls versioning (0 = new, 1+ = saved)
                        builder.Property(nameof(BaseEntity.Version))
                            .IsConcurrencyToken();

                        // Timestamp and soft delete configuration - for all concrete types in TPC
                        builder.Property(nameof(BaseEntity.CreatedAt))
                            .IsRequired()
                            .HasDefaultValueSql("CURRENT_TIMESTAMP")
                            .ValueGeneratedOnAdd();

                        builder.Property(nameof(BaseEntity.UpdatedAt))
                            .IsRequired()
                            .HasDefaultValueSql("CURRENT_TIMESTAMP")
                            .ValueGeneratedOnAddOrUpdate();

                        builder.Property(nameof(BaseEntity.DeletedAt))
                            .IsRequired(false);

                        // Add indexes for performance - for all concrete types in TPC
                        builder.HasIndex(nameof(BaseEntity.CreatedAt));
                        builder.HasIndex(nameof(BaseEntity.DeletedAt));
                    }
                    // Abstract types in TPC inheritance don't get their own tables
                    // Each concrete type gets a complete table with all inherited properties
                }
            );
        }
    }    /// <summary>
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
            // Skip abstract types - they don't get tables in TPC inheritance
            bool isAbstractType = entityType.ClrType.IsAbstract;
            if (isAbstractType)
                continue;

            // Skip types that are part of TPC inheritance hierarchies
            // These are handled differently by EF Core and would cause conflicts
            if (IsTPCInheritanceEntity(entityType.ClrType))
                continue;

            // Add global query filter to exclude soft-deleted entities for concrete types
            // that are NOT part of TPC inheritance hierarchies
            ParameterExpression parameter = Expression.Parameter(entityType.ClrType, "e");
            MemberExpression deletedAtProperty = Expression.Property(parameter, nameof(BaseEntity.DeletedAt));
            BinaryExpression condition = Expression.Equal(deletedAtProperty, Expression.Constant(null, typeof(DateTime?)));
            LambdaExpression lambda = Expression.Lambda(condition, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }/// <summary>
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
    /// Checks if a type is part of a TPC inheritance hierarchy
    /// In this case, we check if it inherits from ResourceBase which uses TPC mapping strategy
    /// </summary>
    private static bool IsTPCInheritanceEntity(Type type)
    {
        if (type == null) return false;

        // Check if the type inherits from ResourceBase (which uses TPC inheritance)
        return typeof(ResourceBase).IsAssignableFrom(type);
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
