using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameGuild.API.Core.Integration;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using GameGuild.Learning.Certificates;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Nethereum.Signer;

namespace GameGuild.API.IntegrationTests;

[CollectionDefinition(nameof(CertificateEvmEvidenceHttpCollection), DisableParallelization = true)]
public sealed class CertificateEvmEvidenceHttpCollection : ICollectionFixture<CertificateEvmEvidenceHttpFixture> { }

public sealed class CertificateEvmEvidenceHttpFixture : IAsyncLifetime, IDbContextFactory<EvmCertificateJournalDbContext>
{
    private readonly ApiPostgreSqlFixture inner = new();
    public Guid TenantId { get; } = Guid.NewGuid();
    public EvmCertificatePublicationNetwork Network { get; } = EvmCertificatePublicationNetwork.Capture("http-evidence-fixture", new()
    {
        RpcEndpoint = "https://fixture.invalid/private-route", ChainId = 11155111,
        ContractAddress = "0x" + new string('1', 40), IssuerId = "0x" + new string('2', 64),
        ExpectedRuntimeCodeSha256 = new string('3', 64), FinalityMode = EvmCertificateFinalityMode.Finalized
    });
    public EvmCertificateJournal Journal => new(this, TimeProvider.System);
    public WebApplicationFactory<Program> CreateFactory(Action<IWebHostBuilder> configuration) => inner.CreateFactory(configuration);

    public async Task InitializeAsync()
    {
        await inner.InitializeAsync();
        await using var journal = CreateDbContext(); await journal.Database.MigrateAsync();
        using var scope = inner.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marker = TenantId.ToString("N");
        db.Set<Tenant>().Add(new Tenant { Id = TenantId, Name = "EVM evidence fixture", Slug = "evm-" + marker,
            AdminEmail = marker + "@example.test", IsActive = true, IsDefault = true });
        await db.SaveChangesAsync();
    }

    public EvmCertificateJournalDbContext CreateDbContext() => new(new DbContextOptionsBuilder<EvmCertificateJournalDbContext>()
        .UseNpgsql(inner.ConnectionString, postgres => postgres.MigrationsHistoryTable(
            EvmCertificateJournalDbContext.MigrationsHistoryTable, EvmCertificateJournalDbContext.SchemaName)).Options);
    public Task DisposeAsync() => inner.DisposeAsync();
}

/// <summary>Production HTTP host, bearer/session validation, tenant context, application PostgreSQL model and actual journal.
/// Magic-link verification and chain-proof responses are controlled boundaries; this is not external-chain acceptance.</summary>
[Collection(nameof(CertificateEvmEvidenceHttpCollection))]
public sealed class CertificateEvmEvidencePostgreSqlHttpTests(CertificateEvmEvidenceHttpFixture fixture)
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnonymousHttpRequestsAreDeniedBeforeAnyJournalOrRpcRead(bool fresh)
    {
        using var h = Create(User(), true); using var client = h.Factory.CreateClient();
        using var response = await client.GetAsync(Route(Guid.NewGuid(), fresh), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.Equal(0, h.Lookup.Calls); h.Proofs.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TheRealBearerOwnerCanReadQueuedStateWithoutAChainProofAndWithoutCaching()
    {
        var user = User(); using var h = Create(user, true); var document = await Seed(h.Factory, user, user, fixture.TenantId, false, false);
        await Queue(document); using var client = await SignIn(h.Factory);
        using var response = await client.GetAsync(Route(document.Id, false), Ct);
        var result = await Read(response);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(CertificateEvmEvidenceReadStatus.Cached, result.ReadStatus);
        Assert.Equal(CertificateEvmValidity.Unknown, result.Validity); Assert.Null(result.ChainProof);
        Assert.False(result.FreshProofRequested); Assert.Equal(2, h.Lookup.Calls); h.Proofs.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AForeignOwnerInTheSameTenantIsDeniedBeforeReadingTheJournal(bool fresh)
    {
        var user = User(); using var h = Create(user, true); var document = await Seed(h.Factory, user, User(), fixture.TenantId, false, false);
        using var client = await SignIn(h.Factory); using var response = await client.GetAsync(Route(document.Id, fresh), Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.Equal(0, h.Lookup.Calls); h.Proofs.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwningACertificateInAnotherTenantDoesNotBypassTheCurrentTenant(bool fresh)
    {
        var user = User(); using var h = Create(user, true); var document = await Seed(h.Factory, user, user, Guid.NewGuid(), false, false);
        using var client = await SignIn(h.Factory); using var response = await client.GetAsync(Route(document.Id, fresh), Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.Equal(0, h.Lookup.Calls); h.Proofs.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnUntrustedAdministratorHeaderCannotElevateARealBearer(bool fresh)
    {
        var user = User(); using var h = Create(user, true); var document = await Seed(h.Factory, user, User(), fixture.TenantId, false, false);
        using var client = await SignIn(h.Factory);
        client.DefaultRequestHeaders.Add(ApiPostgreSqlTestAuthHandler.SystemAdminHeader, "true");
        using var response = await client.GetAsync(Route(document.Id, fresh), Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.Equal(0, h.Lookup.Calls); h.Proofs.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ASoftDeletedCertificateIsNotReturnedOverHttp()
    {
        var user = User(); using var h = Create(user, true); var document = await Seed(h.Factory, user, user, fixture.TenantId, true, false);
        using var client = await SignIn(h.Factory); using var response = await client.GetAsync(Route(document.Id, false), Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.Equal(0, h.Lookup.Calls);
    }

    [Fact]
    public async Task AProofRequestForAQueuedIntentHasNoInventedTransaction()
    {
        var user = User(); using var h = Create(user, true); var document = await Seed(h.Factory, user, user, fixture.TenantId, false, false);
        await Queue(document); using var client = await SignIn(h.Factory);
        using var response = await client.GetAsync(Route(document.Id, true), Ct); var result = await Read(response);
        Assert.Equal(CertificateEvmEvidenceReadStatus.NoRecordedTransaction, result.ReadStatus);
        Assert.Null(result.Anchor!.TransactionHash); Assert.Equal(CertificateEvmValidity.Unknown, result.Validity); h.Proofs.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FreshHttpProofUsesTheOriginalStoredTransactionAndDoesNotMutateTheJournal()
    {
        var user = User(); using var h = Create(user, true); var document = await Seed(h.Factory, user, user, fixture.TenantId, false, false);
        var original = await Prepare(document); h.AnchorProof(original);
        using var client = await SignIn(h.Factory);
        var arbitrary = "0x" + new string('9', 64);
        using var response = await client.GetAsync(Route(document.Id, true) + "?transactionHash=" + arbitrary + "&tenantId=" + Guid.NewGuid(), Ct);
        var result = await Read(response);
        Assert.Equal(CertificateEvmValidity.Valid, result.Validity); Assert.Equal(original.TransactionHash, result.ChainProof!.Evidence!.TransactionHash);
        var after = await fixture.Journal.FindAsync(original.Id, Ct); Assert.Equal(original.Revision, after!.Revision);
        Assert.Single(await fixture.Journal.ListAttemptsAsync(original.Id, Ct));
        var json = await response.Content.ReadAsStringAsync(Ct);
        foreach (var privateName in new[] { "fixture-private-key-reference", "rawTransaction", "signerReference", "leaseToken", "ownerUserId", "tenantId", "private-route" })
        {
            Assert.DoesNotContain(privateName, json, StringComparison.OrdinalIgnoreCase);
        }
        h.Proofs.Verify(p => p.VerifyAnchorAsync(original.CertificateHash, original.TransactionHash!, It.IsAny<CancellationToken>()), Times.Once);
        h.Proofs.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExpiredLocalEvidenceCannotBecomeValidThroughAnOldChainAnchor()
    {
        var user = User(); using var h = Create(user, true); var document = await Seed(h.Factory, user, user, fixture.TenantId, false, true);
        var original = await Prepare(document); h.AnchorProof(original);
        using var client = await SignIn(h.Factory); using var response = await client.GetAsync(Route(document.Id, true), Ct);
        var result = await Read(response); Assert.False(result.LocallyValid); Assert.Equal(CertificateEvmValidity.Invalid, result.Validity);
    }

    [Fact]
    public async Task ProviderFailureIsAnExplicitUnavailableObservationWithNoCredentialDetail()
    {
        var user = User(); using var h = Create(user, true); var document = await Seed(h.Factory, user, user, fixture.TenantId, false, false);
        var original = await Prepare(document);
        h.Proofs.Setup(p => p.VerifyAnchorAsync(original.CertificateHash, original.TransactionHash!, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("credential-bearing-provider-detail"));
        using var client = await SignIn(h.Factory); using var response = await client.GetAsync(Route(document.Id, true), Ct);
        var result = await Read(response); Assert.Equal(CertificateEvmEvidenceReadStatus.Unavailable, result.ReadStatus);
        Assert.Equal(CertificateEvmValidity.Unknown, result.Validity);
        Assert.DoesNotContain("credential-bearing", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDisabledProductionCompositionReportsConfigurationWithoutStartingPublication()
    {
        var user = User(); using var h = Create(user, false); var document = await Seed(h.Factory, user, user, fixture.TenantId, false, false);
        using var client = await SignIn(h.Factory); using var response = await client.GetAsync(Route(document.Id, true), Ct);
        var result = await Read(response); Assert.False(result.Configured);
        Assert.Equal(CertificateEvmEvidenceReadStatus.Disabled, result.ReadStatus); Assert.Null(result.ChainProof);
        using var scope = h.Factory.Services.CreateScope(); Assert.Null(scope.ServiceProvider.GetService<ICertificateEvmPublicationLookup>());
    }

    private Harness Create(User user, bool enableEvidence)
    {
        var lookup = new RecordingLookup(new CertificateEvmJournalLookup(fixture.Journal));
        var proofs = new Mock<IEvmCertificateProofVerifier>(MockBehavior.Strict);
        var factory = fixture.CreateFactory(builder => builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = JwtBearerDefaults.AuthenticationScheme; o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            });
            var magic = new Mock<IEmailVerificationService>();
            magic.Setup(v => v.VerifyMagicLinkTokenAsync("synthetic-verified-certificate-token"))
                .ReturnsAsync(new TokenValidationResult(true, user.Id, user.Email));
            services.RemoveAll<IEmailVerificationService>(); services.AddSingleton(magic.Object);
            if (enableEvidence)
            {
                services.RemoveAll<ICertificateEvmEvidenceReader>();
                services.AddScoped<ICertificateEvmEvidenceReader>(_ => new EvmCertificateEvidenceReader(lookup, proofs.Object, fixture.Network, TimeProvider.System));
            }
        }));
        return new(factory, lookup, proofs);
    }

    private async Task<Certificate> Seed(WebApplicationFactory<Program> factory, User caller, User owner, Guid? tenant, bool deleted, bool expired)
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Add(caller); if (caller.Id != owner.Id) { db.Add(owner); }
        db.Add(new TenantMember { Id = Guid.NewGuid(), UserId = caller.Id, TenantId = fixture.TenantId, IsActive = true, Role = "Member" });
        var certificate = Certificate.Issue(Guid.NewGuid(), Guid.NewGuid(), owner.Id, Guid.NewGuid(), "Private recipient", "Private course",
            expired ? DateTime.UtcNow.AddDays(-1) : null, tenant);
        if (deleted) { certificate.DeletedAt = DateTime.UtcNow; }
        db.Add(certificate); await db.SaveChangesAsync(Ct); return certificate;
    }

    private Task<EvmCertificateJournalSnapshot> Queue(Certificate certificate) => fixture.Journal.EnqueueAsync(new(fixture.Network,
        certificate.Id, certificate.UserId, certificate.TenantId, CertificateEvmEvidenceProducer.ProducerName,
        "0x" + certificate.Id.ToString("N") + certificate.Id.ToString("N"), EvmCertificateOperationKind.Anchor), Ct);

    private async Task<EvmCertificateJournalSnapshot> Prepare(Certificate certificate)
    {
        var queued = await Queue(certificate); var lease = await fixture.Journal.TryAcquireAsync(queued.Id, TimeSpan.FromMinutes(2), Ct);
        Assert.NotNull(lease);
        var sdkBytes = EthECKey.GenerateKey().GetPrivateKeyAsBytes();
        var bytes = new byte[32]; sdkBytes.CopyTo(bytes, bytes.Length - sdkBytes.Length);
        try
        {
            var signer = new EvmCertificateSoftwareSigner(new TransferredKey(bytes));
            var address = await signer.GetAddressAsync("fixture-private-key-reference", Ct);
            var budget = EvmCertificateFeeBudget.Create(200000, 30000000000, 6000000000000000);
            var reserved = await fixture.Journal.ReserveNonceAsync(lease, "fixture-private-key-reference", address, 0, budget, Ct);
            var raw = await signer.SignAsync("fixture-private-key-reference", reserved.PreparedTransaction, 20000000000, 1000000000, Ct);
            await fixture.Journal.StoreSignedAttemptAsync(lease, raw, Ct);
            return (await fixture.Journal.FindAsync(queued.Id, Ct))!;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private async Task<HttpClient> SignIn(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/auth/magic-link:consume", new
        {
            token = "synthetic-verified-certificate-token", tenantId = fixture.TenantId, deviceFingerprint = "certificate-evidence-fixture-device"
        }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.RootElement.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<CertificateEvmEvidenceResponse> Read(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CertificateEvmEvidenceResponse>(Json, Ct))!;
    }
    private static string Route(Guid id, bool fresh) => "/api/certificates/" + id.ToString("D") + (fresh ? "/evm/proof" : "/evm");
    private static User User()
    {
        var marker = Guid.NewGuid().ToString("N"); var user = GameGuild.Identity.Users.User.Create(marker + "@example.test", "Synthetic evidence account");
        user.Username = marker; return user;
    }

    private sealed record Harness(WebApplicationFactory<Program> Factory, RecordingLookup Lookup, Mock<IEvmCertificateProofVerifier> Proofs) : IDisposable
    {
        public void AnchorProof(EvmCertificateJournalSnapshot operation) => Proofs.Setup(v => v.VerifyAnchorAsync(operation.CertificateHash,
            operation.TransactionHash!, It.IsAny<CancellationToken>())).ReturnsAsync(new EvmCertificateProofResult(EvmCertificateProofStatus.Verified,
                new(EvmCertificateOperationKind.Anchor, operation.CertificateHash, operation.TransactionHash!, operation.SignerAddress!,
                    new(99, "0x" + new string('a', 64)), new(operation.Network.ChainId, operation.Network.ContractAddress, operation.Network.IssuerId,
                        operation.Network.RuntimeCodeSha256, operation.Network.FinalityMode, operation.Network.MinimumConfirmations, new(100, "0x" + new string('b', 64))),
                    new(operation.SignerAddress!, "0x" + new string('0', 40), 99, 0))));
        public void Dispose() => Factory.Dispose();
    }
    private sealed class RecordingLookup(ICertificateEvmPublicationLookup inner) : ICertificateEvmPublicationLookup
    {
        public int Calls { get; private set; }
        public Task<EvmCertificateJournalSnapshot?> FindAsync(Guid documentId, Guid owner, Guid? tenant, EvmCertificateOperationKind kind, CancellationToken cancellationToken)
        {
            Calls++; return inner.FindAsync(documentId, owner, tenant, kind, cancellationToken);
        }
    }
    private sealed class TransferredKey(byte[] source) : IEvmCertificatePrivateKeySource
    {
        public ValueTask<byte[]> GetPrivateKeyAsync(string reference, CancellationToken cancellationToken)
        {
            Assert.Equal("fixture-private-key-reference", reference); cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(source.ToArray());
        }
    }
}
