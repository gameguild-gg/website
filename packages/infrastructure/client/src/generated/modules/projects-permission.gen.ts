/**
 * @game-guild/client - ProjectsPermission Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ProjectsPermissionModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Share project with multiple users using a role template
   */
  async postProjectsPermissionsShareWithRole(
    projectId: string,
    body: Types.ProjectsShareProjectWithRoleInput,
  ): Promise<Result<Types.ProjectsShareResult, ApiError>> {
    const url = `/v1/projects/${projectId}/permissions/:share-with-role`;

    // Validate request body
    const validatedBody = safeParse(Types.ProjectsShareProjectWithRoleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ProjectsShareResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get all collaborators on the project
   */
  async getProjectsPermissionsCollaborators(projectId: string): Promise<Result<Types.ProjectsProjectCollaboratorDto[], ApiError>> {
    const url = `/v1/projects/${projectId}/permissions/collaborators`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ProjectsProjectCollaboratorDto[], ApiError>;
  }

  /**
   * Add a collaborator to the project
   */
  async postProjectsPermissionsCollaborators(
    projectId: string,
    body: Types.ProjectsAddCollaboratorInput,
  ): Promise<Result<Types.ProjectsInvitationResult, ApiError>> {
    const url = `/v1/projects/${projectId}/permissions/collaborators`;

    // Validate request body
    const validatedBody = safeParse(Types.ProjectsAddCollaboratorInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ProjectsInvitationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update collaborator permissions
   */
  async putProjectsPermissionsCollaborators(
    projectId: string,
    collaboratorUserId: string,
    body: Types.ProjectsUpdateCollaboratorInput,
  ): Promise<Result<Types.ProjectsPermissionUpdateResult, ApiError>> {
    const url = `/v1/projects/${projectId}/permissions/collaborators/${collaboratorUserId}`;

    // Validate request body
    const validatedBody = safeParse(Types.ProjectsUpdateCollaboratorInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ProjectsPermissionUpdateResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Remove a collaborator from the project
   */
  async deleteProjectsPermissionsCollaborators(projectId: string, collaboratorUserId: string): Promise<Result<Types.ProjectsPermissionUpdateResult, ApiError>> {
    const url = `/v1/projects/${projectId}/permissions/collaborators/${collaboratorUserId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ProjectsPermissionUpdateResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get current user's permissions on the project
   */
  async getProjectsPermissionsMyPermissions(projectId: string): Promise<Result<Types.ProjectsEffectivePermission[], ApiError>> {
    const url = `/v1/projects/${projectId}/permissions/my-permissions`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ProjectsEffectivePermission[], ApiError>;
  }

  /**
   * Get project permission templates for common roles
   */
  async getProjectsPermissionsRoleTemplates(projectId: string): Promise<Result<Types.ProjectsProjectRoleTemplate[], ApiError>> {
    const url = `/v1/projects/${projectId}/permissions/role-templates`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ProjectsProjectRoleTemplate[], ApiError>;
  }
}

export function createProjectsPermissionModule(client: ApiClient): ProjectsPermissionModule {
  return new ProjectsPermissionModule(client);
}
