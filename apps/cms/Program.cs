using GameGuild.Data;
using GameGuild.Modules.User.GraphQL;
using GameGuild.Common.Extensions;
using GameGuild.Common.Middleware;
using GameGuild.Common.Transformers;
using GameGuild.Modules.Auth.Configuration;
using GameGuild.Config;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using MediatR;

// <-- Added using directive for UserProfile module

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Load environment variables from .env file
Env.Load();

// Add configuration services (similar to NestJS ConfigModule)
builder.Services.AddAppConfiguration(builder.Configuration);

// Configure CORS options from appsettings
CorsOptions corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

// Add CORS services
builder.Services.AddCors(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Allow all origins in development for easier testing
            options.AddPolicy(
                "Development",
                policy =>
                {
                    policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                }
            );
        }
        else
        {
            // Use configured origins in production
            options.AddPolicy(
                "Production",
                policy =>
                {
                    policy.WithOrigins(corsOptions.AllowedOrigins)
                        .WithMethods(corsOptions.AllowedMethods)
                        .WithHeaders(corsOptions.AllowedHeaders);

                    if (corsOptions.AllowCredentials)
                    {
                        policy.AllowCredentials();
                    }
                }
            );
        }

        // Default policy for specific origins (can be used in development too)
        options.AddPolicy(
            "Configured",
            policy =>
            {
                policy.WithOrigins(corsOptions.AllowedOrigins)
                    .WithMethods(corsOptions.AllowedMethods)
                    .WithHeaders(corsOptions.AllowedHeaders);

                if (corsOptions.AllowCredentials)
                {
                    policy.AllowCredentials();
                }
            }
        );
    }
);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddControllers(opts =>
    opts.Conventions.Add(new RouteTokenTransformerConvention(new ToKebabParameterTransformer()))
).AddAuthFilters(); // Add authentication filters

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc(
            "v1",
            new OpenApiInfo
            {
                Title = "GameGuild CMS API", Version = "v1", Description = "A Content Management System API for GameGuild"
            }
        );
    }
);

// Add common services and modules (following NestJS module pattern)
builder.Services.AddCommonServices();
builder.Services.AddUserModule();
builder.Services.AddTenantModule();
builder.Services.AddUserProfileModule(); // Register the UserProfile module
builder.Services.AddAuthModule(builder.Configuration); // Register the Auth module

// Add MediatR for CQRS
builder.Services.AddMediatR(typeof(Program));

// Add MediatR pipeline behaviors
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(GameGuild.Common.Behaviors.LoggingBehavior<,>));

// Get connection string from environment
string connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") ??
                          throw new InvalidOperationException("DB_CONNECTION_STRING environment variable is not set. Please check your .env file or environment configuration.");

// Check if we should use in-memory database (for tests)
bool useInMemoryDb = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("USE_IN_MEMORY_DB")) && Environment.GetEnvironmentVariable("USE_IN_MEMORY_DB")!.Equals("true", StringComparison.OrdinalIgnoreCase);

// Add Entity Framework with appropriate provider
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        if (useInMemoryDb)
        {
            // Use InMemory for tests
            options.UseInMemoryDatabase("TestDatabase_" + Guid.NewGuid().ToString());
        }
        else
        {
            // Use SQLite for regular development
            options.UseSqlite(connectionString);
        }

        // Enable sensitive data logging in development
        if (!builder.Environment.IsDevelopment()) return;
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
);

// Add GraphQL services
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddTypeExtension<GameGuild.Modules.Tenant.GraphQL.TenantQueries>()
    .AddTypeExtension<GameGuild.Modules.Tenant.GraphQL.TenantMutations>()
    .AddTypeExtension<GameGuild.Modules.UserProfile.GraphQL.UserProfileQueries>()
    .AddTypeExtension<GameGuild.Modules.UserProfile.GraphQL.UserProfileMutations>()
    .AddTypeExtension<GameGuild.Modules.Auth.GraphQL.AuthQueries>()
    .AddTypeExtension<GameGuild.Modules.Auth.GraphQL.AuthMutations>()
    .AddType<UserType>()
    .AddType<CredentialType>()
    .AddType<GameGuild.Modules.Tenant.GraphQL.TenantType>()
    .AddType<GameGuild.Modules.Tenant.GraphQL.TenantPermissionType>()
    .AddType<GameGuild.Modules.UserProfile.GraphQL.UserProfileType>()
    .ModifyRequestOptions(opt => opt.IncludeExceptionDetails = builder.Environment.IsDevelopment());

WebApplication app = builder.Build();

// Add exception handling middleware (similar to NestJS global filters)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Automatically apply pending migrations and create a database if it doesn't exist
using (IServiceScope scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // Only run migrations on relational databases (not on InMemory)
        if (!context.Database.IsInMemory())
        {
            logger.LogInformation("Applying database migrations...");
            context.Database.Migrate();
            logger.LogInformation("Database migrations applied successfully");
        }
        else
        {
            logger.LogInformation("Using in-memory database, skipping migrations");
            // Create database schema for InMemory database
            context.Database.EnsureCreated();
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while applying database migrations");

        throw; // Rethrow to fail startup if migrations fail
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "GameGuild CMS API v1");
            c.RoutePrefix = "swagger"; // Serve Swagger UI at /swagger
        }
    );
}

app.UseHttpsRedirection();

// Add CORS middleware (must be before authentication and authorization)
if (builder.Environment.IsDevelopment())
{
    app.UseCors("Development");
}
else
{
    app.UseCors("Production");
}

// Add authentication middleware
app.UseAuthModule();

// Map GraphQL endpoint
app.MapGraphQL("/graphql");

app.MapControllers();

app.Run();

// Make Program class accessible for testing
public partial class Program { }
