using System.Security.Cryptography;
using System.Text;
using System.Numerics;
using FluentAssertions;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Funding;

public sealed class HardToSoftConversionPolicyResolverTests
{
    private static readonly Guid TenantId = Guid.Parse("96000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("96000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ResolveAsync_DerivesFeeAndRiskLimitsFromTheSignedActivePolicy()
    {
        const string payload =
            "{\"conversionFeePpm\":10000,\"decisionLifetimeSeconds\":300,\"maximumHardCoinUnitsPerDay\":1000}";
        var resolver = Resolver(Policy(payload), signatureValid: true);

        var result = await resolver.ResolveAsync(TenantId, ActorId, 101, Now, default);

        result.Should().Be(new HardToSoftConversionPolicyAuthorization(
            "BRA", 9, Hash(payload), 2, 1_000, 300));
    }

    [Theory]
    [InlineData(false, "{\"conversionFeePpm\":0,\"decisionLifetimeSeconds\":300,\"maximumHardCoinUnitsPerDay\":1000}")]
    [InlineData(true, "{\"conversionFeePpm\":1000000,\"decisionLifetimeSeconds\":300,\"maximumHardCoinUnitsPerDay\":1000}")]
    [InlineData(true, "{\"conversionFeePpm\":0,\"decisionLifetimeSeconds\":1,\"maximumHardCoinUnitsPerDay\":1000}")]
    [InlineData(true, "{}")]
    public async Task ResolveAsync_FailsClosedForUnsignedOrInvalidPolicies(bool signatureValid, string payload)
    {
        var resolver = Resolver(Policy(payload), signatureValid);

        var action = () => resolver.ResolveAsync(TenantId, ActorId, 100, Now, default).AsTask();

        await action.Should().ThrowAsync<EconomySelfServiceCommandRejectedException>();
    }

    [Fact]
    public async Task ResolveAsync_FailsClosedWhenNoTenantPolicyIsActive()
    {
        var resolver = Resolver(null, signatureValid: true);

        var action = () => resolver.ResolveAsync(TenantId, ActorId, 100, Now, default).AsTask();

        await action.Should().ThrowAsync<EconomySelfServiceCommandRejectedException>();
    }

    [Fact]
    public void CeilingConversionRejectsValuesOutsideThePersistentUnitRange()
    {
        FluentActions.Invoking(() => HardToSoftConversionPolicyResolver.ToLongCeiling(
                (BigInteger.One + long.MaxValue) * 1_000_000,
                1_000_000))
            .Should().Throw<OverflowException>();
    }

    private static HardToSoftConversionPolicyResolver Resolver(
        EconomyCapabilityPolicy? policy,
        bool signatureValid) => new(
        new StubJurisdictionResolver(),
        new StubPolicyStore(policy),
        new StubSignatureVerifier(signatureValid));

    private static EconomyCapabilityPolicy Policy(string payload) => new(
        Guid.NewGuid(),
        $"{TenantId:N}:2:BRA",
        TenantId,
        EconomyValueMovementCapability.ConvertHardToSoft,
        "BRA",
        9,
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
