using cms.Data;
using cms.GraphQL;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables from .env file
Env.Load("../.env");

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "GameGuild CMS API", 
        Version = "v1",
        Description = "A Content Management System API for GameGuild"
    });
});

// Get connection string from environment variable
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") 
    ?? throw new InvalidOperationException("DB_CONNECTION_STRING environment variable is not set. Please check your .env file or environment configuration.");

// Add Entity Framework with dynamic provider selection based on connection string
if (connectionString.Contains("Data Source=") || connectionString.Contains("DataSource="))
{
    // SQLite connection string detected
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));
}
else
{
    // Assume PostgreSQL for other connection strings
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Add GraphQL services
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddType<UserType>();

var app = builder.Build();

// Automatically apply pending migrations and create database if it doesn't exist
using (var scope = app.Services.CreateScope())
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
    });
}

app.UseHttpsRedirection();

// Map GraphQL endpoint
app.MapGraphQL("/graphql");

app.MapControllers();

app.Run();