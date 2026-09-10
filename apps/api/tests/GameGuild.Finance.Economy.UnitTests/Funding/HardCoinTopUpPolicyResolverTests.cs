using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Funding;

public sealed class HardCoinTopUpPolicyResolverTests
{
    private static readonly Guid TenantId = Guid.Parse("97000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("97000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ResolveAsync_DerivesTheUsdQuoteFromTheSignedActivePolicy()
    {
        const string payload =
            "{\"maximumHardCoinUnits\":100000,\"minimumHardCoinUnits\":100,\"provider\":\"stripe\"}";
        var resolver = Resolver(Policy(payload), signatureValid: true);

        var result = await resolver.ResolveAsync(TenantId, ActorId, 250, Now, default);

        result.Should().Be(new HardCoinTopUpPolicyAuthorization(
            "BRA", 11, Hash(payload), 250, 250, "stripe"));
    }

    [Theory]
    [InlineData(false, "{\"maximumHardCoinUnits\":100000,\"minimumHardCoinUnits\":100,\"provider\":\"stripe\"}")]
    [InlineData(true, "{}")]
    [InlineData(true, "{\"maximumHardCoinUnits\":99,\"minimumHardCoinUnits\":100,\"provider\":\"stripe\"}")]
    [InlineData(true, "{\"maximumHardCoinUnits\":100000,\"minimumHardCoinUnits\":0,\"provider\":\"stripe\"}")]
    [InlineData(true, "{\"maximumHardCoinUnits\":100000,\"minimumHardCoinUnits\":100,\"provider\":\"other\"}")]
    public async Task ResolveAsync_FailsClosedForUnsignedOrInvalidPolicies(bool signatureValid, string payload)
    {
        var action = () => Resolver(Policy(payload), signatureValid)
            .ResolveAsync(TenantId, ActorId, 250, Now, default).AsTask();

        await action.Should().ThrowAsync<EconomySelfServiceCommandRejectedException>();
    }

    [Fact]
    public async Task ResolveAsync_FailsClosedOutsideTheSignedAmountWindowOrWithoutPolicy()
    {
        const string payload =
            "{\"maximumHardCoinUnits\":1000,\"minimumHardCoinUnits\":100,\"provider\":\"stripe\"}";

        await FluentActions.Awaiting(() => Resolver(Policy(payload), true)
                .ResolveAsync(TenantId, ActorId, 99, Now, default).AsTask())
            .Should().ThrowAsync<EconomySelfServiceCommandRejectedException>();
        await FluentActions.Awaiting(() => Resolver(Policy(payload), true)
                .ResolveAsync(TenantId, ActorId, 1001, Now, default).AsTask())
            .Should().ThrowAsync<EconomySelfServiceCommandRejectedException>();
        await FluentActions.Awaiting(() => Resolver(null, true)
                .ResolveAsync(TenantId, ActorId, 100, Now, default).AsTask())
            .Should().ThrowAsync<EconomySelfServiceCommandRejectedException>();
        (await Resolver(Policy(payload), true).ResolveAsync(TenantId, ActorId, 251, Now, default))
            .UsdMinorUnits.Should().Be(251);
        await FluentActions.Awaiting(() => Resolver(Policy(payload), true)
                .ResolveAsync(TenantId, ActorId, 0, Now, default).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ResolveAsync_RejectsEveryStaleOrMismatchedPolicyAuthority()
    {
        const string payload =
            "{\"maximumHardCoinUnits\":1000,\"minimumHardCoinUnits\":100,\"provider\":\"stripe\"}";
        var baseline = Policy(payload);
        EconomyCapabilityPolicy[] invalid =
        [
            baseline with { TenantId = null },
            baseline with { TenantId = Guid.NewGuid() },
            baseline with { Capability = EconomyValueMovementCapability.Transfer },
            baseline with { JurisdictionCode = "USA" },
            baseline with { State = EconomyCapabilityPolicyState.PendingApproval },
            baseline with { EffectiveAt = Now.AddSeconds(1) },
            baseline with { ExpiresAt = Now },
            baseline with { ProviderReady = false },
            baseline with { PayloadHash = "invalid-hash" }
        ];

        foreach (var policy in invalid)
        {
            await FluentActions.Awaiting(() => Resolver(policy, true)
                    .ResolveAsync(TenantId, ActorId, 250, Now, default).AsTask())
                .Should().ThrowAsync<EconomySelfServiceCommandRejectedException>();
        }
    }

    [Theory]
    [InlineData("not-json", 250)]
    [InlineData("{\"maximumHardCoinUnits\":1000,\"minimumHardCoinUnits\":\"bad\",\"provider\":\"stripe\"}", 250)]
    [InlineData("{\"maximumHardCoinUnits\":1000,\"minimumHardCoinUnits\":100,\"provider\":42}", 250)]
    [InlineData("{\"maximumHardCoinUnits\":9223372036854775807,\"minimumHardCoinUnits\":1,\"provider\":\"stripe\"}", 9223372036854775807)]
    public async Task ResolveAsync_NormalizesMalformedAndOverflowingPolicyFailures(
        string payload,
        long amount)
    {
        await FluentActions.Awaiting(() => Resolver(Policy(payload), true)
                .ResolveAsync(TenantId, ActorId, amount, Now, default).AsTask())
            .Should().ThrowAsync<EconomySelfServiceCommandRejectedException>();
    }

    private static HardCoinTopUpPolicyResolver Resolver(
        EconomyCapabilityPolicy? policy,
        bool signatureValid) => new(
        new StubJurisdictionResolver(),
        new StubPolicyStore(policy),
        new StubSignatureVerifier(signatureValid));

    private static EconomyCapabilityPolicy Policy(string payload) => new(
        Guid.NewGuid(),
        $"{TenantId:N}:1:BRA",
        TenantId,
        EconomyValueMovementCapability.ConfirmHardCoinFunding,
        "BRA",
        11,
        payload,
        Hash(payload),
        "kms-key",
        "signature",
        Guid.NewGuid(),
        Guid.NewGuid(),
        Now.AddDays(-2),
        Now.AddDays(-1),
        Now.AddDays(-1),
        Now.AddDays(1),
        true,
        EconomyCapabilityPolicyState.Active);

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class StubJurisdictionResolver : IEconomyJurisdictionResolver
    {
        public ValueTask<EconomyJurisdictionResolution> ResolveAsync(
            Guid tenantId, Guid actorId, string? providerJurisdiction,
            string? destinationJurisdiction, DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken) => ValueTask.FromResult(
            new EconomyJurisdictionResolution("BRA", 3, 4, "kyc-evidence"));
    }

    private sealed class StubPolicyStore(EconomyCapabilityPolicy? policy) : IEconomyCapabilityPolicyStore
    {
        public ValueTask<EconomyCapabilityPolicy?> CurrentAsync(
            Guid? tenantId, EconomyValueMovementCapability capability, string jurisdictionCode,
            CancellationToken cancellationToken) => ValueTask.FromResult(policy);
        public ValueTask<EconomyCapabilityPolicy> ProposeAsync(
            EconomyCapabilityPolicyProposal proposal, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<EconomyCapabilityPolicy> ApproveAsync(
            Guid policyId, Guid actorId, string reauthenticationHash, DateTimeOffset approvedAt,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<int> ActivateDueAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubSignatureVerifier(bool valid) : ICapabilityPolicySignatureVerifier
    {
        public ValueTask<bool> VerifyAsync(
            string canonicalPayload, string keyId, string signature,
            CancellationToken cancellationToken) => ValueTask.FromResult(valid);
    }
}
