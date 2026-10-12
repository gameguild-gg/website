using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GameGuild.API.Core.Security;

internal sealed record RefreshTokenCleanupResult(int TokensDeleted, int SessionsDeleted);

internal interface IRefreshTokenCleanupOperation
{
    Task<RefreshTokenCleanupResult> RunAsync(CancellationToken cancellationToken);
}

/// <summary>Internal system operation. Deletion and its audit record commit together.</summary>
internal sealed class RefreshTokenCleanupOperation(
    ApplicationDbContext context,
    IRefreshTokenCleanupRepository tokenStore,
    IUserSessionCleanupRepository sessionStore,
    IOptions<RefreshTokenCleanupOptions> options,
    TimeProvider timeProvider) : IRefreshTokenCleanupOperation
{
    public async Task<RefreshTokenCleanupResult> RunAsync(CancellationToken cancellationToken)
    {
        var policy = options.Value;
        if (policy.Validate().Count != 0) { throw new InvalidOperationException("Invalid refresh-token cleanup configuration."); }
        var cutoffUtc = timeProvider.GetUtcNow().UtcDateTime.AddDays(-policy.RetentionDays);
        var strategy = context.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var tokens = 0;
            var sessions = 0;
            for (var batch = 0; batch < policy.MaxBatchesPerCycle; batch++)
            {
                var deleted = await tokenStore.DeleteExpiredAndRevokedBatchAsync(cutoffUtc, policy.BatchSize, cancellationToken).ConfigureAwait(false);
                tokens += deleted;
                if (deleted == 0) { break; }
            }
            for (var batch = 0; batch < policy.MaxBatchesPerCycle; batch++)
            {
                var deleted = await sessionStore.DeleteRetainedSessionBatchAsync(cutoffUtc, policy.BatchSize, cancellationToken).ConfigureAwait(false);
                sessions += deleted;
                if (deleted == 0) { break; }
            }
            if (tokens != 0 || sessions != 0)
            {
                context.Set<AuditLog>().Add(new AuditLog
                {
                    ActionType = "Authentication.RefreshTokenCleanup",
                    ResourceType = "RefreshToken",
                    Category = AuditCategory.Authentication,
                    Success = true,
                    RiskLevel = AuditRiskLevel.Low,
                    Description = "Scheduled credential retention cleanup",
                    Metadata = JsonSerializer.Serialize(new
                    {
                        ActorKind = "System", CutoffUtc = cutoffUtc, policy.RetentionDays,
                        policy.BatchSize, policy.MaxBatchesPerCycle, TokensDeleted = tokens, SessionsDeleted = sessions
                    })
                });
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new RefreshTokenCleanupResult(tokens, sessions);
        }).ConfigureAwait(false);
        RefreshTokenLifecycleMetrics.RecordCleanupCommitted(result.TokensDeleted, result.SessionsDeleted);
        return result;
    }
}
