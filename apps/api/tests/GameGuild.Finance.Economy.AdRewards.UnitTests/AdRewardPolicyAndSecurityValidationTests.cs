using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.AdRewards.UnitTests;

public sealed class AdRewardPolicyAndSecurityValidationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-18T12:00:00Z");
    private static readonly byte[] SessionSecret = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
    private static readonly byte[] ProviderSecret = Enumerable.Range(33, 32).Select(value => (byte)value).ToArray();

    [Fact]
    public void Policy_RejectsEveryInvalidContractValue()
    {
        AssertPolicyFailure(() => Policy(network: " "), typeof(ArgumentException));
        AssertPolicyFailure(() => Policy(expiresAt: Now.AddHours(-1)), typeof(ArgumentException));
        AssertPolicyFailure(() => Policy(mode: (AdRewardIssuanceMode)99), typeof(ArgumentOutOfRangeException));
        AssertPolicyFailure(() => Policy(state: (AdNetworkYieldState)99), typeof(ArgumentOutOfRangeException));
        AssertPolicyFailure(() => Policy(ecpm: 0), typeof(ArgumentOutOfRangeException));
        AssertPolicyFailure(() => Policy(share: -1), typeof(ArgumentOutOfRangeException));
        AssertPolicyFailure(() => Policy(share: 1_000_001), typeof(ArgumentOutOfRangeException));
        AssertPolicyFailure(() => Policy(buffer: -1), typeof(ArgumentOutOfRangeException));
        AssertPolicyFailure(() => Policy(buffer: 1_000_000), typeof(ArgumentOutOfRangeException));
        AssertPolicyFailure(() => Policy(minimumVisible: -1), typeof(ArgumentOutOfRangeException));
        AssertPolicyFailure(() => Policy(minimumVisible: 1_000_001), typeof(ArgumentOutOfRangeException));
        AssertPolicyFailure(() => Policy(maximumFocusLoss: TimeSpan.FromTicks(-1)), typeof(ArgumentOutOfRangeException));
        AssertPolicyFailure(() => Policy(maximumReward: 0), typeof(ArgumentOutOfRangeException));
        AssertPolicyFailure(() => Policy(reportStaleAfter: TimeSpan.Zero), typeof(ArgumentOutOfRangeException));
        AssertPolicyFailure(() => Policy(ranking: -1), typeof(ArgumentOutOfRangeException));

        var policy = Policy();
        policy.IsEffective(Now).Should().BeTrue();
        policy.IsEffective(Now.AddHours(-2)).Should().BeFalse();
        policy.IsEffective(Now.AddHours(1)).Should().BeFalse();
        policy.IsReportCurrent(Now.AddHours(24)).Should().BeTrue();
        policy.IsReportCurrent(Now.AddHours(24).AddTicks(1)).Should().BeFalse();
    }

    [Fact]
    public void PolicyStore_CoversVersionLookupAbsenceAndNonOverlappingPublication()
    {
        var store = new AdNetworkPolicyStore();
        FluentActions.Invoking(() => store.Publish(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => store.Current(" ", Now)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.Current("unity", Now)).Should().Throw<AdNetworkPolicyUnavailableException>();
        FluentActions.Invoking(() => store.Get("unity", new PolicyVersion(1))).Should().Throw<AdNetworkPolicyUnavailableException>();
        FluentActions.Invoking(() => store.Latest("unity")).Should().Throw<AdNetworkPolicyUnavailableException>();

        var first = Policy();
        var second = Policy(version: 2, effectiveAt: first.ExpiresAt, expiresAt: first.ExpiresAt.AddHours(1));
        store.Publish(first);
        store.Publish(second);

        store.Get(" unity ", first.Version).Should().Be(first);
        store.Latest(" unity ").Should().Be(second);
        store.Current("unity", first.EffectiveAt).Should().Be(first);
        store.Current("unity", second.EffectiveAt).Should().Be(second);
        FluentActions.Invoking(() => store.Get(" ", first.Version)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.Latest(" ")).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ControlState_ValidatesEpochsAndSupportsReversibleMonotonicControls()
    {
        var controls = new AdRewardControlState();
        FluentActions.Invoking(() => controls.DisableNetwork(" ", 1, "reason")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => controls.DisableNetwork("unity", 0, "reason")).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => controls.DisableNetwork("unity", 1, " ")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => controls.EnableNetwork(" ", 1)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => controls.EnableNetwork("unity", 0)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => controls.DisableGlobally(0, "reason")).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => controls.DisableGlobally(1, " ")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => controls.EnableGlobally(0)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => controls.EnsureIssuanceEnabled(" ")).Should().Throw<ArgumentException>();

        controls.DisableNetwork(" unity ", 1, "manual");
        FluentActions.Invoking(() => controls.DisableNetwork("unity", 1, "replay"))
            .Should().Throw<AdRewardControlConflictException>();
        controls.EnableNetwork("unity", 2);
        controls.EnsureIssuanceEnabled("unity");

        controls.DisableGlobally(3, "manual");
        FluentActions.Invoking(() => controls.DisableGlobally(3, "replay"))
            .Should().Throw<AdRewardControlConflictException>();
        controls.EnableGlobally(4);
        FluentActions.Invoking(() => controls.EnableGlobally(4))
            .Should().Throw<AdRewardControlConflictException>();
        controls.EnsureIssuanceEnabled("unity");
    }

    [Fact]
    public void SessionToken_RejectsMalformedInactiveAndInvalidClaims()
    {
        FluentActions.Invoking(() => new AdRewardSessionTokenService(null!, TimeSpan.FromMinutes(1)))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AdRewardSessionTokenService(SessionSecret, TimeSpan.Zero))
            .Should().Throw<ArgumentOutOfRangeException>();
        var service = new AdRewardSessionTokenService(SessionSecret, TimeSpan.FromMinutes(5));
        service.Lifetime.Should().Be(TimeSpan.FromMinutes(5));
        FluentActions.Invoking(() => service.Issue(null!, Now)).Should().Throw<ArgumentNullException>();
        AssertInvalidClaim(service, Claims() with { SessionId = Guid.Empty });
        AssertInvalidClaim(service, Claims() with { UserId = Guid.Empty });
        AssertInvalidClaim(service, Claims() with { Network = " " });
        AssertInvalidClaim(service, Claims() with { CreativeId = "bad\u001fvalue" });
        AssertInvalidClaim(service, Claims() with { RequiredDuration = TimeSpan.Zero });
        AssertInvalidClaim(service, Claims() with { ExpiresAt = Now });
        FluentActions.Invoking(() => service.Issue(
                Claims() with { IssuedAt = Now.AddTicks(1), ExpiresAt = Now.AddMinutes(5) }, Now))
            .Should().Throw<InvalidAdRewardSessionTokenException>();

        FluentActions.Invoking(() => service.Validate(" ", Now)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => service.Validate("not-a-token", Now)).Should().Throw<InvalidAdRewardSessionTokenException>();
        FluentActions.Invoking(() => service.Validate("payload.***", Now)).Should().Throw<InvalidAdRewardSessionTokenException>();
        FluentActions.Invoking(() => service.Validate("payload." + Base64Url(new byte[32]), Now))
            .Should().Throw<InvalidAdRewardSessionTokenException>();
        foreach (var payload in new[] { "a", "ab", "abc" })
            FluentActions.Invoking(() => service.Validate(SignPayload(payload), Now))
                .Should().Throw<InvalidAdRewardSessionTokenException>();

        var token = service.Issue(Claims(), Now);
        FluentActions.Invoking(() => service.Validate(token.Value, Now.AddTicks(-1)))
            .Should().Throw<InvalidAdRewardSessionTokenException>();
    }

    [Fact]
    public void SessionService_ValidatesDependenciesAndRequestBoundary()
    {
        var store = new AdNetworkPolicyStore();
        store.Publish(Policy());
        var controls = new AdRewardControlState();
        var tokens = new AdRewardSessionTokenService(SessionSecret, TimeSpan.FromMinutes(5));
        var entropy = new FixedEntropy();
        FluentActions.Invoking(() => new AdRewardSessionService(null!, controls, tokens, entropy)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AdRewardSessionService(store, null!, tokens, entropy)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AdRewardSessionService(store, controls, null!, entropy)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AdRewardSessionService(store, controls, tokens, null!)).Should().Throw<ArgumentNullException>();
        var service = new AdRewardSessionService(store, controls, tokens, entropy);
        var valid = Request();
        FluentActions.Invoking(() => service.Start(null!, Now)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => service.Start(valid with { UserId = Guid.Empty }, Now)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => service.Start(valid with { Network = " " }, Now)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => service.Start(valid with { CreativeId = " " }, Now)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => service.Start(valid with { DeviceRiskHash = " " }, Now)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => service.Start(valid with { RequiredDuration = TimeSpan.Zero }, Now)).Should().Throw<ArgumentOutOfRangeException>();

        var missing = new AdRewardSessionService(new AdNetworkPolicyStore(), controls, tokens, entropy);
        FluentActions.Invoking(() => missing.Start(valid, Now)).Should().Throw<AdNetworkPolicyUnavailableException>();

        var crypto = new CryptographicAdRewardSessionEntropy();
        crypto.CreateSessionId().Should().NotBeEmpty();
        Convert.FromBase64String(crypto.CreateNonce().Replace('-', '+').Replace('_', '/') + "=").Should().HaveCount(32);
    }

    [Fact]
    public void ProviderProof_ValidatesInputAndRejectsEveryBindingMismatch()
    {
        FluentActions.Invoking(() => new HmacProviderCompletionProofService(" ", ProviderSecret)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new HmacProviderCompletionProofService("unity", null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new HmacProviderCompletionProofService("unity", new byte[31])).Should().Throw<ArgumentException>();
        var service = new HmacProviderCompletionProofService("unity", ProviderSecret);
        var claims = Claims();
        FluentActions.Invoking(() => service.Sign(" ", claims.SessionId, claims.CreativeId, Now, "hash")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => service.Sign("event", Guid.Empty, claims.CreativeId, Now, "hash")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => service.Sign("event", claims.SessionId, " ", Now, "hash")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => service.Sign("event", claims.SessionId, claims.CreativeId, Now, " ")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => service.Verify(null!, claims, Now)).Should().Throw<ArgumentNullException>();
        var proof = service.Sign("event", claims.SessionId, claims.CreativeId, Now, "hash");
        FluentActions.Invoking(() => service.Verify(proof, null!, Now)).Should().Throw<ArgumentNullException>();

        service.Verify(proof with { Network = "other" }, claims, Now).Should().BeFalse();
        service.Verify(proof, claims with { Network = "other" }, Now).Should().BeFalse();
        service.Verify(proof with { SessionId = Guid.NewGuid() }, claims, Now).Should().BeFalse();
        service.Verify(proof with { CreativeId = "other" }, claims, Now).Should().BeFalse();
        service.Verify(proof with { CompletedAt = claims.IssuedAt.AddTicks(-1) }, claims, Now).Should().BeFalse();
        service.Verify(proof with { CompletedAt = Now.AddTicks(1) }, claims, Now).Should().BeFalse();
        service.Verify(proof with { Signature = "***" }, claims, Now).Should().BeFalse();
    }

    [Fact]
    public void PlaybackVerifier_RejectsEveryPhysicalVisibilityAndMilestoneViolation()
    {
        FluentActions.Invoking(() => new AdPlaybackVerifier(null!)).Should().Throw<ArgumentNullException>();
        var verifier = new AdPlaybackVerifier(new HmacProviderCompletionProofService("unity", ProviderSecret));
        var claims = Claims();
        var evidence = Evidence();
        var policy = Policy();
        var proof = new HmacProviderCompletionProofService("unity", ProviderSecret)
            .Sign("event", claims.SessionId, claims.CreativeId, Now.AddSeconds(31), "hash");
        FluentActions.Invoking(() => verifier.Verify(null!, evidence, proof, policy, Now.AddSeconds(32))).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => verifier.Verify(claims, null!, proof, policy, Now.AddSeconds(32))).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => verifier.Verify(claims, evidence, proof, null!, Now.AddSeconds(32))).Should().Throw<ArgumentNullException>();
        AssertPlaybackFailure(verifier, claims, evidence, proof, Policy(effectiveAt: Now.AddMinutes(1)), Now.AddSeconds(32));
        AssertPlaybackFailure(verifier, claims, evidence, proof, Policy(network: "other"), Now.AddSeconds(32));
        AssertPlaybackFailure(verifier, claims, evidence with { StartedAt = Now.AddTicks(-1) }, proof, policy, Now.AddSeconds(32));
        AssertPlaybackFailure(verifier, claims, evidence with { CompletedAt = Now.AddSeconds(33) }, proof, policy, Now.AddSeconds(32));
        AssertPlaybackFailure(verifier, claims, evidence with { CompletedAt = Now.AddTicks(-1) }, proof, policy, Now.AddSeconds(32));
        AssertPlaybackFailure(verifier, claims, evidence with { PlaybackDuration = TimeSpan.FromSeconds(29) }, proof, policy, Now.AddSeconds(32));
        AssertPlaybackFailure(verifier, claims, evidence with { VisibleDuration = TimeSpan.FromTicks(-1) }, proof, policy, Now.AddSeconds(32));
        AssertPlaybackFailure(verifier, claims, evidence with { VisibleDuration = TimeSpan.FromSeconds(31) }, proof, policy, Now.AddSeconds(32));
        AssertPlaybackFailure(verifier, claims, evidence with { FocusLoss = TimeSpan.FromTicks(-1) }, proof, policy, Now.AddSeconds(32));
        AssertPlaybackFailure(verifier, claims, evidence with { FocusLoss = TimeSpan.FromSeconds(4) }, proof, policy, Now.AddSeconds(32));
        AssertPlaybackFailure(verifier, claims, evidence with { VisibleDuration = TimeSpan.FromSeconds(26) }, proof, policy, Now.AddSeconds(32));
        AssertPlaybackFailure(verifier, claims, evidence with { Milestones = [0] }, proof, policy, Now.AddSeconds(32));
        AssertPlaybackFailure(verifier, claims, evidence with { Milestones = [1, 100] }, proof, policy, Now.AddSeconds(32));
        AssertPlaybackFailure(verifier, claims, evidence with { Milestones = [0, 99] }, proof, policy, Now.AddSeconds(32));
        AssertPlaybackFailure(verifier, claims, evidence with { Milestones = [0, 50, 50, 100] }, proof, policy, Now.AddSeconds(32));
        AssertPlaybackFailure(verifier, claims, evidence, proof with { Signature = "***" }, policy, Now.AddSeconds(32));
        FluentActions.Invoking(() => verifier.Verify(claims, evidence, null, Policy(mode: AdRewardIssuanceMode.Disabled), Now.AddSeconds(32)))
            .Should().Throw<AdRewardIssuanceDisabledException>();

        var invalidPolicy = Policy();
        typeof(AdNetworkPolicy)
            .GetField("<IssuanceMode>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(invalidPolicy, (AdRewardIssuanceMode)99);
        FluentActions.Invoking(() => verifier.Verify(claims, evidence, null, invalidPolicy, Now.AddSeconds(32)))
            .Should().Throw<AdPlaybackVerificationException>();
    }

    private static void AssertInvalidClaim(AdRewardSessionTokenService service, AdRewardSessionClaims claims) =>
        FluentActions.Invoking(() => service.Issue(claims, Now)).Should().Throw<ArgumentException>();

    private static void AssertPolicyFailure(Action action, Type exceptionType) =>
        FluentActions.Invoking(action).Should().Throw<Exception>().Which.Should().BeOfType(exceptionType);

    private static void AssertPlaybackFailure(
        AdPlaybackVerifier verifier,
        AdRewardSessionClaims claims,
        AdPlaybackEvidence evidence,
        ProviderCompletionProof proof,
        AdNetworkPolicy policy,
        DateTimeOffset now) => FluentActions.Invoking(() => verifier.Verify(claims, evidence, proof, policy, now))
        .Should().Throw<AdPlaybackVerificationException>();

    private static string SignPayload(string payload)
    {
        var encoded = Base64Url(Encoding.UTF8.GetBytes(payload));
        var signature = Base64Url(HMACSHA256.HashData(SessionSecret, Encoding.UTF8.GetBytes(encoded)));
        return $"{encoded}.{signature}";
    }

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static AdRewardSessionRequest Request() => new(
        Guid.Parse("20000000-0000-0000-0000-000000000002"),
        new WalletId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
        "unity", "creative", "device", TimeSpan.FromSeconds(30));

    private static AdRewardSessionClaims Claims() => new(
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("20000000-0000-0000-0000-000000000002"),
        new WalletId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
        "unity", "creative", "device", "nonce", TimeSpan.FromSeconds(30),
        new PolicyVersion(1), Now, Now.AddMinutes(5));

    private static AdPlaybackEvidence Evidence() => new(
        Now, Now.AddSeconds(31), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(29),
        TimeSpan.FromSeconds(1), [0, 25, 50, 75, 100]);

    private static AdNetworkPolicy Policy(
        string network = "unity",
        long version = 1,
        DateTimeOffset? effectiveAt = null,
        DateTimeOffset? expiresAt = null,
        AdRewardIssuanceMode mode = AdRewardIssuanceMode.ImmediateProviderProof,
        AdNetworkYieldState state = AdNetworkYieldState.Trailing,
        long ecpm = 2_000_000_000,
        int share = 700_000,
        int buffer = 200_000,
        int minimumVisible = 900_000,
        TimeSpan? maximumFocusLoss = null,
        long maximumReward = 1_000,
        TimeSpan? reportStaleAfter = null,
        int ranking = 100) => new(
        network,
        new PolicyVersion(version),
        effectiveAt ?? Now.AddHours(-1),
        expiresAt ?? Now.AddHours(1),
        mode,
        state,
        ecpm,
        share,
        buffer,
        minimumVisible,
        maximumFocusLoss ?? TimeSpan.FromSeconds(3),
        maximumReward,
        Now,
        reportStaleAfter ?? TimeSpan.FromHours(24),
        ranking);

    private sealed class FixedEntropy : IAdRewardSessionEntropy
    {
        public Guid CreateSessionId() => Guid.Parse("40000000-0000-0000-0000-000000000004");
        public string CreateNonce() => "nonce";
    }
}
