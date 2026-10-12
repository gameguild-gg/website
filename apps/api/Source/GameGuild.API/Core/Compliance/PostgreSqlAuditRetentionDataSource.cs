using System.Data;
using System.Data.Common;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.API.Core.Compliance;

/// <summary>Captures tenant aggregates in one repeatable-read snapshot, without loading audit payloads.</summary>
public sealed class PostgreSqlAuditRetentionDataSource(IServiceScopeFactory scopeFactory) : IAuditRetentionDataSource
{
    public async Task<AuditRetentionDataSnapshot> CaptureAsync(
        Guid tenantId, DateTime asOfUtc, int historicalDays, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || asOfUtc.Kind != DateTimeKind.Utc || historicalDays is < 14 or > 365)
        {
            throw new ArgumentException("A tenant, UTC capture time and a supported history window are required.");
        }
        // A simulation command already has its own write transaction. Capture evidence on an independent read connection.
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Database.CreateExecutionStrategy().ExecuteAsync(() =>
            CaptureSnapshotAsync(context, tenantId, asOfUtc, historicalDays, cancellationToken)).ConfigureAwait(false);
    }

    private static async Task<AuditRetentionDataSnapshot> CaptureSnapshotAsync(
        ApplicationDbContext context, Guid tenantId, DateTime asOfUtc, int historicalDays, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        var connection = context.Database.GetDbConnection();
        var cohorts = new List<AuditStorageDailyCohort>();
        await using (var command = CreateCommand(connection, transaction.GetDbTransaction(), tenantId, asOfUtc))
        {
            command.CommandText = """
                SELECT "DateUtc", SUM("Records")::bigint, SUM("Bytes")::numeric
                FROM (
                    SELECT (a."CreatedAt" AT TIME ZONE 'UTC')::date AS "DateUtc",
                        COUNT(*) AS "Records", SUM(pg_column_size(a))::numeric AS "Bytes"
                    FROM "AuditLogs" a WHERE a."TenantId" = @tenant AND a."CreatedAt" <= @asOf
                    GROUP BY 1
                    UNION ALL
                    SELECT (a."Timestamp" AT TIME ZONE 'UTC')::date,
                        COUNT(*), SUM(pg_column_size(a))::numeric
                    FROM "TamperEvidentAuditLogs" a WHERE a."TenantId" = @tenant AND a."Timestamp" <= @asOf
                    GROUP BY 1
                ) measured GROUP BY 1 ORDER BY 1 LIMIT 40001
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cohorts.Add(new(reader.GetFieldValue<DateOnly>(0), reader.GetInt64(1), reader.GetDecimal(2)));
            }
        }
        var accessAges = new List<AuditAccessAgeBucket>();
        DateOnly? firstObservation = null;
        await using (var command = CreateCommand(connection, transaction.GetDbTransaction(), tenantId, asOfUtc))
        {
            AddParameter(command, "windowStart", asOfUtc.Date.AddDays(-historicalDays));
            command.CommandText = """
                SELECT ((o."CreatedAt" AT TIME ZONE 'UTC')::date - o."RecordDateUtc") AS "Age",
                    SUM(o."ReadCount")::bigint, MIN((o."CreatedAt" AT TIME ZONE 'UTC')::date)
                FROM "AuditDataAccessObservations" o
                WHERE o."TenantId" = @tenant AND o."CreatedAt" >= @windowStart AND o."CreatedAt" <= @asOf
                GROUP BY 1 ORDER BY 1 LIMIT 40001
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                accessAges.Add(new(reader.GetInt32(0), reader.GetInt64(1)));
                var date = reader.GetFieldValue<DateOnly>(2);
                if (firstObservation is null || date < firstObservation) { firstObservation = date; }
            }
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(cohorts, accessAges, firstObservation,
            "PostgreSQL pg_column_size(row), summed by UTC record date across AuditLogs and TamperEvidentAuditLogs; actual read observations from audit queries and exports.");
    }

    private static DbCommand CreateCommand(DbConnection connection, DbTransaction transaction, Guid tenantId, DateTime asOfUtc)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 60;
        AddParameter(command, "tenant", tenantId);
        AddParameter(command, "asOf", asOfUtc);
        return command;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
