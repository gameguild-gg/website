using cms.Data;
using cms.Modules.User.GraphQL;
using cms.Common.Extensions;
using cms.Common.Middleware;
using cms.Common.Transformers;
using cms.Modules.Auth.Configuration;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

// <-- Added using directive for UserProfile module

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Load environment variables from .env file
Env.Load();

// Add configuration services (similar to NestJS ConfigModule)
builder.Services.AddAppConfiguration(builder.Configuration);

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

// Get connection string from environment
string connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
                          ?? throw new InvalidOperationException("DB_CONNECTION_STRING environment variable is not set. Please check your .env file or environment configuration.");

// Add Entity Framework with SQLite for development
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseSqlite(connectionString);

        // Enable sensitive data logging in development
        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }
    }
);

// Add GraphQL services
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddTypeExtension<cms.Modules.Tenant.GraphQL.TenantQueries>()
    .AddTypeExtension<cms.Modules.Tenant.GraphQL.TenantMutations>()
    .AddTypeExtension<cms.Modules.UserProfile.GraphQL.UserProfileQueries>()
    .AddTypeExtension<cms.Modules.UserProfile.GraphQL.UserProfileMutations>()
    .AddType<UserType>()
    .AddType<CredentialType>()
    .AddType<cms.Modules.Tenant.GraphQL.TenantType>()
    .AddType<cms.Modules.Tenant.GraphQL.TenantRoleType>()
    .AddType<cms.Modules.Tenant.GraphQL.UserTenantType>()
    .AddType<cms.Modules.Tenant.GraphQL.UserTenantRoleType>()
    .AddType<cms.Modules.UserProfile.GraphQL.UserProfileType>();

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
        logger.LogInformation("Applying database migrations...");
        context.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully");
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

// Add authentication middleware
app.UseAuthModule();

// Map GraphQL endpoint
app.MapGraphQL("/graphql");

app.MapControllers();

app.Run();
