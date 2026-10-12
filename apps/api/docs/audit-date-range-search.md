# Audit date-range search

`GET /v1/admin/audit-logs/search/by-date-range` searches audit events over an explicit or relative date range. It requires the system administrator policy and uses the same filters as the audit log listing and CSV/JSON exports.

## Date inputs

Provide both `Start` and `End`, or provide `Period`:

| Input | Example | Meaning |
| --- | --- | --- |
| ISO-8601 with offset | `Start=2026-09-20T10:00:00-03:00` | Converted to UTC using the supplied offset |
| ISO-8601 without offset | `Start=2026-09-20T10:00:00` | Interpreted in `TimeZoneId` |
| Date-only bound | `Start=2026-09-01&End=2026-09-02` | Start is local midnight; End includes the full local calendar day |
| Unix seconds or milliseconds | `Start=1790000000` | Millisecond timestamps are detected by their magnitude |
| Relative expression | `Start=now-7d&End=now` | Supports seconds (`s`), minutes (`m`), hours (`h`), days (`d`), and weeks (`w`); `last N days` is also accepted |

Supported period shortcuts are `last24h`, `last7d`, `last30d`, `today`, `thisWeek`, and `thisMonth`. Calendar shortcuts use `TimeZoneId`; elapsed shortcuts use rolling UTC durations. `TimeZoneId` defaults to `UTC` and accepts system timezone identifiers such as `America/Sao_Paulo`.

Ambiguous or nonexistent local wall-clock times during daylight-saving transitions are rejected rather than guessed. The endpoint also rejects unknown timezones, unsupported periods, incomplete explicit ranges, and an End earlier than Start. Date-only End values include the entire selected calendar day.

## Filters and response

The search accepts `UserId`, `TenantId`, `ActionType`, `ResourceType`, `Category`, `RiskLevel`, `Success`, and `IpAddress`, plus `Skip` and `Take` (maximum 1,000). Results are ordered newest first. Date predicates use the existing `CreatedAt` and `(TenantId, CreatedAt)` indexes.

The response contains normalized UTC bounds, the selected timezone, paginated matching events, and hourly or daily activity counts. `BucketSize` can be `Hourly` or `Daily`; when omitted, ranges up to 72 hours use hourly buckets and longer ranges use daily buckets. Each activity bucket includes its UTC boundary and its timezone-converted local boundary.

CSV and JSON exports already accept `StartDate` and `EndDate` alongside the same audit filters. Use the resolved UTC bounds from this search response in `POST /v1/admin/audit-logs/export/csv` or `POST /v1/admin/audit-logs/export/json` to export that date range.
