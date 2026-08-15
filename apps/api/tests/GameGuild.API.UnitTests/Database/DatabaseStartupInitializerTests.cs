using FluentAssertions;
using GameGuild.API.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using System.Reflection;
using Npgsql;
using Xunit;

namespace GameGuild.API.UnitTests.Database;

public sealed class DatabaseStartupInitializerTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(20, 30)]
    public void CalculateRetryDelay_UsesLinearBackoffWithConfiguredCeiling(int attempt, int expectedSeconds)
    {
        DatabaseStartupInitializer.CalculateRetryDelay(attempt, 2, 30)
            .Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Theory]
    [InlineData("runtime_user", "\"runtime_user\"")]
    [InlineData("runtime\"user", "\"runtime\"\"user\"")]
    public void QuotePostgresIdentifier_EscapesEmbeddedQuotes(string value, string expected)
    {
        DatabaseStartupInitializer.QuotePostgresIdentifier(value).Should().Be(expected);
    }

    [Fact]
    public void ResolveMigrationOptions_ClampsUnsafeValues()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:MigrationMaxAttempts"] = "999",
            ["Database:MigrationRetryDelaySeconds"] = "0",
            ["Database:MigrationMaxRetryDelaySeconds"] = "999"
        }).Build();

        var options = DatabaseStartupInitializer.ResolveMigrationOptions(configuration);

        options.MaxAttempts.Should().Be(20);
        options.RetryDelaySeconds.Should().Be(1);
        options.MaxRetryDelaySeconds.Should().Be(120);
    }

    [Theory]
    [InlineData("Host=localhost;Database=app;Username=migrator;Password=secret")]
    [InlineData("")]
    public void CreateMigrationDbContext_ConfiguresNpgsqlWithoutOpeningAConnection(string connectionString)
    {
        var method = typeof(DatabaseStartupInitializer).GetMethod(
            "CreateMigrationDbContext",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        using var context = method!.Invoke(null, [connectionString])
            .Should().BeOfType<ApplicationDbContext>().Subject;
        context.Database.ProviderName.Should().Be("Npgsql.EntityFrameworkCore.PostgreSQL");
    }

    [Fact]
    public async Task InitializeAsync_WhenStartupInitializationIsDisabled_ShouldSkipDatabaseAndSeed()
    {
        var seedCalls = 0;
        await using var app = CreateApp(new Dictionary<string, string?>
        {
            ["Database:RunStartupInitialization"] = "false"
        });

        var result = await DatabaseStartupInitializer.InitializeAsync(app, (_, _) =>
        {
            seedCalls++;
            return Task.CompletedTask;
        });

        result.Should().BeTrue();
        seedCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializeAsync_WhenSeedFails_ShouldHonorFailClosedSetting(bool failClosed)
    {
        await using var app = CreateApp(
            new Dictionary<string, string?>
            {
                ["Database:RunStartupInitialization"] = "true",
                ["Database:FailStartupOnSeedFailure"] = failClosed.ToString()
            },
            services => services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString())));

        Func<Task<bool>> act = () => DatabaseStartupInitializer.InitializeAsync(
            app,
            (_, _) => throw new InvalidOperationException("seed failed"));

        if (failClosed)
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("seed failed");
        else
            (await act()).Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenSeedFailSettingIsOmitted_ShouldUseEnvironmentFallback()
    {
        await using var app = CreateApp(
            new Dictionary<string, string?>
            {
                ["Database:RunStartupInitialization"] = "true"
            },
            services => services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString())));

        var result = await DatabaseStartupInitializer.InitializeAsync(
            app,
            (_, _) => throw new InvalidOperationException("seed failed"));

        result.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WithPendingSqliteMigration_ShouldApplyAndRemainIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var seedCalls = 0;
        await using var app = CreateApp(
            new Dictionary<string, string?>
            {
                ["Database:RunStartupInitialization"] = "true"
            },
            services => services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(connection);
                options.ReplaceService<IMigrationsAssembly, CoverageMigrationsAssembly>();
            }));

        Task Seed(IServiceProvider _, CancellationToken __)
        {
            seedCalls++;
            return Task.CompletedTask;
        }

        (await DatabaseStartupInitializer.InitializeAsync(app, Seed)).Should().BeTrue();
        (await DatabaseStartupInitializer.InitializeAsync(app, Seed)).Should().BeTrue();

        seedCalls.Should().Be(2);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'StartupCoverage';";
        Convert.ToInt32(await command.ExecuteScalarAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ApplyMigrationsAsync_WithSeparateMigrationConnection_ShouldUseProvidedContextFactory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var app = CreateApp(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MigrationConnection"] = MigrationConnection,
            ["Database:GrantRuntimeRoleAfterMigrations"] = "false",
            ["Database:FailStartupOnMigrationFailure"] = "true",
            ["Database:MigrationMaxAttempts"] = "1"
        });
        var factoryCalls = 0;

        ApplicationDbContext CreateContext(string connectionString)
        {
            new NpgsqlConnectionStringBuilder(connectionString).Username.Should().Be("migration_user");
            factoryCalls++;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .ReplaceService<IMigrationsAssembly, CoverageMigrationsAssembly>()
                .Options;
            return new ApplicationDbContext(options);
        }

        var result = await DatabaseStartupInitializer.ApplyMigrationsAsync(app, CreateContext);

        result.Should().BeTrue();
        factoryCalls.Should().Be(1);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'StartupCoverage';";
        Convert.ToInt32(await command.ExecuteScalarAsync()).Should().Be(1);
    }

    [Fact]
    public async Task InitializeAsync_WhenMigrationKeepsFailing_ShouldRetryAndReturnFalseInTesting()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"startup-{Guid.NewGuid():N}"));
        try
        {
            await using var app = CreateApp(
                new Dictionary<string, string?>
                {
                    ["Database:RunStartupInitialization"] = "true",
                    ["Database:MigrationMaxAttempts"] = "2",
                    ["Database:MigrationRetryDelaySeconds"] = "1",
                    ["Database:MigrationMaxRetryDelaySeconds"] = "1",
                    ["Database:FailStartupOnMigrationFailure"] = "false"
                },
                services => services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseSqlite($"Data Source={directory.FullName}");
                    options.ReplaceService<IMigrationsAssembly, CoverageMigrationsAssembly>();
                }));

            var result = await DatabaseStartupInitializer.InitializeAsync(app, (_, _) => Task.CompletedTask);

            result.Should().BeFalse();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task InitializeAsync_WhenMigrationFailsInFailClosedMode_ShouldThrow()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"startup-{Guid.NewGuid():N}"));
        try
        {
            await using var app = CreateApp(
                new Dictionary<string, string?>
                {
                    ["Database:RunStartupInitialization"] = "true",
                    ["Database:MigrationMaxAttempts"] = "1",
                    ["Database:FailStartupOnMigrationFailure"] = "true"
                },
                services => services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseSqlite($"Data Source={directory.FullName}");
                    options.ReplaceService<IMigrationsAssembly, CoverageMigrationsAssembly>();
                }));

            Func<Task> act = () => DatabaseStartupInitializer.InitializeAsync(app, (_, _) => Task.CompletedTask);

            await act.Should().ThrowAsync<SqliteException>();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(false, null, null)]
    [InlineData(true, null, null)]
    [InlineData(true, RuntimeConnection, RuntimeConnection)]
    [InlineData(true, RuntimeWithoutUser, MigrationConnection)]
    public async Task GrantRuntimeRolePrivilegesAsync_ShouldSkipWhenGrantIsNotApplicable(
        bool enabled,
        string? runtimeConnection,
        string? migrationConnection)
    {
        await using var app = CreateApp(new Dictionary<string, string?>
        {
            ["Database:GrantRuntimeRoleAfterMigrations"] = enabled.ToString(),
            ["ConnectionStrings:DefaultConnection"] = runtimeConnection,
            ["ConnectionStrings:MigrationConnection"] = migrationConnection
        });
        await using var db = CreateGrantContext(new SuppressGrantInterceptor());

        Func<Task> act = () => InvokeGrantRuntimeRolePrivilegesAsync(app, db);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GrantRuntimeRolePrivilegesAsync_ShouldGrantEveryDiscoveredSchema()
    {
        var interceptor = new SuppressGrantInterceptor();
        await using var app = CreateApp(CreateGrantConfiguration(failClosed: false));
        await using var db = CreateGrantContext(interceptor);

        await InvokeGrantRuntimeRolePrivilegesAsync(app, db);

        interceptor.CommandCount.Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GrantRuntimeRolePrivilegesAsync_WhenGrantFails_ShouldHonorFailClosedSetting(bool failClosed)
    {
        await using var app = CreateApp(CreateGrantConfiguration(failClosed));
        await using var db = CreateGrantContext(new ThrowingGrantInterceptor());

        Func<Task> act = () => InvokeGrantRuntimeRolePrivilegesAsync(app, db);

        if (failClosed)
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("grant failed");
        else
            await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GrantRuntimeRolePrivilegesAsync_WhenFailSettingIsOmitted_ShouldUseEnvironmentFallback()
    {
        var configuration = CreateGrantConfiguration(failClosed: false);
        configuration.Remove("Database:FailStartupOnGrantFailure");
        await using var app = CreateApp(configuration);
        await using var db = CreateGrantContext(new ThrowingGrantInterceptor());

        var action = () => InvokeGrantRuntimeRolePrivilegesAsync(app, db);

        await action.Should().NotThrowAsync();
    }

    private static WebApplication CreateApp(
        IReadOnlyDictionary<string, string?> settings,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ApplicationName = typeof(DatabaseStartupInitializer).Assembly.FullName
        });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddSingleton<DatabaseConnectivityProbe>();
        configureServices?.Invoke(builder.Services);
        return builder.Build();
    }

    private static GrantCoverageDbContext CreateGrantContext(DbCommandInterceptor interceptor)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<GrantCoverageDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        return new GrantCoverageDbContext(options, connection);
    }

    private static Dictionary<string, string?> CreateGrantConfiguration(bool failClosed) => new()
    {
        ["Database:GrantRuntimeRoleAfterMigrations"] = "true",
        ["Database:FailStartupOnGrantFailure"] = failClosed.ToString(),
        ["ConnectionStrings:DefaultConnection"] = RuntimeConnection,
        ["ConnectionStrings:MigrationConnection"] = MigrationConnection
    };

    private static Task InvokeGrantRuntimeRolePrivilegesAsync(WebApplication app, DbContext db)
    {
        var method = typeof(DatabaseStartupInitializer).GetMethod(
            "GrantRuntimeRolePrivilegesAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        return method!.Invoke(null, [app, db]).Should().BeAssignableTo<Task>().Subject;
    }

    private const string RuntimeConnection =
        "Host=database;Database=app;Username=runtime_user;Password=runtime-secret";
    private const string RuntimeWithoutUser =
        "Host=database;Database=app;Username=;Password=runtime-secret";
    private const string MigrationConnection =
        "Host=database;Database=app;Username=migration_user;Password=migration-secret";

    private sealed class GrantCoverageDbContext(
        DbContextOptions<GrantCoverageDbContext> options,
        SqliteConnection connection) : DbContext(options)
    {
        public override void Dispose()
        {
            base.Dispose();
            connection.Dispose();
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await connection.DisposeAsync();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PublicGrantEntity>().ToTable("PublicRows");
            modelBuilder.Entity<AuditGrantEntity>().ToTable("AuditRows", "audit");
        }
    }

    private sealed class PublicGrantEntity
    {
        public int Id { get; set; }
    }

    private sealed class AuditGrantEntity
    {
        public int Id { get; set; }
    }

    private sealed class SuppressGrantInterceptor : DbCommandInterceptor
    {
        public int CommandCount { get; private set; }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CommandCount++;
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
        }
    }

    private sealed class ThrowingGrantInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("grant failed");
    }
}

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608150001_CoverageStartup")]
public sealed class CoverageStartupMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            "StartupCoverage",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true)
            },
            constraints: table => table.PrimaryKey("PK_StartupCoverage", value => value.Id));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("StartupCoverage");
}

public sealed class CoverageMigrationsAssembly : IMigrationsAssembly
{
    public IReadOnlyDictionary<string, TypeInfo> Migrations { get; } =
        new Dictionary<string, TypeInfo>
        {
            ["202608150001_CoverageStartup"] = typeof(CoverageStartupMigration).GetTypeInfo()
        };

    public ModelSnapshot? ModelSnapshot => null;

    public Assembly Assembly => typeof(CoverageMigrationsAssembly).Assembly;

    public string? FindMigrationId(string nameOrId) => Migrations.Keys.FirstOrDefault(
        migrationId => migrationId.Equals(nameOrId, StringComparison.OrdinalIgnoreCase));

    public Migration CreateMigration(TypeInfo migrationClass, string activeProvider)
    {
        var migration = (Migration)Activator.CreateInstance(migrationClass.AsType())!;
        migration.ActiveProvider = activeProvider;
        return migration;
    }
}
