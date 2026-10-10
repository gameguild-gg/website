/**
 * @game-guild/client - ComplianceAuditSecurityEvents Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ComplianceAuditSecurityEventsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Returns the security alert queue for the current tenant.
   */
  async getApiAuditSecurityEventsAlerts(query?: {
    status?: Types.ComplianceAuditSecurityAlertStatus;
    severity?: Types.ComplianceAuditAuditRiskLevel;
    kind?: Types.ComplianceAuditSecurityEventKind;
    ruleId?: string;
    subjectUserId?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditSecurityAlertOutput[], ApiError>> {
    const url = '/api/audit/security-events/alerts';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditSecurityAlertOutput[], ApiError>;
  }

  /**
   * Resolves a security alert, completing the incident lifecycle (detection → acknowledgement →
   * resolution). The acting administrator is derived from the request context and recorded together
   * with the resolution instant and note; the resolution is audited as a security event.
   */
  async postApiAuditSecurityEventsAlertsResolve(
    alertId: string,
    body: Types.ComplianceAuditResolveSecurityAlertInput,
  ): Promise<Result<Types.ComplianceAuditSecurityAlertOutput, ApiError>> {
    const url = `/api/audit/security-events/alerts/${alertId}:resolve`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditResolveSecurityAlertInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSecurityAlertOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Acknowledges an open security alert. The acting administrator is derived from the request context.
   */
  async postApiAuditSecurityEventsAlertsAcknowledge(
    alertId: string,
    body: Types.ComplianceAuditAcknowledgeSecurityAlertInput,
  ): Promise<Result<Types.ComplianceAuditSecurityAlertOutput, ApiError>> {
    const url = `/api/audit/security-events/alerts/${alertId}/acknowledge`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditAcknowledgeSecurityAlertInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSecurityAlertOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Returns the durable delivery status of the security event pipeline for this instance.
   */
  async getApiAuditSecurityEventsDeliveryStatus(): Promise<Result<Types.ComplianceAuditSecurityEventDeliveryStatusOutput, ApiError>> {
    const url = '/api/audit/security-events/delivery-status';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSecurityEventDeliveryStatusOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Runs one retention enforcement pass for the current tenant now. Passes respect legal holds
   * and are recorded in the execution history; use `DryRun` to preview deletions.
   */
  async postApiAuditSecurityEventsRetentionEnforce(
    body: Types.ComplianceAuditEnforceSecurityLogRetentionInput,
  ): Promise<Result<Types.ComplianceAuditSecurityLogRetentionExecutionOutput, ApiError>> {
    const url = '/api/audit/security-events/retention/enforce';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditEnforceSecurityLogRetentionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSecurityLogRetentionExecutionOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Returns the retention execution history for the current tenant.
   */
  async getApiAuditSecurityEventsRetentionExecutions(query?: {
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditSecurityLogRetentionExecutionOutput[], ApiError>> {
    const url = '/api/audit/security-events/retention/executions';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditSecurityLogRetentionExecutionOutput[], ApiError>;
  }

  /**
   * Returns the security log retention policy for the current tenant, when configured.
   */
  async getApiAuditSecurityEventsRetentionPolicy(): Promise<Result<Types.ComplianceAuditSecurityLogRetentionPolicyOutput, ApiError>> {
    const url = '/api/audit/security-events/retention/policy';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSecurityLogRetentionPolicyOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Creates or updates the security log retention policy for the current tenant.
   */
  async putApiAuditSecurityEventsRetentionPolicy(
    body: Types.ComplianceAuditConfigureSecurityLogRetentionInput,
  ): Promise<Result<Types.ComplianceAuditSecurityLogRetentionPolicyOutput, ApiError>> {
    const url = '/api/audit/security-events/retention/policy';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditConfigureSecurityLogRetentionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSecurityLogRetentionPolicyOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Returns the complete security event taxonomy used by the security event pipeline.
   */
  async getApiAuditSecurityEventsTaxonomy(): Promise<Result<Types.ComplianceAuditSecurityEventTaxonomyOutput, ApiError>> {
    const url = '/api/audit/security-events/taxonomy';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSecurityEventTaxonomyOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Returns the security alert queue for the current tenant.
   */
  async getAuditSecurityEventsAlerts(query?: {
    status?: Types.ComplianceAuditSecurityAlertStatus;
    severity?: Types.ComplianceAuditAuditRiskLevel;
    kind?: Types.ComplianceAuditSecurityEventKind;
    ruleId?: string;
    subjectUserId?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditSecurityAlertOutput[], ApiError>> {
    const url = '/v1/audit/security-events/alerts';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditSecurityAlertOutput[], ApiError>;
  }

  /**
   * Resolves a security alert, completing the incident lifecycle (detection → acknowledgement →
   * resolution). The acting administrator is derived from the request context and recorded together
   * with the resolution instant and note; the resolution is audited as a security event.
   */
  async postAuditSecurityEventsAlertsResolve(
    alertId: string,
    body: Types.ComplianceAuditResolveSecurityAlertInput,
  ): Promise<Result<Types.ComplianceAuditSecurityAlertOutput, ApiError>> {
    const url = `/v1/audit/security-events/alerts/${alertId}:resolve`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditResolveSecurityAlertInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSecurityAlertOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Acknowledges an open security alert. The acting administrator is derived from the request context.
   */
  async postAuditSecurityEventsAlertsAcknowledge(
    alertId: string,
    body: Types.ComplianceAuditAcknowledgeSecurityAlertInput,
  ): Promise<Result<Types.ComplianceAuditSecurityAlertOutput, ApiError>> {
    const url = `/v1/audit/security-events/alerts/${alertId}/acknowledge`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditAcknowledgeSecurityAlertInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSecurityAlertOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Returns the durable delivery status of the security event pipeline for this instance.
   */
  async getAuditSecurityEventsDeliveryStatus(): Promise<Result<Types.ComplianceAuditSecurityEventDeliveryStatusOutput, ApiError>> {
    const url = '/v1/audit/security-events/delivery-status';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSecurityEventDeliveryStatusOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Runs one retention enforcement pass for the current tenant now. Passes respect legal holds
   * and are recorded in the execution history; use `DryRun` to preview deletions.
   */
  async postAuditSecurityEventsRetentionEnforce(
    body: Types.ComplianceAuditEnforceSecurityLogRetentionInput,
  ): Promise<Result<Types.ComplianceAuditSecurityLogRetentionExecutionOutput, ApiError>> {
    const url = '/v1/audit/security-events/retention/enforce';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditEnforceSecurityLogRetentionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSecurityLogRetentionExecutionOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Returns the retention execution history for the current tenant.
   */
  async getAuditSecurityEventsRetentionExecutions(query?: {
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditSecurityLogRetentionExecutionOutput[], ApiError>> {
    const url = '/v1/audit/security-events/retention/executions';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditSecurityLogRetentionExecutionOutput[], ApiError>;
  }

  /**
   * Returns the security log retention policy for the current tenant, when configured.
   */
  async getAuditSecurityEventsRetentionPolicy(): Promise<Result<Types.ComplianceAuditSecurityLogRetentionPolicyOutput, ApiError>> {
    const url = '/v1/audit/security-events/retention/policy';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSecurityLogRetentionPolicyOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Creates or updates the security log retention policy for the current tenant.
   */
  async putAuditSecurityEventsRetentionPolicy(
    body: Types.ComplianceAuditConfigureSecurityLogRetentionInput,
  ): Promise<Result<Types.ComplianceAuditSecurityLogRetentionPolicyOutput, ApiError>> {
    const url = '/v1/audit/security-events/retention/policy';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditConfigureSecurityLogRetentionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSecurityLogRetentionPolicyOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Returns the complete security event taxonomy used by the security event pipeline.
   */
  async getAuditSecurityEventsTaxonomy(): Promise<Result<Types.ComplianceAuditSecurityEventTaxonomyOutput, ApiError>> {
    const url = '/v1/audit/security-events/taxonomy';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSecurityEventTaxonomyOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createComplianceAuditSecurityEventsModule(client: ApiClient): ComplianceAuditSecurityEventsModule {
  return new ComplianceAuditSecurityEventsModule(client);
}
