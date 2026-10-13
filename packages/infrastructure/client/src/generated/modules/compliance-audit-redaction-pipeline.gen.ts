/**
 * @game-guild/client - ComplianceAuditRedactionPipeline Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ComplianceAuditRedactionPipelineModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Pipeline status: cached rule snapshots, enabled rule counts (global and tenant), vault size,
   * and the last rule-refresh outcome.
   */
  async getApiAuditRedactionPipeline(): Promise<Result<Types.ComplianceAuditPiiRedactionPipelineStatusOutput, ApiError>> {
    const url = '/api/audit/redaction-pipeline';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditPiiRedactionPipelineStatusOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lists the tenant's rules plus the platform-global rules visible to it.
   */
  async getApiAuditRedactionPipelineRulesForGetApiAuditRedactionPipelineRules(): Promise<Result<Types.ComplianceAuditPiiRedactionRuleOutput[], ApiError>> {
    const url = '/api/audit/redaction-pipeline/rules';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditPiiRedactionRuleOutput[], ApiError>;
  }

  /**
   * Creates a redaction rule. Guarded to tenant administrators; audited; invalidates the rule cache.
   */
  async postApiAuditRedactionPipelineRules(
    body: Types.ComplianceAuditUpsertPiiRedactionRuleInput,
  ): Promise<Result<Types.ComplianceAuditPiiRedactionRuleOutput, ApiError>> {
    const url = '/api/audit/redaction-pipeline/rules';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditUpsertPiiRedactionRuleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditPiiRedactionRuleOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Gets one rule by id.
   */
  async getApiAuditRedactionPipelineRulesForGetApiAuditRedactionPipelineRulesByRuleId(
    ruleId: string,
  ): Promise<Result<Types.ComplianceAuditPiiRedactionRuleOutput, ApiError>> {
    const url = `/api/audit/redaction-pipeline/rules/${ruleId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditPiiRedactionRuleOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Updates a rule. Versioned: the request must carry the revision observed on read; a stale
   * revision returns 409 Conflict.
   */
  async putApiAuditRedactionPipelineRules(
    ruleId: string,
    body: Types.ComplianceAuditUpsertPiiRedactionRuleInput,
    query?: { expectedRevision?: number },
  ): Promise<Result<Types.ComplianceAuditPiiRedactionRuleOutput, ApiError>> {
    const url = `/api/audit/redaction-pipeline/rules/${ruleId}`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditUpsertPiiRedactionRuleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      params: query,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditPiiRedactionRuleOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Deletes a rule. Versioned like the update; audited at high risk.
   */
  async deleteApiAuditRedactionPipelineRules(ruleId: string, query?: { expectedRevision?: number }): Promise<Result<void, ApiError>> {
    const url = `/api/audit/redaction-pipeline/rules/${ruleId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Dry-runs the enabled rules against sample field values without persisting anything — the
   * pattern-recognition verification surface for administrators.
   */
  async postApiAuditRedactionPipelineTest(
    body: Types.ComplianceAuditPiiRedactionTestRunInput,
  ): Promise<Result<Types.ComplianceAuditPiiRedactionTestRunOutput, ApiError>> {
    const url = '/api/audit/redaction-pipeline/test';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditPiiRedactionTestRunInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditPiiRedactionTestRunOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Resolves a deterministic token to its original value from the token vault for an authorized
   * investigation. Requires a reason; audited at critical risk.
   */
  async getApiAuditRedactionPipelineTokens(
    token: string,
    query?: { reason?: string },
  ): Promise<Result<Types.ComplianceAuditPiiTokenResolutionOutput, ApiError>> {
    const url = `/api/audit/redaction-pipeline/tokens/${token}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditPiiTokenResolutionOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Pipeline status: cached rule snapshots, enabled rule counts (global and tenant), vault size,
   * and the last rule-refresh outcome.
   */
  async getAuditRedactionPipeline(): Promise<Result<Types.ComplianceAuditPiiRedactionPipelineStatusOutput, ApiError>> {
    const url = '/v1/audit/redaction-pipeline';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditPiiRedactionPipelineStatusOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lists the tenant's rules plus the platform-global rules visible to it.
   */
  async getAuditRedactionPipelineRulesForGetAuditRedactionPipelineRules(): Promise<Result<Types.ComplianceAuditPiiRedactionRuleOutput[], ApiError>> {
    const url = '/v1/audit/redaction-pipeline/rules';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditPiiRedactionRuleOutput[], ApiError>;
  }

  /**
   * Creates a redaction rule. Guarded to tenant administrators; audited; invalidates the rule cache.
   */
  async postAuditRedactionPipelineRules(
    body: Types.ComplianceAuditUpsertPiiRedactionRuleInput,
  ): Promise<Result<Types.ComplianceAuditPiiRedactionRuleOutput, ApiError>> {
    const url = '/v1/audit/redaction-pipeline/rules';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditUpsertPiiRedactionRuleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditPiiRedactionRuleOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Gets one rule by id.
   */
  async getAuditRedactionPipelineRulesForGetAuditRedactionPipelineRulesByRuleId(
    ruleId: string,
  ): Promise<Result<Types.ComplianceAuditPiiRedactionRuleOutput, ApiError>> {
    const url = `/v1/audit/redaction-pipeline/rules/${ruleId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditPiiRedactionRuleOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Updates a rule. Versioned: the request must carry the revision observed on read; a stale
   * revision returns 409 Conflict.
   */
  async putAuditRedactionPipelineRules(
    ruleId: string,
    body: Types.ComplianceAuditUpsertPiiRedactionRuleInput,
    query?: { expectedRevision?: number },
  ): Promise<Result<Types.ComplianceAuditPiiRedactionRuleOutput, ApiError>> {
    const url = `/v1/audit/redaction-pipeline/rules/${ruleId}`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditUpsertPiiRedactionRuleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      params: query,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditPiiRedactionRuleOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Deletes a rule. Versioned like the update; audited at high risk.
   */
  async deleteAuditRedactionPipelineRules(ruleId: string, query?: { expectedRevision?: number }): Promise<Result<void, ApiError>> {
    const url = `/v1/audit/redaction-pipeline/rules/${ruleId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Dry-runs the enabled rules against sample field values without persisting anything — the
   * pattern-recognition verification surface for administrators.
   */
  async postAuditRedactionPipelineTest(
    body: Types.ComplianceAuditPiiRedactionTestRunInput,
  ): Promise<Result<Types.ComplianceAuditPiiRedactionTestRunOutput, ApiError>> {
    const url = '/v1/audit/redaction-pipeline/test';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditPiiRedactionTestRunInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditPiiRedactionTestRunOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Resolves a deterministic token to its original value from the token vault for an authorized
   * investigation. Requires a reason; audited at critical risk.
   */
  async getAuditRedactionPipelineTokens(token: string, query?: { reason?: string }): Promise<Result<Types.ComplianceAuditPiiTokenResolutionOutput, ApiError>> {
    const url = `/v1/audit/redaction-pipeline/tokens/${token}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditPiiTokenResolutionOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createComplianceAuditRedactionPipelineModule(client: ApiClient): ComplianceAuditRedactionPipelineModule {
  return new ComplianceAuditRedactionPipelineModule(client);
}
