/**
 * @game-guild/client - ComplianceAuditQueries Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ComplianceAuditQueriesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Execute an audit query DSL expression (JSON or light text syntax) and return a
   * data-masked, paginated page of matching audit entries plus aggregates over the full
   * matched set. Execution is bounded by a row-scan cap and a wall-clock timeout.
   */
  async postAuditQueriesExecute(
    body: Types.ComplianceAuditAuditQueryExecuteApiInput,
  ): Promise<Result<Types.ComplianceAuditAuditQueryDslExecutionResult, ApiError>> {
    const url = '/v1/audit-queries/:execute';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditAuditQueryExecuteApiInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditQueryDslExecutionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Paged-results GET variant of the DSL executor for the light text syntax
   * (e.g. GET /v1/audit-queries:results?query=actor:alice AND severity:Critical).
   */
  async getAuditQueriesResults(query?: {
    query?: string;
    Skip?: number;
    Take?: number;
  }): Promise<Result<Types.ComplianceAuditAuditQueryDslExecutionResult, ApiError>> {
    const url = '/v1/audit-queries/:results';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditQueryDslExecutionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Validate a DSL query without executing it and return its canonical JSON form.
   * Saved-view editors use this to fail closed before persisting a query.
   */
  async postAuditQueriesValidate(
    body: Types.ComplianceAuditAuditQueryValidateApiInput,
  ): Promise<Result<Types.ComplianceAuditAuditQueryValidationOutput, ApiError>> {
    const url = '/v1/audit-queries/:validate';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditAuditQueryValidateApiInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditQueryValidationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * List saved audit query views visible to the acting user: their own private views
   * plus every shared view within the caller's tenant.
   */
  async getAuditQueriesSavedForGetAuditQueriesSaved(): Promise<Result<Types.ComplianceAuditSavedAuditQueryViewOutput[], ApiError>> {
    const url = '/v1/audit-queries/saved';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditSavedAuditQueryViewOutput[], ApiError>;
  }

  /**
   * Create a saved audit query view. The query is validated (and tenant-scope checked)
   * before persistence; a creation revision snapshot is recorded.
   */
  async postAuditQueriesSaved(
    body: Types.ComplianceAuditSaveSavedAuditQueryApiInput,
  ): Promise<Result<Types.ComplianceAuditSavedAuditQueryViewOutput, ApiError>> {
    const url = '/v1/audit-queries/saved';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditSaveSavedAuditQueryApiInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSavedAuditQueryViewOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get one saved view. Private views are only visible to their owner.
   */
  async getAuditQueriesSavedForGetAuditQueriesSavedById(id: string): Promise<Result<Types.ComplianceAuditSavedAuditQueryViewOutput, ApiError>> {
    const url = `/v1/audit-queries/saved/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSavedAuditQueryViewOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update a saved view (owner only). A new revision snapshot is recorded.
   */
  async putAuditQueriesSaved(
    id: string,
    body: Types.ComplianceAuditSaveSavedAuditQueryApiInput,
  ): Promise<Result<Types.ComplianceAuditSavedAuditQueryViewOutput, ApiError>> {
    const url = `/v1/audit-queries/saved/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditSaveSavedAuditQueryApiInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSavedAuditQueryViewOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Delete a saved view (owner only). Its revision history is retained for audit.
   */
  async deleteAuditQueriesSaved(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/audit-queries/saved/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Execute a saved view by identifier (owner or shared), paging over its canonical query.
   * Execution usage and attribution are recorded on the view and in the audit log.
   */
  async postAuditQueriesSavedExecute(
    id: string,
    body: Types.ComplianceAuditAuditQueryPagingApiInput,
  ): Promise<Result<Types.ComplianceAuditAuditQueryDslExecutionResult, ApiError>> {
    const url = `/v1/audit-queries/saved/${id}:execute`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditAuditQueryPagingApiInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditQueryDslExecutionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Full revision history of a saved view: who changed what, when, and the exact query
   * payload of every revision. Revisions survive deletion of the view itself.
   */
  async getAuditQueriesSavedRevisions(id: string): Promise<Result<Types.ComplianceAuditSavedAuditQueryRevisionOutput[], ApiError>> {
    const url = `/v1/audit-queries/saved/${id}/revisions`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditSavedAuditQueryRevisionOutput[], ApiError>;
  }

  /**
   * Built-in role-based query templates for common investigation and compliance scenarios.
   */
  async getAuditQueriesTemplates(): Promise<Result<Types.ComplianceAuditAuditQueryTemplate[], ApiError>> {
    const url = '/v1/audit-queries/templates';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditAuditQueryTemplate[], ApiError>;
  }
}

export function createComplianceAuditQueriesModule(client: ApiClient): ComplianceAuditQueriesModule {
  return new ComplianceAuditQueriesModule(client);
}
