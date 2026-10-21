using GameGuild.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace GameGuild.API.Core.Integration;

/// <summary>All lifecycle StartingAsync callbacks finish before any hosted StartAsync, including the outbox and publisher.</summary>
internal sealed class CertificateEvmJournalReadiness : IHostedLifecycleService
{
    private readonly IDbContextFactory<EvmCertificateJournalDbContext> contexts;

    public CertificateEvmJournalReadiness(IDbContextFactory<EvmCertificateJournalDbContext> contexts) =>
        this.contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var journal = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var expected = journal.Database.GetMigrations().ToArray();
            var applied = (await journal.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToArray();
            if (expected.Length == 0 || !expected.SequenceEqual(applied, StringComparer.Ordinal))
            {
                throw Unavailable();
            }
            // History alone is insufficient if a table was removed or the runtime role cannot read it.
            await journal.Database.ExecuteSqlRawAsync("""
                SELECT 1 FROM "gameguild.authentication".evm_certificate_operations LIMIT 0;
                SELECT 1 FROM "gameguild.authentication".evm_certificate_nonces LIMIT 0;
                SELECT 1 FROM "gameguild.authentication".evm_certificate_attempts LIMIT 0;
                SELECT 1 FROM "gameguild.authentication".evm_certificate_events LIMIT 0;
                """, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception)
        {
            // Never put a connection string or provider exception into host startup logs.
            throw Unavailable();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static InvalidOperationException Unavailable() => new(
        "The EVM certificate journal is unavailable, unreadable or not at the expected schema version. Refusing to start publication.");
}
