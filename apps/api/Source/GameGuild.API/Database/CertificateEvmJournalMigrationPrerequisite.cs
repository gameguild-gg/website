using GameGuild.Identity.Authentication;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.API.Database;

/// <summary>Uses the existing startup migrator's connection and role; the runtime factory never applies DDL.</summary>
internal sealed class CertificateEvmJournalMigrationPrerequisite : IDatabaseMigrationPrerequisite
{
    public CertificateEvmJournalMigrationPrerequisite() { }

    public async Task PrepareAsync(DbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        try
        {
            if (!db.Database.IsNpgsql())
            {
                throw new InvalidOperationException("The enabled EVM journal requires PostgreSQL.");
            }
            var options = new DbContextOptionsBuilder<EvmCertificateJournalDbContext>()
                .UseNpgsql(db.Database.GetDbConnection(), postgres => postgres.MigrationsHistoryTable(
                    EvmCertificateJournalDbContext.MigrationsHistoryTable, EvmCertificateJournalDbContext.SchemaName)).Options;
            await using var journal = new EvmCertificateJournalDbContext(options);
            await journal.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception)
        {
            throw new InvalidOperationException("The EVM certificate journal migration failed. Startup must verify the dedicated journal before publishing.");
        }
    }
}
