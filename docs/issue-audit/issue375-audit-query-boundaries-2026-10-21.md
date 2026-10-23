# Issue 375: audit query boundaries

Issue 375 retains all four approved acceptance criteria and remains open. This increment addresses nine existing audit actions, two conditional text-search surfaces, and authentication-event isolation in unified lists, CSV exports and dashboard aggregates. Application-wide endpoint reconciliation and content/resource-context acceptance remain pending.

## Operation requirements

The operation requirement composes with the existing domain policy. Neither `audit:read` nor another operation substitutes for the required key. The existing DAC filter resolves the authenticated actor and tenant, including explicit denies. Existing tenant clamps remain in the controllers.

| Controller action | Required operation |
| --- | --- |
| Audit.GetAuditLogs | List; also Search when SearchText has a non-blank value |
| Audit.SearchAuditLogsByDateRange | Search |
| Audit.SearchAuditLogsWithConditions, including its legacy route | Query |
| Audit.SearchAuditLogsByActionType | Search |
| Audit.GetAuditActionTypeTaxonomy | List |
| Audit.GetAuditStatistics | Query |
| SecurityAudit.GetSecurityAuditLogs | List; also Search when SearchText has a non-blank value |
| SecurityAudit.GetPermissionLogs | List |
| SecurityAudit.GetSecurityDashboard | Query |

`RequireSearchPermission` uses the existing conditional DAC attribute extension. Empty and whitespace-only search values keep the List requirement. A non-blank value among repeated parameters requires Search. It must accompany the independent List and domain policy requirements.

## Result isolation

AuthenticationAttempt already records nullable TenantId. Unified queries now pass the requested tenant into the authentication-row query and retain the recorded tenant in projections. Dashboard authentication aggregates filter that same field before counting, grouping or selecting top users/IPs. Tenant-scoped requests exclude both foreign-tenant and unscoped historical records. An authorized platform-wide request with no tenant filter retains its existing scope.

The standalone authentication-attempt endpoint retains its existing SystemAdmin requirement. No tenant is inferred from a user's current memberships, and no historical events are reassigned.

## Acceptance evidence required

The increment defines 34 HTTP/PostgreSQL cases using the real JWT handler and persisted permission grants, eight repository/PostgreSQL cases, plus 12 attribute contract cases. They cover denied operation grants, the three positive operation paths, non-substitution, cross-tenant grant/request isolation, explicit denies, text-search composition and authentication data/aggregate isolation on list, dashboard and export routes.

The first hosted run on `1bdd550d07fb0b03c9d59dad3dc1a33c65464a89` executed all 34 HTTP cases: 29 passed and five security-audit requests returned HTTP 500. The server diagnostics identify an existing EF query translation failure in `PermissionAuditLogRepository.GetByDateRangeAsync`: dereferencing the converted `TenantId` value inside the expression cannot be translated by PostgreSQL. All eight repository query shapes using that predicate now compare the mapped value object directly, retaining server-side tenant filtering. Each new repository case checks own-tenant results, absence of results for an unknown tenant, and the intentional platform-wide behavior of a null scope. The five HTTP failures must also pass before accepting this correction.

These are test definitions until hosted CI executes them. Full current-source CI, the existing authorization/audit/API suites, and review of actual results are required before accepting the increment. This document is not proof that the full issue is complete.

## Remaining full-issue scope

- Inventory every applicable application list/search/query endpoint, including exports, scheduled histories and other audit controllers.
- Reconcile user, tenant, content-type and resource requirements for those endpoints.
- Verify returned results and denied responses with persisted grants and actual HTTP authentication.
- Preserve the independent scope of issue 307 and all four criteria of issue 375.
