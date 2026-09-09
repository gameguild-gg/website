using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameGuild.Finance.Economy.AdRewards.Persistence;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.AdRewards.UnitTests;

public sealed class DurableAdRewardInfrastructureTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProviderResolver_IsCaseInsensitiveAndFailsClosed()
    {
        var adapter = new ProviderAdapter("network-a");
        var resolver = new AdRewardProviderAdapterResolver([adapter]);

        resolver.Resolve(" NETWORK-A ").Should().BeSameAs(adapter);
        FluentActions.Invoking(() => resolver.Resolve("missing"))
            .Should().Throw<AdRewardProviderUnavailableException>();
        FluentActions.Invoking(() => resolver.Resolve(" ")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new AdRewardProviderAdapterResolver(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AdRewardProviderAdapterResolver([
                new ProviderAdapter("same"), new ProviderAdapter("SAME")]))
            .Should().Throw<ArgumentException>();
        new AdRewardProviderUnavailableException("unavailable").Message.Should().Be("unavailable");
    }

    [Fact]
    public async Task KmsToken_RoundTripsAndRejectsTamperingTimingAndMalformedPayloads()
    {
        var signer = new RecordingSigner();
        var verifier = new RecordingVerifier();
        var protector = new KmsAdRewardSessionTokenProtector(signer, verifier);
        var claims = Claims();

        var token = await protector.ProtectAsync(claims);
        var restored = await protector.UnprotectAsync(token, Now);

        restored.Should().BeEquivalentTo(claims);
        signer.Payload.Should().NotBeNullOrWhiteSpace();
        verifier.Payload.Should().Be(signer.Payload);
        KmsAdRewardSessionTokenProtector.HashToken(token.Value).Should().HaveLength(64);
        KmsAdRewardSessionTokenProtector.HashOpaque("opaque").Should().HaveLength(64);

        await FluentActions.Awaiting(() => protector.UnprotectAsync(null!, Now).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => protector.UnprotectAsync(new SignedAdRewardSession(" "), Now).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => protector.UnprotectAsync(new SignedAdRewardSession("one.two"), Now).AsTask())
            .Should().ThrowAsync<InvalidAdRewardSessionTokenException>();
        await FluentActions.Awaiting(() => protector.UnprotectAsync(new SignedAdRewardSession("***.a.a"), Now).AsTask())
            .Should().ThrowAsync<InvalidAdRewardSessionTokenException>();
        await FluentActions.Awaiting(() => protector.UnprotectAsync(new SignedAdRewardSession("a.a.a"), Now).AsTask())
            .Should().ThrowAsync<InvalidAdRewardSessionTokenException>();
        await FluentActions.Awaiting(() => protector.UnprotectAsync(new SignedAdRewardSession("aaaa.aaaa.aaaa"), Now).AsTask())
            .Should().ThrowAsync<InvalidAdRewardSessionTokenException>();
        await FluentActions.Awaiting(() => protector.UnprotectAsync(ReplacePayload(token, "null"), Now).AsTask())
            .Should().ThrowAsync<InvalidAdRewardSessionTokenException>();
        var invalidPrimitivePayload = JsonSerializer.Serialize(new
        {
            claims.SessionId,
            claims.TenantId,
            claims.UserId,
            WalletId = Guid.Empty,
            claims.Network,
            claims.CreativeId,
            claims.DeviceRiskHash,
            claims.IpRiskHash,
            claims.AsnRiskHash,
            claims.Nonce,
            RequiredDurationTicks = claims.RequiredDuration.Ticks,
            PolicyVersion = claims.PolicyVersion.Value,
            claims.IssuedAt,
            claims.ExpiresAt
        });
        await FluentActions.Awaiting(() => protector.UnprotectAsync(
                ReplacePayload(token, invalidPrimitivePayload), Now).AsTask())
            .Should().ThrowAsync<InvalidAdRewardSessionTokenException>();

        verifier.Result = false;
        await FluentActions.Awaiting(() => protector.UnprotectAsync(token, Now).AsTask())
            .Should().ThrowAsync<InvalidAdRewardSessionTokenException>();
        verifier.Result = true;
        await FluentActions.Awaiting(() => protector.UnprotectAsync(token, claims.IssuedAt.AddTicks(-1)).AsTask())
            .Should().ThrowAsync<InvalidAdRewardSessionTokenException>();
        await FluentActions.Awaiting(() => protector.UnprotectAsync(token, claims.ExpiresAt).AsTask())
            .Should().ThrowAsync<ExpiredAdRewardSessionTokenException>();

        FluentActions.Invoking(() => new KmsAdRewardSessionTokenProtector(null!, verifier))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new KmsAdRewardSessionTokenProtector(signer, null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task KmsToken_ValidatesEveryRequiredClaim()
    {
        var protector = new KmsAdRewardSessionTokenProtector(new RecordingSigner(), new RecordingVerifier());
        var claims = Claims();
        var invalid = new[]
        {
            claims with { SessionId = Guid.Empty },
            claims with { TenantId = Guid.Empty },
            claims with { UserId = Guid.Empty },
            claims with { WalletId = default },
            claims with { Network = " " },
            claims with { CreativeId = " " },
            claims with { DeviceRiskHash = " " },
            claims with { IpRiskHash = " " },
            claims with { AsnRiskHash = " " },
            claims with { Nonce = " " },
            claims with { RequiredDuration = TimeSpan.Zero },
            claims with { ExpiresAt = claims.IssuedAt }
        };

        await FluentActions.Awaiting(() => protector.ProtectAsync(null!).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();
        foreach (var item in invalid)
        {
            await FluentActions.Awaiting(() => protector.ProtectAsync(item).AsTask())
                .Should().ThrowAsync<ArgumentException>();
        }
    }

    [Fact]
    public async Task PolicyReader_ReturnsSignedEffectiveAndVersionedPolicies()
    {
        await using var context = Context();
        var tenant = Guid.NewGuid();
        context.Set<AdNetworkPolicyVersionRow>().Add(PolicyRow(tenant, "network-a", 1));
        await context.SaveChangesAsync();
        var verifier = new RecordingVerifier();
        var reader = new PostgreSqlDurableAdRewardPolicyReader(context, verifier);

        var effective = await reader.GetEffectiveAsync(tenant, " network-a ", Now);
        var version = await reader.GetVersionAsync(tenant, "network-a", new PolicyVersion(1));

        effective.Should().BeEquivalentTo(version);
        effective.TenantId.Should().Be(tenant);
        effective.Policy.Network.Should().Be("network-a");
        effective.Policy.Version.Value.Should().Be(1);
        effective.Budget.MaximumUserSoftUnits.Should().Be(10_000);
        effective.MaximumIpSoftUnits.Should().Be(10_000);
        effective.ProviderCertified.Should().BeTrue();
        verifier.Payload.Should().Be("canonical-policy");
    }

    [Fact]
    public async Task PolicyReader_FailsClosedForMissingInvalidOrIneffectivePolicies()
    {
        await using var context = Context();
        var tenant = Guid.NewGuid();
        var reader = new PostgreSqlDurableAdRewardPolicyReader(context, new RecordingVerifier());

        await FluentActions.Awaiting(() => reader.GetEffectiveAsync(tenant, "network-a", Now).AsTask())
            .Should().ThrowAsync<AdNetworkPolicyUnavailableException>();
        await FluentActions.Awaiting(() => reader.GetVersionAsync(tenant, "network-a", new PolicyVersion(1)).AsTask())
            .Should().ThrowAsync<AdNetworkPolicyUnavailableException>();
        await FluentActions.Awaiting(() => reader.GetEffectiveAsync(Guid.Empty, "network-a", Now).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => reader.GetEffectiveAsync(tenant, " ", Now).AsTask())
            .Should().ThrowAsync<ArgumentException>();

        var badHash = PolicyRow(tenant, "bad-hash", 1);
        badHash.PayloadHash = "wrong";
        var sameActor = PolicyRow(tenant, "same-actor", 1);
        sameActor.ApprovedBy = sameActor.ProposedBy;
        var expired = PolicyRow(tenant, "expired", 1);
        context.AddRange(badHash, sameActor, expired);
        await context.SaveChangesAsync();

        await FluentActions.Awaiting(() => reader.GetVersionAsync(tenant, "bad-hash", new PolicyVersion(1)).AsTask())
            .Should().ThrowAsync<AdNetworkPolicyUnavailableException>();
        await FluentActions.Awaiting(() => reader.GetVersionAsync(tenant, "same-actor", new PolicyVersion(1)).AsTask())
            .Should().ThrowAsync<AdNetworkPolicyUnavailableException>();
        await FluentActions.Awaiting(() => reader.GetEffectiveAsync(tenant, "expired", Now.AddDays(2)).AsTask())
            .Should().ThrowAsync<AdNetworkPolicyUnavailableException>();

        var rejectedVerifier = new PostgreSqlDurableAdRewardPolicyReader(context, new RecordingVerifier { Result = false });
        await FluentActions.Awaiting(() => rejectedVerifier.GetVersionAsync(tenant, "expired", new PolicyVersion(1)).AsTask())
            .Should().ThrowAsync<AdNetworkPolicyUnavailableException>();

        FluentActions.Invoking(() => new PostgreSqlDurableAdRewardPolicyReader(null!, new RecordingVerifier()))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlDurableAdRewardPolicyReader(context, null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlDurableAdRewardPolicyReader(new StubContext(), new RecordingVerifier()))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task SessionReader_IsTenantAndUserScopedAndMapsOptionalCompletion()
    {
        await using var context = Context();
        var row = SessionRow();
        context.Add(row);
        await context.SaveChangesAsync();
        var reader = new PostgreSqlDurableAdRewardSessionReader(context);

        var status = await reader.FindAsync(row.TenantId, row.UserId, row.Id);

        status.Should().NotBeNull();
        status!.SessionId.Should().Be(row.Id);
        status.State.Should().Be(row.State);
        status.Network.Should().Be(row.Network);
        status.CreativeId.Should().Be(row.CreativeId);
        status!.RewardSoftUnits.Should().Be(0);
        status.PostingId.Should().BeNull();
        status.IssuedAt.Should().Be(row.IssuedAt);
        status.ExpiresAt.Should().Be(row.ExpiresAt);
        status.UpdatedAt.Should().Be(row.UpdatedAt);
        (await reader.FindAsync(Guid.NewGuid(), row.UserId, row.Id)).Should().BeNull();

        context.Add(new AdRewardCompletionRow
        {
            SessionId = row.Id, TenantId = row.TenantId, UserId = row.UserId, WalletId = row.WalletId,
            Network = row.Network, PolicyVersion = 1, IdempotencyKey = "completion", State = AdRewardCompletionState.Issued,
            RewardSoftUnits = 25, SourceStampId = Guid.NewGuid(), PostingId = Guid.NewGuid(), OutputLotId = Guid.NewGuid(),
            EvidenceHashes = "[]", CompletedAt = Now, Version = 1
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        status = await reader.FindAsync(row.TenantId, row.UserId, row.Id);
        status!.RewardSoftUnits.Should().Be(25);
        status.PostingId.Should().NotBeNull();

        await FluentActions.Awaiting(() => reader.FindAsync(Guid.Empty, row.UserId, row.Id).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => reader.FindAsync(row.TenantId, Guid.Empty, row.Id).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => reader.FindAsync(row.TenantId, row.UserId, Guid.Empty).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        FluentActions.Invoking(() => new PostgreSqlDurableAdRewardSessionReader(new StubContext()))
            .Should().Throw<InvalidOperationException>();
    }

    private static DurableAdRewardSessionClaims Claims() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), WalletId.New(), "network-a", "creative",
        "device", "ip", "asn", "nonce", TimeSpan.FromSeconds(30), new PolicyVersion(1),
        Now.AddMinutes(-1), Now.AddMinutes(4));

    private static SignedAdRewardSession ReplacePayload(SignedAdRewardSession token, string payload)
    {
        var parts = token.Value.Split('.');
        parts[0] = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new SignedAdRewardSession(string.Join('.', parts));
    }

    private static AdNetworkPolicyVersionRow PolicyRow(Guid tenant, string network, long version)
    {
        var payload = "canonical-policy";
        return new AdNetworkPolicyVersionRow
        {
            TenantId = tenant, Network = network, Version = version,
            EffectiveAt = Now.AddHours(-1), ExpiresAt = Now.AddHours(1),
            IssuanceMode = AdRewardIssuanceMode.ImmediateProviderProof, YieldState = AdNetworkYieldState.Trailing,
            EstimatedNetEcpmUsdNanos = 1_000_000_000, ContractedRevenueSharePpm = 500_000,
            SafetyBufferPpm = 100_000, MinimumVisiblePpm = 800_000,
            MaximumFocusLossTicks = TimeSpan.FromSeconds(2).Ticks, MaximumRewardSoftUnits = 1_000,
            ReportsCurrentThrough = Now, ReportStaleAfterTicks = TimeSpan.FromHours(1).Ticks, Ranking = 1,
            MaximumUserSoftUnits = 10_000, MaximumDeviceSoftUnits = 10_000,
            MaximumIpSoftUnits = 10_000, MaximumAsnSoftUnits = 10_000,
            MaximumNetworkSoftUnits = 10_000, MaximumGlobalSoftUnits = 10_000,
            FundedLossBudgetUsdNanos = 10_000_000_000, BudgetWindowTicks = TimeSpan.FromDays(1).Ticks,
            ProviderHash = "provider-hash", ProviderCertified = true, CanonicalPayload = payload,
            PayloadHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload))),
            KeyId = "key", Signature = "signature", ProposedBy = Guid.NewGuid(), ApprovedBy = Guid.NewGuid(),
            PublishedAt = Now.AddHours(-2)
        };
    }

    private static AdRewardSessionRow SessionRow() => new()
    {
        Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), UserId = Guid.NewGuid(), WalletId = Guid.NewGuid(),
        Network = "network-a", PolicyVersion = 1, CreativeId = "creative", DeviceRiskHash = "device",
        IpRiskHash = "ip", AsnRiskHash = "asn", NonceHash = "nonce", TokenHash = "token", TokenKeyId = "key",
        RequiredDurationTicks = TimeSpan.FromSeconds(30).Ticks, State = DurableAdRewardSessionState.Issued,
        StartIdempotencyKeyHash = Guid.NewGuid().ToString("N"), StartRequestHash = "request",
        IssuedAt = Now.AddMinutes(-1), ExpiresAt = Now.AddMinutes(4), UpdatedAt = Now.AddMinutes(-1), Version = 1
    };

    private static TestDbContext Context() => new(new DbContextOptionsBuilder<TestDbContext>()
        .UseInMemoryDatabase("ad-infrastructure-" + Guid.NewGuid().ToString("N")).Options);

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new AdRewardsModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingSigner : ICapabilityReceiptSigner
    {
        public string? Payload { get; private set; }
        public ValueTask<CapabilityReceiptSignature> SignAsync(string canonicalPayload, CancellationToken cancellationToken)
        {
            Payload = canonicalPayload;
            return ValueTask.FromResult(new CapabilityReceiptSignature("key-1", "signature-1"));
        }
    }

    private sealed class RecordingVerifier : ICapabilityPolicySignatureVerifier
    {
        public bool Result { get; set; } = true;
        public string? Payload { get; private set; }
        public ValueTask<bool> VerifyAsync(string canonicalPayload, string keyId, string signature, CancellationToken cancellationToken)
        {
            Payload = canonicalPayload;
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class ProviderAdapter(string network) : IAdRewardProviderAdapter
    {
        public string Network { get; } = network;
        public ValueTask<AdRewardProviderProofVerification> VerifyCompletionAsync(
            DurableAdRewardSessionClaims session, ProviderCompletionProof proof,
            DateTimeOffset receivedAt, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AdRewardProviderProofVerification(true, proof.EvidenceHash, "payload", receivedAt));
        public ValueTask<bool> VerifyReportAsync(AdProviderReport report, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);
    }
}
