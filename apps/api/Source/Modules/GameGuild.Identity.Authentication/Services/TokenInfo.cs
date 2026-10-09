namespace GameGuild.Identity.Authentication;

internal class TokenInfo
{
    private int _consumed;

    public bool IsConsumed => Volatile.Read(ref _consumed) != 0;

    public bool TryConsume() => Interlocked.CompareExchange(ref _consumed, 1, 0) == 0;

    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
}
