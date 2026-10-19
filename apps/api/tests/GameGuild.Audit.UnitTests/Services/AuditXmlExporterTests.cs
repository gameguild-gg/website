using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using GameGuild.Compliance.Audit;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class AuditXmlExporterTests
{
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    [Fact]
    public async Task Complete_document_preserves_context_metadata_and_nulls_as_well_formed_utf8()
    {
        var record = Record();
        await using var stream = new MemoryStream();
        await AuditXmlExporter.WriteAsync(stream, Document(Records([record])), CancellationToken.None);

        Assert.True(stream.CanWrite);
        var text = Encoding.UTF8.GetString(stream.ToArray());
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", text);
        var document = XDocument.Parse(text);
        Assert.Null(document.DocumentType);
        var root = document.Root!;
        Assert.Equal("1.0", root.Element("schemaVersion")!.Value);
        Assert.Equal("1", root.Element("pagination")!.Element("totalRecords")!.Value);
        var item = Assert.Single(root.Element("records")!.Elements("record"));
        Assert.Equal(record.Id.ToString("D"), item.Element("id")!.Value);
        Assert.Equal("PermissionGranted", item.Element("event")!.Element("actionType")!.Value);
        Assert.Equal("Permission", item.Element("event")!.Element("category")!.Value);
        Assert.Equal("High", item.Element("event")!.Element("riskLevel")!.Value);
        Assert.Equal(record.Actor.UserId!.Value.ToString("D"), item.Element("actor")!.Element("userId")!.Value);
        Assert.Equal(record.Actor.TenantId!.Value.ToString("D"), item.Element("actor")!.Element("tenantId")!.Value);
        Assert.Equal(record.Actor.SessionId!.Value.ToString("D"), item.Element("actor")!.Element("sessionId")!.Value);
        Assert.Equal("Permission", item.Element("resource")!.Element("resourceType")!.Value);
        Assert.Equal("true", item.Element("resource")!.Element("resourceId")!.Attribute(Xsi + "nil")!.Value);
        Assert.Equal("127.0.0.1", item.Element("network")!.Element("ipAddress")!.Value);
        Assert.Equal("test-agent", item.Element("network")!.Element("userAgent")!.Value);
        Assert.Equal("true", item.Element("outcome")!.Element("success")!.Value);
        Assert.Equal("true", item.Element("outcome")!.Element("errorMessage")!.Attribute(Xsi + "nil")!.Value);
        Assert.Equal("trace-id", item.Element("correlationId")!.Value);
        Assert.Equal("2026-10-19T12:34:56.0000000Z", item.Element("createdAt")!.Value);
        using var metadata = JsonDocument.Parse(item.Element("metadata")!.Value);
        Assert.Equal("</metadata><record>injected</record>", metadata.RootElement.GetProperty("text").GetString());
        Assert.True(metadata.RootElement.GetProperty("nested").GetProperty("enabled").GetBoolean());
    }

    [Theory]
    [InlineData("<record>&\"' ]]> </record>")]
    [InlineData("line1\r\nline2\rline3\n\t🚀日本語")]
    [InlineData("")]
    public async Task Valid_text_round_trips_without_injection_or_newline_normalization(string value)
    {
        var record = Record() with { Description = value };
        var item = await ExportOneAsync(record);
        Assert.Equal(value, item.Element("description")!.Value);
        Assert.Null(item.Element("description")!.Attribute("encoding"));
        Assert.Null(item.Element("description")!.Attribute(Xsi + "nil"));
    }

    [Theory]
    [InlineData(0x0000, "AAA=")]
    [InlineData(0x0001, "AQA=")]
    [InlineData(0xD800, "ANg=")]
    [InlineData(0xDC00, "ANw=")]
    [InlineData(0xFFFF, "//8=")]
    public async Task Values_outside_XML10_are_losslessly_labelled_as_base64_utf16le(int codeUnit, string expected)
    {
        var value = new string((char)codeUnit, 1);
        var item = await ExportOneAsync(Record() with { Description = value });
        var field = item.Element("description")!;
        Assert.Equal("base64-utf16le", field.Attribute("encoding")!.Value);
        Assert.Equal(expected, field.Value);
    }

    [Fact]
    public async Task Empty_exports_have_a_complete_envelope_and_no_record_elements()
    {
        await using var stream = new MemoryStream();
        var document = new AuditJsonExportDocument("1.0", new AuditJsonExportPagination(1, 500, 0, 0), Records([]));
        await AuditXmlExporter.WriteAsync(stream, document, CancellationToken.None);
        var xml = XDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        Assert.Equal("0", xml.Root!.Element("pagination")!.Element("totalRecords")!.Value);
        Assert.Empty(xml.Root.Element("records")!.Elements());
    }

    [Fact]
    public async Task Already_cancelled_exports_do_not_enumerate_or_write()
    {
        var enumerated = false;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await using var stream = new MemoryStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AuditXmlExporter.WriteAsync(
            stream, Document(Records([Record()], () => enumerated = true)), cancelled.Token));
        Assert.False(enumerated);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public async Task Cancellation_reaches_an_actual_blocked_destination_write()
    {
        using var cancelled = new CancellationTokenSource();
        await using var stream = new BlockingWriteStream();
        var export = AuditXmlExporter.WriteAsync(stream, Document(Records([Record()])), cancelled.Token);
        await stream.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => export.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(stream.ReceivedCancelableToken);
    }

    [Fact]
    public async Task Source_failure_propagates_and_never_becomes_a_complete_export_document()
    {
        var failure = new InvalidOperationException("source failed");
        await using var stream = new MemoryStream();
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AuditXmlExporter.WriteAsync(stream, Document(FailingRecords(failure)), CancellationToken.None));
        Assert.Same(failure, observed);
        Assert.True(stream.CanWrite);
        Assert.Throws<XmlException>(() => XDocument.Parse(Encoding.UTF8.GetString(stream.ToArray())));
    }

    [Fact]
    public async Task Records_are_enumerated_once_and_output_is_flushed_before_the_entire_source_is_consumed()
    {
        await using var stream = new MemoryStream();
        var count = 0;
        var source = Records(Enumerable.Repeat(Record(), 512), () =>
        {
            if (count == 256) { Assert.True(stream.Length > 0); }
            count++;
        });
        await AuditXmlExporter.WriteAsync(stream, Document(source), CancellationToken.None);
        Assert.Equal(512, count);
        Assert.Equal(512, XDocument.Parse(Encoding.UTF8.GetString(stream.ToArray())).Root!
            .Element("records")!.Elements("record").Count());
    }

    [Fact]
    public async Task Action_search_export_preserves_the_existing_DTO_projection_and_does_not_add_raw_metadata()
    {
        var id = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var result = await AuditActionTypeExportFormatter.ToXmlAsync([new AuditLogDto
        {
            Id = id, TenantId = tenant, ActionType = "PermissionGranted", ResourceType = "Permission",
            Description = "<untrusted>", Success = true, CreatedAt = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc)
        }], CancellationToken.None);
        var document = XDocument.Parse(Encoding.UTF8.GetString(result));
        var item = Assert.Single(document.Root!.Element("records")!.Elements("record"));
        Assert.Equal(id.ToString("D"), item.Element("id")!.Value);
        Assert.Equal(tenant.ToString("D"), item.Element("actor")!.Element("tenantId")!.Value);
        Assert.Equal("<untrusted>", item.Element("description")!.Value);
        Assert.Equal("true", item.Element("metadata")!.Attribute(Xsi + "nil")!.Value);
    }

    private static AuditJsonExportRecord Record() => AuditJsonExportMapper.Map(new AuditLog
    {
        Id = Guid.NewGuid(), UserId = Guid.NewGuid(), TenantId = Guid.NewGuid(), SessionId = Guid.NewGuid(),
        ActionType = "PermissionGranted", ResourceType = "Permission", IpAddress = "127.0.0.1", UserAgent = "test-agent",
        Description = "grant", Success = true, RiskLevel = AuditRiskLevel.High, Category = AuditCategory.Permission,
        CorrelationId = "trace-id", CreatedAt = new DateTime(2026, 10, 19, 12, 34, 56, DateTimeKind.Utc),
        Metadata = "{\"text\":\"</metadata><record>injected</record>\",\"nested\":{\"enabled\":true}}"
    });

    private static AuditJsonExportDocument Document(IAsyncEnumerable<AuditJsonExportRecord> records) =>
        new("1.0", new AuditJsonExportPagination(1, 500, 1, 1), records);

    private static async Task<XElement> ExportOneAsync(AuditJsonExportRecord record)
    {
        await using var stream = new MemoryStream();
        await AuditXmlExporter.WriteAsync(stream, Document(Records([record])), CancellationToken.None);
        return Assert.Single(XDocument.Parse(Encoding.UTF8.GetString(stream.ToArray())).Root!.Element("records")!.Elements("record"));
    }

    private static async IAsyncEnumerable<AuditJsonExportRecord> Records(
        IEnumerable<AuditJsonExportRecord> records,
        Action? beforeYield = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        foreach (var record in records) { cancellationToken.ThrowIfCancellationRequested(); beforeYield?.Invoke(); yield return record; }
    }

    private static async IAsyncEnumerable<AuditJsonExportRecord> FailingRecords(Exception failure)
    {
        await Task.CompletedTask;
        yield return Record();
        throw failure;
    }

    private sealed class BlockingWriteStream : MemoryStream
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool ReceivedCancelableToken { get; private set; }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReceivedCancelableToken = cancellationToken.CanBeCanceled;
            Entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }
}
