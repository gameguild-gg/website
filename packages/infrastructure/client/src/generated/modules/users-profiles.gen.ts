/**
 * @game-guild/client - UsersProfiles Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class UsersProfilesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get user profile by user ID
   */
  async getUsersProfile(userId: string): Promise<Result<Types.IdentityUsersUserProfileDto, ApiError>> {
    const url = `/v1/users/${userId}/profile`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersUserProfileDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Replace user profile (full update)
   */
  async putUsersProfile(userId: string, body: Types.IdentityUsersReplaceUserProfileInput): Promise<Result<Types.IdentityUsersUserProfileDto, ApiError>> {
    const url = `/v1/users/${userId}/profile`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersReplaceUserProfileInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersUserProfileDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update user profile (partial update)
   */
  async patchUsersProfile(userId: string, body: Types.IdentityUsersUpdateUserProfileInput): Promise<Result<Types.IdentityUsersUserProfileDto, ApiError>> {
    const url = `/v1/users/${userId}/profile`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersUpdateUserProfileInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersUserProfileDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Find all user profiles with pagination, search, and sorting
   */
  async getUsersProfiles(query?: {
    page?: number;
    pageSize?: number;
    search?: string;
    sortBy?: string;
    sortDirection?: string;
  }): Promise<Result<Types.PagedResultUserProfileDto, ApiError>> {
    const url = '/v1/users/profiles';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultUserProfileDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createUsersProfilesModule(client: ApiClient): UsersProfilesModule {
  return new UsersProfilesModule(client);
}
