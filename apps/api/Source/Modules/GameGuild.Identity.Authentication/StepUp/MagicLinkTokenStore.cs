using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

public sealed class MagicLinkToken
{
    public string TokenHash { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

public interface IMagicLinkTokenStore
{
    Task AddAsync(
        string token,
        Guid userId,
        string email,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task<TokenValidationResult> ConsumeAsync(
        string token,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken = default);

    Task<bool> IsValidAsync(
        string token,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public sealed class DatabaseMagicLinkTokenStore(IApplicationDbContext context) : IMagicLinkTokenStore
{
    private DbSet<MagicLinkToken> Tokens => context.Set<MagicLinkToken>();

    public async Task AddAsync(
        string token,
        Guid userId,
        string email,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;
        await Tokens.Where(item => item.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        await Tokens.AddAsync(new MagicLinkToken
        {
            TokenHash = HashToken(token),
            UserId = userId,
            Email = email.ToLowerInvariant(),
            ExpiresAt = expiresAt
        }, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TokenValidationResult> ConsumeAsync(
        string token,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(token);
        var changed = await Tokens
            .Where(item => item.TokenHash == tokenHash && item.ExpiresAt > consumedAt && item.ConsumedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.ConsumedAt, consumedAt),
                cancellationToken)
            .ConfigureAwait(false);

        if (changed != 1)
        {
            return TokenValidationResult.Failed("Invalid or expired token");
        }

        var item = await Tokens.AsNoTracking()
            .SingleAsync(item => item.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);
        return new TokenValidationResult(true, item.UserId, item.Email);
    }

    public Task<bool> IsValidAsync(
        string token,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(token);
        return Tokens.AnyAsync(
            item => item.TokenHash == tokenHash && item.ExpiresAt > now && item.ConsumedAt == null,
            cancellationToken);
    }

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
