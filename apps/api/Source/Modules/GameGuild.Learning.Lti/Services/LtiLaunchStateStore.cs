using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace GameGuild.Learning.Lti;

/// <summary>
/// Single-use store for OIDC login state and nonce issued by /lti/login and consumed by /lti/launch.
/// ponytail: in-memory with 10-minute TTL — single-instance ceiling; move to Redis when the API
/// scales past one instance (launch traffic would otherwise land on a node that didn't issue the state).
/// </summary>
public sealed class LtiLaunchStateStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    private sealed record Entry(Guid DeploymentId, string Nonce, DateTimeOffset ExpiresAt);

    public (string State, string Nonce) Issue(Guid deploymentId)
    {
        var state = RandomToken();
        var nonce = RandomToken();
        _entries[state] = new Entry(deploymentId, nonce, DateTimeOffset.UtcNow.Add(Ttl));
        PruneExpired();
        return (state, nonce);
    }

    /// <summary>
    /// Atomically consumes the state; succeeds only for an unconsumed, unexpired entry
    /// whose deployment and nonce match. Consuming the state also consumes its nonce.
    /// </summary>
    public bool TryConsume(string state, string nonce, Guid deploymentId)
    {
        if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(nonce))
        {
            return false;
        }

        if (!_entries.TryRemove(state, out var entry))
        {
            return false;
        }

        return entry.DeploymentId == deploymentId
               && entry.Nonce == nonce
               && entry.ExpiresAt > DateTimeOffset.UtcNow;
    }

    private void PruneExpired()
    {
        foreach (var (state, entry) in _entries)
        {
            if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                _entries.TryRemove(state, out _);
            }
        }
    }

    private static string RandomToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes);
    }
}
