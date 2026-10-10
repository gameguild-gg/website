using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using GameGuild;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameGuild.API.UnitTests.Integration;

/// <summary>
///     Boots a real Kestrel HTTPS host with TLS client-certificate negotiation and asserts
///     the client-certificate authentication scheme end to end: handshake, chain validation
///     against the configured CA allowlist, service-account binding, and the resulting
///     service-actor claims.
/// </summary>
public sealed class ClientCertificateAuthenticationKestrelTests : IClassFixture<KestrelClientCertificateFixture>
{
    private readonly KestrelClientCertificateFixture _fixture;

    public ClientCertificateAuthenticationKestrelTests(KestrelClientCertificateFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BoundTrustedClientCertificate_AuthenticatesAsServiceActor()
    {
        await using var host = await _fixture.StartHostAsync();
        var (statusCode, body, negotiatedCertificate) = await host.SendRawHttpsRequestAsync(_fixture.BoundClientCertificate);

        if (statusCode != (int)HttpStatusCode.OK)
        {
            Assert.Fail(
                $"Expected OK got {statusCode} '{body}'.\n" +
                $"Client negotiated certificate: {negotiatedCertificate}\n" +
                $"Server logs:\n{host.DumpRecentLogs()}");
        }

        using var document = JsonDocument.Parse(body);
        var payload = document.RootElement;
        Assert.Equal(host.BoundAccountId.ToString(), payload.GetProperty("sub").GetString());
        Assert.Equal("svc-kestrel-bound", payload.GetProperty("clientId").GetString());
        Assert.Equal("Service", payload.GetProperty("actorKind").GetString());
        Assert.Equal("client_credentials", payload.GetProperty("grantType").GetString());
        Assert.Equal("client_certificate", payload.GetProperty("authMethod").GetString());
        Assert.Equal("Service", payload.GetProperty("resolvedActorKind").GetString());
        Assert.Equal(
            ["read:jobs", "write:reports"],
            payload.GetProperty("scopes").EnumerateArray().Select(scope => scope.GetString()!).OrderBy(scope => scope).ToArray());
    }

    [Fact]
    public async Task RequestWithoutClientCertificate_IsRejectedAtTheTlsLayer()
    {
        await using var host = await _fixture.StartHostAsync();

        // The test host requires a client certificate during the handshake, so a
        // certificate-less client cannot even establish the connection.
        var exception = await Record.ExceptionAsync(() => host.SendRawHttpsRequestAsync(clientCertificate: null));
        Assert.True(
            exception is System.Security.Authentication.AuthenticationException or IOException,
            $"Expected the TLS handshake to fail without a client certificate, got: {exception}");
    }

    [Fact]
    public async Task ClientCertificateFromUntrustedAuthority_IsRejected()
    {
        await using var host = await _fixture.StartHostAsync();

        var (statusCode, _, _) = await host.SendRawHttpsRequestAsync(_fixture.RogueClientCertificate);

        Assert.Equal((int)HttpStatusCode.Unauthorized, statusCode);
    }

    [Fact]
    public async Task TrustedClientCertificateWithoutBinding_IsRejected()
    {
        await using var host = await _fixture.StartHostAsync();

        var (statusCode, _, _) = await host.SendRawHttpsRequestAsync(_fixture.UnboundClientCertificate);

        Assert.Equal((int)HttpStatusCode.Unauthorized, statusCode);
    }
}

public sealed class KestrelClientCertificateFixture : IAsyncLifetime, IDisposable
{
    public const string TlsClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";
    public const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    public X509Certificate2 CertificateAuthority { get; private set; } = null!;
    public X509Certificate2 BoundClientCertificate { get; private set; } = null!;
    public X509Certificate2 UnboundClientCertificate { get; private set; } = null!;
    public X509Certificate2 RogueClientCertificate { get; private set; } = null!;
    private X509Certificate2 _rogueCertificateAuthority = null!;
    private X509Certificate2 _serverCertificate = null!;

    public Guid BoundAccountId { get; } = Guid.NewGuid();

    public Task InitializeAsync()
    {
        CertificateAuthority = CreateCertificateAuthority("GameGuild Kestrel Test Root");
        _rogueCertificateAuthority = CreateCertificateAuthority("GameGuild Rogue Root");
        BoundClientCertificate = CreateClientCertificate("bound-kestrel-client", CertificateAuthority);
        UnboundClientCertificate = CreateClientCertificate("unbound-kestrel-client", CertificateAuthority);
        RogueClientCertificate = CreateClientCertificate("rogue-kestrel-client", _rogueCertificateAuthority);
        _serverCertificate = CreateServerCertificate();
        return Task.CompletedTask;
    }

    public async Task<TestKestrelHost> StartHostAsync()
    {
        var builder = WebApplication.CreateBuilder();
        var logCollector = new CollectingLoggerProvider();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logCollector);
        // Bind 127.0.0.1:0 explicitly: ListenLocalhost(0) cannot allocate a dynamic
        // port because it would have to bind the IPv4 and IPv6 loopback together.
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0, listenOptions => listenOptions.UseHttps(httpsOptions =>
        {
            httpsOptions.ServerCertificate = _serverCertificate;
            // Require the certificate during the handshake so the TLS layer itself
            // negotiates it (the AllowCertificate mode defers the request, which the
            // Linux stack cannot satisfy after the handshake). Chain/binding validation
            // stays in the authentication handler so its fail-closed rules govern the
            // authorization outcome.
            httpsOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
            httpsOptions.ClientCertificateValidation = (_, _, _) => true;
        })));

        var databaseName = $"kestrel-client-certs-{Guid.NewGuid():N}";
        builder.Services.AddDbContext<KestrelServiceAccountDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddSingleton<IApplicationDbContext>(provider =>
            provider.GetRequiredService<KestrelServiceAccountDbContext>());
        builder.Services
            .AddAuthentication()
            .AddClientCertificateAuthentication(options => options.TrustedCertificateAuthorities.Add(CertificateAuthority));

        var app = builder.Build();
        app.UseAuthentication();
        app.MapGet("/cert-whoami", async context =>
        {
            var result = await context.AuthenticateAsync(ClientCertificateAuthenticationOptions.SchemeName);
            if (!result.Succeeded || result.Principal is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync($"unauthenticated [{DescribeFailure(context)}]");
                return;
            }

            var principal = result.Principal;
            await context.Response.WriteAsJsonAsync(new
            {
                sub = principal.FindFirst("sub")?.Value,
                clientId = principal.FindFirst("client_id")?.Value,
                actorKind = principal.FindFirst("actor_kind")?.Value,
                grantType = principal.FindFirst("grant_type")?.Value,
                authMethod = principal.FindFirst("auth_method")?.Value,
                scopes = principal.FindAll("scope").Select(claim => claim.Value).ToArray(),
                resolvedActorKind = ActorKindResolver.Resolve(
                    principal.FindFirst("grant_type")?.Value,
                    principal.FindFirst("actor_kind")?.Value,
                    principal.FindFirst("sub")?.Value).ToString()
            });
        });

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KestrelServiceAccountDbContext>();
        dbContext.ServiceAccounts.Add(new ServiceAccount
        {
            Id = BoundAccountId,
            ClientId = "svc-kestrel-bound",
            ClientSecretHash = "unused",
            Name = "Kestrel bound service",
            Scopes = "read:jobs,write:reports",
            IsActive = true,
            CertificateThumbprint = ClientCertificateAuthenticationUtilities.GetNormalizedThumbprint(BoundClientCertificate)
        });
        await dbContext.SaveChangesAsync();

        await app.StartAsync();
        return new TestKestrelHost(app, this, logCollector);
    }

    /// <summary>
    ///     Re-runs the handler's chain policy outside the authentication pipeline so a
    ///     failing integration test reports which stage denied the certificate.
    /// </summary>
    private string DescribeFailure(HttpContext context)
    {
        var clientCertificate = context.Connection.ClientCertificate;
        if (clientCertificate is null)
        {
            return "no client certificate reached the application";
        }

        using var chain = new X509Chain
        {
            ChainPolicy =
            {
                TrustMode = X509ChainTrustMode.CustomRootTrust,
                RevocationMode = X509RevocationMode.NoCheck,
                VerificationFlags = X509VerificationFlags.NoFlag,
                VerificationTime = DateTime.UtcNow
            }
        };
        chain.ChainPolicy.CustomTrustStore.Add(CertificateAuthority);
        var built = chain.Build(clientCertificate);
        var statuses = string.Join(
            ", ",
            chain.ChainStatus.Select(status => $"{status.Status}: {status.StatusInformation.Trim()}"));
        return $"chain built={built}"
            + (string.IsNullOrEmpty(statuses) ? string.Empty : $" statuses=[{statuses}]")
            + $" thumbprint={ClientCertificateAuthenticationUtilities.GetNormalizedThumbprint(clientCertificate)}"
            + $" bound={ClientCertificateAuthenticationUtilities.GetNormalizedThumbprint(BoundClientCertificate)}";
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        CertificateAuthority?.Dispose();
        BoundClientCertificate?.Dispose();
        UnboundClientCertificate?.Dispose();
        RogueClientCertificate?.Dispose();
        _rogueCertificateAuthority?.Dispose();
        _serverCertificate?.Dispose();
    }

    private static X509Certificate2 CreateCertificateAuthority(string commonName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature, true));
        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        return ImportPersistable(ephemeral);
    }

    private static X509Certificate2 CreateClientCertificate(string commonName, X509Certificate2 certificateAuthority)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new(TlsClientAuthenticationOid) }, true));

        // Issued leaves must stay inside the issuer's validity window: deriving the
        // bounds from UtcNow here races the CA creation instant by a second and
        // makes Create() throw when the leaf outlives its issuer.
        using var issued = request.Create(
            certificateAuthority,
            certificateAuthority.NotBefore,
            certificateAuthority.NotAfter,
            RandomNumberGenerator.GetBytes(16));
        return ImportPersistable(issued);
    }

    private static X509Certificate2 CreateServerCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new(ServerAuthenticationOid) }, true));
        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        return ImportPersistable(ephemeral);
    }

    private static X509Certificate2 ImportPersistable(X509Certificate2 certificate)
    {
        var pfx = certificate.Export(X509ContentType.Pfx);
        return X509CertificateLoader.LoadPkcs12(pfx, null, X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    }
}

public sealed class TestKestrelHost(
    WebApplication app,
    KestrelClientCertificateFixture fixture,
    CollectingLoggerProvider logCollector) : IAsyncDisposable
{
    public Guid BoundAccountId => fixture.BoundAccountId;

    public string DumpRecentLogs() => string.Join(
        "\n",
        logCollector.Entries.TakeLast(50).Select(entry => $"[{entry.Level}] {entry.Category}: {entry.Message}"));

    /// <summary>
    ///     Performs a raw HTTPS/1.1 GET over an explicit <see cref="SslStream"/> handshake so the
    ///     test controls (and can report) exactly which client certificate was negotiated. The
    ///     HttpClient stack performs the same handshake but hides the negotiation outcome, which
    ///     made Linux-side failures invisible.
    /// </summary>
    public async Task<(int StatusCode, string Body, string NegotiatedCertificate)> SendRawHttpsRequestAsync(
        X509Certificate2? clientCertificate)
    {
        var baseAddress = app.Urls.Single(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        var uri = new Uri(baseAddress);

        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(IPAddress.Loopback, uri.Port).ConfigureAwait(false);

        using var sslStream = new SslStream(tcpClient.GetStream(), leaveInnerStreamOpen: false);
        var authenticationOptions = new SslClientAuthenticationOptions
        {
            TargetHost = uri.Host,
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
            ClientCertificates = clientCertificate is null
                ? new X509CertificateCollection()
                : new X509CertificateCollection { clientCertificate },
            LocalCertificateSelectionCallback = clientCertificate is null
                ? null
                : (_, _, _, _, _) => clientCertificate
        };
        await sslStream.AuthenticateAsClientAsync(authenticationOptions).ConfigureAwait(false);

        var negotiatedCertificate = sslStream.LocalCertificate is null
            ? "<none>"
            : $"{sslStream.LocalCertificate.Subject} [{sslStream.LocalCertificate.GetCertHashString()}]";

        var request = Encoding.ASCII.GetBytes("GET /cert-whoami HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n");
        await sslStream.WriteAsync(request).ConfigureAwait(false);
        await sslStream.FlushAsync().ConfigureAwait(false);

        using var memory = new MemoryStream();
        await sslStream.CopyToAsync(memory).ConfigureAwait(false);
        var raw = Encoding.UTF8.GetString(memory.ToArray());

        var separator = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var head = separator < 0 ? raw : raw[..separator];
        var body = separator < 0 ? string.Empty : raw[(separator + 4)..];

        var statusCode = int.Parse(head.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture);
        return (statusCode, body, negotiatedCertificate);
    }

    public async ValueTask DisposeAsync()
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }
}

/// <summary>
///     Captures server-side log entries so failing integration tests can print why
///     the authentication handler rejected a certificate.
/// </summary>
public sealed class CollectingLoggerProvider : ILoggerProvider
{
    public sealed record LogEntry(string Category, LogLevel Level, string Message);

    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class CollectingLogger(CollectingLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                provider._entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception)));
            }
        }
    }
}

file sealed class KestrelServiceAccountDbContext(DbContextOptions<KestrelServiceAccountDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<ServiceAccount> ServiceAccounts => Set<ServiceAccount>();

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default) => Database.BeginTransactionAsync(cancellationToken);
}
