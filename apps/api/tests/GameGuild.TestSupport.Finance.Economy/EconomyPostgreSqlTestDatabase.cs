using Npgsql;
using Testcontainers.PostgreSql;

namespace GameGuild.TestSupport.Finance.Economy;

public sealed class EconomyPostgreSqlTestDatabase : IAsyncDisposable
{
    private static readonly SemaphoreSlim GateRoleBootstrapLock = new(1, 1);
    private static readonly SemaphoreSlim GateDatabaseLifecycleLock = new(1, 1);
    private static bool _gateRolesBootstrapped;

    private readonly string? _adminConnectionString;
    private readonly string? _databaseName;
    private readonly PostgreSqlContainer? _container;
    private readonly bool _dropDatabaseOnDispose;
    private readonly bool _usesGateDatabase;

    private EconomyPostgreSqlTestDatabase(
        string connectionString,
        string? adminConnectionString,
        string? databaseName,
        PostgreSqlContainer? container,
        bool dropDatabaseOnDispose,
        bool usesGateDatabase)
    {
        ConnectionString = connectionString;
        _adminConnectionString = adminConnectionString;
        _databaseName = databaseName;
        _container = container;
        _dropDatabaseOnDispose = dropDatabaseOnDispose;
        _usesGateDatabase = usesGateDatabase;
    }

    public string ConnectionString { get; }

    public static async Task<EconomyPostgreSqlTestDatabase> CreateAsync(
        string databasePrefix,
        CancellationToken cancellationToken = default)
    {
        var gateConnectionString = Environment.GetEnvironmentVariable("ECONOMY_POSTGRES_CONNECTION");

        if (!string.IsNullOrWhiteSpace(gateConnectionString))
        {
            return await CreateFromGateAsync(
                    gateConnectionString,
                    CreateGateDatabaseName(databasePrefix),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var databaseName = CreateDatabaseName(databasePrefix);

        var container = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase(databaseName)
            .WithUsername("test")
            .WithPassword("test")
            .WithCleanUp(true)
            .Build();
        await container.StartAsync(cancellationToken).ConfigureAwait(false);
        var databaseBuilder = new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Pooling = false };
        var adminBuilder = new NpgsqlConnectionStringBuilder(databaseBuilder.ConnectionString)
        {
            Database = "postgres",
            Pooling = false
        };

        return new EconomyPostgreSqlTestDatabase(
            databaseBuilder.ConnectionString,
            adminBuilder.ConnectionString,
            databaseBuilder.Database,
            container,
            false,
            false);
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_adminConnectionString) || string.IsNullOrWhiteSpace(_databaseName))
        {
            throw new InvalidOperationException("The PostgreSQL test database cannot be reset without an administrative connection.");
        }

        if (_usesGateDatabase)
        {
            await GateDatabaseLifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await ResetGateDatabaseSchemaAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                GateDatabaseLifecycleLock.Release();
            }
            return;
        }

        await ResetDatabaseAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
            return;
        }

        if (!_dropDatabaseOnDispose || string.IsNullOrWhiteSpace(_adminConnectionString) || string.IsNullOrWhiteSpace(_databaseName))
        {
            return;
        }

        if (_usesGateDatabase)
        {
            await GateDatabaseLifecycleLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await DropDatabaseAsync().ConfigureAwait(false);
            }
            finally
            {
                GateDatabaseLifecycleLock.Release();
            }
            return;
        }

        await DropDatabaseAsync().ConfigureAwait(false);
    }

    private static async Task<EconomyPostgreSqlTestDatabase> CreateFromGateAsync(
        string gateConnectionString,
        string databaseName,
        CancellationToken cancellationToken)
    {
        var configuredTemplateDatabase = Environment.GetEnvironmentVariable("ECONOMY_POSTGRES_TEMPLATE_DATABASE");
        var templateDatabase = string.IsNullOrWhiteSpace(configuredTemplateDatabase)
            ? null
            : ValidateDatabaseName(configuredTemplateDatabase, "ECONOMY_POSTGRES_TEMPLATE_DATABASE");
        var adminBuilder = new NpgsqlConnectionStringBuilder(gateConnectionString) { Pooling = false };
        // The disposable gate shares one PostgreSQL server across isolated test
        // databases. Schema reset can briefly wait behind concurrent migrations;
        // use an explicit administrative timeout instead of turning the complete
        // test assembly into a serial queue.
        adminBuilder.Timeout = 30;
        adminBuilder.CommandTimeout = 120;
        await GateDatabaseLifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new NpgsqlConnection(adminBuilder.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureGateRolesAsync(connection, cancellationToken).ConfigureAwait(false);
            await using var databaseExists = connection.CreateCommand();
            databaseExists.CommandText = "SELECT 1 FROM pg_database WHERE datname = @databaseName;";
            databaseExists.Parameters.AddWithValue("databaseName", databaseName);
            var exists = await databaseExists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
            if (!exists)
            {
                if (templateDatabase is not null)
                {
                    await using var templateExists = connection.CreateCommand();
                    templateExists.CommandText = "SELECT 1 FROM pg_database WHERE datname = @databaseName;";
                    templateExists.Parameters.AddWithValue("databaseName", templateDatabase);
                    if (await templateExists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
                    {
                        throw new InvalidOperationException(
                            $"The Economy PostgreSQL template database '{templateDatabase}' does not exist.");
                    }
                }
                await using var createDatabase = connection.CreateCommand();
                createDatabase.CommandText = templateDatabase is null
                    ? $"CREATE DATABASE \"{databaseName}\";"
                    : $"CREATE DATABASE \"{databaseName}\" WITH TEMPLATE \"{templateDatabase}\" STRATEGY WAL_LOG;";
                await createDatabase.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            GateDatabaseLifecycleLock.Release();
        }

        var databaseBuilder = new NpgsqlConnectionStringBuilder(adminBuilder.ConnectionString)
        {
            Database = databaseName,
            Pooling = false
        };
        return new EconomyPostgreSqlTestDatabase(
            databaseBuilder.ConnectionString,
            adminBuilder.ConnectionString,
            databaseName,
            null,
            true,
            true);
    }

    private async Task ResetDatabaseAsync(CancellationToken cancellationToken)
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(_adminConnectionString!);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await DropDatabaseAsync(connection, _databaseName!, cancellationToken).ConfigureAwait(false);
        await using var createDatabase = connection.CreateCommand();
        createDatabase.CommandText = $"CREATE DATABASE \"{_databaseName}\";";
        await createDatabase.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ResetGateDatabaseSchemaAsync(CancellationToken cancellationToken)
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            DO $schemas$
            DECLARE schema_name text;
            BEGIN
                FOR schema_name IN
                    SELECT nspname
                    FROM pg_namespace
                    WHERE nspname NOT IN ('pg_catalog', 'information_schema', 'public')
                      AND nspname NOT LIKE 'pg_%'
                LOOP
                    EXECUTE format('DROP SCHEMA IF EXISTS %I CASCADE', schema_name);
                END LOOP;
            END
            $schemas$;
            DROP SCHEMA public CASCADE;
            CREATE SCHEMA public;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task DropDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString!);
        await connection.OpenAsync().ConfigureAwait(false);
        await DropDatabaseAsync(connection, _databaseName!, CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task EnsureGateRolesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await GateRoleBootstrapLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_gateRolesBootstrapped)
            {
                return;
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                DO $roles$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'gameguild_economy_migration') THEN
                        CREATE ROLE gameguild_economy_migration NOLOGIN;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'gameguild_economy_runtime') THEN
                        CREATE ROLE gameguild_economy_runtime NOLOGIN;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'gameguild_economy_writer') THEN
                        CREATE ROLE gameguild_economy_writer NOLOGIN;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'gameguild_economy_procedure_owner') THEN
                        CREATE ROLE gameguild_economy_procedure_owner NOLOGIN;
                    END IF;
                END
                $roles$;
                """;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _gateRolesBootstrapped = true;
        }
        finally
        {
            GateRoleBootstrapLock.Release();
        }
    }

    private static string CreateDatabaseName(string databasePrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePrefix);
        var normalized = new string(databasePrefix
            .Where(character => char.IsAsciiLetterOrDigit(character) || character == '_')
            .Select(char.ToLowerInvariant)
            .ToArray());
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Database prefix must contain a letter, digit, or underscore.", nameof(databasePrefix));
        }

        var prefix = normalized[..Math.Min(normalized.Length, 22)];
        return $"economy_{prefix}_{Guid.NewGuid():N}";
    }

    private static string CreateGateDatabaseName(string databasePrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePrefix);
        var normalized = new string(databasePrefix
            .Where(character => char.IsAsciiLetterOrDigit(character) || character == '_')
            .Select(char.ToLowerInvariant)
            .ToArray());
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Database prefix must contain a letter, digit, or underscore.", nameof(databasePrefix));
        }

        var prefix = normalized[..Math.Min(normalized.Length, 22)];
        return $"economy_{prefix}_{Guid.NewGuid():N}";
    }

    private static string ValidateDatabaseName(string databaseName, string source)
    {
        var trimmed = databaseName.Trim();
        if (trimmed.Length is 0 or > 63 || trimmed.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new InvalidOperationException(
                $"{source} must contain only ASCII letters, digits, or underscores and be at most 63 characters.");
        }
        return trimmed;
    }

    private static async Task DropDatabaseAsync(
        NpgsqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var terminateConnections = connection.CreateCommand();
        terminateConnections.CommandText =
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @databaseName AND pid <> pg_backend_pid();";
        terminateConnections.Parameters.AddWithValue("databaseName", databaseName);
        await terminateConnections.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var dropDatabase = connection.CreateCommand();
        dropDatabase.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\";";
        await dropDatabase.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
