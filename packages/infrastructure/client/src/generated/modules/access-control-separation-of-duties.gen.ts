/**
 * @game-guild/client - AccessControlSeparationOfDuties Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AccessControlSeparationOfDutiesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get all SoD rules for a tenant
   */
  async getSodRulesForGetSodRules(query?: { tenantId?: string; activeOnly?: boolean }): Promise<Result<Types.IdentityAuthorizationSoDRule[], ApiError>> {
    const url = '/v1/sod/rules';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityAuthorizationSoDRule[], ApiError>;
  }

  /**
   * Create a new SoD rule
   */
  async postSodRules(body: Types.IdentityAuthorizationCommandsCreateSoDRuleCommand): Promise<Result<Types.IdentityAuthorizationSoDRule, ApiError>> {
    const url = '/v1/sod/rules';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationCommandsCreateSoDRuleCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationSoDRuleSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get a SoD rule by ID
   */
  async getSodRulesForGetSodRulesById(id: string): Promise<Result<Types.IdentityAuthorizationSoDRule, ApiError>> {
    const url = `/v1/sod/rules/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationSoDRuleSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update an existing SoD rule
   */
  async putSodRules(id: string, body: Types.IdentityAuthorizationControllersUpdateSoDRuleInput): Promise<Result<Types.IdentityAuthorizationSoDRule, ApiError>> {
    const url = `/v1/sod/rules/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationControllersUpdateSoDRuleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationSoDRuleSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Delete a SoD rule
   */
  async deleteSodRules(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/sod/rules/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Scan for SoD violations (admin only)
   */
  async postSodViolationsScan(query?: { tenantId?: string }): Promise<Result<number, ApiError>> {
    const url = '/v1/sod/violations:scan';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<number, ApiError>;
  }

  /**
   * Grant an exception for a SoD violation
   */
  async postSodViolationsException(
    id: string,
    body: Types.IdentityAuthorizationControllersGrantExceptionInput,
  ): Promise<Result<Types.IdentityAuthorizationSoDViolation, ApiError>> {
    const url = `/v1/sod/violations/${id}:exception`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationControllersGrantExceptionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationSoDViolationSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Resolve a SoD violation
   */
  async postSodViolationsResolve(
    id: string,
    body: Types.IdentityAuthorizationControllersResolveViolationInput,
  ): Promise<Result<Types.IdentityAuthorizationSoDViolation, ApiError>> {
    const url = `/v1/sod/violations/${id}:resolve`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationControllersResolveViolationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationSoDViolationSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get active SoD violations
   */
  async getSodViolationsActive(query?: { tenantId?: string }): Promise<Result<Types.IdentityAuthorizationSoDViolation[], ApiError>> {
    const url = '/v1/sod/violations/active';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityAuthorizationSoDViolation[], ApiError>;
  }

  /**
   * Detect SoD violations for a user
   */
  async getSodViolationsDetect(userId: string, query?: { tenantId?: string }): Promise<Result<Types.IdentityAuthorizationSoDViolation[], ApiError>> {
    const url = `/v1/sod/violations/detect/${userId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityAuthorizationSoDViolation[], ApiError>;
  }

  /**
   * Get SoD violations for a user
   */
  async getSodViolationsUser(userId: string, query?: { tenantId?: string }): Promise<Result<Types.IdentityAuthorizationSoDViolation[], ApiError>> {
    const url = `/v1/sod/violations/user/${userId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityAuthorizationSoDViolation[], ApiError>;
  }
}

export function createAccessControlSeparationOfDutiesModule(client: ApiClient): AccessControlSeparationOfDutiesModule {
  return new AccessControlSeparationOfDutiesModule(client);
}
