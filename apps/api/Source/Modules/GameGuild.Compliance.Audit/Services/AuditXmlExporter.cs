using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Xml;

namespace GameGuild.Compliance.Audit;

/// <summary>Streams the audit document contract as XML 1.0 without buffering its record sequence.</summary>
public static class AuditXmlExporter
{
    private const string SchemaInstanceNamespace = "http://www.w3.org/2001/XMLSchema-instance";

    public static async Task WriteDocumentAsync(
        Stream destination,
        AuditJsonExportDocument document,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        await using var writer = XmlWriter.Create(new ExportWriteStream(destination, cancellationToken), new XmlWriterSettings
        {
            Async = true,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            CloseOutput = false,
            CheckCharacters = true,
            NewLineHandling = NewLineHandling.Entitize,
            WriteEndDocumentOnClose = false
        });
        await writer.WriteStartDocumentAsync().ConfigureAwait(false);
        await writer.WriteStartElementAsync(null, "auditExport", null).ConfigureAwait(false);
        await writer.WriteAttributeStringAsync("xmlns", "xsi", null, SchemaInstanceNamespace).ConfigureAwait(false);
        await FieldAsync(writer, "schemaVersion", document.SchemaVersion).ConfigureAwait(false);
        await writer.WriteStartElementAsync(null, "pagination", null).ConfigureAwait(false);
        await FieldAsync(writer, "pageNumber", Invariant(document.Pagination.PageNumber)).ConfigureAwait(false);
        await FieldAsync(writer, "pageSize", Invariant(document.Pagination.PageSize)).ConfigureAwait(false);
        await FieldAsync(writer, "totalRecords", Invariant(document.Pagination.TotalRecords)).ConfigureAwait(false);
        await FieldAsync(writer, "totalPages", Invariant(document.Pagination.TotalPages)).ConfigureAwait(false);
        await writer.WriteEndElementAsync().ConfigureAwait(false);
        await writer.WriteStartElementAsync(null, "records", null).ConfigureAwait(false);

        var count = 0;
        await foreach (var record in document.Records.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RecordAsync(writer, record).ConfigureAwait(false);
            if (++count % 256 == 0) { await writer.FlushAsync().ConfigureAwait(false); }
        }

        cancellationToken.ThrowIfCancellationRequested();
        await writer.WriteEndElementAsync().ConfigureAwait(false);
        await writer.WriteEndElementAsync().ConfigureAwait(false);
        await writer.WriteEndDocumentAsync().ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    private static async Task RecordAsync(XmlWriter writer, AuditJsonExportRecord record)
    {
        await writer.WriteStartElementAsync(null, "record", null).ConfigureAwait(false);
        await FieldAsync(writer, "id", record.Id.ToString("D")).ConfigureAwait(false);
        await GroupAsync(writer, "event", ("actionType", record.Event.ActionType),
            ("category", record.Event.Category), ("riskLevel", record.Event.RiskLevel)).ConfigureAwait(false);
        await GroupAsync(writer, "actor", ("userId", record.Actor.UserId?.ToString("D")),
            ("tenantId", record.Actor.TenantId?.ToString("D")), ("sessionId", record.Actor.SessionId?.ToString("D"))).ConfigureAwait(false);
        await GroupAsync(writer, "resource", ("resourceType", record.Resource.ResourceType),
            ("resourceId", record.Resource.ResourceId)).ConfigureAwait(false);
        await GroupAsync(writer, "network", ("ipAddress", record.Network.IpAddress),
            ("userAgent", record.Network.UserAgent)).ConfigureAwait(false);
        await GroupAsync(writer, "outcome", ("success", XmlConvert.ToString(record.Outcome.Success)),
            ("errorMessage", record.Outcome.ErrorMessage)).ConfigureAwait(false);
        await FieldAsync(writer, "description", record.Description).ConfigureAwait(false);
        await FieldAsync(writer, "metadata", record.Metadata?.GetRawText()).ConfigureAwait(false);
        await FieldAsync(writer, "correlationId", record.CorrelationId).ConfigureAwait(false);
        await FieldAsync(writer, "createdAt", record.CreatedAt.ToString("O", CultureInfo.InvariantCulture)).ConfigureAwait(false);
        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    private static async Task GroupAsync(XmlWriter writer, string name, params (string Name, string? Value)[] fields)
    {
        await writer.WriteStartElementAsync(null, name, null).ConfigureAwait(false);
        foreach (var field in fields) { await FieldAsync(writer, field.Name, field.Value).ConfigureAwait(false); }
        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    private static async Task FieldAsync(XmlWriter writer, string name, string? value)
    {
        await writer.WriteStartElementAsync(null, name, null).ConfigureAwait(false);
        if (value is null)
        {
            await writer.WriteAttributeStringAsync("xsi", "nil", SchemaInstanceNamespace, "true").ConfigureAwait(false);
        }
        else
        {
            // XML 1.0 cannot represent every .NET string. Preserve invalid code units as
            // explicitly labelled UTF-16LE bytes rather than dropping data or emitting invalid XML.
            var encoded = EncodeInvalidXml(value);
            if (encoded is not null)
            {
                await writer.WriteAttributeStringAsync(null, "encoding", null, "base64-utf16le").ConfigureAwait(false);
            }
            await writer.WriteStringAsync(encoded ?? value).ConfigureAwait(false);
        }
        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    private static string? EncodeInvalidXml(string value)
    {
        try { XmlConvert.VerifyXmlChars(value); return null; }
        catch (XmlException)
        {
            var bytes = new byte[checked(value.Length * sizeof(char))];
            for (var index = 0; index < value.Length; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * sizeof(char), sizeof(char)), value[index]);
            }
            return Convert.ToBase64String(bytes);
        }
    }

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);

    // XmlWriter's async methods do not accept a CancellationToken. Forward the export
    // token to the real destination so cancellation also interrupts a blocked write/flush.
    private sealed class ExportWriteStream(Stream destination, CancellationToken exportToken) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => destination.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            throw new NotSupportedException("The export destination is write-only.");
        }
        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException($"Seeking to offset {offset} with origin {origin} is not supported.");
        public override void SetLength(long value)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            throw new NotSupportedException("The export destination cannot be resized.");
        }
        public override void Flush() { exportToken.ThrowIfCancellationRequested(); destination.Flush(); }
        public override void Write(byte[] buffer, int offset, int count)
        {
            exportToken.ThrowIfCancellationRequested();
            destination.Write(buffer, offset, count);
        }
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(exportToken, cancellationToken);
            await destination.FlushAsync(linked.Token).ConfigureAwait(false);
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(exportToken, cancellationToken);
            await destination.WriteAsync(buffer, linked.Token).ConfigureAwait(false);
        }
    }
}
