# Environment Configuration Guide

## Overview

The GameGuild CMS now uses environment variables for configuration instead of hardcoded values in `appsettings.json` files. This approach provides better security and flexibility for different deployment environments.

## Quick Setup

1. **Copy the base `.env` file** (already provided in the repository):
   ```bash
   # The .env file is already in the repository with default values
   ```

2. **Customize your database connection** by editing `.env`:
   ```bash
   # For Development (SQLite) - Default
   DB_CONNECTION_STRING=Data Source=app.db
   
   # For Production (PostgreSQL) - Replace with your values
   # DB_CONNECTION_STRING=Host=localhost;Database=gameguild_cms;Username=postgres;Password=your_password_here
   ```

3. **Run the application**:
   ```bash
   dotnet run
   ```

## Environment Variables

### Required Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `DB_CONNECTION_STRING` | Database connection string | `Data Source=app.db` (SQLite)<br>`Host=localhost;Database=gameguild_cms;Username=postgres;Password=secret` (PostgreSQL) |

### Optional Variables

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `ASPNETCORE_ENVIRONMENT` | Application environment | `Development` | `Production` |

## Database Provider Detection

The application automatically detects the database provider based on the connection string:

- **SQLite**: Connection strings containing `Data Source=` or `DataSource=`
- **PostgreSQL**: All other connection strings

## Development vs Production

### Development (SQLite)
```bash
# .env
DB_CONNECTION_STRING=Data Source=app.db
ASPNETCORE_ENVIRONMENT=Development
```

### Production (PostgreSQL)
```bash
# .env
DB_CONNECTION_STRING=Host=your-db-host;Database=gameguild_cms;Username=your-user;Password=your-password
ASPNETCORE_ENVIRONMENT=Production
```

## Security Best Practices

1. **Never commit sensitive values** to version control
2. **Never commit database files** to git (they contain sensitive data and can be large)
3. **Use strong passwords** for production databases
4. **Restrict database access** to only necessary IP addresses
5. **Use environment-specific `.env` files** for different deployments
6. **Consider using secrets management** for production (Azure Key Vault, AWS Secrets Manager, etc.)

## Database File Management

### ⚠️ Important: Database Files and Git

**Database files should NEVER be committed to version control** for several reasons:
- They contain sensitive user data
- They can become very large over time
- They create merge conflicts between developers
- They expose production data in development environments

The `.gitignore` file is configured to exclude:
```
# SQLite database files - DO NOT COMMIT TO GIT
*.db
*.db-shm
*.db-wal
*.db-journal
```

### Development Setup

1. **First run**: The application automatically creates the database file (`app.db`) when you run `dotnet run`
2. **Database location**: The SQLite file is created in the project root directory
3. **Migrations**: All migrations are applied automatically on startup
4. **Fresh start**: Delete `app.db` and restart the application to get a clean database

## Docker Deployment

For Docker deployments, you can override environment variables:

```bash
# Using docker run
docker run -e DB_CONNECTION_STRING="Host=db;Database=gameguild_cms;Username=postgres;Password=secret" your-image

# Using docker-compose
version: '3.8'
services:
  cms:
    image: your-image
    environment:
      - DB_CONNECTION_STRING=Host=db;Database=gameguild_cms;Username=postgres;Password=secret
```

## Migration from Old Configuration

The application has been migrated from:
- ❌ `appsettings.json` with hardcoded connection strings
- ✅ `.env` file with environment variables

### What Changed
1. Removed `ConnectionStrings` section from `appsettings.json` and `appsettings.Development.json`
2. Added `.env` file with base configuration
3. Updated `Program.cs` to use DotNetEnv package
4. Updated `ApplicationDbContextFactory.cs` for design-time operations

## Troubleshooting

### Application Won't Start
```
InvalidOperationException: DB_CONNECTION_STRING environment variable is not set
```
**Solution**: Ensure your `.env` file exists and contains the `DB_CONNECTION_STRING` variable.

### Database Connection Issues
1. **SQLite**: Ensure the directory is writable
2. **PostgreSQL**: Verify connection string and database server availability

### Design-Time Issues (Migrations)
The `ApplicationDbContextFactory` reads the same `.env` file, so ensure it's present in the project root.

## Implementation Details

### DotNetEnv Package
The application uses the `DotNetEnv` package (v3.1.1) for loading environment variables from `.env` files:

```csharp
using DotNetEnv;

// Load .env file
Env.Load();

// Access environment variables
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
```

### Dynamic Provider Selection
```csharp
if (connectionString.Contains("Data Source=") || connectionString.Contains("DataSource="))
{
    // SQLite
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));
}
else
{
    // PostgreSQL
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));
}
```

This setup provides a robust, secure, and flexible configuration system for the GameGuild CMS.
