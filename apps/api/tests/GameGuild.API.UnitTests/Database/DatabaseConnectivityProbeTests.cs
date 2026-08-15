using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using GameGuild.API.Database;

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
}
