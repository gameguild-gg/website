using System.Globalization;
using System.Text;

namespace GameGuild.Compliance.Audit;

public static class AuditCsvExporter
{
    private static readonly IReadOnlyDictionary<string, Func<AuditLog, string?>> Columns =
        new Dictionary<string, Func<AuditLog, string?>>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(AuditLog.Id)] = auditLog => auditLog.Id.ToString("D", CultureInfo.InvariantCulture),
            [nameof(AuditLog.ActionType)] = auditLog => auditLog.ActionType,
            [nameof(AuditLog.ResourceType)] = auditLog => auditLog.ResourceType,
            [nameof(AuditLog.ResourceId)] = auditLog => auditLog.ResourceId,
            [nameof(AuditLog.UserId)] = auditLog => auditLog.UserId?.ToString("D", CultureInfo.InvariantCulture),
            [nameof(AuditLog.TenantId)] = auditLog => auditLog.TenantId?.ToString("D", CultureInfo.InvariantCulture),
            [nameof(AuditLog.IpAddress)] = auditLog => auditLog.IpAddress,
            [nameof(AuditLog.UserAgent)] = auditLog => auditLog.UserAgent,
            [nameof(AuditLog.SessionId)] = auditLog => auditLog.SessionId?.ToString("D", CultureInfo.InvariantCulture),
            [nameof(AuditLog.Description)] = auditLog => auditLog.Description,
            [nameof(AuditLog.Metadata)] = auditLog => auditLog.Metadata,
            [nameof(AuditLog.Success)] = auditLog => auditLog.Success.ToString(),
            [nameof(AuditLog.ErrorMessage)] = auditLog => auditLog.ErrorMessage,
            [nameof(AuditLog.RiskLevel)] = auditLog => auditLog.RiskLevel.ToString(),
            [nameof(AuditLog.Category)] = auditLog => auditLog.Category.ToString(),
            [nameof(AuditLog.CorrelationId)] = auditLog => auditLog.CorrelationId,
            [nameof(AuditLog.CreatedAt)] = auditLog => auditLog.CreatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
        };

    private static readonly string[] DefaultColumns =
    [
        nameof(AuditLog.Id),
        nameof(AuditLog.ActionType),
        nameof(AuditLog.ResourceType),
        nameof(AuditLog.ResourceId),
        nameof(AuditLog.UserId),
        nameof(AuditLog.TenantId),
        nameof(AuditLog.IpAddress),
        nameof(AuditLog.UserAgent),
        nameof(AuditLog.SessionId),
        nameof(AuditLog.Description),
        nameof(AuditLog.Metadata),
        nameof(AuditLog.Success),
        nameof(AuditLog.ErrorMessage),
        nameof(AuditLog.RiskLevel),
        nameof(AuditLog.Category),
        nameof(AuditLog.CorrelationId),
        nameof(AuditLog.CreatedAt)
    ];

    public static IReadOnlyList<string> SupportedColumns => DefaultColumns;

    public static bool TryResolveColumns(
        IEnumerable<string?>? requestedColumns,
        out IReadOnlyList<string> resolvedColumns,
        out string? error)
    {
        if (requestedColumns is null)
        {
            resolvedColumns = DefaultColumns;
            error = null;
            return true;
        }

        var requested = requestedColumns.ToArray();
        if (requested.Length == 0)
        {
            resolvedColumns = DefaultColumns;
            error = null;
            return true;
        }

        var resolved = new List<string>(requested.Length);
        var uniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in requested)
        {
            if (string.IsNullOrWhiteSpace(name) || !Columns.ContainsKey(name))
            {
                resolvedColumns = [];
                error = $"Unsupported CSV column '{name}'. Supported columns: {string.Join(", ", DefaultColumns)}.";
                return false;
            }

            if (!uniqueNames.Add(name))
            {
                resolvedColumns = [];
                error = $"CSV column '{name}' was selected more than once.";
                return false;
            }

            resolved.Add(DefaultColumns.Single(column => string.Equals(column, name, StringComparison.OrdinalIgnoreCase)));
        }

        resolvedColumns = resolved;
        error = null;
        return true;
    }

    public static async Task WriteAsync(
        Stream destination,
        IAsyncEnumerable<AuditLog> records,
        IReadOnlyList<string> columns,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(columns);

        await using var writer = new StreamWriter(
            destination,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 64 * 1024,
            leaveOpen: true)
        {
            NewLine = "\r\n"
        };

        await writer.WriteLineAsync(string.Join(',', columns).AsMemory(), cancellationToken).ConfigureAwait(false);

        var written = 0;
        await foreach (var record in records.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var values = columns.Select(column => EscapeField(Columns[column](record)));
            await writer.WriteLineAsync(string.Join(',', values).AsMemory(), cancellationToken).ConfigureAwait(false);

            written++;
            if (written % 256 == 0)
            {
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static string EscapeField(string? value)
    {
        if (string.IsNullOrEmpty(value)) { return value ?? string.Empty; }

        // Spreadsheet consumers must treat formula-like audit text as a literal cell.
        // JSON exports retain the original value for lossless machine consumption.
        if (RequiresSpreadsheetLiteral(value)) { value = "'" + value; }

        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0) { return value; }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static bool RequiresSpreadsheetLiteral(string value)
    {
        foreach (var character in value)
        {
            if (character is '\t' or '\r' or '\n') { return true; }
            if (char.IsWhiteSpace(character)) { continue; }

            return character is '=' or '+' or '-' or '@' or '＝' or '＋' or '－' or '＠';
        }

        return false;
    }
}
