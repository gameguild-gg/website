/**
 * @game-guild/client - AuthTrustedDevices Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AuthTrustedDevicesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get trusted devices for the current user
   *
   * Retrieves a list of devices that have been marked as trusted for the current user.
   */
  async getAuthTrustedDevices(): Promise<Result<Types.IdentityAuthenticationTrustedDeviceOutput[], ApiError>> {
    const url = '/v1/auth/trusted-devices';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityAuthenticationTrustedDeviceOutput[], ApiError>;
  }

  /**
   * Trust the current device
   *
   * Marks the current device as trusted, allowing faster authentication in the future.
   */
  async postAuthTrustedDevices(
    body: Types.IdentityAuthenticationTrustDeviceInput,
  ): Promise<Result<Types.IdentityAuthenticationSessionSuccessOutput, ApiError>> {
    const url = '/v1/auth/trusted-devices';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationTrustDeviceInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSessionSuccessOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Revoke trust for a specific device
   *
   * Removes a device from the trusted devices list.
   */
  async deleteAuthTrustedDevices(deviceId: string): Promise<Result<Types.IdentityAuthenticationSessionSuccessOutput, ApiError>> {
    const url = `/v1/auth/trusted-devices/${deviceId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSessionSuccessOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAuthTrustedDevicesModule(client: ApiClient): AuthTrustedDevicesModule {
  return new AuthTrustedDevicesModule(client);
}
