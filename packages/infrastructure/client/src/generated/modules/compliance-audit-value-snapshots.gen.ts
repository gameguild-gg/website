/**
 * @game-guild/client - ComplianceAuditValueSnapshots Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ComplianceAuditValueSnapshotsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Searches the value snapshot store by entity, key, changed field, value pattern, actor, and time window.
   */
  async getApiAuditValueSnapshotsForGetApiAuditValueSnapshots(query?: {
    EntityName?: string;
    KeyId?: string;
    Field?: string;
    ValuePattern?: string;
    ChangeKind?: string;
    ActorId?: string;
    FromUtc?: string;
    ToUtc?: string;
    Skip?: number;
    Take?: number;
  }): Promise<Result<Types.ComplianceAuditValueSnapshotSearchResult, ApiError>> {
    const url = '/api/audit/value-snapshots';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotSearchResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Returns one snapshot with its structured field-level difference analysis.
   */
  async getApiAuditValueSnapshotsForGetApiAuditValueSnapshotsById(id: string): Promise<Result<Types.ComplianceAuditValueSnapshotOutput, ApiError>> {
    const url = `/api/audit/value-snapshots/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Point-in-time value history for one entity field (all fields default to the most changed one).
   */
  async getApiAuditValueSnapshotsHistory(query?: {
    entityName?: string;
    keyId?: string;
    field?: string;
  }): Promise<Result<Types.ComplianceAuditValueHistoryOutput, ApiError>> {
    const url = '/api/audit/value-snapshots/history';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueHistoryOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Change impact analysis: the cascade of one mutation across its operation and entity lineage.
   */
  async getApiAuditValueSnapshotsImpact(snapshotId: string): Promise<Result<Types.ComplianceAuditChangeImpactOutput, ApiError>> {
    const url = `/api/audit/value-snapshots/impact/${snapshotId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditChangeImpactOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Runs one retention pass now: deletion (respecting legal holds), archive-tier stripping, and deduplication compaction. Use `DryRun` to preview.
   */
  async postApiAuditValueSnapshotsRetentionEnforce(
    body: Types.ComplianceAuditEnforceValueSnapshotRetentionInput,
  ): Promise<Result<Types.ComplianceAuditValueSnapshotRetentionExecutionOutput, ApiError>> {
    const url = '/api/audit/value-snapshots/retention/enforce';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditEnforceValueSnapshotRetentionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotRetentionExecutionOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Returns the tenant's value snapshot retention policy, when configured.
   */
  async getApiAuditValueSnapshotsRetentionPolicy(): Promise<Result<Types.ComplianceAuditValueSnapshotRetentionPolicyOutput, ApiError>> {
    const url = '/api/audit/value-snapshots/retention/policy';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotRetentionPolicyOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Creates or updates the value snapshot retention policy (retention days, archive tier, entity overrides, legal hold).
   */
  async putApiAuditValueSnapshotsRetentionPolicy(
    body: Types.ComplianceAuditConfigureValueSnapshotRetentionInput,
  ): Promise<Result<Types.ComplianceAuditValueSnapshotRetentionPolicyOutput, ApiError>> {
    const url = '/api/audit/value-snapshots/retention/policy';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditConfigureValueSnapshotRetentionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotRetentionPolicyOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Builds a rollback plan restoring one entity to its recorded state at a point in time.
   */
  async postApiAuditValueSnapshotsRollbackPlans(
    body: Types.ComplianceAuditCreateValueSnapshotRollbackPlanInput,
  ): Promise<Result<Types.ComplianceAuditValueSnapshotRollbackPlanOutput, ApiError>> {
    const url = '/api/audit/value-snapshots/rollback-plans';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditCreateValueSnapshotRollbackPlanInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotRollbackPlanOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Returns one rollback plan with its reconstructed basis state.
   */
  async getApiAuditValueSnapshotsRollbackPlans(planId: string): Promise<Result<Types.ComplianceAuditValueSnapshotRollbackPlanOutput, ApiError>> {
    const url = `/api/audit/value-snapshots/rollback-plans/${planId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotRollbackPlanOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Executes a rollback plan. Fails closed while newer changes exist after the basis snapshot unless explicitly accepted.
   */
  async postApiAuditValueSnapshotsRollbackPlansExecute(
    planId: string,
    body: Types.ComplianceAuditExecuteValueSnapshotRollbackInput,
  ): Promise<Result<Types.ComplianceAuditValueSnapshotRollbackPlanOutput, ApiError>> {
    const url = `/api/audit/value-snapshots/rollback-plans/${planId}:execute`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditExecuteValueSnapshotRollbackInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotRollbackPlanOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Storage accounting: delta/full share, truncation counts, archive tier, deduplication savings.
   */
  async getApiAuditValueSnapshotsStorage(): Promise<Result<Types.ComplianceAuditValueSnapshotStorageOverview, ApiError>> {
    const url = '/api/audit/value-snapshots/storage';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotStorageOverviewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Value trend analysis for one field over a time window with statistical reporting.
   */
  async getApiAuditValueSnapshotsTrends(query?: {
    entityName?: string;
    keyId?: string;
    field?: string;
    fromUtc?: string;
    toUtc?: string;
  }): Promise<Result<Types.ComplianceAuditValueTrendOutput, ApiError>> {
    const url = '/api/audit/value-snapshots/trends';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueTrendOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Searches the value snapshot store by entity, key, changed field, value pattern, actor, and time window.
   */
  async getAuditValueSnapshotsForGetAuditValueSnapshots(query?: {
    EntityName?: string;
    KeyId?: string;
    Field?: string;
    ValuePattern?: string;
    ChangeKind?: string;
    ActorId?: string;
    FromUtc?: string;
    ToUtc?: string;
    Skip?: number;
    Take?: number;
  }): Promise<Result<Types.ComplianceAuditValueSnapshotSearchResult, ApiError>> {
    const url = '/v1/audit/value-snapshots';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotSearchResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Returns one snapshot with its structured field-level difference analysis.
   */
  async getAuditValueSnapshotsForGetAuditValueSnapshotsById(id: string): Promise<Result<Types.ComplianceAuditValueSnapshotOutput, ApiError>> {
    const url = `/v1/audit/value-snapshots/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Point-in-time value history for one entity field (all fields default to the most changed one).
   */
  async getAuditValueSnapshotsHistory(query?: {
    entityName?: string;
    keyId?: string;
    field?: string;
  }): Promise<Result<Types.ComplianceAuditValueHistoryOutput, ApiError>> {
    const url = '/v1/audit/value-snapshots/history';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueHistoryOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Change impact analysis: the cascade of one mutation across its operation and entity lineage.
   */
  async getAuditValueSnapshotsImpact(snapshotId: string): Promise<Result<Types.ComplianceAuditChangeImpactOutput, ApiError>> {
    const url = `/v1/audit/value-snapshots/impact/${snapshotId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditChangeImpactOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Runs one retention pass now: deletion (respecting legal holds), archive-tier stripping, and deduplication compaction. Use `DryRun` to preview.
   */
  async postAuditValueSnapshotsRetentionEnforce(
    body: Types.ComplianceAuditEnforceValueSnapshotRetentionInput,
  ): Promise<Result<Types.ComplianceAuditValueSnapshotRetentionExecutionOutput, ApiError>> {
    const url = '/v1/audit/value-snapshots/retention/enforce';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditEnforceValueSnapshotRetentionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotRetentionExecutionOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Returns the tenant's value snapshot retention policy, when configured.
   */
  async getAuditValueSnapshotsRetentionPolicy(): Promise<Result<Types.ComplianceAuditValueSnapshotRetentionPolicyOutput, ApiError>> {
    const url = '/v1/audit/value-snapshots/retention/policy';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotRetentionPolicyOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Creates or updates the value snapshot retention policy (retention days, archive tier, entity overrides, legal hold).
   */
  async putAuditValueSnapshotsRetentionPolicy(
    body: Types.ComplianceAuditConfigureValueSnapshotRetentionInput,
  ): Promise<Result<Types.ComplianceAuditValueSnapshotRetentionPolicyOutput, ApiError>> {
    const url = '/v1/audit/value-snapshots/retention/policy';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditConfigureValueSnapshotRetentionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotRetentionPolicyOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Builds a rollback plan restoring one entity to its recorded state at a point in time.
   */
  async postAuditValueSnapshotsRollbackPlans(
    body: Types.ComplianceAuditCreateValueSnapshotRollbackPlanInput,
  ): Promise<Result<Types.ComplianceAuditValueSnapshotRollbackPlanOutput, ApiError>> {
    const url = '/v1/audit/value-snapshots/rollback-plans';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditCreateValueSnapshotRollbackPlanInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotRollbackPlanOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Returns one rollback plan with its reconstructed basis state.
   */
  async getAuditValueSnapshotsRollbackPlans(planId: string): Promise<Result<Types.ComplianceAuditValueSnapshotRollbackPlanOutput, ApiError>> {
    const url = `/v1/audit/value-snapshots/rollback-plans/${planId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotRollbackPlanOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Executes a rollback plan. Fails closed while newer changes exist after the basis snapshot unless explicitly accepted.
   */
  async postAuditValueSnapshotsRollbackPlansExecute(
    planId: string,
    body: Types.ComplianceAuditExecuteValueSnapshotRollbackInput,
  ): Promise<Result<Types.ComplianceAuditValueSnapshotRollbackPlanOutput, ApiError>> {
    const url = `/v1/audit/value-snapshots/rollback-plans/${planId}:execute`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditExecuteValueSnapshotRollbackInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotRollbackPlanOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Storage accounting: delta/full share, truncation counts, archive tier, deduplication savings.
   */
  async getAuditValueSnapshotsStorage(): Promise<Result<Types.ComplianceAuditValueSnapshotStorageOverview, ApiError>> {
    const url = '/v1/audit/value-snapshots/storage';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueSnapshotStorageOverviewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Value trend analysis for one field over a time window with statistical reporting.
   */
  async getAuditValueSnapshotsTrends(query?: {
    entityName?: string;
    keyId?: string;
    field?: string;
    fromUtc?: string;
    toUtc?: string;
  }): Promise<Result<Types.ComplianceAuditValueTrendOutput, ApiError>> {
    const url = '/v1/audit/value-snapshots/trends';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditValueTrendOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createComplianceAuditValueSnapshotsModule(client: ApiClient): ComplianceAuditValueSnapshotsModule {
  return new ComplianceAuditValueSnapshotsModule(client);
}
