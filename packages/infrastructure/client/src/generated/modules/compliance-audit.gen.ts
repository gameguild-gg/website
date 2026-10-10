/**
 * @game-guild/client - ComplianceAudit Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ComplianceAuditModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Export audit logs (admin only)
   */
  async postApiAuditExportCsv(body: Types.ComplianceAuditAuditExportInput): Promise<Result<Blob, ApiError>> {
    const url = '/api/audit/export/csv';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditAuditExportInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      responseType: 'blob',
      headers: { Accept: 'text/csv' },
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<Blob, ApiError>;
  }

  /**
   * Streams a versioned JSON audit export with pagination metadata.
   */
  async postApiAuditExportJson(body: Types.ComplianceAuditAuditExportInput): Promise<Result<Types.ComplianceAuditAuditJsonExportDocument, ApiError>> {
    const url = '/api/audit/export/json';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditAuditExportInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditJsonExportDocumentSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get audit logs with filtering and pagination
   */
  async getAdminAuditLogs(query?: {
    UserId?: string;
    TenantId?: string;
    ActionType?: string;
    ResourceType?: string;
    Category?: Types.ComplianceAuditAuditCategory;
    RiskLevel?: Types.ComplianceAuditAuditRiskLevel;
    Success?: boolean;
    StartDate?: string;
    EndDate?: string;
    IpAddress?: string;
    Skip?: number;
    Take?: number;
  }): Promise<Result<Types.ComplianceAuditAuditLogOutput, ApiError>> {
    const url = '/v1/admin/audit-logs';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditLogOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Export audit logs (admin only)
   */
  async postAdminAuditLogsExport(body: Types.ComplianceAuditAuditExportInput): Promise<Result<Blob, ApiError>> {
    const url = '/v1/admin/audit-logs/:export';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditAuditExportInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      responseType: 'blob',
      headers: { Accept: 'text/csv' },
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<Blob, ApiError>;
  }

  /**
   * Returns the current state of an export started by the authenticated administrator.
   */
  async getAdminAuditLogsExportProgress(exportId: string): Promise<Result<Types.ComplianceAuditAuditExportProgressOutput, ApiError>> {
    const url = `/v1/admin/audit-logs/export/${exportId}/progress`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditExportProgressOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Export audit logs (admin only)
   */
  async postAdminAuditLogsExportCsv(body: Types.ComplianceAuditAuditExportInput): Promise<Result<Blob, ApiError>> {
    const url = '/v1/admin/audit-logs/export/csv';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditAuditExportInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      responseType: 'blob',
      headers: { Accept: 'text/csv' },
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<Blob, ApiError>;
  }

  /**
   * Streams a versioned JSON audit export with pagination metadata.
   */
  async postAdminAuditLogsExportJson(body: Types.ComplianceAuditAuditExportInput): Promise<Result<Types.ComplianceAuditAuditJsonExportDocument, ApiError>> {
    const url = '/v1/admin/audit-logs/export/json';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditAuditExportInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditJsonExportDocumentSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Downloads a completed scheduled export stored for its tenant.
   */
  async getAdminAuditLogsScheduledExportHistoryDownload(historyId: string, query?: { tenantId?: string }): Promise<Result<void, ApiError>> {
    const url = `/v1/admin/audit-logs/scheduled-export-history/${historyId}/download`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Lists recurring audit exports for a tenant.
   */
  async getAdminAuditLogsScheduledExports(query?: { tenantId?: string }): Promise<Result<Types.ComplianceAuditScheduledAuditExportOutput[], ApiError>> {
    const url = '/v1/admin/audit-logs/scheduled-exports';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditScheduledAuditExportOutput[], ApiError>;
  }

  /**
   * Creates a recurring audit export delivered to the tenant's configured storage.
   *
   * Uses a five-field cron expression and the supplied timezone. During a repeated local time at the end of daylight
   * saving, the first UTC occurrence is used. Files are removed after the configured retention period while their
   * execution history remains available.
   */
  async postAdminAuditLogsScheduledExports(
    body: Types.ComplianceAuditCreateScheduledAuditExportInput,
  ): Promise<Result<Types.ComplianceAuditScheduledAuditExportOutput, ApiError>> {
    const url = '/v1/admin/audit-logs/scheduled-exports';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditCreateScheduledAuditExportInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditScheduledAuditExportOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Disables a recurring audit export without deleting its execution history.
   */
  async deleteAdminAuditLogsScheduledExports(exportId: string, query?: { tenantId?: string }): Promise<Result<void, ApiError>> {
    const url = `/v1/admin/audit-logs/scheduled-exports/${exportId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Lists recent executions for a scheduled audit export.
   */
  async getAdminAuditLogsScheduledExportsHistory(
    exportId: string,
    query?: { tenantId?: string },
  ): Promise<Result<Types.ComplianceAuditAuditExportHistoryOutput[], ApiError>> {
    const url = `/v1/admin/audit-logs/scheduled-exports/${exportId}/history`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditAuditExportHistoryOutput[], ApiError>;
  }

  /**
   * Searches audit events by multiple action types, groups, and taxonomy categories.
   */
  async getAdminAuditLogsSearchByActionType(query?: {
    ActionTypes?: string[];
    ActionGroups?: string[];
    Categories?: string[];
    LogicalOperator?: Types.ComplianceAuditAuditActionTypeLogicalOperator;
    UserId?: string;
    TenantId?: string;
    StartDate?: string;
    EndDate?: string;
    Skip?: number;
    Take?: number;
    SortBy?: Types.ComplianceAuditAuditActionTypeSortField;
    SortDirection?: Types.ComplianceAuditAuditActionTypeSortDirection;
    IncludeTrends?: boolean;
    TrendBucketSize?: Types.ComplianceAuditAuditActivityBucketSize;
    IncludeRelatedActions?: boolean;
  }): Promise<Result<Types.ComplianceAuditAuditActionTypeSearchOutput, ApiError>> {
    const url = '/v1/admin/audit-logs/search/by-action-type';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditActionTypeSearchOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Exports matching action-type audit events as CSV or JSON.
   */
  async getAdminAuditLogsSearchByActionTypeExport(query?: {
    format?: string;
    ActionTypes?: string[];
    ActionGroups?: string[];
    Categories?: string[];
    LogicalOperator?: Types.ComplianceAuditAuditActionTypeLogicalOperator;
    UserId?: string;
    TenantId?: string;
    StartDate?: string;
    EndDate?: string;
    Skip?: number;
    Take?: number;
    SortBy?: Types.ComplianceAuditAuditActionTypeSortField;
    SortDirection?: Types.ComplianceAuditAuditActionTypeSortDirection;
    IncludeTrends?: boolean;
    TrendBucketSize?: Types.ComplianceAuditAuditActivityBucketSize;
    IncludeRelatedActions?: boolean;
  }): Promise<Result<void, ApiError>> {
    const url = '/v1/admin/audit-logs/search/by-action-type/export';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Lists the hierarchical action type taxonomy and predefined investigation groups.
   */
  async getAdminAuditLogsSearchByActionTypeTaxonomy(): Promise<Result<Types.ComplianceAuditAuditActionTypeTaxonomyOutput, ApiError>> {
    const url = '/v1/admin/audit-logs/search/by-action-type/taxonomy';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditActionTypeTaxonomyOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Searches audit records over an explicit or relative date range and returns matching events with a time histogram.
   *
   * Use `start`/`end` with ISO-8601 timestamps, Unix seconds or milliseconds, or relative expressions
   * such as `now-7d` and `now`. Alternatively use `period=last24h|last7d|last30d|today|thisWeek|thisMonth`.
   * Offset-free values are interpreted in `timeZoneId` (UTC by default). Date-only end values include that
   * calendar day. Hourly or daily histogram buckets include both UTC and local timestamps.
   */
  async getAdminAuditLogsSearchByDateRange(query?: {
    Start?: string;
    End?: string;
    Period?: string;
    TimeZoneId?: string;
    BucketSize?: Types.ComplianceAuditAuditActivityBucketSize;
    UserId?: string;
    TenantId?: string;
    ActionType?: string;
    ResourceType?: string;
    Category?: Types.ComplianceAuditAuditCategory;
    RiskLevel?: Types.ComplianceAuditAuditRiskLevel;
    Success?: boolean;
    IpAddress?: string;
    Skip?: number;
    Take?: number;
  }): Promise<Result<Types.ComplianceAuditAuditDateRangeSearchOutput, ApiError>> {
    const url = '/v1/admin/audit-logs/search/by-date-range';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditDateRangeSearchOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get audit log statistics
   */
  async getAdminAuditLogsStatistics(query?: { StartDate?: string; EndDate?: string }): Promise<Result<Types.ComplianceAuditAuditStatisticsOutput, ApiError>> {
    const url = '/v1/admin/audit-logs/statistics';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditStatisticsOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createComplianceAuditModule(client: ApiClient): ComplianceAuditModule {
  return new ComplianceAuditModule(client);
}
