using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GameGuild.Finance.Economy.AdRewards;

public sealed record ProviderCompletionProof(
    string Network,
    string ProviderEventId,
    Guid SessionId,
    string CreativeId,
    DateTimeOffset CompletedAt,
    string EvidenceHash,
    string Signature);

public sealed class HmacProviderCompletionProofService
{
    private readonly string _network;
    private readonly byte[] _secret;

    public HmacProviderCompletionProofService(string network, byte[] secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(network);
        ArgumentNullException.ThrowIfNull(secret);
        if (secret.Length < 32) throw new ArgumentException("Provider proof secret must contain at least 32 bytes.", nameof(secret));
        _network = network.Trim();
        _secret = [.. secret];
    }

    public ProviderCompletionProof Sign(
        string providerEventId,
        Guid sessionId,
        string creativeId,
        DateTimeOffset completedAt,
        string evidenceHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEventId);
        if (sessionId == Guid.Empty) throw new ArgumentException("Session ID is required.", nameof(sessionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(creativeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceHash);
        var canonical = Canonical(_network, providerEventId, sessionId, creativeId, completedAt, evidenceHash);
        var signature = Convert.ToBase64String(HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(canonical)));
        return new ProviderCompletionProof(
            _network, providerEventId.Trim(), sessionId, creativeId.Trim(), completedAt,
            evidenceHash.Trim(), signature);
    }

    public bool Verify(ProviderCompletionProof proof, AdRewardSessionClaims claims, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(proof);
        ArgumentNullException.ThrowIfNull(claims);
        if (!string.Equals(proof.Network, _network, StringComparison.Ordinal) ||
            !string.Equals(proof.Network, claims.Network, StringComparison.Ordinal) ||
            proof.SessionId != claims.SessionId ||
            !string.Equals(proof.CreativeId, claims.CreativeId, StringComparison.Ordinal) ||
            proof.CompletedAt < claims.IssuedAt || proof.CompletedAt > now)
            return false;
        byte[] supplied;
        try { supplied = Convert.FromBase64String(proof.Signature); }
        catch (FormatException) { return false; }
        var canonical = Canonical(
            proof.Network, proof.ProviderEventId, proof.SessionId, proof.CreativeId,
            proof.CompletedAt, proof.EvidenceHash);
        var expected = HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(canonical));
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }

    private static string Canonical(
        string network,
        string eventId,
        Guid sessionId,
        string creativeId,
        DateTimeOffset completedAt,
        string evidenceHash) => string.Join('|',
        network, eventId.Trim(), sessionId.ToString("N"), creativeId.Trim(),
        completedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), evidenceHash.Trim());
}

public sealed record AdPlaybackEvidence(
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    TimeSpan PlaybackDuration,
    TimeSpan VisibleDuration,
    TimeSpan FocusLoss,
    IReadOnlyList<int> Milestones);

public sealed class AdPlaybackVerifier
{
    private readonly HmacProviderCompletionProofService _providerProof;

    public AdPlaybackVerifier(HmacProviderCompletionProofService providerProof) =>
        _providerProof = providerProof ?? throw new ArgumentNullException(nameof(providerProof));

    public bool Verify(
        AdRewardSessionClaims claims,
        AdPlaybackEvidence evidence,
        ProviderCompletionProof? proof,
        AdNetworkPolicy policy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(policy);
        if (!policy.IsEffective(now) || !string.Equals(policy.Network, claims.Network, StringComparison.Ordinal))
            throw new AdPlaybackVerificationException("The session policy is not current or bound to the network.");
        if (evidence.StartedAt < claims.IssuedAt || evidence.CompletedAt > now ||
            evidence.CompletedAt < evidence.StartedAt ||
            evidence.PlaybackDuration < claims.RequiredDuration ||
            evidence.VisibleDuration < TimeSpan.Zero || evidence.VisibleDuration > evidence.PlaybackDuration ||
            evidence.FocusLoss < TimeSpan.Zero || evidence.FocusLoss > policy.MaximumFocusLoss)
            throw new AdPlaybackVerificationException("Playback timing is not physically valid.");
        if ((decimal)evidence.VisibleDuration.Ticks * 1_000_000 <
            (decimal)evidence.PlaybackDuration.Ticks * policy.MinimumVisiblePpm)
            throw new AdPlaybackVerificationException("Playback visibility is below policy.");
        if (evidence.Milestones.Count < 2 || evidence.Milestones[0] != 0 || evidence.Milestones[^1] != 100 ||
            evidence.Milestones.Where((value, index) => index > 0 && value <= evidence.Milestones[index - 1]).Any())
            throw new AdPlaybackVerificationException("Playback milestones are incomplete or unordered.");

        return policy.IssuanceMode switch
        {
            AdRewardIssuanceMode.ImmediateProviderProof => proof is null
                ? throw new AdProviderProofRequiredException("Independent provider completion proof is required.")
                : _providerProof.Verify(proof, claims, now)
                    ? true
                    : throw new AdPlaybackVerificationException("Provider completion proof is invalid."),
            AdRewardIssuanceMode.DeferredReport => false,
            AdRewardIssuanceMode.Disabled => throw new AdRewardIssuanceDisabledException("This network cannot issue ad rewards."),
            _ => throw new AdPlaybackVerificationException("Unknown ad reward issuance mode.")
        };
    }
}

public sealed class AdPlaybackVerificationException(string message) : InvalidOperationException(message);
public sealed class AdProviderProofRequiredException(string message) : InvalidOperationException(message);
