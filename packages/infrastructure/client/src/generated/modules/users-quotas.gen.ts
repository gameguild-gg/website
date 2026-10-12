/**
 * @game-guild/client - UsersQuotas Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class UsersQuotasModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get all quotas for a user
   *
   * Retrieves all configured resource quotas for a specific user.
   */
  async getUsersQuotasForGetUsersByUserIdQuotas(userId: string): Promise<Result<Types.ResourcesResourceQuotaOutput[], ApiError>> {
    const url = `/v1/users/${userId}/quotas`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ResourcesResourceQuotaOutput[], ApiError>;
  }

  /**
   * Get specific quota for a resource type
   *
   * Retrieves the quota configuration for a specific resource type for a user.
   */
  async getUsersQuotasForGetUsersByUserIdQuotasByType(
    userId: string,
    type: Types.ResourcesResourceUsageType,
  ): Promise<Result<Types.ResourcesResourceQuotaOutput, ApiError>> {
    const url = `/v1/users/${userId}/quotas/${type}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesResourceQuotaOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Set or update a quota for a resource type
   *
   * Creates or updates the quota configuration for a specific resource type for a user.
   */
  async putUsersQuotas(userId: string, type: Types.ResourcesResourceUsageType, body: Types.ResourcesSetQuotaInput): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/quotas/${type}`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesSetQuotaInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Delete a quota for a resource type
   *
   * Removes the quota configuration for a specific resource type for a user.
   */
  async deleteUsersQuotas(userId: string, type: Types.ResourcesResourceUsageType): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/quotas/${type}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Check if a usage amount would exceed quota
   *
   * Validates whether a proposed usage amount would exceed the configured quota limits without recording any usage.
   */
  async postUsersQuotasCheck(
    userId: string,
    type: Types.ResourcesResourceUsageType,
    body: Types.ResourcesCheckResourceQuotaInput,
  ): Promise<Result<Types.ResourcesResourceQuotaEnforcementResult, ApiError>> {
    const url = `/v1/users/${userId}/quotas/${type}:check`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesCheckResourceQuotaInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesResourceQuotaEnforcementResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Reset quota usage to zero
   *
   * Resets the current usage counter for a specific resource quota to zero without changing the quota limits.
   */
  async postUsersQuotasReset(userId: string, type: Types.ResourcesResourceUsageType): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/quotas/${type}:reset`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Toggle quota activation status
   *
   * Activates or deactivates a resource quota. Inactive quotas are not enforced.
   */
  async postUsersQuotasToggle(
    userId: string,
    type: Types.ResourcesResourceUsageType,
    body: Types.ResourcesToggleResourceQuotaInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/quotas/${type}:toggle`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesToggleResourceQuotaInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createUsersQuotasModule(client: ApiClient): UsersQuotasModule {
  return new UsersQuotasModule(client);
}
