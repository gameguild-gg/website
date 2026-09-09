using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameGuild.CQRS;
using GameGuild.CQRS.Models;

namespace GameGuild;

/// <summary>
///     Generic base entity class that provides common properties and functionality for all domain entities.
///     Supports different ID types while maintaining the same base functionality.
/// </summary>
/// <typeparam name="TKey">The type of the entity's identifier</typeparam>
public abstract class EntityBase<TKey> : IEntity<TKey>, ITenantScoped, IHasDomainEvents, IHasIntegrationEvents where TKey : IEquatable<TKey>
{
    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly List<IDurableIntegrationEvent> _integrationEvents = [];

    /// <summary>
    ///     Default constructor
    /// </summary>
    protected EntityBase() { }

    /// <summary>
    ///     Constructor for partial initialization (useful for updates).
    ///     Uses a non-virtual initialization path to avoid virtual member calls in constructors.
    /// </summary>
    /// <param name="partial">Partial entity data to initialize with</param>
    protected EntityBase(object partial) : this() { InitializeFromPartial(partial); }

    public virtual bool IsGlobal { get => TenantId == null; }

    /// <summary>
    ///     Checks if this entity is newly created (not yet persisted to a database)
    /// </summary>
    public virtual bool IsNew { get => Version == 0; }

    /// <summary>
    ///     Checks if this entity is soft-deleted
    /// </summary>
    public virtual bool IsDeleted { get => DeletedAt.HasValue; }

    /// <summary> Domain events raised by this entity </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents { get => _domainEvents.AsReadOnly(); }

    [NotMapped]
    public IReadOnlyList<IDurableIntegrationEvent> IntegrationEvents => _integrationEvents.AsReadOnly();

    /// <summary>
    ///     Unique identifier for the entity.
    ///     Setter is public for EF Core materialization; prefer constructor or factory methods for domain code.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual TKey Id { get; set; } = default!;

    /// <summary>
    ///     Version number for optimistic concurrency control.
    ///     Uses ConcurrencyCheck for cross-database compatibility (Postgres, SQLite, SQL Server).
    ///     Protected setter prevents direct manipulation outside the entity hierarchy; EF Core uses backing field.
    /// </summary>
    [ConcurrencyCheck]
    public int Version { get; set; } = 0;

    /// <summary>
    ///     Timestamp when the entity was created.
    ///     Protected setter prevents modification after initial creation; EF Core uses backing field.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = SystemClock.UtcNow;

    /// <summary>
    ///     Timestamp when the entity was last updated.
    ///     Protected setter — use <see cref="Touch"/> to update. EF Core uses backing field.
    /// </summary>
    [Required]
    public DateTime UpdatedAt { get; set; } = SystemClock.UtcNow;

    /// <summary>
    ///     Timestamp when the entity was soft-deleted (null if not deleted).
    ///     Protected setter — use <see cref="SoftDelete"/>/<see cref="Restore"/>. EF Core uses backing field.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    ///     Updates the UpdatedAt timestamp to the current UTC time.
    /// </summary>
    public void Touch() { UpdatedAt = SystemClock.UtcNow; }

    /// <summary>
    ///     Soft-delete the entity by setting DeletedAt timestamp.
    ///     A new entity (Version == 0) cannot be soft-deleted.
    /// </summary>
    public void SoftDelete()
    {
        if (IsDeleted) return;
        if (IsNew) throw new InvalidOperationException("Cannot soft-delete an entity that has not been persisted (Version == 0).");

        DeletedAt = SystemClock.UtcNow;
        Touch();
    }

    /// <summary>
    ///     Restore a soft-deleted entity by clearing DeletedAt timestamp.
    /// </summary>
    public void Restore()
    {
        if (!IsDeleted) return;

        DeletedAt = null;
        Touch();
    }

    public virtual Guid? TenantId { get; set; }

    /// <summary>
    ///     Sets the TenantId property on this entity.
    /// </summary>
    /// <param name="tenantId">The tenant ID to set</param>
    public void SetTenantId(Guid tenantId)
    {
        TenantId = tenantId;
    }

    /// <summary>
    ///     Sets the TenantId property on this entity from a <see cref="TenantId"/> value object.
    /// </summary>
    /// <param name="tenantId">The tenant ID value object</param>
    public void SetTenantId(TenantId tenantId)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        TenantId = tenantId.Value;
    }

    /// <summary>
    ///     Private non-virtual method to update timestamp during construction
    /// </summary>
    private void UpdateTimestamp() { UpdatedAt = SystemClock.UtcNow; }

    /// <summary> Adds a domain event to the entity's event collection </summary>
    /// <param name="domainEvent"> The domain event to add </param>
    public void AddDomainEvent(IDomainEvent domainEvent) { _domainEvents.Add(domainEvent); }

    /// <summary> Removes a specific domain event from the entity's event collection </summary>
    /// <param name="domainEvent"> The domain event to remove </param>
    public void RemoveDomainEvent(IDomainEvent domainEvent) { _domainEvents.Remove(domainEvent); }

    /// <summary> Clears all domain events from the entity's event collection </summary>
    public void ClearDomainEvents() { _domainEvents.Clear(); }

    public void AddIntegrationEvent(IDurableIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _integrationEvents.Add(integrationEvent);
    }

    public void ClearIntegrationEvents() { _integrationEvents.Clear(); }

    protected void Raise(IDomainEvent domainEvent) { _domainEvents.Add(domainEvent); }

    /// <summary>
    ///     Gets a dictionary representation of the entity's current state.
    ///     Internal to prevent external consumers from bypassing encapsulation.
    /// </summary>
    /// <returns>Dictionary with property names and values</returns>
    internal virtual Dictionary<string, object?> ToDictionary()
    {
        return EntityPropertyMapper.GetProperties(this);
    }

    /// <summary>
    ///     Override for better debugging and logging
    /// </summary>
    public override string ToString() { return $"{GetType().Name} {{ Id = {Id}, Version = {Version}, CreatedAt = {CreatedAt:O}, UpdatedAt = {UpdatedAt:O}{(IsDeleted ? " (DELETED)" : "")} }}"; }

    protected virtual void ApplyPartial(object? partial)
    {
        InitializeFromPartial(partial);
    }

    /// <summary>
    ///     Non-virtual initialization method safe to call from constructors.
    ///     Converts the partial object to a property dictionary and applies via <see cref="EntityPropertyMapper"/>.
    /// </summary>
    private void InitializeFromPartial(object? partial)
    {
        if (partial is null) return;

        var dictionary = EntityPropertyMapper.ToDictionary(partial);
        SetPropertiesInternal(dictionary, true);
    }

    /// <summary>
    ///     Sets multiple properties from a dictionary (useful for partial updates)
    /// </summary>
    /// <param name="properties">Dictionary of property names and values</param>
    public virtual void SetProperties(Dictionary<string, object?> properties) { SetPropertiesInternal(properties, false); }

    /// <summary>
    ///     Internal method to set properties with option to use non-virtual timestamp update.
    ///     Delegates to <see cref="EntityPropertyMapper"/> for the actual property mapping.
    /// </summary>
    /// <param name="properties">Dictionary of property names and values</param>
    /// <param name="isFromConstructor">True if called from constructor to avoid virtual method calls</param>
    private void SetPropertiesInternal(Dictionary<string, object?> properties, bool isFromConstructor)
    {
        EntityPropertyMapper.SetProperties(this, properties, propertyName =>
        {
            // Don't auto-update UpdatedAt for CreatedAt changes
            if (!string.Equals(propertyName, nameof(CreatedAt), StringComparison.Ordinal))
            {
                if (isFromConstructor)
                    UpdateTimestamp();
                else
                    Touch();
            }
        });
    }

}

/// <summary>
///     Base entity class that provides common properties and functionality for all domain entities.
/// </summary>
public class EntityBase : EntityBase<Guid>, IEntity
{
    /// <summary>
    ///     Default constructor — generates a new GUID if Id is empty.
    /// </summary>
    protected EntityBase()
    {
        EnsureIdGenerated();
    }

    /// <summary>
    ///     Constructor for partial initialization (useful for updates)
    /// </summary>
    /// <param name="partial">Partial entity data to initialize with</param>
    protected EntityBase(object partial) : base(partial)
    {
        EnsureIdGenerated();
    }

    /// <summary>
    ///     Non-virtual method to generate a GUID for new entities.
    ///     Safe to call from constructors.
    /// </summary>
    private void EnsureIdGenerated()
    {
        if (Id == Guid.Empty) Id = Guid.NewGuid();
    }

    /// <summary>
    ///     Static factory method to create an entity with initial properties
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="partial">Initial properties</param>
    /// <returns>New instance of the entity</returns>
    public static T Create<T>(object partial) where T : EntityBase, new()
    {
        var instance = new T();

        if (partial != null)
        {
            var propDict = EntityPropertyMapper.ToDictionary(partial);
            instance.SetProperties(propDict);
        }

        return instance;
    }

    /// <summary>
    ///     Static factory method to create an entity (parameterless)
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <returns>New instance of the entity</returns>
    public static T Create<T>() where T : EntityBase, new() { return new T(); }
}
