# CMS Quick Start Guide

## Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## Getting Started

### 1. Clone and Navigate
```bash
cd /path/to/gameguild/services/cms
```

### 2. Run the Application
```bash
dotnet run
```

That's it! The application will:
- ✅ Automatically install NuGet packages
- ✅ Create the SQLite database (`app.db`) - *Note: Database files are excluded from git*
- ✅ Apply all migrations
- ✅ Start the web server on `http://localhost:5001`

## ⚠️ Important Notes

### Database Files and Git
- **Database files (`*.db`) are automatically excluded from git** for security reasons
- Each developer gets their own local database when they first run the application
- To reset your local database, simply delete `app.db` and restart the application

### Environment Configuration
- Configuration is managed through `.env` files (see [ENVIRONMENT-SETUP.md](ENVIRONMENT-SETUP.md))
- The base `.env` file is included in the repository with safe default values

## API Endpoints

### REST API - Users Management
- `GET /api/users` - List all users
- `GET /api/users/{id}` - Get user by ID
- `POST /api/users` - Create new user
- `PUT /api/users/{id}` - Update user
- `DELETE /api/users/{id}` - Delete user

### GraphQL API
- **Endpoint**: `http://localhost:5001/graphql`
- **Interactive IDE**: Open the GraphQL endpoint in your browser for Banana Cake Pop
- **Queries**: `users`, `userById`, `userByUsername`, `activeUsers`
- **Mutations**: `createUser`, `updateUser`, `deleteUser`, `toggleUserStatus`

### API Documentation
- **Swagger UI**: `http://localhost:5001/swagger`
- **GraphQL Schema**: Available in Banana Cake Pop IDE
- **Full GraphQL Docs**: See `README-GraphQL.md`

### Example Usage

#### REST API
```bash
# Create a user
curl -X POST http://localhost:5001/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "username": "johndoe",
    "email": "john@example.com",
    "firstName": "John",
    "lastName": "Doe"
  }'

# Get all users
curl -X GET http://localhost:5001/api/users
```

#### GraphQL API
```bash
# Query all users
curl -X POST http://localhost:5001/graphql \
  -H "Content-Type: application/json" \
  -d '{"query": "{ users { id username email } }"}'

# Create a user with GraphQL
curl -X POST http://localhost:5001/graphql \
  -H "Content-Type: application/json" \
  -d '{
    "query": "mutation CreateUser($input: CreateUserInput!) { createUser(input: $input) { id username email } }",
    "variables": {
      "input": {
        "username": "johndoe",
        "email": "john@example.com",
        "firstName": "John",
        "lastName": "Doe"
      }
    }
  }'
```

## Development

### Adding New Entities
1. Create a model in `Models/`
2. Add DbSet to `ApplicationDbContext`
3. Create migration: `dotnet ef migrations add AddNewEntity`
4. Restart app (migrations apply automatically)

### Database Management
- **View Database**: Use SQLite browser or `sqlite3 app.db`
- **Reset Database**: Delete `app.db` and restart app
- **Manual Migration**: `dotnet ef database update`

## Production Deployment
The same code automatically switches to PostgreSQL in production environments. See `README-EntityFramework.md` for details.

## Troubleshooting

### Port Already in Use
```bash
dotnet run --urls "http://localhost:5002"
```

### Reset Everything
```bash
rm -f *.db*
dotnet run
```
