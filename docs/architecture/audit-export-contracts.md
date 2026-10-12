# Audit export contracts: CSV and JSON

Canonical requirements: [#171](https://github.com/gameguild-gg/gameguild/issues/171)
and [#173](https://github.com/gameguild-gg/gameguild/issues/173).
The title-only requests #170 and #172 refer to these same exports.

## HTTP surface and access

POST /api/audit/export/csv and POST /api/audit/export/json are aliases of the
existing AuditController actions. Existing consumers keep the versioned routes:

| Format | Existing route |
| --- | --- |
| CSV | /v1/admin/audit-logs/export/csv |
| CSV | /v1/admin/audit-logs/:export |
| JSON | /v1/admin/audit-logs/export/json |

All routes inherit the persisted SystemAdmin policy and the ExpensiveOperations
rate policy. An authenticated role claim alone does not replace missing policy
definitions: normal startup must seed the registered policies. PostgreSQL HTTP
tests use the real PolicyDefinitionSeeder. Anonymous requests are challenged and
ordinary tenant members cannot export. The acting administrator comes from the
server actor context; UserId in the request filters the records being exported.
SystemAdmin is explicitly allowed to select data across tenants.

The shared concurrency limiter partitions exports by authenticated user, so
switching between aliases and versioned paths does not create extra permits.
Limits come from the existing RateLimiting configuration. Completion releases
the permit; excess concurrent requests receive 429 Problem Details and retry
headers.

## Request and response

Both formats accept AuditExportRequest. Filters include tenant, actor, action,
resource type, category, risk, outcome, UTC date bounds and IP address.
Date bounds must be ordered. When selecting a page, supply both PageNumber and
PageSize; pages begin at one and the supported size range is 1–1000.

CSV accepts a case-insensitive ordered list of known column names; unknown,
blank or repeated columns are rejected. Omitting columns selects the documented
AuditCsvExporter.SupportedColumns. Without pagination, CSV streams all matches.
JSON defaults to page one with 500 records and always returns schemaVersion 1.0,
pagination totals and nested event, actor, resource, network and outcome objects.
Structured metadata remains structured; non-object JSON is wrapped in a value
property, and legacy non-JSON text in a raw property.

Successful responses use attachment filenames, the format's UTF-8 content type,
X-Audit-Export-Id, X-Audit-Total-Records and page headers when applicable.
JSON supports optional gzip through the existing response-compression middleware:
request Accept-Encoding: gzip. Without that header, an uncompressed response
remains available. The format-specific route respects Accept before starting work.
The CSV 200 response explicitly declares text/csv with a binary schema. The host
OpenAPI filter preserves explicit status-specific media declarations over inherited
controller defaults; it leaves undeclared response formats and error statuses alone.
Regenerated CSV methods return Blob and request text/csv, including both existing
methods. JSON methods retain the existing typed schema and response validation.

GET /v1/admin/audit-logs/export/{exportId}/progress is restricted to the
administrator who initiated that export. Unknown IDs and another administrator's
IDs return 404. ExportAuditLogs is recorded before querying the export data.
Scheduled exports reuse the existing scheduled-export service and storage.

## Streaming, errors and spreadsheet text

The command returns an asynchronous record sequence instead of materializing a
list. CSV writes UTF-8 without a BOM, uses CRLF records, doubles embedded quotes,
quotes comma/quote/newline fields, and flushes every 256 records. JSON serializes
its asynchronous sequence incrementally within the requested page. Request
cancellation stops enumeration and produces the existing cancellation notification.

Before a response starts, invalid input and preparation failures use structured
Problem Details. Internal exception messages and database details are withheld.
After bytes have been sent, a failed stream is aborted; it cannot be replaced
with a successful-looking file or a second JSON document. Progress and completion/
failure notifications remain available through the existing export services.

CSV prefixes a single apostrophe when text begins with formula operators, their
fullwidth variants, or leading tab/newline controls; whitespace before an operator
is also considered. RFC 4180 escaping is applied after that text conversion.
JSON preserves original strings for lossless machine processing. This CSV guard
protects the initial spreadsheet import and intentionally changes dangerous text
cells. It is not a guarantee across every spreadsheet program or after files are
saved and reopened; [OWASP documents those limitations](https://community.owasp.org/attacks/CSV_Injection).

## Acceptance evidence

| Requirement | Focused evidence |
| --- | --- |
| Original and compatible routes, filters, headers, audit event, progress ownership | AuditExportPostgreSqlHttpTests.Requested_export_routes_stream_filtered_records_with_download_headers |
| Authentication and admin access | Anonymous_and_non_admin_actors_cannot_export |
| Validation and structured errors | Invalid_pagination_returns_structured_errors_before_starting_export; Invalid_csv_columns_are_rejected_before_starting_export; Preparation_failures_return_safe_structured_errors |
| Media negotiation | Unacceptable_export_media_types_do_not_start_export |
| Explicit OpenAPI media and generated download methods | OpenApiDeclaredResponseContentTypesTests; generated/audit-export-contracts.test.ts |
| Pagination, nested schema, gzip and original JSON text | Json_pagination_and_optional_gzip_preserve_schema_and_original_text |
| Shared concurrent export limit | Concurrent_alias_and_versioned_exports_share_the_user_rate_limit |
| Incremental CSV/JSON, bounded test sink and cancellation | AuditControllerCoverageCompletionTests.Exports_ShouldWriteBeforeEnumerationCompletesWithoutBufferingTheDataset; Exports_ShouldStopEnumerationAndNotifyCancellationWhenTheClientDisconnects |
| Failure after streaming starts | Exports_ShouldAbortStartedStreamsAndRecordSafeFailureStatus |
| UTF-8, column order, RFC escaping and formula-like cells | Existing ExportAuditLogs_ShouldStreamEscapedCsvAndHonorSelectedColumnOrder; AuditExportTransportContractTests |

Local TRX files, initial gap reproductions, build logs and the primary-checkout
preservation snapshots are retained under artifacts/test-results/audit-export-contracts.
The complete solution build passed with zero warnings/errors. Final local C#
coverage passed 4,136 cases: audit 437, authentication 1,852, authorization 1,667,
API architecture/security/OpenAPI 147 and PostgreSQL export HTTP 33. The regenerated
client suite passed 1,112 cases, including real HTTP CSV downloads and JSON schema
validation through both route families. Its TypeScript check is recorded separately.
[PR #678](https://github.com/gameguild-gg/gameguild/pull/678) merged to develop at 2026-10-04T15:16:33Z after final-head CI acceptance (head 0197b882ddd49774dbaa075692b7f685f4b0ed8f; merge c9c6e0cb535144408862f544022bc4babc2929f6). Canonical #171/#173 are closed COMPLETED with criterion-by-criterion evidence; #170/#172 are closed as native GitHub duplicates linked to those canonicals. The matrix reflects the verified 64 closed / 264 open state of the entire 328-issue scope.


Build provenance: the complete local solution build passed on production/client implementation 22732e81de72fa02deaee1ab061e85fef3d85fc1. The final PR commit only changes two test-double files: both edited test projects built locally with zero warnings/errors, and the final-head Release API build passed on GitHub with zero warnings/errors and 2,946 passing API-related cases. All 4,136 selected local C# cases were rerun on the final head; client sources remained identical. Subsequent capped local full-build attempts hit compiler memory limits and are excluded from successful build/test evidence. No machine configuration was changed.
