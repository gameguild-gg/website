# GameGuild SharedKernel Configuration

This directory contains shared configuration utilities and option classes that can be used across all GameGuild modules.

## Core Utilities

### IOptionBuilder<T>

Interface that defines the contract for option builders. While not directly implemented by static classes, it documents the expected method signatures.

### OptionBuilderUtilities

Static utility class with common configuration binding methods:

- `CreateAndBind<T>()` - Binds configuration to options with defaults
- `CreateBindAndValidate<T>()` - Binds with optional validation

### SharedConfigurationExtensions

Extension methods for `IServiceCollection`:

- `ConfigureOptions<T>()` - Configures options with automatic section detection
- `ConfigureOptionsFromSection<T>()` - Configures from specific section

### BaseOptions & ModuleOptions

Base classes for configuration options:

- `BaseOptions` - Provides common validation infrastructure
- `ModuleOptions` - Extends BaseOptions with module-specific features

## Presentation Layer Configurations

### Available Option Types

- `CorsOptions` + `CorsOptionsBuilder` - Cross-Origin Resource Sharing
- `HealthCheckOptions` - Health check configuration
- `PresentationLayerOptions` + `PresentationLayerOptionsBuilder` - Main presentation settings
- `HttpLoggingOptions` + `HttpLoggingOptionsBuilder` - HTTP request/response logging
- `AuthenticationOptions` - Authentication and JWT configuration
- `ApiVersioningOptions` - API versioning configuration
- `MemoryCachingOptions` - Memory caching configuration

External OAuth providers are configured under `Authentication:ExternalProviders:Providers`. Provider credentials are read from the application's configured secret store; the values below are placeholders:

```json
{
  "Authentication": {
    "ExternalProviders": {
      "Providers": {
        "microsoft": {
          "Enabled": true,
          "ClientId": "<client-id>",
          "ClientSecret": "<secret-store-reference>",
          "Tenant": "organizations",
          "Scopes": [ "openid", "email", "profile" ]
        }
      }
    }
  }
}
```

Supported provider keys are `discord`, `github`, `google`, and `microsoft`. Disabled providers are not used; enabled providers require a client ID and secret. Empty scopes use provider defaults. Endpoint overrides are optional, but if used, authorization, token, and user-information endpoints must all be absolute HTTPS URLs. Existing `OAuth:<Provider>:ClientId` and `OAuth:<Provider>:ClientSecret` configuration remains a fallback for compatibility.

When using a custom GitHub API endpoint and the profile does not expose an email, set `UserEmailEndpoint` to that deployment's email-list endpoint. Without it, GameGuild skips the extra email lookup so the access token is never sent to public GitHub on behalf of a custom endpoint.

## Infrastructure Configurations

### Available Option Types

- `DatabaseOptions` - Database connection and EF Core settings
- `CachingOptions` - Memory and distributed caching configuration
- `ExternalApiOptions` - External API integration and HTTP client settings
- `FileStorageOptions` + `FileStorageProvider` enum - File storage configuration (Local, Azure Blob, S3, Google Cloud)
- `MessageQueueOptions` - Message queue services configuration (RabbitMQ, Azure Service Bus)
- `MonitoringOptions` - Application monitoring, logging, and health checks
- `InfrastructureLayerOptions` - Main infrastructure layer configuration that combines all above options

## Application Layer Configurations

### Available Option Types

- `ApplicationLayerOptions` - Main application layer configuration with MediatR, AutoMapper, FluentValidation
- `BackgroundServiceOptions` - Background task and service configuration

## Usage Examples

### Security Headers

The API binds `PresentationLayer:SecurityHeaders` and applies those settings in the request pipeline. Defaults enable
`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, Content Security Policy, Permissions Policy, and
no-cache headers for sensitive routes. The API keeps separate policies for regular responses and Swagger pages.

```json
{
  "PresentationLayer": {
    "SecurityHeaders": {
      "EnableXFrameOptions": true,
      "XFrameOptionsValue": "DENY",
      "EnableContentSecurityPolicy": true,
      "ContentSecurityPolicyValue": "default-src 'none'; frame-ancestors 'none'"
    }
  }
}
```

Custom header values are validated before the middleware is configured and must not contain line breaks.

### Authentication Password Policy

Local account registration uses the typed password policy under `PresentationLayer:Authentication:PasswordPolicy`.
Minimum and maximum lengths and BCrypt generation cost (10–16, default 12) are validated at startup. For compatibility, `Authentication:PasswordPolicy` and the
older top-level `PasswordPolicy` keys remain supported as fallbacks by the password hasher.

```json
{
  "PresentationLayer": {
    "Authentication": {
      "PasswordPolicy": {
        "MinPasswordLength": 8,
        "MaxPasswordLength": 128,
        "BCryptWorkFactor": 12,
        "RequireUppercase": true,
        "RequireLowercase": true,
        "RequireDigit": true,
        "RequireSpecialChar": true
      }
    }
  }
}
```

### Optional Cookie Authentication

Cookie authentication is disabled by default and is registered as a named scheme without changing JWT bearer as the
default. Cookie settings are configured under `PresentationLayer:Authentication:Cookie`; the server enforces
`HttpOnly`, HTTPS-only transmission, `Path=/`, and no cookie domain. Authentication failures return 401/403 instead
of redirecting API clients to HTML pages.

```json
{
  "PresentationLayer": {
    "Authentication": {
      "EnableCookieAuthentication": true,
      "Cookie": {
        "SchemeName": "GameGuildCookie",
        "Name": "__Host-GameGuild.Auth",
        "Expiration": "08:00:00",
        "SlidingExpiration": false,
        "SameSite": "Strict"
      }
    }
  }
}
```

The cookie scheme is opt-in. Use HTTPS for the application host before enabling it.

### Optional HTTP Basic Authentication

HTTP Basic authentication is disabled by default and registers a named scheme without replacing JWT bearer as the
default. It accepts local email or username credentials, requires HTTPS on every request, and refuses accounts with
MFA enabled because the Basic protocol cannot complete a second-factor challenge. Protect endpoints that select this
scheme with the configured authentication rate-limit policy; the Basic handler itself does not provide account
lockout.

```json
{
  "PresentationLayer": {
    "Authentication": {
      "EnableBasicAuthentication": true,
      "Basic": {
        "SchemeName": "LegacyBasic",
        "Realm": "GameGuild Legacy API"
      }
    }
  }
}
```

The scheme is intended only for integrations that require Basic credentials. Keep it disabled unless a protected
endpoint explicitly selects it, and enable it only after HTTPS and the authentication rate-limit policy are active.

### Basic Module Configuration

```csharp
using GameGuild.Configuration;

public class MyModuleOptions : ModuleOptions
{
    public string ConnectionString { get; set; } = "";
    public int MaxRetries { get; set; } = 3;
    
    public override void Validate()
    {
        base.Validate();
        if (string.IsNullOrEmpty(ConnectionString))
            throw new InvalidOperationException("ConnectionString is required");
    }
}

// In DependencyInjection:
services.ConfigureOptions<MyModuleOptions>(
    configuration,
    () => new MyModuleOptions(),
    options => options.Validate()
);
```

### Using Specific Builders

```csharp
// CORS configuration
var corsOptions = CorsOptionsBuilder.Create(configuration);
services.AddSingleton(corsOptions);

// Presentation layer
var presentationOptions = PresentationLayerOptionsBuilder.CreateWithValidation(configuration);
```

## Benefits

1. **Consistency** - All modules use the same configuration patterns
2. **Reusability** - Common configuration types shared across modules
3. **Validation** - Built-in validation infrastructure
4. **Type Safety** - Strongly-typed configuration with compile-time checking
5. **Testability** - Easy to mock and test configuration
