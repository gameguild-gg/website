/**
 * @game-guild/client - UsersResourcesSettings Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class UsersResourcesSettingsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get all setting overrides for a user
   *
   * Retrieves all resource setting overrides for a specific user.
   */
  async getUsersResourcesSettingsForGetUsersByUserIdResourcesSettings(userId: string): Promise<Result<Types.ResourcesResourceSettings[], ApiError>> {
    const url = `/v1/users/${userId}/resources/settings`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ResourcesResourceSettings[], ApiError>;
  }

  /**
   * Get a specific setting override by key for a user
   *
   * Retrieves a specific resource setting override by its key for a user.
   */
  async getUsersResourcesSettingsForGetUsersByUserIdResourcesSettingsByKey(
    userId: string,
    key: string,
  ): Promise<Result<Types.ResourcesResourceSettings, ApiError>> {
    const url = `/v1/users/${userId}/resources/settings/${key}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesResourceSettingsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Create or update a setting override for a user
   *
   * Creates a new setting override or updates an existing one for a user.
   */
  async putUsersResourcesSettings(
    userId: string,
    key: string,
    body: Types.ResourcesSetUserResourceSettingsInput,
  ): Promise<Result<Types.ResourcesResourceSettings, ApiError>> {
    const url = `/v1/users/${userId}/resources/settings/${key}`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesSetUserResourceSettingsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesResourceSettingsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createUsersResourcesSettingsModule(client: ApiClient): UsersResourcesSettingsModule {
  return new UsersResourcesSettingsModule(client);
}
