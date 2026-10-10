namespace GameGuild.Identity.Authentication;

/// <summary>
///     Stored state of an issued one-time email sign-in code. The code itself
///     is never kept: only its SHA-256 digest, so a cache or log disclosure
///     cannot leak a usable credential.
/// </summary>
internal class EmailCodeInfo
{
    private int _consumed;

    public bool IsConsumed => Volatile.Read(ref _consumed) != 0;

    public bool TryConsume() => Interlocked.CompareExchange(ref _consumed, 1, 0) == 0;

    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public byte[] CodeDigest { get; set; } = [];

    public DateTime ExpiresAt { get; set; }

    public int FailedAttempts { get; set; }
}
