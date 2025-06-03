# CMS Project Structure - NestJS-like Architecture

This document describes the reorganized C# ASP.NET Core CMS application structure that follows NestJS patterns.

## 📁 Project Structure

```
src/
├── Program.cs                     # Main entry point (equivalent to main.ts)
├── appsettings.*.json            # Configuration files
├── Common/                       # Shared utilities, middleware, filters
│   ├── Middleware/               # Custom middleware
│   │   └── ExceptionHandlingMiddleware.cs
│   ├── Extensions/               # Service collection extensions
│   │   └── ServiceCollectionExtensions.cs
│   ├── Filters/                  # Global filters (to be added)
│   └── Constants/                # Application constants (to be added)
├── Config/                       # Configuration classes
│   ├── AppConfig.cs              # Application configuration
│   └── DatabaseConfig.cs         # Database configuration
├── Data/                         # Database context and configurations
│   ├── ApplicationDbContext.cs   # EF Core DbContext
│   ├── ApplicationDbContextFactory.cs
│   └── Configurations/           # Entity configurations (to be added)
├── Modules/                      # Feature modules (like NestJS modules)
│   └── User/                     # User module
│       ├── Controllers/          # REST API controllers
│       │   └── UsersController.cs
│       ├── Services/             # Business logic services
│       │   └── UserService.cs
│       ├── Dtos/                 # Data Transfer Objects
│       │   └── UserDtos.cs
│       ├── Models/               # Domain models
│       │   └── User.cs
│       └── GraphQL/              # GraphQL resolvers and types
│           ├── Query.cs
│           ├── Mutation.cs
│           ├── UserType.cs
│           └── UserInputs.cs
└── Migrations/                   # EF Core migrations
```

## 🔄 NestJS to C# Mapping

| NestJS Concept | C# ASP.NET Core Equivalent | Location |
|----------------|----------------------------|----------|
| Module | Service Registration + Extensions | `Common/Extensions/` |
| Controller | Controller | `Modules/*/Controllers/` |
| Service | Service (with Interface) | `Modules/*/Services/` |
| DTO | DTO Classes | `Modules/*/Dtos/` |
| Entity | Model Classes | `Modules/*/Models/` |
| Guard | Middleware/Filters | `Common/Middleware/` |
| Pipe | Model Validation | Built-in with `[ApiController]` |
| Interceptor | Middleware | `Common/Middleware/` |
| Exception Filter | Exception Middleware | `Common/Middleware/` |

## 🚀 Features Implemented

### ✅ Completed
- **Modular Structure**: Organized by feature modules (User module)
- **Service Layer**: Dependency injection with interfaces
- **DTOs**: Request/Response data transfer objects
- **Exception Handling**: Global exception middleware
- **GraphQL Integration**: Query and mutation resolvers
- **PostgreSQL Connection**: Entity Framework Core with Npgsql
- **Swagger Documentation**: API documentation
- **Service Extensions**: Modular service registration

### 🔄 Ready to Add
- **Authentication Module**: JWT-based auth (like NestJS AuthModule)
- **CORS Configuration**: Cross-origin resource sharing
- **Rate Limiting**: Request throttling
- **Caching**: Memory/Redis caching
- **Logging**: Structured logging with Serilog
- **Validation**: FluentValidation for complex scenarios
- **Testing**: Unit and integration tests

## 🛠️ Next Steps

### 1. Database Migration
```bash
# Create a new migration for the restructured model
dotnet ef migrations add UserModelRestructure

# Apply migrations to database
dotnet ef database update
```

### 2. Add PostgreSQL Connection
Update your `.env` file:
```
DB_CONNECTION_STRING=Host=localhost;Database=gameguild_cms;Username=postgres;Password=your_password
```

### 3. Test the API
- **REST API**: `https://localhost:5001/users`
- **GraphQL**: `https://localhost:5001/graphql`
- **Swagger**: `https://localhost:5001/swagger`

### 4. Add More Modules
Follow the User module pattern to add:
- Auth module (`Modules/Auth/`)
- Content module (`Modules/Content/`)
- Other feature modules

## 📝 Key Improvements

1. **Separation of Concerns**: Each module is self-contained
2. **Dependency Injection**: Proper service registration
3. **Type Safety**: Strong typing with DTOs and interfaces
4. **Error Handling**: Centralized exception management
5. **Documentation**: Auto-generated API docs
6. **Scalability**: Easy to add new modules and features

## 🎯 Benefits of This Structure

- **Familiar to NestJS developers**: Similar patterns and organization
- **Maintainable**: Clear separation of responsibilities
- **Testable**: Easy to unit test individual components
- **Scalable**: Can grow with your application needs
- **Best Practices**: Follows .NET Core conventions
