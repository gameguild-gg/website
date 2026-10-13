/**
 * @game-guild/client - IdentityProvisioningScim Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class IdentityProvisioningScimModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async postScimV2Bulk(body: Types.IdentityProvisioningScimScimBulkInput): Promise<Result<Types.IdentityProvisioningScimScimBulkOutput, ApiError>> {
    const url = '/scim/v2/Bulk';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityProvisioningScimScimBulkInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityProvisioningScimScimBulkOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lists provisioned groups with optional filter and 1-based pagination.
   */
  async getScimV2GroupsForGetScimV2Groups(query?: {
    filter?: string;
    startIndex?: string;
    count?: string;
  }): Promise<Result<Types.IdentityProvisioningScimScimListResponseScimGroupResource, ApiError>> {
    const url = '/scim/v2/Groups';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityProvisioningScimScimListResponseScimGroupResourceSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Creates a group. Idempotent on externalId (repeat POST returns 200).
   */
  async postScimV2Groups(body: Types.IdentityProvisioningScimScimGroupInput): Promise<Result<Types.IdentityProvisioningScimScimGroupResource, ApiError>> {
    const url = '/scim/v2/Groups';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityProvisioningScimScimGroupInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityProvisioningScimScimGroupResourceSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Fetches one provisioned group with its members.
   */
  async getScimV2GroupsForGetScimV2GroupsByRoleId(roleId: string): Promise<Result<Types.IdentityProvisioningScimScimGroupResource, ApiError>> {
    const url = `/scim/v2/Groups/${roleId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityProvisioningScimScimGroupResourceSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Replaces a group, including its full member list (RFC 7644 §3.5.1).
   */
  async putScimV2Groups(
    roleId: string,
    body: Types.IdentityProvisioningScimScimGroupInput,
  ): Promise<Result<Types.IdentityProvisioningScimScimGroupResource, ApiError>> {
    const url = `/scim/v2/Groups/${roleId}`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityProvisioningScimScimGroupInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityProvisioningScimScimGroupResourceSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Deletes a group: memberships are removed and the backing role deactivated.
   */
  async deleteScimV2Groups(roleId: string): Promise<Result<void, ApiError>> {
    const url = `/scim/v2/Groups/${roleId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Patches a group: displayName, externalId and the members paths
   * (`members`, `members[value eq "…"]`). Membership changes are
   * audited and bump the tenant security version.
   */
  async patchScimV2Groups(
    roleId: string,
    body: Types.IdentityProvisioningScimScimPatchInput,
  ): Promise<Result<Types.IdentityProvisioningScimScimGroupResource, ApiError>> {
    const url = `/scim/v2/Groups/${roleId}`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityProvisioningScimScimPatchInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityProvisioningScimScimGroupResourceSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getScimV2Resourcetypes(): Promise<Result<void, ApiError>> {
    const url = '/scim/v2/ResourceTypes';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getScimV2SchemasForGetScimV2Schemas(): Promise<Result<void, ApiError>> {
    const url = '/scim/v2/Schemas';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getScimV2SchemasForGetScimV2SchemasBySchemaId(schemaId: string): Promise<Result<void, ApiError>> {
    const url = `/scim/v2/Schemas/${schemaId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getScimV2Serviceproviderconfig(): Promise<Result<void, ApiError>> {
    const url = '/scim/v2/ServiceProviderConfig';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Lists provisioned users with optional filter and 1-based pagination.
   */
  async getScimV2UsersForGetScimV2Users(query?: {
    filter?: string;
    startIndex?: string;
    count?: string;
  }): Promise<Result<Types.IdentityProvisioningScimScimListResponseScimUserResource, ApiError>> {
    const url = '/scim/v2/Users';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityProvisioningScimScimListResponseScimUserResourceSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Creates a user. Idempotent on externalId: a repeated POST with the same
   * externalId returns the existing resource with 200 instead of creating a copy.
   */
  async postScimV2Users(body: Types.IdentityProvisioningScimScimUserInput): Promise<Result<Types.IdentityProvisioningScimScimUserResource, ApiError>> {
    const url = '/scim/v2/Users';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityProvisioningScimScimUserInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityProvisioningScimScimUserResourceSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Fetches one provisioned user by id.
   */
  async getScimV2UsersForGetScimV2UsersByUserId(userId: string): Promise<Result<Types.IdentityProvisioningScimScimUserResource, ApiError>> {
    const url = `/scim/v2/Users/${userId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityProvisioningScimScimUserResourceSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Replaces a provisioned user (RFC 7644 §3.5.1).
   */
  async putScimV2Users(
    userId: string,
    body: Types.IdentityProvisioningScimScimUserInput,
  ): Promise<Result<Types.IdentityProvisioningScimScimUserResource, ApiError>> {
    const url = `/scim/v2/Users/${userId}`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityProvisioningScimScimUserInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityProvisioningScimScimUserResourceSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Deprovisions a user: soft delete plus immediate session and token revocation.
   */
  async deleteScimV2Users(userId: string): Promise<Result<void, ApiError>> {
    const url = `/scim/v2/Users/${userId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Patches a provisioned user (RFC 7644 §3.5.2 add/remove/replace).
   */
  async patchScimV2Users(
    userId: string,
    body: Types.IdentityProvisioningScimScimPatchInput,
  ): Promise<Result<Types.IdentityProvisioningScimScimUserResource, ApiError>> {
    const url = `/scim/v2/Users/${userId}`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityProvisioningScimScimPatchInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityProvisioningScimScimUserResourceSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createIdentityProvisioningScimModule(client: ApiClient): IdentityProvisioningScimModule {
  return new IdentityProvisioningScimModule(client);
}
