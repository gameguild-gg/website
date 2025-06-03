# Development Setup Instructions

## 🚀 Quick Setup

### 1. Install .NET EF Tools (if not already installed)
```bash
dotnet tool install --global dotnet-ef
```

### 2. Configure PostgreSQL Connection
Update the `.env` file in the `apps/cms` directory:
```env
DB_CONNECTION_STRING=Host=localhost;Database=gameguild_cms;Username=postgres;Password=your_actual_password
ASPNETCORE_ENVIRONMENT=Development
DOCUMENTATION_ENABLED=true
```

### 3. Create and Apply Database Migration
```bash
cd apps/cms/src
dotnet ef migrations add UpdatedUserModel
dotnet ef database update
```

### 4. Run the Application
```bash
cd apps/cms/src
dotnet run
```

### 5. Test Endpoints

#### REST API
- GET `/users` - Get all users
- POST `/users` - Create user
- GET `/users/{id}` - Get user by ID
- PUT `/users/{id}` - Update user
- DELETE `/users/{id}` - Delete user
- GET `/health` - Health check

#### GraphQL
- Endpoint: `/graphql`
- Queries: `users`, `userById(id: 1)`, `activeUsers`
- Mutations: `createUser`, `updateUser`, `deleteUser`

#### Documentation
- Swagger UI: `/swagger`

## 🏗️ Project Structure Benefits

### Modular Architecture
- Each feature is in its own module (`Modules/User/`, `Modules/Auth/`, etc.)
- Easy to add new features without affecting existing code
- Clear separation of concerns

### Service Layer Pattern
- Controllers are thin and delegate to services
- Services contain business logic
- Easy to unit test and mock dependencies

### DTO Pattern
- Clean separation between API contracts and domain models
- Input validation through data annotations
- Type-safe API responses

### Configuration Management
- Environment-based configuration
- Type-safe configuration classes
- Easy to add new configuration options

## 🔧 Adding New Modules

To add a new module (e.g., `Product`):

1. Create folder structure:
```
Modules/Product/
├── Controllers/ProductController.cs
├── Services/ProductService.cs
├── Dtos/ProductDtos.cs
├── Models/Product.cs
└── GraphQL/ProductQueries.cs
```

2. Add service registration in `ServiceCollectionExtensions.cs`:
```csharp
public static IServiceCollection AddProductModule(this IServiceCollection services)
{
    services.AddScoped<IProductService, ProductService>();
    return services;
}
```

3. Register in `Program.cs`:
```csharp
builder.Services.AddProductModule();
```

## 🧪 Testing

### Unit Tests Structure
```
tests/
├── Unit/
│   ├── Services/
│   │   └── UserServiceTests.cs
│   └── Controllers/
│       └── UsersControllerTests.cs
└── Integration/
    └── ApiTests.cs
```

### Example Test Setup
```csharp
// In UserServiceTests.cs
[Test]
public async Task GetAllUsersAsync_ShouldReturnUsers()
{
    // Arrange
    var context = CreateInMemoryContext();
    var service = new UserService(context);
    
    // Act
    var result = await service.GetAllUsersAsync();
    
    // Assert
    Assert.NotNull(result);
}
```

This structure provides a solid foundation that closely mirrors NestJS patterns while leveraging .NET Core best practices.
