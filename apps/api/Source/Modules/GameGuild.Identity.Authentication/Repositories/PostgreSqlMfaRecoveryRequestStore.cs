using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     PostgreSQL-backed persistence for lost-MFA-device recovery requests, following
///     the PostgreSqlSignInMfaChallengeStore pattern (shared IApplicationDbContext).
/// </summary>
public sealed class PostgreSqlMfaRecoveryRequestStore(IApplicationDbContext context) : IMfaRecoveryRequestStore
{
    private DbSet<MfaRecoveryRequest> Requests => context.Set<MfaRecoveryRequest>();

    public async Task AddAsync(MfaRecoveryRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        await Requests.AddAsync(request, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<MfaRecoveryRequest?> FindLiveByUserAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return Requests.SingleOrDefaultAsync(request => request.UserId == userId &&
            (request.Status == MfaRecoveryRequestStatus.Pending ||
             request.Status == MfaRecoveryRequestStatus.AwaitingConfirmation), cancellationToken);
    }

    public Task<MfaRecoveryRequest?> FindLiveByRequestTokenHashAsync(string requestTokenHash, CancellationToken cancellationToken)
    {
        RequireDigest(requestTokenHash);
        return Requests.SingleOrDefaultAsync(request => request.RequestTokenHash == requestTokenHash &&
            (request.Status == MfaRecoveryRequestStatus.Pending ||
             request.Status == MfaRecoveryRequestStatus.AwaitingConfirmation), cancellationToken);
    }

    public Task<MfaRecoveryRequest?> FindAwaitingByConfirmationTokenHashAsync(string confirmationTokenHash, CancellationToken cancellationToken)
    {
        RequireDigest(confirmationTokenHash);
        return Requests.SingleOrDefaultAsync(request => request.ConfirmationTokenHash == confirmationTokenHash &&
            request.Status == MfaRecoveryRequestStatus.AwaitingConfirmation, cancellationToken);
    }

    public Task<int> ExpireOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        return Requests.Where(request => request.RequestedAt < cutoff &&
                (request.Status == MfaRecoveryRequestStatus.Pending ||
                 request.Status == MfaRecoveryRequestStatus.AwaitingConfirmation))
            .ExecuteUpdateAsync(setters => setters.SetProperty(request => request.Status, MfaRecoveryRequestStatus.Expired),
                cancellationToken);
    }

    public async Task<MfaRecoveryRequest?> CancelLiveByUserAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var request = await FindLiveByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return null;
        }

        request.Status = MfaRecoveryRequestStatus.Cancelled;
        request.ConsumedAt = SystemClock.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return request;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void RequireDigest(string tokenHash)
    {
        if (tokenHash is not { Length: 64 } || !tokenHash.All(IsAsciiDigitLowerHex))
        {
            throw new ArgumentException("A lowercase 64-character hex digest is required.", nameof(tokenHash));
        }
    }

    private static bool IsAsciiDigitLowerHex(char c) => c is (>= '0' and <= '9') or (>= 'a' and <= 'f');
}
