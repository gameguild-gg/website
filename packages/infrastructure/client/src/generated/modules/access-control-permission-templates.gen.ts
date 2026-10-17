/**
 * @game-guild/client - AccessControlPermissionTemplates Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AccessControlPermissionTemplatesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Lists permission templates with their role keys and current version.
   */
  async getAuthorizationPermissionTemplates(query?: {
    includeInactive?: boolean;
    category?: string;
  }): Promise<Result<Types.IdentityAuthorizationPermissionTemplateSummary[], ApiError>> {
    const url = '/api/v1/authorization/permission-templates';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityAuthorizationPermissionTemplateSummary[], ApiError>;
  }

  /**
   * Creates a permission template. The initial permission/role set is snapshotted
   * as version 1 and the creation is audited.
   */
  async postAuthorizationPermissionTemplates(
    body: Types.IdentityAuthorizationCreatePermissionTemplateCommand,
  ): Promise<Result<Types.IdentityAuthorizationPermissionTemplateMutationResult, ApiError>> {
    const url = '/api/v1/authorization/permission-templates';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationCreatePermissionTemplateCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationPermissionTemplateMutationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Updates a permission template. Every accepted update appends a new version
   * snapshot with the computed diff and bumps the security version of every
   * tenant holding an active instantiation.
   */
  async putAuthorizationPermissionTemplates(
    templateId: string,
    body: Types.IdentityAuthorizationUpdatePermissionTemplateCommand,
  ): Promise<Result<Types.IdentityAuthorizationPermissionTemplateMutationResult, ApiError>> {
    const url = `/api/v1/authorization/permission-templates/${templateId}`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationUpdatePermissionTemplateCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationPermissionTemplateMutationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Deletes a permission template. Templates with active instantiations are
   * pinned (fail closed) and must be migrated away first.
   */
  async deleteAuthorizationPermissionTemplates(templateId: string, query?: { reason?: string }): Promise<Result<void, ApiError>> {
    const url = `/api/v1/authorization/permission-templates/${templateId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Instantiates a template into a tenant: grants the template permissions to the
   * target users and assigns the template's roles (role templates). Use
   * `dryRun` for the preview-only form.
   */
  async postAuthorizationPermissionTemplatesInstantiate(
    templateId: string,
    body: Types.IdentityAuthorizationInstantiateTemplateInput,
  ): Promise<Result<Types.IdentityAuthorizationPermissionTemplateInstantiationResult, ApiError>> {
    const url = `/api/v1/authorization/permission-templates/${templateId}:instantiate`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationInstantiateTemplateInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationPermissionTemplateInstantiationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   *     Dry-run preview of instantiating a template into a tenant: per-target
   *     permission grants and role assignments that would apply, without mutating
   * anything.
   */
  async postAuthorizationPermissionTemplatesInstantiatePreview(
    templateId: string,
    body: Types.IdentityAuthorizationPreviewInstantiationInput,
  ): Promise<Result<Types.IdentityAuthorizationPermissionTemplateInstantiationResult, ApiError>> {
    const url = `/api/v1/authorization/permission-templates/${templateId}:instantiate-preview`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationPreviewInstantiationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationPermissionTemplateInstantiationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Migrates a template's instantiated targets to a target version (issue #370):
   * applies the version delta across the tenants that instantiated the template,
   * batched, with durable per-batch progress. Supports dry-run.
   */
  async postAuthorizationPermissionTemplatesMigrations(
    templateId: string,
    body: Types.IdentityAuthorizationMigrateTemplateInput,
  ): Promise<Result<Types.IdentityAuthorizationPermissionTemplateMigrationResult, ApiError>> {
    const url = `/api/v1/authorization/permission-templates/${templateId}/migrations`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationMigrateTemplateInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationPermissionTemplateMigrationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Reads the durable progress of one migration (issue #370).
   */
  async getAuthorizationPermissionTemplatesMigrations(
    templateId: string,
    migrationId: string,
  ): Promise<Result<Types.IdentityAuthorizationPermissionTemplateMigrationResult, ApiError>> {
    const url = `/api/v1/authorization/permission-templates/${templateId}/migrations/${migrationId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationPermissionTemplateMigrationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Rolls back a completed template migration (issue #370): restores every
   * affected target to its recorded pre-migration state.
   */
  async postAuthorizationPermissionTemplatesMigrationsRollback(
    templateId: string,
    migrationId: string,
    body: Types.IdentityAuthorizationRollbackMigrationInput,
  ): Promise<Result<Types.IdentityAuthorizationPermissionTemplateMigrationResult, ApiError>> {
    const url = `/api/v1/authorization/permission-templates/${templateId}/migrations/${migrationId}:rollback`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationRollbackMigrationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationPermissionTemplateMigrationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lists the append-only version history of a template (issue #370), newest first.
   */
  async getAuthorizationPermissionTemplatesVersions(
    templateId: string,
  ): Promise<Result<Types.IdentityAuthorizationPermissionTemplateVersionSummary[], ApiError>> {
    const url = `/api/v1/authorization/permission-templates/${templateId}/versions`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityAuthorizationPermissionTemplateVersionSummary[], ApiError>;
  }
}

export function createAccessControlPermissionTemplatesModule(client: ApiClient): AccessControlPermissionTemplatesModule {
  return new AccessControlPermissionTemplatesModule(client);
}
