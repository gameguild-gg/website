# Audit search by action type

The system-admin API exposes action-type search under the versioned audit-log route:

- `GET /v1/admin/audit-logs/search/by-action-type`
- `GET /v1/admin/audit-logs/search/by-action-type/taxonomy`
- `GET /v1/admin/audit-logs/search/by-action-type/export?format=csv|json`

Search supports exact action types, the predefined `CRUD`, `Security`, and `Admin` groups, hierarchical category paths, user and tenant IDs, UTC-normalized date bounds, sorting, pagination, action frequency, time trends, and correlation-based related-action suggestions. Authentication and system-admin authorization follow the containing `AuditController` policy.

## Search example

```http
GET /v1/admin/audit-logs/search/by-action-type?ActionTypes=Login&ActionTypes=SecurityViolation&ActionGroups=Security&LogicalOperator=All&StartDate=2026-09-01T00%3A00%3A00Z&EndDate=2026-10-01T00%3A00%3A00Z&SortBy=CreatedAt&SortDirection=Descending&Skip=0&Take=100&IncludeTrends=true&TrendBucketSize=Daily&IncludeRelatedActions=true
```

Values within `ActionTypes`, `ActionGroups`, and `Categories` are combined with OR. `LogicalOperator` combines those dimensions: `Any` is OR, `All` is AND, and `None` excludes rows matching any selected dimension. `StartDate` and `EndDate` are inclusive. Pagination accepts `Skip >= 0` and `1 <= Take <= 1000`.

Category paths are case-insensitive and use the taxonomy shape `Root.Subcategory`, for example `Security.Authentication`, `Security.Authorization`, `Security.Sessions`, `Security.ThreatDetection`, `Identity.UserManagement`, `Identity.Usernames`, `Administration.Platform`, `Data.Movement`, `Data.Privacy`, and `Tenancy.Management`. The taxonomy endpoint returns the currently registered action types with their category paths and group memberships.

The response contains paginated records, frequency counts over the full filtered result set, and hourly or daily per-action trends when requested. Related-action suggestions count other records that share a non-empty correlation ID with a record on the current page; suggestions are bounded to 20 action types and 5,000 correlated records.

## Export

Use the same filter parameters with the export route and `format=csv` or `format=json`. Export ignores `Skip` and `Take` and includes all matching records when the result contains at most 10,000 records. Larger exports return HTTP 413 so callers can narrow their filters instead of receiving an incomplete file. CSV fields are quoted and escaped; JSON uses the audit-log DTO shape.
