namespace cms.Common.Entities;

/// <summary>
/// Base entity class that provides common properties and functionality for all domain entities.
/// Mirrors the functionality of NestJS EntityBase with UUID primary keys, version control, and soft delete.
/// Uses Guid as the default ID type to match the NestJS implementation.
/// </summary>
public class BaseEntity : BaseEntity<Guid>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    protected BaseEntity()
    {
        // Generate new GUID for new entities
        if (Id == Guid.Empty)
        {
            Id = Guid.NewGuid();
        }
    }

    /// <summary>
    /// Constructor for partial initialization (useful for updates)
    /// Mirrors the NestJS EntityDto constructor: constructor(partial: Partial<typeof this>)
    /// </summary>
    /// <param name="partial">Partial entity data to initialize with</param>
    protected BaseEntity(object partial) : base(partial)
    {
        // Generate new GUID for new entities if not provided in partial
        if (Id == Guid.Empty)
        {
            Id = Guid.NewGuid();
        }
    }

    /// <summary>
    /// Static factory method to create an entity with initial properties
    /// Mirrors the NestJS EntityDto.create() method
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="partial">Initial properties</param>
    /// <returns>New instance of the entity</returns>
    public static T Create<T>(object partial) where T : BaseEntity, new()
    {
        return (T)Activator.CreateInstance(typeof(T), partial)!;
    }

    /// <summary>
    /// Static factory method to create an entity (parameterless)
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <returns>New instance of the entity</returns>
    public static T Create<T>() where T : BaseEntity, new()
    {
        return new T();
    }

    /// <summary>
    /// Checks if this entity is newly created (not persisted to database)
    /// </summary>
    public override bool IsNew
    {
        get => Id == Guid.Empty;
    }
}
