/**
 * @game-guild/client - UsersEntitlements Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class UsersEntitlementsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get entitlements for a specific user (admin only)
   */
  async getUsersEntitlements(userId: string): Promise<Result<Types.CommerceProductsEntitlementInfoDto[], ApiError>> {
    const url = `/v1/users/${userId}/entitlements`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.CommerceProductsEntitlementInfoDto[], ApiError>;
  }

  /**
   * Get current user's entitlements
   */
  async getUsersMeEntitlements(): Promise<Result<Types.CommerceProductsEntitlementInfoDto[], ApiError>> {
    const url = '/v1/users/me/entitlements';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.CommerceProductsEntitlementInfoDto[], ApiError>;
  }
}

export function createUsersEntitlementsModule(client: ApiClient): UsersEntitlementsModule {
  return new UsersEntitlementsModule(client);
}
