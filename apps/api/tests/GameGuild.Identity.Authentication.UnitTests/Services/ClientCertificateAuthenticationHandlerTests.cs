using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using GameGuild.Identity.Authentication.UnitTests.Infrastructure;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class ClientCertificateAuthenticationHandlerTests : IDisposable
{
    private readonly X509Certificate2 _certificateAuthority = ClientCertificateTestFactory.CreateCertificateAuthority("GameGuild Test Root");
    private readonly X509Certificate2 _clientCertificate;

    public ClientCertificateAuthenticationHandlerTests()
    {
        _clientCertificate = ClientCertificateTestFactory.CreateClientCertificate("bound-client", _certificateAuthority);
    }

    public void Dispose()
    {
        _certificateAuthority.Dispose();
        _clientCertificate.Dispose();
    }

    [Fact]
    public async Task AuthenticatedCertificate_BoundToActiveServiceAccount_YieldsServicePrincipal()
    {
        var accountId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        dbContext.ServiceAccounts.Add(CreateAccount(accountId, certificate: _clientCertificate));
        await dbContext.SaveChangesAsync();

        var (handler, context) = CreateHandler(dbContext, allowlist: [_certificateAuthority]);
        context.Connection.ClientCertificate = _clientCertificate;

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        var principal = result.Principal!;
        principal.FindFirst("sub")!.Value.Should().Be(accountId.ToString());
        principal.FindFirst("client_id")!.Value.Should().Be("svc-bound");
        principal.FindFirst("actor_kind")!.Value.Should().Be("Service");
        principal.FindFirst("grant_type")!.Value.Should().Be("client_credentials");
        principal.FindFirst("auth_method")!.Value.Should().Be("client_certificate");
        principal.FindAll("scope").Select(claim => claim.Value).Should().BeEquivalentTo("read:users", "write:jobs");
        // The same claims ActorContextMiddleware uses resolve the service actor kind.
        ActorKindResolverFixtures.ResolveFromPrincipal(principal).Should().Be("Service");
    }

    [Fact]
    public async Task CertificateBoundBySpkiKeyPinOnly_AlsoAuthenticates()
    {
        var accountId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        dbContext.ServiceAccounts.Add(CreateAccount(accountId, spkiSha256: ClientCertificateAuthenticationUtilities.ComputeSpkiSha256Hex(_clientCertificate)));
        await dbContext.SaveChangesAsync();

        var (handler, context) = CreateHandler(dbContext, allowlist: [_certificateAuthority]);
        context.Connection.ClientCertificate = _clientCertificate;

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirst("sub")!.Value.Should().Be(accountId.ToString());
    }

    [Fact]
    public async Task CertificateFromUntrustedAuthority_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        using var rogueAuthority = ClientCertificateTestFactory.CreateCertificateAuthority("Rogue Root");
        using var rogueCertificate = ClientCertificateTestFactory.CreateClientCertificate("rogue-client", rogueAuthority);

        var (handler, context) = CreateHandler(dbContext, allowlist: [_certificateAuthority]);
        context.Connection.ClientCertificate = rogueCertificate;

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task TrustedCertificateNotBoundToAnyServiceAccount_IsRejected()
    {
        await using var dbContext = CreateDbContext();

        var (handler, context) = CreateHandler(dbContext, allowlist: [_certificateAuthority]);
        context.Connection.ClientCertificate = _clientCertificate;

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("not bound");
    }

    [Fact]
    public async Task EmptyCaAllowlist_FailsClosed()
    {
        await using var dbContext = CreateDbContext();

        var (handler, context) = CreateHandler(dbContext, allowlist: []);
        context.Connection.ClientCertificate = _clientCertificate;

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("CA allowlist");
    }

    [Fact]
    public async Task CertificateBoundToLockedServiceAccount_IsRejected()
    {
        var account = CreateAccount(Guid.NewGuid(), certificate: _clientCertificate);
        account.Lock("test");
        await using var dbContext = CreateDbContext();
        dbContext.ServiceAccounts.Add(account);
        await dbContext.SaveChangesAsync();

        var (handler, context) = CreateHandler(dbContext, allowlist: [_certificateAuthority]);
        context.Connection.ClientCertificate = _clientCertificate;

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task CertificateBoundToServiceAccountRestrictedToAnotherIp_IsRejected()
    {
        var account = CreateAccount(Guid.NewGuid(), certificate: _clientCertificate, allowedIpAddresses: "203.0.113.50");
        await using var dbContext = CreateDbContext();
        dbContext.ServiceAccounts.Add(account);
        await dbContext.SaveChangesAsync();

        var (handler, context) = CreateHandler(dbContext, allowlist: [_certificateAuthority]);
        context.Connection.ClientCertificate = _clientCertificate;
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        // The rejected attempt counts toward lockout, matching the secret path.
        dbContext.ServiceAccounts.Single().FailedAuthenticationAttempts.Should().Be(1);
    }

    [Fact]
    public async Task AmbiguousBindingAcrossTwoServiceAccounts_FailsClosed()
    {
        await using var dbContext = CreateDbContext();
        dbContext.ServiceAccounts.Add(CreateAccount(Guid.NewGuid(), certificate: _clientCertificate));
        dbContext.ServiceAccounts.Add(CreateAccount(Guid.NewGuid(), certificate: _clientCertificate, clientId: "svc-bound-2"));
        await dbContext.SaveChangesAsync();

        var (handler, context) = CreateHandler(dbContext, allowlist: [_certificateAuthority]);
        context.Connection.ClientCertificate = _clientCertificate;

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("ambiguous");
    }

    [Fact]
    public async Task SuccessfulAuthentication_RecordsUsageAndAuditsSuccess()
    {
        var accountId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        dbContext.ServiceAccounts.Add(CreateAccount(accountId, certificate: _clientCertificate));
        await dbContext.SaveChangesAsync();

        var auditSink = new Mock<IAuthenticationAuditEventSink>();
        var (handler, context) = CreateHandler(dbContext, allowlist: [_certificateAuthority], auditSink.Object);
        context.Connection.ClientCertificate = _clientCertificate;

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        var persisted = dbContext.ServiceAccounts.Single();
        persisted.AuthenticationCount.Should().Be(1);
        persisted.LastAuthenticatedAt.Should().NotBeNull();
        auditSink.Verify(sink => sink.RecordAsync(
                It.Is<AuthenticationAuditEvent>(auditEvent =>
                    auditEvent.ActionType == "Authentication.Succeeded"
                    && auditEvent.UserId == accountId
                    && auditEvent.Success
                    && auditEvent.Method == "ClientCertificate"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RejectedCertificate_AuditsFailureReasonWithoutFingerprint()
    {
        await using var dbContext = CreateDbContext();
        var auditSink = new Mock<IAuthenticationAuditEventSink>();
        var (handler, context) = CreateHandler(dbContext, allowlist: [_certificateAuthority], auditSink.Object);
        context.Connection.ClientCertificate = _clientCertificate;

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        auditSink.Verify(sink => sink.RecordAsync(
                It.Is<AuthenticationAuditEvent>(auditEvent =>
                    auditEvent.ActionType == "Authentication.Failed"
                    && !auditEvent.Success
                    && auditEvent.ErrorMessage == "CertificateNotBound"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void GetNormalizedThumbprint_StripsSeparatorsAndUppercases()
    {
        var expected = _clientCertificate.Thumbprint;

        var normalized = ClientCertificateAuthenticationUtilities.GetNormalizedThumbprint(_clientCertificate);

        normalized.Should().Be(expected);
        normalized.Should().MatchRegex("^[0-9A-F]{40}$");
    }

    [Fact]
    public void ComputeSpkiSha256Hex_PinsThePublicKeyNotTheWholeCertificate()
    {
        var first = ClientCertificateAuthenticationUtilities.ComputeSpkiSha256Hex(_clientCertificate);

        // The hash is stable per key and differs between certificates with different keys.
        using var otherCertificate = ClientCertificateTestFactory.CreateClientCertificate("other-client", _certificateAuthority);

        var second = ClientCertificateAuthenticationUtilities.ComputeSpkiSha256Hex(otherCertificate);

        first.Should().MatchRegex("^[0-9A-F]{64}$");
        first.Should().NotBe(second);
    }

    private static InMemoryServiceAccountDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<InMemoryServiceAccountDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static ServiceAccount CreateAccount(
        Guid id,
        X509Certificate2? certificate = null,
        string? spkiSha256 = null,
        string? allowedIpAddresses = null,
        string clientId = "svc-bound") =>
        new()
        {
            Id = id,
            ClientId = clientId,
            ClientSecretHash = "hash",
            Name = "Bound service",
            Scopes = "read:users,write:jobs",
            IsActive = true,
            AllowedIpAddresses = allowedIpAddresses,
            CertificateThumbprint = certificate is null ? null : ClientCertificateAuthenticationUtilities.GetNormalizedThumbprint(certificate),
            CertificateSpkiSha256 = spkiSha256
        };

    private static (ClientCertificateAuthenticationHandler Handler, DefaultHttpContext Context) CreateHandler(
        InMemoryServiceAccountDbContext dbContext,
        IList<X509Certificate2> allowlist,
        IAuthenticationAuditEventSink? auditSink = null)
    {
        var certificateOptions = new ClientCertificateAuthenticationOptions();
        foreach (var authority in allowlist)
        {
            certificateOptions.TrustedCertificateAuthorities.Add(authority);
        }

        var optionsMonitor = new Mock<IOptionsMonitor<ClientCertificateAuthenticationOptions>>();
        optionsMonitor.Setup(monitor => monitor.Get(It.IsAny<string>())).Returns(certificateOptions);
        optionsMonitor.SetupGet(monitor => monitor.CurrentValue).Returns(certificateOptions);

        var handler = new ClientCertificateAuthenticationHandler(
            optionsMonitor.Object,
            NullLoggerFactory.Instance,
            System.Text.Encodings.Web.UrlEncoder.Default,
            dbContext,
            auditSink);
        var context = new DefaultHttpContext();
        handler.InitializeAsync(
            new AuthenticationScheme(
                ClientCertificateAuthenticationOptions.SchemeName,
                ClientCertificateAuthenticationOptions.SchemeName,
                typeof(ClientCertificateAuthenticationHandler)),
            context).GetAwaiter().GetResult();
        return (handler, context);
    }

    private sealed class InMemoryServiceAccountDbContext(DbContextOptions<InMemoryServiceAccountDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public DbSet<ServiceAccount> ServiceAccounts => Set<ServiceAccount>();

        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default) => Database.BeginTransactionAsync(cancellationToken);
    }
}

/// <summary>
///     Mirrors the ActorContextMiddleware resolution inputs for principal-level assertions.
/// </summary>
internal static class ActorKindResolverFixtures
{
    public static string ResolveFromPrincipal(ClaimsPrincipal principal) =>
        ActorKindResolver.Resolve(
            principal.FindFirst("grant_type")?.Value,
            principal.FindFirst("actor_kind")?.Value,
            principal.FindFirst("sub")?.Value).ToString();
}
