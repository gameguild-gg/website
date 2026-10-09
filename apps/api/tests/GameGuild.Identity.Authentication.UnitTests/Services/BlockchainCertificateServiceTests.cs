using FluentAssertions;
using GameGuild.Configuration.ApplicationLayer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public class BlockchainCertificateServiceTests
{
    private static (LocalBlockchainCertificateService Service, AnchorDbContext Context) CreateLocalService()
    {
        var context = AnchorDbContext.Create();
        var service = new LocalBlockchainCertificateService(
            context,
            BlockchainCertificateOptions.CreateDefault(),
            NullLogger<LocalBlockchainCertificateService>.Instance);

        return (service, context);
    }

    private static string ValidIssuancePayload(
        Guid? id = null,
        string? recipientHash = null,
        DateTime? issuedAt = null,
        string issuer = "gameguild",
        int version = 1)
    {
        return BlockchainCertificateCanonicalizer.BuildIssuancePayload(
            id ?? Guid.NewGuid(),
            recipientHash ?? BlockchainCertificateCanonicalizer.ComputeSha256Hex("recipient"),
            issuedAt ?? new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            issuer,
            version);
    }

    // ── Canonicalization: determinism ────────────────────────────────────

    [Fact]
    public void BuildIssuancePayload_IsDeterministic_AndUsesSortedCanonicalKeys()
    {
        var id = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        var recipientHash = BlockchainCertificateCanonicalizer.ComputeSha256Hex("Ada Lovelace");
        var issuedAt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        var first = BlockchainCertificateCanonicalizer.BuildIssuancePayload(id, recipientHash, issuedAt, "gameguild", 1);
        var second = BlockchainCertificateCanonicalizer.BuildIssuancePayload(id, recipientHash, issuedAt, "gameguild", 1);

        first.Should().Be(second);
        first.Should().Be(
            $$"""{"id":"01234567-89ab-cdef-0123-456789abcdef","issued-at":"2026-01-15T10:30:00.0000000Z","issuer":"gameguild","recipient-hash":"{{recipientHash}}","version":1}""");
    }

    [Fact]
    public void CanonicalizeIssuancePayload_IsOrderInsensitive_AndDropsUnknownFields()
    {
        var recipientHash = BlockchainCertificateCanonicalizer.ComputeSha256Hex("recipient");
        var expected = ValidIssuancePayload(
            id: Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
            recipientHash: recipientHash);

        // Same logical content, different key order, plus PII-bearing extras that must be dropped.
        var shuffled = $$"""
            {"version":1,"recipientName":"Ada Lovelace","recipient-hash":"{{recipientHash}}","email":"ada@example.com","issuer":"gameguild","id":"01234567-89ab-cdef-0123-456789abcdef","issued-at":"2026-01-15T10:30:00.0000000Z","notes":"free text with personal data"}
            """;

        var canonical = BlockchainCertificateCanonicalizer.CanonicalizeIssuancePayload(shuffled);

        canonical.Should().Be(expected);
    }

    [Fact]
    public void CanonicalizeIssuancePayload_PII_IsNeverPresentInCanonicalOutput()
    {
        var payload = """
            {"id":"01234567-89ab-cdef-0123-456789abcdef","issued-at":"2026-01-15T10:30:00.0000000Z","issuer":"gameguild","recipient-hash":"abc","version":1,"recipientName":"Ada Lovelace","email":"ada@example.com"}
            """;

        var canonical = BlockchainCertificateCanonicalizer.CanonicalizeIssuancePayload(payload);

        canonical.Should().NotContain("Ada Lovelace");
        canonical.Should().NotContain("ada@example.com");
        canonical.Should().NotContain("recipientName");
        canonical.Should().NotContain("email");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    [InlineData("""{"id":"x"}""")]
    [InlineData("""{"id":"x","issued-at":"y","issuer":"z","recipient-hash":"h","version":"not-a-number"}""")]
    public void CanonicalizeIssuancePayload_ReturnsNull_ForInvalidInput(string payload)
    {
        BlockchainCertificateCanonicalizer.CanonicalizeIssuancePayload(payload).Should().BeNull();
    }

    [Fact]
    public void ComputeSha256Hex_IsDeterministic_AndProducesLowercaseHex()
    {
        var first = BlockchainCertificateCanonicalizer.ComputeSha256Hex("gameguild");
        var second = BlockchainCertificateCanonicalizer.ComputeSha256Hex("gameguild");

        first.Should().Be(second);
        first.Should().HaveLength(64);
        first.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    // ── Local provider: anchor/verify round-trip ─────────────────────────

    [Fact]
    public async Task AnchorCertificateAsync_PersistsOnlyHashAndCanonicalPayload()
    {
        var (service, context) = CreateLocalService();
        var userId = Guid.NewGuid();
        var payload = """
            {"id":"01234567-89ab-cdef-0123-456789abcdef","issued-at":"2026-01-15T10:30:00.0000000Z","issuer":"gameguild","recipient-hash":"abc","version":1,"recipientName":"Ada Lovelace","email":"ada@example.com"}
            """;

        var result = await service.AnchorCertificateAsync(userId, payload, "learning-certificate");

        result.IsSuccess.Should().BeTrue();
        result.DataHash.Should().Be(BlockchainCertificateCanonicalizer.ComputeSha256Hex(
            BlockchainCertificateCanonicalizer.CanonicalizeIssuancePayload(payload)!));
        result.TransactionHash.Should().Be(BlockchainCertificateCanonicalizer.ComputeDeterministicTransactionHash(
            "local-anchor",
            BlockchainCertificateCanonicalizer.CanonicalizeIssuancePayload(payload)!));

        var anchor = await context.Set<BlockchainCertificateAnchor>().SingleAsync();
        anchor.UserId.Should().Be(userId);
        anchor.CertificateType.Should().Be("learning-certificate");
        anchor.BlockchainNetwork.Should().Be("local");
        // PII exclusion: only the whitelisted canonical payload is stored.
        anchor.CertificateData.Should().NotContain("Ada Lovelace");
        anchor.CertificateData.Should().NotContain("ada@example.com");
        anchor.CertificateData.Should().Contain("recipient-hash");
        anchor.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task AnchorCertificateAsync_RejectsNonCanonicalPayload()
    {
        var (service, context) = CreateLocalService();

        var result = await service.AnchorCertificateAsync(Guid.NewGuid(), "not json", "learning-certificate");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("canonical");
        (await context.Set<BlockchainCertificateAnchor>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AnchorCertificateAsync_IsIdempotent_ForTheSamePayload()
    {
        var (service, context) = CreateLocalService();
        var payload = ValidIssuancePayload();

        var first = await service.AnchorCertificateAsync(Guid.NewGuid(), payload, "learning-certificate");
        var second = await service.AnchorCertificateAsync(Guid.NewGuid(), payload, "learning-certificate");

        second.IsSuccess.Should().BeTrue();
        second.TransactionHash.Should().Be(first.TransactionHash);
        (await context.Set<BlockchainCertificateAnchor>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task VerifyCertificateAsync_RoundTrip_SucceedsForAnchoredCertificate()
    {
        var (service, _) = CreateLocalService();
        var payload = ValidIssuancePayload();
        var anchorResult = await service.AnchorCertificateAsync(Guid.NewGuid(), payload, "learning-certificate");

        (await service.VerifyCertificateAsync(anchorResult.DataHash!, anchorResult.TransactionHash!)).Should().BeTrue();
        (await service.VerifyCertificateAsync(anchorResult.DataHash!, "0x-wrong-transaction")).Should().BeFalse();
        (await service.VerifyCertificateAsync(new string('a', 64), anchorResult.TransactionHash!)).Should().BeFalse();
    }

    // ── Local provider: revocation semantics ─────────────────────────────

    [Fact]
    public async Task RevokeCertificateAsync_AppendsSecondAnchorLinkingOriginal_AndInvalidatesVerify()
    {
        var (service, context) = CreateLocalService();
        var payload = ValidIssuancePayload();
        var anchorResult = await service.AnchorCertificateAsync(Guid.NewGuid(), payload, "learning-certificate");
        (await service.VerifyCertificateAsync(anchorResult.DataHash!, anchorResult.TransactionHash!)).Should().BeTrue();

        var revocationTransaction = await service.RevokeCertificateAsync(anchorResult.DataHash!, "compromised issuance");

        var anchors = await context.Set<BlockchainCertificateAnchor>()
            .OrderBy(anchor => anchor.AnchoredAt)
            .ToListAsync();
        anchors.Should().HaveCount(2);

        var original = anchors.Single(anchor => anchor.CertificateType == "learning-certificate");
        original.IsRevoked.Should().BeTrue();
        original.RevocationReason.Should().Be("compromised issuance");
        original.RevocationTransactionHash.Should().Be(revocationTransaction);

        var revocation = anchors.Single(anchor => anchor.CertificateType != "learning-certificate");
        revocation.CertificateType.Should().Be(LocalBlockchainCertificateService.RevocationCertificateTypePrefix + anchorResult.DataHash);
        revocation.TransactionHash.Should().Be(revocationTransaction);
        revocation.CertificateData.Should().Contain(original.Id.ToString("D"));
        // The published revocation payload links to the original; the reason is never anchored.
        revocation.CertificateData.Should().NotContain("compromised issuance");

        (await service.VerifyCertificateAsync(anchorResult.DataHash!, anchorResult.TransactionHash!)).Should().BeFalse();
    }

    [Fact]
    public async Task RevokeCertificateAsync_IsIdempotent()
    {
        var (service, _) = CreateLocalService();
        var anchorResult = await service.AnchorCertificateAsync(Guid.NewGuid(), ValidIssuancePayload(), "learning-certificate");

        var first = await service.RevokeCertificateAsync(anchorResult.DataHash!, "reason");
        var second = await service.RevokeCertificateAsync(anchorResult.DataHash!, "reason again");

        second.Should().Be(first);
    }

    [Fact]
    public async Task RevokeCertificateAsync_Throws_WhenCertificateWasNeverAnchored()
    {
        var (service, _) = CreateLocalService();

        var act = () => service.RevokeCertificateAsync(new string('b', 64), "reason");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateVerifiableCredentialAsync_ThrowsNotSupported_ForLocalProvider()
    {
        var (service, _) = CreateLocalService();

        var act = () => service.GenerateVerifiableCredentialAsync(Guid.NewGuid(), "EmailVerified", new Dictionary<string, object>());

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task GetUserCertificatesAsync_ReturnsUserAnchors_NewestFirst()
    {
        var (service, _) = CreateLocalService();
        var userId = Guid.NewGuid();
        await service.AnchorCertificateAsync(userId, ValidIssuancePayload(), "learning-certificate");
        await service.AnchorCertificateAsync(Guid.NewGuid(), ValidIssuancePayload(), "learning-certificate");

        var anchors = await service.GetUserCertificatesAsync(userId);

        anchors.Should().HaveCount(1);
        anchors[0].UserId.Should().Be(userId);
    }

    // ── Disabled provider: no-op semantics ───────────────────────────────

    [Fact]
    public async Task DisabledProvider_IsAFullNoOp()
    {
        var service = new DisabledBlockchainCertificateService();

        var anchor = await service.AnchorCertificateAsync(Guid.NewGuid(), ValidIssuancePayload(), "learning-certificate");
        anchor.IsSuccess.Should().BeFalse();
        anchor.ErrorMessage.Should().Contain("disabled");

        (await service.VerifyCertificateAsync(new string('c', 64), "0xtx")).Should().BeFalse();
        (await service.GetUserCertificatesAsync(Guid.NewGuid())).Should().BeEmpty();

        await FluentActions.Awaiting(() => service.RevokeCertificateAsync(new string('c', 64), "reason"))
            .Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => service.GenerateVerifiableCredentialAsync(Guid.NewGuid(), "EmailVerified", new Dictionary<string, object>()))
            .Should().ThrowAsync<NotSupportedException>();
    }

    // ── Provider selection (config gate) ─────────────────────────────────

    [Fact]
    public void AddBlockchainCertificateAnchoring_DefaultsToDisabledNoOp()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        services.AddBlockchainCertificateAnchoring(configuration);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IBlockchainCertificateService>()
            .Should().BeOfType<DisabledBlockchainCertificateService>();
        provider.GetRequiredService<BlockchainCertificateOptions>().Provider
            .Should().Be(BlockchainCertificateOptions.ProviderNone);
    }

    [Fact]
    public void AddBlockchainCertificateAnchoring_LocalProvider_RegistersLocalService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IApplicationDbContext>(AnchorDbContext.Create());
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlockchainCertificates:Provider"] = "local"
            })
            .Build();

        services.AddBlockchainCertificateAnchoring(configuration);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IBlockchainCertificateService>()
            .Should().BeOfType<LocalBlockchainCertificateService>();
    }

    [Fact]
    public void AddBlockchainCertificateAnchoring_UnknownProvider_FailsClosed()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlockchainCertificates:Provider"] = "mainnet-ethereum"
            })
            .Build();

        var act = () => services.AddBlockchainCertificateAnchoring(configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage("*mainnet-ethereum*");
    }

    private sealed class AnchorDbContext(DbContextOptions<AnchorDbContext> options) : DbContext(options), IApplicationDbContext
    {
        public static AnchorDbContext Create()
        {
            var inMemoryOptions = new DbContextOptionsBuilder<AnchorDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new AnchorDbContext(inMemoryOptions);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Database.BeginTransactionAsync(cancellationToken);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new BlockchainCertificateAnchorConfiguration());
        }
    }
}
