# Database Management

## Overview

This project uses SQLite for development and PostgreSQL for production. Database files are **never committed to git** for security and practical reasons.

## Database File Exclusion

The following files are excluded from git via `.gitignore`:

```
# SQLite database files - DO NOT COMMIT TO GIT
*.db
*.db-shm
*.db-wal
*.db-journal
```

## Why Database Files Are Excluded

1. **Security**: Database files contain user data and potentially sensitive information
2. **Size**: Database files grow over time and can become very large
3. **Conflicts**: Binary database files create merge conflicts between developers
4. **Environment Isolation**: Each environment should have its own database

## Local Development

### First Time Setup
```bash
# Clone the repository
git clone <repository-url>
cd services/cms

# Run the application (this creates the database automatically)
dotnet run
```

The application will:
1. Load environment variables from `.env`
2. Create `app.db` SQLite file if it doesn't exist
3. Apply all pending migrations automatically
4. Start the web server

### Reset Local Database
```bash
# Stop the application (Ctrl+C)
# Delete the database file
rm app.db

# Restart the application
dotnet run
```

### Backup/Restore (if needed)
```bash
# Create a backup
cp app.db app.db.backup

# Restore from backup
cp app.db.backup app.db
```

## Production Deployment

Production uses PostgreSQL with connection strings from environment variables:

```bash
# Production environment variable
DB_CONNECTION_STRING=Host=your-db-host;Database=gameguild_cms;Username=your-user;Password=your-password
```

## Migration Management

Migrations are handled automatically:
- **Development**: Applied on application startup
- **Production**: Applied on application startup (use migration scripts for critical deployments)

### Create New Migration
```bash
# Add a new migration
dotnet ef migrations add YourMigrationName

# The migration files will be created in the Migrations/ folder
# These files SHOULD be committed to git
```

### Manual Migration Commands (if needed)
```bash
# Apply migrations manually
dotnet ef database update

# Generate SQL script for migrations
dotnet ef migrations script
```

## Best Practices

1. ✅ **DO**: Commit migration files (`Migrations/` folder)
2. ✅ **DO**: Use environment variables for connection strings
3. ✅ **DO**: Test migrations on a copy of production data before deployment
4. ❌ **DON'T**: Commit database files (`*.db`)
5. ❌ **DON'T**: Hardcode connection strings in code
6. ❌ **DON'T**: Share database files between developers

## Troubleshooting

### "Database file is locked"
```bash
# Make sure the application is not running
# Check for zombie processes
ps aux | grep dotnet
kill <process-id> # if needed

# Try again
dotnet run
```

### "Migration already applied"
```bash
# Check migration status
dotnet ef migrations list

# If needed, reset migrations (DESTRUCTIVE - loses data)
rm app.db
dotnet run
```

### "Connection string not found"
Check that your `.env` file exists and contains:
```
DB_CONNECTION_STRING=Data Source=app.db
```
