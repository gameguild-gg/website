using cms.Data;
using cms.Modules.User.GraphQL;
using cms.Common.Extensions;
using cms.Common.Middleware;
using cms.Config;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using Microsoft.OpenApi.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Load environment variables from .env file
Env.Load("../.env");

// Add configuration services (similar to NestJS ConfigModule)
builder.Services.AddAppConfiguration(builder.Configuration);

// Load environment variables from .env file
Env.Load("../.env");

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddControllers();

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

// Get database configuration
var dbConfig = builder.Services.BuildServiceProvider().GetRequiredService<DatabaseConfig>();
string connectionString = dbConfig.ConnectionString;

if (string.IsNullOrEmpty(connectionString))
{
    connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
                      ?? throw new InvalidOperationException("DB_CONNECTION_STRING environment variable is not set. Please check your .env file or environment configuration.");
}

// Add Entity Framework with PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    
    if (dbConfig.EnableSensitiveDataLogging)
        options.EnableSensitiveDataLogging();
        
    if (dbConfig.EnableDetailedErrors)
        options.EnableDetailedErrors();
});

// Add GraphQL services
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddType<UserType>();

WebApplication app = builder.Build();

// Add exception handling middleware (similar to NestJS global filters)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Automatically apply pending migrations and create a database if it doesn't exist
using (IServiceScope scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();
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

// Map GraphQL endpoint
app.MapGraphQL("/graphql");

app.MapControllers();

app.Run();
