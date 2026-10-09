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

    private static void AppendRow(StringBuilder csv, IEnumerable<string?> fields)
    {
        csv.AppendJoin(',', fields.Select(AuditCsvExporter.EscapeField));
        csv.Append("\r\n");
    }
}
