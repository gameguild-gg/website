using FluentAssertions;
using GameGuild.API;
using GameGuild.API.Database;
using GameGuild.API.UnitTests.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ProblemDetailsOptions = GameGuild.Configuration.PresentationLayer.ProblemDetails.ProblemDetailsOptions;

namespace GameGuild.API.UnitTests.Core;

public sealed class SchemaNotReadyProblemDetailsTests
{
    [Fact]
    public void IsDatabaseSchemaNotReadyException_ShouldDetectPostgresMissingRelationSqlState()
    {
        InfrastructureServiceCollectionExtensions
            .IsDatabaseSchemaNotReadyException(new FakePostgresException("42P01", "relation \"Users\" does not exist"))
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsDatabaseSchemaNotReadyException_ShouldDetectMissingRelationMessage()
    {
        InfrastructureServiceCollectionExtensions
            .IsDatabaseSchemaNotReadyException(new InvalidOperationException("relation \"TenantDomains\" does not exist"))
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsDatabaseSchemaNotReadyException_ShouldDetectNestedMissingRelation()
    {
        var exception = new InvalidOperationException(
            "Outer wrapper",
            new FakePostgresException("42P01", "relation \"AspNetUsers\" does not exist"));

        InfrastructureServiceCollectionExtensions
            .IsDatabaseSchemaNotReadyException(exception)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsDatabaseSchemaNotReadyException_ShouldIgnoreOtherExceptions()
    {
        InfrastructureServiceCollectionExtensions
            .IsDatabaseSchemaNotReadyException(new InvalidOperationException("Invalid credentials"))
            .Should()
            .BeFalse();
    }

    [Fact]
    public void IsDatabaseSchemaNotReadyException_WhenMessageMentionsOnlyRelation_ShouldReturnFalse()
    {
        InfrastructureServiceCollectionExtensions
            .IsDatabaseSchemaNotReadyException(new InvalidOperationException("relation is unavailable"))
            .Should()
            .BeFalse();
    }

    [Fact]
    public void IsDatabaseSchemaNotReadyException_WhenExceptionIsNull_ShouldThrow()
    {
        var act = () => InfrastructureServiceCollectionExtensions.IsDatabaseSchemaNotReadyException(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SetupProblemDetails_ShouldTranslateMissingSchemaFailuresToServiceUnavailable()
    {
        var services = new ServiceCollection();
        services.SetupProblemDetails(new ConfigurationBuilder().Build(), ProblemDetailsOptions.CreateDefault());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.ProblemDetailsOptions>>().Value;
        var httpContext = new DefaultHttpContext { TraceIdentifier = "trace-42" };
        httpContext.Request.Path = "/api/users";
        var context = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails(),
            Exception = new InvalidOperationException("relation users does not exist")
        };

        options.CustomizeProblemDetails!(context);

        context.ProblemDetails.Status.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.ProblemDetails.Type.Should().Be("urn:problem-type:database-schema-not-ready");
        context.ProblemDetails.Extensions["traceId"].Should().Be("trace-42");
    }

    [Fact]
    public async Task DatabaseReadinessHealthCheck_WithInMemoryDatabase_ShouldReportHealthy()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);

        var result = await new DatabaseReadinessHealthCheck(context)
            .CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data["pendingMigrations"].Should().Be(0);
    }

    [Fact]
    public async Task DatabaseReadinessHealthCheck_WithPendingRelationalMigration_ShouldReportDegradedThenHealthy()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .ReplaceService<IMigrationsAssembly, CoverageMigrationsAssembly>()
            .Options;
        await using var context = new ApplicationDbContext(options);
        var healthCheck = new DatabaseReadinessHealthCheck(context);

        var pending = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        await context.Database.MigrateAsync();
        var current = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        pending.Status.Should().Be(HealthStatus.Degraded);
        pending.Data["pendingMigrations"].Should().Be(1);
        current.Status.Should().Be(HealthStatus.Healthy);
        current.Data["appliedMigrations"].Should().Be(1);
    }

    [Fact]
    public async Task DatabaseReadinessHealthCheck_WhenDatabaseCannotBeOpened_ShouldReportUnreachable()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.db");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={path};Mode=ReadOnly")
            .Options;
        await using var context = new ApplicationDbContext(options);

        var result = await new DatabaseReadinessHealthCheck(context)
            .CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Application database is unreachable.");
    }

    [Fact]
    public async Task DatabaseReadinessHealthCheck_WhenCheckThrows_ShouldReportFailure()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);
        await context.DisposeAsync();

        var result = await new DatabaseReadinessHealthCheck(context)
            .CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Application database health check failed.");
        result.Exception.Should().BeOfType<ObjectDisposedException>();
    }

    private sealed class FakePostgresException(string sqlState, string message) : Exception(message)
    {
        public string SqlState { get; } = sqlState;
    }
}
