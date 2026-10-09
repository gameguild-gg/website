using FluentAssertions;
using GameGuild.API.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace GameGuild.API.UnitTests.Database;

public sealed class DatabaseStartupConfigurationTests
{
    [Fact]
    public void Validate_RejectsMissingRuntimeConnectionInProduction()
    {
        var configuration = CreateConfiguration(null, MigrationConnection);

        var failures = DatabaseStartupConfiguration.Validate(configuration, Environments.Production);

        failures.Should().ContainSingle(message => message.Contains("runtime", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsMissingMigrationConnectionInProduction()
    {
        var configuration = CreateConfiguration(RuntimeConnection);

        var failures = DatabaseStartupConfiguration.Validate(configuration, Environments.Production);

        failures.Should().ContainSingle(message => message.Contains("migration", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsSharedRuntimeAndMigrationRoleByDefault()
    {
        var configuration = CreateConfiguration(RuntimeConnection, RuntimeConnection);

        var failures = DatabaseStartupConfiguration.Validate(configuration, Environments.Production);

        failures.Should().ContainSingle(message => message.Contains("distinct", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_AllowsSharedRoleOnlyWhenExplicitlyConfigured()
    {
        var configuration = CreateConfiguration(RuntimeConnection, RuntimeConnection, new Dictionary<string, string?>
        {
            ["Database:AllowSameMigrationUser"] = "true"
        });

        var failures = DatabaseStartupConfiguration.Validate(configuration, Environments.Production);

        failures.Should().BeEmpty();
    }

    [Fact]
    public void Validate_AllowsRuntimeConnectionAsMigrationConnectionWhenExplicitlyConfigured()
    {
        var configuration = CreateConfiguration(RuntimeConnection, values: new Dictionary<string, string?>
        {
            ["Database:AllowSameMigrationUser"] = "true"
        });

        DatabaseStartupConfiguration.Validate(configuration, Environments.Production).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Host=database;Database=game_guild;Password=runtime-secret", MigrationConnection)]
    [InlineData(RuntimeConnection, "Host=database;Database=game_guild;Password=migration-secret")]
    public void Validate_RejectsConnectionsWithoutDatabaseRoles(string runtimeConnection, string migrationConnection)
    {
        var configuration = CreateConfiguration(runtimeConnection, migrationConnection);

        DatabaseStartupConfiguration.Validate(configuration, Environments.Production)
            .Should().ContainSingle(message => message.Contains("identify their roles", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AcceptsDistinctRuntimeAndMigrationRolesInProduction()
    {
        var configuration = CreateConfiguration(RuntimeConnection, MigrationConnection);

        var failures = DatabaseStartupConfiguration.Validate(configuration, Environments.Production);

        failures.Should().BeEmpty();
    }

    [Fact]
    public void Validate_AcceptsPostgresMigrationConnectionEnvironmentAliasInProduction()
    {
        var configuration = CreateConfiguration(RuntimeConnection, values: new Dictionary<string, string?>
        {
            ["POSTGRES_MIGRATION_CONNECTION"] = MigrationConnection
        });

        var failures = DatabaseStartupConfiguration.Validate(configuration, Environments.Production);

        failures.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    [InlineData("Testing")]
    public void Validate_AllowsDevelopmentAndTestToUseRuntimeMigrationFallback(string environmentName)
    {
        var configuration = CreateConfiguration(RuntimeConnection);

        var failures = DatabaseStartupConfiguration.Validate(configuration, environmentName);

        failures.Should().BeEmpty();
    }

    [Fact]
    public void Validate_AllowsExternalMigrationWhenStartupInitializationIsDisabled()
    {
        var configuration = CreateConfiguration(null, values: new Dictionary<string, string?>
        {
            ["Database:RunStartupInitialization"] = "false"
        });

        var failures = DatabaseStartupConfiguration.Validate(configuration, Environments.Production);

        failures.Should().BeEmpty();
    }

    [Fact]
    public void Validate_RejectsMalformedConnections()
    {
        var configuration = CreateConfiguration("not-a-postgres-connection", "also-invalid");

        var failures = DatabaseStartupConfiguration.Validate(configuration, Environments.Production);

        failures.Should().ContainSingle(message => message.Contains("valid PostgreSQL", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsDefaultPostgresRuntimeCredentialsInProduction()
    {
        var configuration = CreateConfiguration(
            "Host=database;Database=app;Username=postgres;Password=postgres",
            MigrationConnection);

        var failures = DatabaseStartupConfiguration.Validate(configuration, Environments.Production);

        failures.Should().ContainSingle(message => message.Contains("default PostgreSQL credentials", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ThrowIfInvalid_RejectsUnsafeConfigurationAndAcceptsValidConfiguration()
    {
        var invalid = () => DatabaseStartupConfiguration.ThrowIfInvalid(
            CreateConfiguration(RuntimeConnection),
            Environments.Production);
        var valid = () => DatabaseStartupConfiguration.ThrowIfInvalid(
            CreateConfiguration(RuntimeConnection, MigrationConnection),
            Environments.Production);

        invalid.Should().Throw<InvalidOperationException>().WithMessage("*Unsafe database startup configuration*");
        valid.Should().NotThrow();
    }

    [Theory]
    [InlineData("Test")]
    [InlineData("Testing")]
    public void ShouldRunStartupInitialization_DefaultsToFalseForTestEnvironments(string environmentName)
    {
        var configuration = CreateConfiguration(RuntimeConnection);

        DatabaseStartupConfiguration.ShouldRunStartupInitialization(configuration, environmentName)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void ShouldRunStartupInitialization_DefaultsToTrueOutsideTestEnvironments(string environmentName)
    {
        var configuration = CreateConfiguration(RuntimeConnection);

        DatabaseStartupConfiguration.ShouldRunStartupInitialization(configuration, environmentName)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldRunStartupInitialization_HonorsExplicitTestOverride()
    {
        var configuration = CreateConfiguration(RuntimeConnection, values: new Dictionary<string, string?>
        {
            ["Database:RunStartupInitialization"] = "true"
        });

        DatabaseStartupConfiguration.ShouldRunStartupInitialization(configuration, "Testing")
            .Should().BeTrue();
    }

    [Fact]
    public void DesignTimeFactory_PrefersMigrationConnection()
    {
        var configuration = CreateConfiguration(RuntimeConnection, MigrationConnection);

        var resolved = DesignTimeDbContextFactory.ResolveConnectionString(configuration);

        resolved.Should().Contain("game_guild_migrator");
        resolved.Should().NotContain("game_guild_runtime");
    }

    private const string RuntimeConnection =
        "Host=database;Database=game_guild;Username=game_guild_runtime;Password=runtime-secret";

    private const string MigrationConnection =
        "Host=database;Database=game_guild;Username=game_guild_migrator;Password=migration-secret";

    private static IConfiguration CreateConfiguration(
        string? runtimeConnection,
        string? migrationConnection = null,
        IReadOnlyDictionary<string, string?>? values = null)
    {
        var settings = new Dictionary<string, string?>();
        if (runtimeConnection is not null)
        {
            settings["ConnectionStrings:DefaultConnection"] = runtimeConnection;
        }

        if (migrationConnection is not null)
        {
            settings["ConnectionStrings:MigrationConnection"] = migrationConnection;
        }

        if (values is not null)
        {
            foreach (var (key, value) in values)
            {
                settings[key] = value;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
