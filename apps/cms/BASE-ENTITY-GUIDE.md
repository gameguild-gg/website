# Base Entity Documentation

## Overview

The Base Entity system provides a foundation for all domain entities in the CMS application, mirroring the functionality of NestJS EntityDto. It includes common properties, validation, and utility methods that all entities inherit.

## Architecture

### Core Components

1. **IEntity Interface** (`Common/Entities/IEntity.cs`)
   - Defines the contract for all entities
   - Provides both generic and non-generic versions
   - Ensures consistent entity structure across the application

2. **BaseEntity<TKey> Class** (`Common/Entities/BaseEntityGeneric.cs`)
   - Generic base class supporting different ID types
   - Implements IEntity interface
   - Provides core functionality for all entities

3. **BaseEntity Class** (`Common/Entities/BaseEntity.cs`)
   - Convenience class that uses `int` as the default ID type
   - Inherits from `BaseEntity<int>`
   - Provides static factory methods

4. **ModelBuilderExtensions** (`Common/Data/ModelBuilderExtensions.cs`)
   - Entity Framework configuration extensions
   - Automatically configures base entity properties
   - Sets up database constraints and indexes

## Features

### Common Properties

All entities automatically inherit these properties:

```csharp
public int Id { get; set; }                    // Primary key
public DateTime CreatedAt { get; set; }        // Creation timestamp
public DateTime UpdatedAt { get; set; }        // Last update timestamp
```

### Automatic Timestamp Management

- **CreatedAt**: Set automatically when entity is first created
- **UpdatedAt**: Updated automatically on entity modifications
- **Database Level**: Configured with `CURRENT_TIMESTAMP` defaults
- **Application Level**: Managed by `ApplicationDbContext.SaveChanges()`

### Utility Methods

#### Touch()
```csharp
user.Touch(); // Updates UpdatedAt to current UTC time
```

#### SetProperties()
```csharp
var properties = new Dictionary<string, object?>
{
    { "Name", "John Doe" },
    { "Email", "john@example.com" }
};
user.SetProperties(properties); // Sets multiple properties at once
```

#### Static Factory Methods
```csharp
// Create with properties
var user = BaseEntity.Create<User>(new Dictionary<string, object?>
{
    { "Name", "Jane Doe" },
    { "Email", "jane@example.com" }
});

// Create empty
var user = BaseEntity.Create<User>();
```

#### IsNew Property
```csharp
bool isNew = user.IsNew; // Returns true if Id == 0 (not persisted)
```

#### ToDictionary()
```csharp
var dict = user.ToDictionary(); // Gets all properties as dictionary
```

### Type Safety

The system supports both generic and non-generic approaches:

```csharp
// Using generic base with custom ID type
public class CustomEntity : BaseEntity<Guid>
{
    // Custom properties
}

// Using convenience base with int ID
public class User : BaseEntity
{
    // Custom properties
}
```

## Entity Framework Integration

### Automatic Configuration

The `ModelBuilderExtensions.ConfigureBaseEntities()` method automatically:

- Configures primary keys
- Sets up auto-increment for ID fields
- Configures timestamp columns with database defaults
- Adds performance indexes on CreatedAt

### Usage in DbContext

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // Configure all base entities automatically
    modelBuilder.ConfigureBaseEntities();
    
    // Entity-specific configurations
    modelBuilder.Entity<User>(entity =>
    {
        entity.ToTable("Users");
        entity.HasIndex(e => e.Email).IsUnique();
        // ... other configurations
    });
}
```

### Automatic Timestamp Updates

The `ApplicationDbContext` automatically handles timestamp updates:

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    UpdateTimestamps(); // Automatically called
    return await base.SaveChangesAsync(cancellationToken);
}
```

## Usage Examples

### Creating a New Entity

```csharp
// Option 1: Standard constructor
var user = new User
{
    Name = "John Doe",
    Email = "john@example.com"
};

// Option 2: Factory method with properties
var user = BaseEntity.Create<User>(new Dictionary<string, object?>
{
    { "Name", "John Doe" },
    { "Email", "john@example.com" }
});

// Option 3: Constructor with properties
var user = new User(new Dictionary<string, object?>
{
    { "Name", "John Doe" },
    { "Email", "john@example.com" }
});
```

### Updating an Entity

```csharp
// Option 1: Direct property assignment
user.Name = "Updated Name";
user.Touch(); // Manually update timestamp

// Option 2: Batch property update
user.SetProperties(new Dictionary<string, object?>
{
    { "Name", "Updated Name" },
    { "IsActive", false }
}); // Automatically calls Touch()
```

### Checking Entity State

```csharp
if (user.IsNew)
{
    // Handle new entity
    await dbContext.Users.AddAsync(user);
}
else
{
    // Handle existing entity
    dbContext.Users.Update(user);
}
```

## Best Practices

### 1. Always Inherit from BaseEntity

```csharp
// ✅ Good
public class User : BaseEntity
{
    // Custom properties only
}

// ❌ Avoid
public class User
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    // Duplicating base functionality
}
```

### 2. Use Factory Methods for Complex Initialization

```csharp
// ✅ Good - Clear and testable
var user = BaseEntity.Create<User>(userData);

// ❌ Avoid - Multiple lines for simple creation
var user = new User();
user.SetProperties(userData);
```

### 3. Let the System Handle Timestamps

```csharp
// ✅ Good - Let EF handle it
await dbContext.SaveChangesAsync();

// ❌ Avoid - Manual timestamp management (unless specific need)
user.UpdatedAt = DateTime.UtcNow;
await dbContext.SaveChangesAsync();
```

### 4. Use SetProperties for Partial Updates

```csharp
// ✅ Good - Efficient partial updates
user.SetProperties(updateData);

// ❌ Avoid - Individual property assignments for multiple properties
user.Name = updateData["Name"]?.ToString();
user.Email = updateData["Email"]?.ToString();
user.Touch();
```

## Testing

The base entity functionality is covered by comprehensive unit tests in `Tests/Common/Entities/BaseEntityTests.cs`. Run tests with:

```bash
dotnet test --filter "BaseEntityTests"
```

## Migration

When upgrading existing entities to use BaseEntity:

1. Update entity classes to inherit from `BaseEntity`
2. Remove manual `Id`, `CreatedAt`, `UpdatedAt` properties
3. Update Entity Framework configurations
4. Create and run database migration
5. Update any direct timestamp manipulation code

## Compatibility

- **Entity Framework Core**: 6.0+
- **PostgreSQL**: All supported versions
- **C#**: 10.0+
- **.NET**: 6.0+

## Future Enhancements

Potential improvements to consider:

1. **Soft Delete**: Add `IsDeleted` property and automatic filtering
2. **Audit Trail**: Track who created/modified entities
3. **Optimistic Concurrency**: Add version/timestamp for concurrency control
4. **Custom ID Types**: Enhanced support for UUID, composite keys
5. **Validation**: Integration with FluentValidation for complex rules

## Comparison with NestJS EntityDto

| Feature | NestJS EntityDto | C# BaseEntity |
|---------|------------------|---------------|
| Common Properties | ✅ id, createdAt, updatedAt | ✅ Id, CreatedAt, UpdatedAt |
| Type Safety | ✅ TypeScript interfaces | ✅ C# interfaces and generics |
| Factory Methods | ✅ Static create methods | ✅ Static Create<T> methods |
| Partial Updates | ✅ Object spread/assign | ✅ SetProperties dictionary |
| Validation | ✅ class-validator decorators | ✅ Data annotations |
| ORM Integration | ✅ TypeORM entities | ✅ EF Core entities |
| Automatic Timestamps | ✅ @CreateDateColumn, @UpdateDateColumn | ✅ EF Core SaveChanges override |

The C# implementation provides equivalent functionality while leveraging .NET's strong typing and Entity Framework's capabilities.
