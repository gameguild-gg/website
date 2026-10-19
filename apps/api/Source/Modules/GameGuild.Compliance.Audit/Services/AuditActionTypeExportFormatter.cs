using System.Globalization;
using System.Text;
using System.Text.Json;

namespace GameGuild.Compliance.Audit;

public static class AuditActionTypeExportFormatter
{
    private static readonly string[] CsvColumns =
    [
        "Id", "ActionType", "ResourceType", "ResourceId", "UserId", "TenantId", "IpAddress",
        "Description", "Success", "RiskLevel", "Category", "CorrelationId", "CreatedAt"
    ];

    public static byte[] ToJson(IEnumerable<AuditLogDto> logs)
    {
        ArgumentNullException.ThrowIfNull(logs);
        return JsonSerializer.SerializeToUtf8Bytes(logs, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    public static byte[] ToCsv(IEnumerable<AuditLogDto> logs)
    {
        ArgumentNullException.ThrowIfNull(logs);
        var csv = new StringBuilder();
        AppendRow(csv, CsvColumns);

        foreach (var log in logs)
        {
            AppendRow(csv,
            [
                log.Id.ToString(),
                log.ActionType,
                log.ResourceType,
                log.ResourceId,
                log.UserId?.ToString(),
                log.TenantId?.ToString(),
                log.IpAddress,
                log.Description,
                log.Success.ToString(CultureInfo.InvariantCulture),
                log.RiskLevel.ToString(),
                log.Category.ToString(),
                log.CorrelationId,
                log.CreatedAt.ToString("O", CultureInfo.InvariantCulture)
            ]);
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public static async Task<byte[]> ToXmlAsync(IEnumerable<AuditLogDto> logs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(logs);
        var snapshot = logs.ToArray();
        var document = new AuditJsonExportDocument(
            "1.0",
            new AuditJsonExportPagination(1, Math.Max(snapshot.Length, 1), snapshot.Length, snapshot.Length == 0 ? 0 : 1),
            MapXmlRecordsAsync(snapshot, cancellationToken));
        await using var stream = new MemoryStream();
        await AuditXmlExporter.WriteDocumentAsync(stream, document, cancellationToken).ConfigureAwait(false);
        return stream.ToArray();
    }

    private static async IAsyncEnumerable<AuditJsonExportRecord> MapXmlRecordsAsync(
        IEnumerable<AuditLogDto> logs,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        foreach (var log in logs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Match the existing action-search DTO projection; it does not include raw metadata.
            yield return new AuditJsonExportRecord(
                log.Id,
                new AuditEventContext(log.ActionType, log.Category.ToString(), log.RiskLevel.ToString()),
                new AuditActorContext(log.UserId, log.TenantId, log.SessionId),
                new AuditResourceContext(log.ResourceType, log.ResourceId),
                new AuditNetworkContext(log.IpAddress, log.UserAgent),
                new AuditOutcomeContext(log.Success, log.ErrorMessage),
                log.Description, null, log.CorrelationId, log.CreatedAt);
        }
    }

    private static void AppendRow(StringBuilder csv, IEnumerable<string?> fields)
    {
        csv.AppendJoin(',', fields.Select(AuditCsvExporter.EscapeField));
        csv.Append("\r\n");
    }
}
