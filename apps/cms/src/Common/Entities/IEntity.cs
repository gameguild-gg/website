namespace GameGuild.Common.Entities;

/// <summary>
/// Interface that defines the contract for all entities in the system.
/// Provides the basic structure that all domain entities should implement.
/// Mirrors the NestJS EntityDto interface.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Unique identifier for the entity (UUID)
    /// </summary>
    Guid Id
    {
        get;
        set;
    }

    /// <summary>
    /// Version number for optimistic concurrency control
    /// </summary>
    int Version
    {
        get;
        set;
    }

    /// <summary>
    /// Timestamp when the entity was created
    /// </summary>
    DateTime CreatedAt
    {
        get;
        set;
    }

    /// <summary>
    /// Timestamp when the entity was last updated
    /// </summary>
    DateTime UpdatedAt
    {
        get;
        set;
    }

    /// <summary>
    /// Timestamp when the entity was soft-deleted (null if not deleted)
    /// </summary>
    DateTime? DeletedAt
    {
        get;
        set;
    }

    /// <summary>
    /// Updates the UpdatedAt timestamp to the current UTC time
    /// </summary>
    void Touch();

    /// <summary>
    /// Checks if this entity is newly created (not persisted to database)
    /// </summary>
    bool IsNew
    {
        get;
    }

    /// <summary>
    /// Checks if this entity is soft-deleted
    /// </summary>
    bool IsDeleted
    {
        get;
    }

    /// <summary>
    /// Soft-delete the entity by setting DeletedAt timestamp
    /// </summary>
    void SoftDelete();

    /// <summary>
    /// Restore a soft-deleted entity by clearing DeletedAt timestamp
    /// </summary>
    void Restore();
}

/// <summary>
/// Generic interface for entities with typed ID (for backward compatibility)
/// </summary>
/// <typeparam name="TKey">The type of the entity's identifier</typeparam>
public interface IEntity<TKey> : IEntity where TKey : IEquatable<TKey>
{
    /// <summary>
    /// Unique identifier for the entity with specific type
    /// </summary>
    new TKey Id
    {
        get;
        set;
    }
}
