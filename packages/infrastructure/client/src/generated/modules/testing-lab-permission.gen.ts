/**
 * @game-guild/client - TestingLabPermission Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class TestingLabPermissionModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get all TestingLab role templates
   */
  async getApiTestingLabPermissionsRoleTemplates(): Promise<Result<Types.TestingLabTestingLabRoleTemplate[], ApiError>> {
    const url = '/api/testing-lab/permissions/role-templates';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.TestingLabTestingLabRoleTemplate[], ApiError>;
  }

  /**
   * Create a new TestingLab role template
   */
  async postApiTestingLabPermissionsRoleTemplates(
    body: Types.TestingLabCreateTestingLabRoleInput,
  ): Promise<Result<Types.TestingLabTestingLabRoleTemplate, ApiError>> {
    const url = '/api/testing-lab/permissions/role-templates';

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabCreateTestingLabRoleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingLabRoleTemplateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update an existing TestingLab role template
   */
  async putApiTestingLabPermissionsRoleTemplates(
    idOrName: string,
    body: Types.TestingLabUpdateTestingLabRoleInput,
  ): Promise<Result<Types.TestingLabTestingLabRoleTemplate, ApiError>> {
    const url = `/api/testing-lab/permissions/role-templates/${idOrName}`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabUpdateTestingLabRoleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingLabRoleTemplateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Delete a TestingLab role template
   */
  async deleteApiTestingLabPermissionsRoleTemplates(idOrName: string): Promise<Result<void, ApiError>> {
    const url = `/api/testing-lab/permissions/role-templates/${idOrName}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Delete a TestingLab role template by name (legacy compatibility for clients that don't yet have Ids)
   */
  async deleteApiTestingLabPermissionsRoleTemplatesByName(name: string): Promise<Result<void, ApiError>> {
    const url = `/api/testing-lab/permissions/role-templates/by-name/${name}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get TestingLab permissions for a specific user
   */
  async getApiTestingLabPermissionsUsers(userId: string, query?: { tenantId?: string }): Promise<Result<Types.TestingLabUserTestingLabPermissions, ApiError>> {
    const url = `/api/testing-lab/permissions/users/${userId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabUserTestingLabPermissionsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Check if a user can perform an action on a TestingLab resource
   */
  async getApiTestingLabPermissionsUsersCheck(
    userId: string,
    resourceType: string,
    query?: { action?: string; resourceId?: string; tenantId?: string },
  ): Promise<Result<boolean, ApiError>> {
    const url = `/api/testing-lab/permissions/users/${userId}/check/${resourceType}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<boolean, ApiError>;
  }

  /**
   * Grant permission to a specific TestingLab resource
   */
  async postApiTestingLabPermissionsUsersResources(
    userId: string,
    resourceType: string,
    resourceId: string,
    body: Types.TestingLabGrantResourcePermissionInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/testing-lab/permissions/users/${userId}/resources/${resourceType}/${resourceId}`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabGrantResourcePermissionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Revoke permission from a specific TestingLab resource
   */
  async deleteApiTestingLabPermissionsUsersResources(
    userId: string,
    resourceType: string,
    resourceId: string,
    query?: { action?: string; tenantId?: string },
  ): Promise<Result<void, ApiError>> {
    const url = `/api/testing-lab/permissions/users/${userId}/resources/${resourceType}/${resourceId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Assign a TestingLab role to a user
   */
  async postApiTestingLabPermissionsUsersRoles(userId: string, body: Types.TestingLabAssignTestingLabRoleInput): Promise<Result<void, ApiError>> {
    const url = `/api/testing-lab/permissions/users/${userId}/roles`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabAssignTestingLabRoleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Revoke a TestingLab role from a user
   */
  async deleteApiTestingLabPermissionsUsersRoles(userId: string, roleName: string, query?: { tenantId?: string }): Promise<Result<void, ApiError>> {
    const url = `/api/testing-lab/permissions/users/${userId}/roles/${roleName}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createTestingLabPermissionModule(client: ApiClient): TestingLabPermissionModule {
  return new TestingLabPermissionModule(client);
}
