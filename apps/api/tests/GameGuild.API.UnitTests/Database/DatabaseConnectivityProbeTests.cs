using System.Data.Common;
using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using GameGuild.API.Database;
using Moq;
using Npgsql;

namespace GameGuild.API.UnitTests.Database;

public class DatabaseConnectivityProbeTests
{
    [Fact]
    public async Task IsReachableAsync_ShouldReturnFalse_WhenConnectionStringIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var probe = new DatabaseConnectivityProbe(configuration);

        var result = await probe.IsReachableAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsReachableAsync_ShouldReturnFalse_WhenConnectionStringIsInvalid()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "not-a-valid-connection-string"
            })
            .Build();
        var probe = new DatabaseConnectivityProbe(configuration);

        var result = await probe.IsReachableAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsReachableAsync_ShouldReturnFalse_WhenHostIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Database=test;Username=test;Password=test;Port=5432"
            })
            .Build();
        var probe = new DatabaseConnectivityProbe(configuration);

        var result = await probe.IsReachableAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsReachableAsync_ShouldReturnTrue_WhenConnectionOpens()
    {
        string? openedConnectionString = null;
        var connection = CreateConnectionMock();
        var probe = new DatabaseConnectivityProbe(
            CreateValidConfiguration(),
            connectionString =>
            {
                openedConnectionString = connectionString;
                return connection.Object;
            },
            TimeSpan.FromMilliseconds(100));

        var result = await probe.IsReachableAsync();

        result.Should().BeTrue();
        openedConnectionString.Should().Contain("Host=127.0.0.1");
        connection.Verify(candidate => candidate.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task IsReachableAsync_ShouldReturnFalse_WhenProviderRejectsConnection()
    {
        var connection = CreateConnectionMock(_ => Task.FromException(new NpgsqlException("offline")));
        var probe = new DatabaseConnectivityProbe(
            CreateValidConfiguration(),
            _ => connection.Object,
            TimeSpan.FromMilliseconds(100));

        var result = await probe.IsReachableAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsReachableAsync_ShouldReturnFalse_WhenOpeningRejectsConnectionString()
    {
        var probe = new DatabaseConnectivityProbe(
            CreateValidConfiguration(),
            _ => throw new ArgumentException("invalid"),
            TimeSpan.FromMilliseconds(100));

        var result = await probe.IsReachableAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsReachableAsync_ShouldReturnFalse_WhenInternalTimeoutExpires()
    {
        var connection = CreateConnectionMock(token => Task.Delay(Timeout.InfiniteTimeSpan, token));
        var probe = new DatabaseConnectivityProbe(
            CreateValidConfiguration(),
            _ => connection.Object,
            TimeSpan.FromMilliseconds(10));

        var result = await probe.IsReachableAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsReachableAsync_ShouldPropagateCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var connection = CreateConnectionMock(token => Task.Delay(Timeout.InfiniteTimeSpan, token));
        var probe = new DatabaseConnectivityProbe(
            CreateValidConfiguration(),
            _ => connection.Object,
            TimeSpan.FromSeconds(1));

        var action = () => probe.IsReachableAsync(cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task IsReachableAsync_ShouldReturnFalse_WhenTcpPortIsOpenButPostgresHandshakeFails()
    {
        using var listener = new TcpListener(IPAddress.Loopback, port: 0);
        using var serverCts = new CancellationTokenSource();
        listener.Start();
        var serverTask = RejectPostgresHandshakeAsync(listener, serverCts.Token);

        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    $"Host=127.0.0.1;Port={endpoint.Port};Database=test;Username=test;Password=test"
            })
            .Build();
        var probe = new DatabaseConnectivityProbe(configuration);

        try
        {
            var result = await probe.IsReachableAsync();

            result.Should().BeFalse();
        }
        finally
        {
            serverCts.Cancel();
            listener.Stop();
            await serverTask;
        }
    }

    private static async Task RejectPostgresHandshakeAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            await client.GetStream().WriteAsync(new byte[] { 0 }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (SocketException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static IConfiguration CreateValidConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=127.0.0.1;Port=5432;Database=test;Username=test;Password=test"
            })
            .Build();

    private static Mock<DbConnection> CreateConnectionMock(Func<CancellationToken, Task>? openAsync = null)
    {
        var connection = new Mock<DbConnection>();
        connection
            .Setup(candidate => candidate.OpenAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken cancellationToken) =>
                openAsync?.Invoke(cancellationToken) ?? Task.CompletedTask);
        connection
            .Setup(candidate => candidate.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        return connection;
    }
}
