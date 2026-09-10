using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.AdRewards.UnitTests;

public sealed class AdRewardSessionSecurityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-18T12:00:00Z");
    private static readonly byte[] SessionSecret = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
    private static readonly byte[] ProviderSecret = Enumerable.Range(33, 32).Select(value => (byte)value).ToArray();

    [Fact]
    public void PolicyStore_SelectsCurrentVersionAndPreservesColdStartState()
    {
        var store = new AdNetworkPolicyStore();
        var policy = Policy(version: 1, state: AdNetworkYieldState.ColdStart);

        store.Publish(policy);

        store.Current("unity", Now).Should().Be(policy);
        policy.YieldState.Should().Be(AdNetworkYieldState.ColdStart);
        policy.EstimatedNetEcpmUsdNanos.Should().Be(2_000_000_000);
    }

    [Fact]
    public void PolicyStore_RejectsOverlappingOrRegressingVersions()
    {
        var store = new AdNetworkPolicyStore();
        store.Publish(Policy(version: 2));

        FluentActions.Invoking(() => store.Publish(Policy(version: 1)))
            .Should().Throw<AdNetworkPolicyConflictException>();
        FluentActions.Invoking(() => store.Publish(Policy(version: 3, effectiveAt: Now.AddMinutes(30))))
            .Should().Throw<AdNetworkPolicyConflictException>();
    }

    [Fact]
    public void ControlState_EnforcesIndependentGlobalAndNetworkKillSwitches()
    {
        var controls = new AdRewardControlState();

        controls.EnsureIssuanceEnabled("unity");
        controls.DisableNetwork("unity", 2, "provider-report-stale");
        FluentActions.Invoking(() => controls.EnsureIssuanceEnabled("unity"))
            .Should().Throw<AdRewardIssuanceDisabledException>();

        controls.EnableNetwork("unity", 3);
        controls.DisableGlobally(4, "fraud-loss-budget-exhausted");
        FluentActions.Invoking(() => controls.EnsureIssuanceEnabled("unity"))
            .Should().Throw<AdRewardIssuanceDisabledException>();
    }

    [Fact]
    public void SessionToken_IsSignedShortLivedAndBoundToEveryClaim()
    {
        var service = new AdRewardSessionTokenService(SessionSecret, TimeSpan.FromMinutes(5));
        var claims = Claims();

        var token = service.Issue(claims, Now);
        var validated = service.Validate(token.Value, Now.AddMinutes(4));

        validated.Should().Be(claims);
        FluentActions.Invoking(() => service.Validate(token.Value + "x", Now.AddMinutes(4)))
            .Should().Throw<InvalidAdRewardSessionTokenException>();
        FluentActions.Invoking(() => service.Validate(token.Value, Now.AddMinutes(5)))
            .Should().Throw<ExpiredAdRewardSessionTokenException>();
    }

    [Fact]
    public void SessionToken_RejectsWeakSecretsAndCallerControlledExpiry()
    {
        FluentActions.Invoking(() => new AdRewardSessionTokenService(new byte[31], TimeSpan.FromMinutes(5)))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new AdRewardSessionTokenService(SessionSecret, TimeSpan.FromMinutes(11)))
            .Should().Throw<ArgumentOutOfRangeException>();

        var service = new AdRewardSessionTokenService(SessionSecret, TimeSpan.FromMinutes(5));
        FluentActions.Invoking(() => service.Issue(Claims() with { ExpiresAt = Now.AddMinutes(8) }, Now))
            .Should().Throw<InvalidAdRewardSessionTokenException>();
    }

    [Fact]
    public void SessionService_StartsServerOwnedSessionFromCurrentPolicy()
    {
        var store = new AdNetworkPolicyStore();
        store.Publish(Policy());
        var tokens = new AdRewardSessionTokenService(SessionSecret, TimeSpan.FromMinutes(5));
        var service = new AdRewardSessionService(
            store,
            new AdRewardControlState(),
            tokens,
            new FixedSessionEntropy());

        var result = service.Start(new AdRewardSessionRequest(
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            new WalletId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
            " unity ",
            " creative-1 ",
            " device-risk-hash ",
            TimeSpan.FromSeconds(30)), Now);

        result.Claims.SessionId.Should().Be(FixedSessionEntropy.SessionId);
        result.Claims.Network.Should().Be("unity");
        result.Claims.CreativeId.Should().Be("creative-1");
        result.Claims.Nonce.Should().Be(FixedSessionEntropy.Nonce);
        result.Claims.PolicyVersion.Should().Be(new PolicyVersion(1));
        result.Claims.ExpiresAt.Should().Be(Now.AddMinutes(5));
        tokens.Validate(result.Token.Value, Now).Should().Be(result.Claims);
    }

    [Fact]
    public void SessionService_FailsClosedForDisabledStaleOrKilledNetwork()
    {
        var disabledStore = new AdNetworkPolicyStore();
        disabledStore.Publish(Policy(mode: AdRewardIssuanceMode.Disabled));
        var controls = new AdRewardControlState();
        var tokens = new AdRewardSessionTokenService(SessionSecret, TimeSpan.FromMinutes(5));
        var request = new AdRewardSessionRequest(
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            new WalletId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
            "unity", "creative-1", "device-risk-hash", TimeSpan.FromSeconds(30));

        FluentActions.Invoking(() => new AdRewardSessionService(
                disabledStore, controls, tokens, new FixedSessionEntropy()).Start(request, Now))
            .Should().Throw<AdRewardIssuanceDisabledException>();

        var staleStore = new AdNetworkPolicyStore();
        staleStore.Publish(Policy(reportsCurrentThrough: Now.AddDays(-2)));
        FluentActions.Invoking(() => new AdRewardSessionService(
                staleStore, controls, tokens, new FixedSessionEntropy()).Start(request, Now))
            .Should().Throw<AdNetworkReportStaleException>();

        var activeStore = new AdNetworkPolicyStore();
        activeStore.Publish(Policy());
        controls.DisableNetwork("unity", 1, "manual-stop");
        FluentActions.Invoking(() => new AdRewardSessionService(
                activeStore, controls, tokens, new FixedSessionEntropy()).Start(request, Now))
            .Should().Throw<AdRewardIssuanceDisabledException>();
    }

    [Fact]
    public void ProviderProof_IsCryptographicallyBoundToSessionCreativeAndCompletion()
    {
        var verifier = new HmacProviderCompletionProofService("unity", ProviderSecret);
        var claims = Claims();
        var proof = verifier.Sign(
            "event-1", claims.SessionId, claims.CreativeId, Now.AddSeconds(31), "provider-evidence");

        verifier.Verify(proof, claims, Now.AddSeconds(32)).Should().BeTrue();
        verifier.Verify(proof with { CreativeId = "other" }, claims, Now.AddSeconds(32)).Should().BeFalse();
        verifier.Verify(proof with { Signature = proof.Signature + "x" }, claims, Now.AddSeconds(32)).Should().BeFalse();
    }

    [Fact]
    public void PlaybackVerifier_RequiresOrderedTimingVisibilityAndProviderProof()
    {
        var claims = Claims();
        var proofService = new HmacProviderCompletionProofService("unity", ProviderSecret);
        var proof = proofService.Sign(
            "event-1", claims.SessionId, claims.CreativeId, Now.AddSeconds(31), "provider-evidence");
        var verifier = new AdPlaybackVerifier(proofService);
        var evidence = Evidence();

        verifier.Verify(claims, evidence, proof, Policy(), Now.AddSeconds(32)).Should().BeTrue();

        FluentActions.Invoking(() => verifier.Verify(
                claims,
                evidence with { VisibleDuration = TimeSpan.FromSeconds(10) },
                proof,
                Policy(),
                Now.AddSeconds(32)))
            .Should().Throw<AdPlaybackVerificationException>();
        FluentActions.Invoking(() => verifier.Verify(
                claims,
                evidence with { Milestones = [0, 50, 25, 100] },
                proof,
                Policy(),
                Now.AddSeconds(32)))
            .Should().Throw<AdPlaybackVerificationException>();
    }

    [Fact]
    public void ImmediatePolicyRequiresProofWhileDeferredPolicyCreatesNoFalseAuthority()
    {
        var claims = Claims();
        var verifier = new AdPlaybackVerifier(new HmacProviderCompletionProofService("unity", ProviderSecret));

        FluentActions.Invoking(() => verifier.Verify(claims, Evidence(), null, Policy(), Now.AddSeconds(32)))
            .Should().Throw<AdProviderProofRequiredException>();

        var deferred = Policy(mode: AdRewardIssuanceMode.DeferredReport);
        verifier.Verify(claims, Evidence(), null, deferred, Now.AddSeconds(32)).Should().BeFalse();
    }

    private static AdRewardSessionClaims Claims() => new(
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("20000000-0000-0000-0000-000000000002"),
        new WalletId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
        "unity",
        "creative-1",
        "device-risk-hash",
        "nonce-1",
        TimeSpan.FromSeconds(30),
        new PolicyVersion(1),
        Now,
        Now.AddMinutes(5));

    private static AdPlaybackEvidence Evidence() => new(
        Now,
        Now.AddSeconds(31),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(29),
        TimeSpan.FromSeconds(1),
        [0, 25, 50, 75, 100]);

    private static AdNetworkPolicy Policy(
        long version = 1,
        AdNetworkYieldState state = AdNetworkYieldState.Trailing,
        AdRewardIssuanceMode mode = AdRewardIssuanceMode.ImmediateProviderProof,
        DateTimeOffset? effectiveAt = null,
        DateTimeOffset? reportsCurrentThrough = null) => new(
        "unity",
        new PolicyVersion(version),
        effectiveAt ?? Now.AddHours(-1),
        Now.AddHours(1),
        mode,
        state,
        estimatedNetEcpmUsdNanos: 2_000_000_000,
        contractedRevenueSharePpm: 700_000,
        safetyBufferPpm: 200_000,
        minimumVisiblePpm: 900_000,
        maximumFocusLoss: TimeSpan.FromSeconds(3),
        maximumRewardSoftUnits: 1_000,
        reportsCurrentThrough: reportsCurrentThrough ?? Now,
        reportStaleAfter: TimeSpan.FromHours(24),
        ranking: 100);

    private sealed class FixedSessionEntropy : IAdRewardSessionEntropy
    {
        internal static readonly Guid SessionId = Guid.Parse("40000000-0000-0000-0000-000000000004");
        internal const string Nonce = "server-nonce";

        public Guid CreateSessionId() => SessionId;
        public string CreateNonce() => Nonce;
    }
}
