# Entity Framework Core Setup - SQLite (Development) + PostgreSQL (Production)

## Overview

This CMS project is configured to use **SQLite** for local development and **PostgreSQL** for production environments. This setup provides the best of both worlds:

- **Development**: SQLite requires no setup, works locally, and is perfect for development
- **Production**: PostgreSQL provides enterprise-grade performance and features

## Database Provider Configuration

### Automatic Provider Selection

The application automatically selects the database provider based on the environment:

```csharp
// In Program.cs
if (builder.Environment.IsDevelopment())
{
    // Use SQLite for development
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));
}
else
{
    // Use PostgreSQL for production
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));
}
```

### Connection Strings

#### Development (SQLite)
```json
// appsettings.Development.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=app.db"
  }
}
```

#### Production (PostgreSQL)
```json
// appsettings.Production.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=gameguild_cms;Username=postgres;Password=your_password_here"
  }
}
```

## Migration Strategy

### Automated Database Management

The application automatically handles database creation and migration:

```csharp
// In Program.cs - Automatic database setup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate(); // Creates DB if missing, applies pending migrations
}
```

### Single Database File

The setup uses a single database file (`app.db`) for both design-time and runtime operations:

```csharp
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlite("Data Source=app.db"); // Same file as runtime
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
```

### Cross-Database Compatibility

Entity Framework Core migrations are designed to be database-agnostic for most common operations. The following work seamlessly across SQLite and PostgreSQL:

✅ **Compatible Operations:**
- Basic table creation
- Column definitions (string, int, DateTime, bool)
- Primary keys and foreign keys
- Basic indexes
- Data annotations (`[Required]`, `[MaxLength]`, etc.)

⚠️ **Considerations:**
- Use EF Core data types instead of database-specific types
- Avoid raw SQL in migrations when possible
- Test migrations on both database types if using advanced features

## NuGet Packages

The project includes both database providers:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.0"/>
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0"/>
```

## Development Workflow

### 1. Create Migration
```bash
dotnet ef migrations add YourMigrationName
```

### 2. Database Auto-Creation
The database is automatically created and updated when you run the application:
```bash
dotnet run
```
**No manual database commands needed!** The app will:
- Create `app.db` if it doesn't exist
- Apply any pending migrations automatically
- Start the web server

### 3. Manual Database Update (Optional)
If you prefer manual control:
```bash
dotnet ef database update
```

## Production Deployment

### 1. Set Environment
```bash
export ASPNETCORE_ENVIRONMENT=Production
```

### 2. Update Connection String
Update `appsettings.Production.json` with your PostgreSQL connection details.

### 3. Run Migrations
```bash
dotnet ef database update --environment Production
```

## Testing the Setup

### API Endpoints Available

- `GET /api/users` - Get all users
- `GET /api/users/{id}` - Get user by ID
- `POST /api/users` - Create new user
- `PUT /api/users/{id}` - Update user
- `DELETE /api/users/{id}` - Delete user

### Example API Usage

```bash
# Create a user
curl -X POST http://localhost:5001/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "username": "john_doe",
    "email": "john@example.com",
    "firstName": "John",
    "lastName": "Doe"
  }'

# Get all users
curl -X GET http://localhost:5001/api/users
```

## Benefits of This Approach

1. **Zero Configuration**: No database setup required - just run the app!
2. **Single Database File**: Only `app.db` is created, no confusion with multiple DB files
3. **Automatic Migration**: Database is created and updated automatically on startup
4. **Production-Ready**: Same codebase works seamlessly in production with PostgreSQL
5. **Migration Compatibility**: EF Core handles database differences automatically
6. **Performance**: SQLite for development speed, PostgreSQL for production scalability
7. **Developer Friendly**: New developers can start immediately without any database setup

## Database Files

- **app.db** - Main SQLite database file
- **app.db-shm** - SQLite shared memory file (automatically managed)
- **app.db-wal** - SQLite write-ahead log file (automatically managed)

*Note: Only `app.db` needs to be in version control. The other files are runtime artifacts.*

## Notes

- **Automatic Setup**: Database is created and migrations applied automatically on startup
- **Single Database File**: Uses only `app.db` for both design-time and runtime operations
- **Migration Compatibility**: Migrations generated with SQLite work seamlessly with PostgreSQL
- **Zero Configuration**: New developers can start immediately - just `dotnet run`
- **Environment Specific**: Connection strings automatically switch based on environment
- **Production Ready**: Same codebase deploys to PostgreSQL without code changes
