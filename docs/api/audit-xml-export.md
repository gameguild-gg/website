# XML audit exports — issue #301

## Contract

The original issue requires JSON, XML and CSV audit exports. XML uses the same
filtered audit document as the JSON stream, including pagination, actor, tenant,
session, resource, network, outcome, correlation, timestamp and JSON metadata.
Existing CSV and JSON routes and request contracts remain supported.

- `POST /v1/admin/audit-logs/export/xml`
- `POST /api/audit/export/xml` (alias)
- `GET /v1/admin/audit-logs/search/by-action-type/export?format=xml&ActionTypes=...`
- Scheduled exports with the existing `ExportFormat.Xml` value (`2`).

The POST requests remain JSON `AuditExportRequest` bodies. Responses are UTF-8
`application/xml` attachments. The authenticated actor, tenant restrictions,
`AuditExport` policy, expensive-operation rate limit, validation, pagination,
progress ownership and signed completion-webhook flow are shared with existing
exports. Column selection remains CSV-specific. The action-search route retains
its 10,000-record limit and existing DTO projection; it does not add raw metadata.

The XML envelope is `auditExport`, containing `schemaVersion`, `pagination` and
`records/record` elements. Record groups use the same names as JSON: `event`,
`actor`, `resource`, `network` and `outcome`. `metadata` contains JSON as escaped
XML text. Null values use `xsi:nil="true"`; empty strings remain distinct. The
schema version is `1.0`. No DTD, executable content or external entity is emitted.

## Data preservation and streaming

The forward-only writer enumerates the source once and periodically flushes
without materializing the full export. Cancellation reaches source enumeration
and destination writes/flushes. Failed source streams remain failed exports;
the writer does not turn a partial sequence into a complete XML document.
Scheduled artifacts retain the existing checksum, history, expiry and tenant
storage/download boundaries, with the XML extension and content type.

XML 1.0 cannot represent every UTF-16 code unit accepted by a .NET string. A field
containing such data uses `encoding="base64-utf16le"`, and its text is Base64 of
the original UTF-16 little-endian code units. Consumers must decode that marker
explicitly. This preserves null characters and unpaired surrogates without
dropping evidence or emitting an invalid XML character reference. Valid text is
escaped by `XmlWriter`; carriage returns are entitized to preserve line endings.

The implementation follows the framework's
[XML writer contract](https://learn.microsoft.com/en-us/dotnet/api/system.xml.xmlwriter?view=net-10.0)
and [newline handling](https://learn.microsoft.com/en-us/dotnet/api/system.xml.newlinehandling?view=net-10.0).

## Acceptance boundary

Source and test definitions are prepared. No local SDK, build, test, database or
application execution was performed. Current-source hosted compilation, unit and
PostgreSQL/HTTP results, OpenAPI generation and generated client reconciliation
must complete before accepting this increment. This document does not certify
the other original functional and technical requirements of #301.
