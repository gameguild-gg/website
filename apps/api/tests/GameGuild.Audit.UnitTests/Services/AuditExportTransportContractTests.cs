using GameGuild.Compliance.Audit;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class AuditExportTransportContractTests
{
    [Theory]
    [InlineData(nameof(AuditController.ExportAuditLogs), "/api/audit/export/csv")]
    [InlineData(nameof(AuditController.ExportAuditLogsJson), "/api/audit/export/json")]
    public void Requested_routes_are_explicit_aliases_of_the_existing_guarded_actions(string action, string route)
    {
        var method = typeof(AuditController).GetMethod(action)!;
        var attributes = method.GetCustomAttributes(typeof(HttpPostAttribute), true).Cast<HttpPostAttribute>();
        Assert.Contains(attributes, attribute => attribute.Template == route);
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@SUM(1)", "'@SUM(1)")]
    [InlineData("\t=1", "'\t=1")]
    [InlineData(" \t=1", "' \t=1")]
    [InlineData("  =1", "'  =1")]
    [InlineData("\r\n=1", "\"'\r\n=1\"")]
    [InlineData("\tordinary text", "'\tordinary text")]
    [InlineData("＝1", "'＝1")]
    [InlineData("＋1", "'＋1")]
    [InlineData("－1", "'－1")]
    [InlineData("＠SUM(1)", "'＠SUM(1)")]
    [InlineData("\u00a0=1", "'\u00a0=1")]
    [InlineData("=HYPERLINK(\"https://example.test\")", "\"'=HYPERLINK(\"\"https://example.test\"\")\"")]
    [InlineData("+SUM(1,2)", "\"'+SUM(1,2)\"")]
    [InlineData("normal, \"quoted\"", "\"normal, \"\"quoted\"\"\"")]
    [InlineData("normal text", "normal text")]
    [InlineData("", "")]
    [InlineData("  ", "  ")]
    public void Spreadsheet_formulas_are_literal_text_with_RFC4180_escaping(string value, string expected)
    {
        Assert.Equal(expected, AuditCsvExporter.EscapeField(value));
    }

    [Fact]
    public void Null_csv_values_are_empty_cells()
    {
        Assert.Equal(string.Empty, AuditCsvExporter.EscapeField(null));
    }
}
