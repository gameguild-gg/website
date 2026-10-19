using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class EvmCertificateReadServiceRegistrationTests
{
    [Fact]
    public void ClientRegistrationDisablesRedirectsAndCookiesAndUsesTheExplicitDeadline()
    {
        var services = new ServiceCollection();
        services.AddEvmCertificateVerification(EvmCertificateNetworkOptionsTests.ValidOptions());
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(EvmCertificateRpcClient.HttpClientName);
        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
        var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(EvmCertificateRpcClient.HttpClientName);
        while (handler is DelegatingHandler delegating) handler = delegating.InnerHandler!;
        var sockets = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.False(sockets.AllowAutoRedirect);
        Assert.False(sockets.UseCookies);
        using var scope = provider.CreateScope();
        Assert.IsType<EvmCertificateRpcClient>(scope.ServiceProvider.GetRequiredService<IEvmCertificateRpc>());
        Assert.IsType<EvmCertificateProofVerifier>(scope.ServiceProvider.GetRequiredService<IEvmCertificateProofVerifier>());
    }

    [Fact]
    public async Task NamedClientDoesNotLogCredentialBearingRpcEndpoint()
    {
        using var logs = new CapturedLogs();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(logs));
        var options = EvmCertificateNetworkOptionsTests.ValidOptions();
        options.RpcEndpoint = "https://rpc.invalid/private-provider-marker";
        services.AddEvmCertificateVerification(options);
        services.AddHttpClient(EvmCertificateRpcClient.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new ReplyHandler());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IEvmCertificateRpc>()
            .RequestAsync(EvmCertificateRpcMethod.ChainId, []);
        Assert.Equal("0xaa36a7", result.GetString());
        Assert.DoesNotContain(logs.Messages, message => message.Contains("private-provider-marker", StringComparison.Ordinal));
    }

    [Fact]
    public void IncompleteSettingsFailBeforeClientOrVerifierRegistration()
    {
        var services = new ServiceCollection();
        var options = EvmCertificateNetworkOptionsTests.ValidOptions();
        options.ContractAddress = string.Empty;
        Assert.Throws<ArgumentException>(() => services.AddEvmCertificateVerification(options));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IEvmCertificateRpc));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IEvmCertificateProofVerifier));
    }

    private sealed class ReplyHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var id = document.RootElement.GetProperty("id").GetInt64();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":\"0xaa36a7\"}}")
            };
        }
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public ConcurrentBag<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Messages);
        public void Dispose() { }

        private sealed class CaptureLogger(ConcurrentBag<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, exception));
        }
    }
}
