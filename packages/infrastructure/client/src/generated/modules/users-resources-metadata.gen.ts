/**
 * @game-guild/client - UsersResourcesMetadata Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class UsersResourcesMetadataModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get all metadata entries for a user
   *
   * Retrieves all resource metadata entries for a specific user.
   */
  async getUsersResourcesMetadataForGetUsersByUserIdResourcesMetadata(userId: string): Promise<Result<Types.ResourcesResourceMetadata[], ApiError>> {
    const url = `/v1/users/${userId}/resources/metadata`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ResourcesResourceMetadata[], ApiError>;
  }

  /**
   * Get a specific metadata entry by key for a user
   *
   * Retrieves a specific resource metadata entry by its key for a user.
   */
  async getUsersResourcesMetadataForGetUsersByUserIdResourcesMetadataByKey(
    userId: string,
    key: string,
  ): Promise<Result<Types.ResourcesResourceMetadata, ApiError>> {
    const url = `/v1/users/${userId}/resources/metadata/${key}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesResourceMetadataSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Create or update a metadata entry for a user
   *
   * Creates a new metadata entry or updates an existing one for a user.
   */
  async putUsersResourcesMetadata(
    userId: string,
    key: string,
    body: Types.ResourcesSetResourceMetadataInput,
  ): Promise<Result<Types.ResourcesResourceMetadata, ApiError>> {
    const url = `/v1/users/${userId}/resources/metadata/${key}`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesSetResourceMetadataInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesResourceMetadataSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createUsersResourcesMetadataModule(client: ApiClient): UsersResourcesMetadataModule {
  return new UsersResourcesMetadataModule(client);
}
