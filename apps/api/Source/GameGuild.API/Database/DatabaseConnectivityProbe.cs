using System.Data.Common;
using Npgsql;

namespace GameGuild.API.Database;

public sealed class DatabaseConnectivityProbe
{
    private static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromSeconds(1);
    private readonly IConfiguration _configuration;
    private readonly Func<string, DbConnection> _connectionFactory;
    private readonly TimeSpan _probeTimeout;

    public DatabaseConnectivityProbe(IConfiguration configuration)
        : this(configuration, static connectionString => new NpgsqlConnection(connectionString), DefaultProbeTimeout)
    {
    }

    internal DatabaseConnectivityProbe(
        IConfiguration configuration,
        Func<string, DbConnection> connectionFactory,
        TimeSpan probeTimeout)
    {
        _configuration = configuration;
        _connectionFactory = connectionFactory;
        _probeTimeout = probeTimeout;
    }

    public async Task<bool> IsReachableAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = PostgresConnectionString.Resolve(_configuration);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        NpgsqlConnectionStringBuilder connectionStringBuilder;
        try
        {
            connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(connectionStringBuilder.Host) || connectionStringBuilder.Port <= 0)
        {
            return false;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_probeTimeout);

        try
        {
            connectionStringBuilder.Timeout = Math.Max(1, (int)Math.Ceiling(_probeTimeout.TotalSeconds));
            connectionStringBuilder.CommandTimeout = Math.Max(1, (int)Math.Ceiling(_probeTimeout.TotalSeconds));

            await OpenConnectionAsync(connectionStringBuilder.ConnectionString, timeoutCts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (NpgsqlException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private async Task OpenConnectionAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    }
}
