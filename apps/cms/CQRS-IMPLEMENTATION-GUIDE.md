# CQRS Implementation Guide

## Overview

This project implements the CQRS (Command Query Responsibility Segregation) pattern using **MediatR** library, providing clean separation between commands (writes) and queries (reads).

## Architecture

### Commands
Commands represent write operations that change the state of the system:
- `LocalSignUpCommand` - User registration
- `LocalSignInCommand` - User authentication
- `UpdateUserProfileCommand` - Profile updates
- `CreateUserCommand` - User creation

### Queries
Queries represent read operations that retrieve data:
- `GetUserByEmailQuery` - Find user by email
- `GetAllUsersQuery` - Get all users
- `GetUserProfileByUserIdQuery` - Get user profile

### Handlers
Each command and query has a corresponding handler:
- `LocalSignUpHandler` - Handles user registration
- `GetUserByEmailQueryHandler` - Handles user lookup
- `UpdateUserProfileHandler` - Handles profile updates

### Notifications
Domain events for side effects:
- `UserSignedUpNotification` - Published when user registers
- `UserProfileUpdatedNotification` - Published when profile changes

## Usage Examples

### REST API with CQRS

```csharp
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("signup")]
    public async Task<ActionResult<SignInResponseDto>> LocalSignUp([FromBody] LocalSignUpRequestDto request)
    {
        var command = new LocalSignUpCommand
        {
            Email = request.Email,
            Password = request.Password,
            Username = request.Username
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
```

### GraphQL with CQRS

```csharp
[ExtendObjectType<Mutation>]
public class AuthMutations
{
    public async Task<SignInResponseDto> LocalSignUp(
        LocalSignUpRequestDto input,
        [Service] IMediator mediator)
    {
        var command = new LocalSignUpCommand
        {
            Email = input.Email,
            Password = input.Password,
            Username = input.Username
        };

        return await mediator.Send(command);
    }
}
```

## Benefits

### 1. **Separation of Concerns**
- Commands handle business logic and validation
- Queries handle data retrieval optimization
- Clear separation between reads and writes

### 2. **Scalability**
- Different scaling strategies for reads vs writes
- Independent optimization of command and query sides
- Easier to implement read replicas

### 3. **Testability**
- Each handler can be unit tested independently
- Mock dependencies easily with interfaces
- Clear input/output contracts

### 4. **Maintainability**
- Single responsibility principle
- Easy to add new features without affecting existing code
- Clear code organization

## Pipeline Behaviors

### Logging Behavior
Automatically logs all requests and responses:

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {RequestName}", typeof(TRequest).Name);
        var response = await next();
        _logger.LogInformation("Handled {RequestName}", typeof(TRequest).Name);
        return response;
    }
}
```

### Validation Behavior
Validates requests using FluentValidation:

```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var failures = _validators
            .Select(v => v.Validate(request))
            .SelectMany(result => result.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}
```

## Testing Examples

### Command Handler Testing

```csharp
public class LocalSignUpHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_ReturnsSignInResponse()
    {
        // Arrange
        var command = new LocalSignUpCommand
        {
            Email = "test@example.com",
            Password = "password123",
            Username = "testuser"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test@example.com", result.User.Email);
    }
}
```

## Migration from Service Pattern

### Before (Service Pattern)
```csharp
public async Task<SignInResponseDto> LocalSignUp([FromBody] LocalSignUpRequestDto request)
{
    return await _authService.LocalSignUpAsync(request);
}
```

### After (CQRS Pattern)
```csharp
public async Task<SignInResponseDto> LocalSignUp([FromBody] LocalSignUpRequestDto request)
{
    var command = new LocalSignUpCommand
    {
        Email = request.Email,
        Password = request.Password,
        Username = request.Username
    };

    return await _mediator.Send(command);
}
```

## Best Practices

### 1. **Command Naming**
- Use imperative verbs: `CreateUser`, `UpdateProfile`, `DeletePost`
- Be specific about the action: `ActivateUser` vs `UpdateUser`

### 2. **Query Naming**
- Use descriptive names: `GetUserByEmail`, `GetActiveUsers`
- Include filtering criteria in the name when relevant

### 3. **Handler Organization**
- One handler per command/query
- Keep handlers focused and single-purpose
- Use dependency injection for services

### 4. **Validation**
- Validate in command handlers, not controllers
- Use domain-specific validation rules
- Throw meaningful exceptions

### 5. **Notifications**
- Use for cross-cutting concerns (logging, email, analytics)
- Keep notification handlers lightweight
- Don't fail the main operation if notification fails

## Performance Considerations

### 1. **Query Optimization**
- Use read-optimized models for queries
- Consider caching for frequently accessed data
- Use projection to return only needed fields

### 2. **Command Optimization**
- Validate early to avoid expensive operations
- Use transactions for data consistency
- Consider async processing for heavy operations

### 3. **Monitoring**
- Log command/query execution times
- Monitor handler performance
- Track business metrics

## Integration with Existing Code

The CQRS implementation is designed to coexist with existing service-based code:

1. **Gradual Migration**: Convert endpoints one by one
2. **Dual Support**: Both CQRS and service patterns can coexist
3. **Legacy Compatibility**: Existing services remain functional

Example showing both patterns:

```csharp
// CQRS approach
public async Task<IEnumerable<User>> GetUsers([Service] IMediator mediator)
{
    var query = new GetAllUsersQuery { IncludeDeleted = false };
    return await mediator.Send(query);
}

// Legacy service approach  
public async Task<IEnumerable<User>> GetUsersLegacy([Service] IUserService userService)
{
    return await userService.GetAllUsersAsync();
}
```

This allows for gradual adoption and testing of the CQRS pattern without breaking existing functionality.
