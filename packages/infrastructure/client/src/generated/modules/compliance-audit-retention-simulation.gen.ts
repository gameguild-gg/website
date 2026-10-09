/**
 * @game-guild/client - ComplianceAuditRetentionSimulation Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ComplianceAuditRetentionSimulationModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getApiAuditRetentionPoliciesForGetApiAuditRetentionPolicies(query?: {
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditAuditRetentionSimulationSummary[], ApiError>> {
    const url = '/api/audit/retention-policies';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditAuditRetentionSimulationSummary[], ApiError>;
  }

  /**
   */
  async postApiAuditRetentionPolicies(
    body: Types.ComplianceAuditRunAuditRetentionSimulationInput,
  ): Promise<Result<Types.ComplianceAuditAuditRetentionSimulationOutput, ApiError>> {
    const url = '/api/audit/retention-policies';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditRunAuditRetentionSimulationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditRetentionSimulationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiAuditRetentionPoliciesForGetApiAuditRetentionPoliciesById(
    id: string,
  ): Promise<Result<Types.ComplianceAuditAuditRetentionSimulationOutput, ApiError>> {
    const url = `/api/audit/retention-policies/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditRetentionSimulationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Gets the tenant retention policy configuration. With includeInherited, a tenant
   * without an explicit configuration receives the platform baseline template the policy tree inherits from.
   */
  async getApiAuditRetentionPoliciesConfiguration(query?: {
    includeInherited?: boolean;
  }): Promise<Result<Types.ComplianceAuditAuditRetentionConfigurationOutput, ApiError>> {
    const url = '/api/audit/retention-policies/configuration';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditRetentionConfigurationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putApiAuditRetentionPoliciesConfiguration(
    body: Types.ComplianceAuditConfigureAuditRetentionInput,
  ): Promise<Result<Types.ComplianceAuditAuditRetentionConfigurationOutput, ApiError>> {
    const url = '/api/audit/retention-policies/configuration';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditConfigureAuditRetentionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditRetentionConfigurationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lists the pre-built retention policy templates (platform baseline plus SOC 2, ISO 27001, GDPR,
   * HIPAA, PCI DSS and FedRAMP presets) with their inheritance chain and sensitivity retention floors.
   */
  async getApiAuditRetentionPoliciesTemplates(): Promise<Result<Types.ComplianceAuditAuditRetentionPolicyTemplate[], ApiError>> {
    const url = '/api/audit/retention-policies/templates';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditAuditRetentionPolicyTemplate[], ApiError>;
  }

  /**
   */
  async getApiAuditRetentionSimulationForGetApiAuditRetentionSimulation(query?: {
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditAuditRetentionSimulationSummary[], ApiError>> {
    const url = '/api/audit/retention-simulation';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditAuditRetentionSimulationSummary[], ApiError>;
  }

  /**
   */
  async postApiAuditRetentionSimulation(
    body: Types.ComplianceAuditRunAuditRetentionSimulationInput,
  ): Promise<Result<Types.ComplianceAuditAuditRetentionSimulationOutput, ApiError>> {
    const url = '/api/audit/retention-simulation';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditRunAuditRetentionSimulationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditRetentionSimulationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiAuditRetentionSimulationForGetApiAuditRetentionSimulationById(
    id: string,
  ): Promise<Result<Types.ComplianceAuditAuditRetentionSimulationOutput, ApiError>> {
    const url = `/api/audit/retention-simulation/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditRetentionSimulationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Gets the tenant retention policy configuration. With includeInherited, a tenant
   * without an explicit configuration receives the platform baseline template the policy tree inherits from.
   */
  async getApiAuditRetentionSimulationConfiguration(query?: {
    includeInherited?: boolean;
  }): Promise<Result<Types.ComplianceAuditAuditRetentionConfigurationOutput, ApiError>> {
    const url = '/api/audit/retention-simulation/configuration';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditRetentionConfigurationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putApiAuditRetentionSimulationConfiguration(
    body: Types.ComplianceAuditConfigureAuditRetentionInput,
  ): Promise<Result<Types.ComplianceAuditAuditRetentionConfigurationOutput, ApiError>> {
    const url = '/api/audit/retention-simulation/configuration';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditConfigureAuditRetentionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditRetentionConfigurationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lists the pre-built retention policy templates (platform baseline plus SOC 2, ISO 27001, GDPR,
   * HIPAA, PCI DSS and FedRAMP presets) with their inheritance chain and sensitivity retention floors.
   */
  async getApiAuditRetentionSimulationTemplates(): Promise<Result<Types.ComplianceAuditAuditRetentionPolicyTemplate[], ApiError>> {
    const url = '/api/audit/retention-simulation/templates';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditAuditRetentionPolicyTemplate[], ApiError>;
  }

  /**
   */
  async getAuditRetentionPoliciesForGetAuditRetentionPolicies(query?: {
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditAuditRetentionSimulationSummary[], ApiError>> {
    const url = '/v1/audit/retention-policies';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditAuditRetentionSimulationSummary[], ApiError>;
  }

  /**
   */
  async postAuditRetentionPolicies(
    body: Types.ComplianceAuditRunAuditRetentionSimulationInput,
  ): Promise<Result<Types.ComplianceAuditAuditRetentionSimulationOutput, ApiError>> {
    const url = '/v1/audit/retention-policies';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditRunAuditRetentionSimulationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditRetentionSimulationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAuditRetentionPoliciesForGetAuditRetentionPoliciesById(id: string): Promise<Result<Types.ComplianceAuditAuditRetentionSimulationOutput, ApiError>> {
    const url = `/v1/audit/retention-policies/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditRetentionSimulationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Gets the tenant retention policy configuration. With includeInherited, a tenant
   * without an explicit configuration receives the platform baseline template the policy tree inherits from.
   */
  async getAuditRetentionPoliciesConfiguration(query?: {
    includeInherited?: boolean;
  }): Promise<Result<Types.ComplianceAuditAuditRetentionConfigurationOutput, ApiError>> {
    const url = '/v1/audit/retention-policies/configuration';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditRetentionConfigurationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putAuditRetentionPoliciesConfiguration(
    body: Types.ComplianceAuditConfigureAuditRetentionInput,
  ): Promise<Result<Types.ComplianceAuditAuditRetentionConfigurationOutput, ApiError>> {
    const url = '/v1/audit/retention-policies/configuration';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditConfigureAuditRetentionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditRetentionConfigurationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lists the pre-built retention policy templates (platform baseline plus SOC 2, ISO 27001, GDPR,
   * HIPAA, PCI DSS and FedRAMP presets) with their inheritance chain and sensitivity retention floors.
   */
  async getAuditRetentionPoliciesTemplates(): Promise<Result<Types.ComplianceAuditAuditRetentionPolicyTemplate[], ApiError>> {
    const url = '/v1/audit/retention-policies/templates';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditAuditRetentionPolicyTemplate[], ApiError>;
  }

  /**
   */
  async getAuditRetentionSimulationForGetAuditRetentionSimulation(query?: {
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditAuditRetentionSimulationSummary[], ApiError>> {
    const url = '/v1/audit/retention-simulation';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditAuditRetentionSimulationSummary[], ApiError>;
  }

  /**
   */
  async postAuditRetentionSimulation(
    body: Types.ComplianceAuditRunAuditRetentionSimulationInput,
  ): Promise<Result<Types.ComplianceAuditAuditRetentionSimulationOutput, ApiError>> {
    const url = '/v1/audit/retention-simulation';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditRunAuditRetentionSimulationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditRetentionSimulationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAuditRetentionSimulationForGetAuditRetentionSimulationById(
    id: string,
  ): Promise<Result<Types.ComplianceAuditAuditRetentionSimulationOutput, ApiError>> {
    const url = `/v1/audit/retention-simulation/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditRetentionSimulationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Gets the tenant retention policy configuration. With includeInherited, a tenant
   * without an explicit configuration receives the platform baseline template the policy tree inherits from.
   */
  async getAuditRetentionSimulationConfiguration(query?: {
    includeInherited?: boolean;
  }): Promise<Result<Types.ComplianceAuditAuditRetentionConfigurationOutput, ApiError>> {
    const url = '/v1/audit/retention-simulation/configuration';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditRetentionConfigurationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putAuditRetentionSimulationConfiguration(
    body: Types.ComplianceAuditConfigureAuditRetentionInput,
  ): Promise<Result<Types.ComplianceAuditAuditRetentionConfigurationOutput, ApiError>> {
    const url = '/v1/audit/retention-simulation/configuration';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditConfigureAuditRetentionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditRetentionConfigurationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lists the pre-built retention policy templates (platform baseline plus SOC 2, ISO 27001, GDPR,
   * HIPAA, PCI DSS and FedRAMP presets) with their inheritance chain and sensitivity retention floors.
   */
  async getAuditRetentionSimulationTemplates(): Promise<Result<Types.ComplianceAuditAuditRetentionPolicyTemplate[], ApiError>> {
    const url = '/v1/audit/retention-simulation/templates';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditAuditRetentionPolicyTemplate[], ApiError>;
  }
}

export function createComplianceAuditRetentionSimulationModule(client: ApiClient): ComplianceAuditRetentionSimulationModule {
  return new ComplianceAuditRetentionSimulationModule(client);
}
