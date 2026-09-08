/**
 * @game-guild/client - Tenants Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class TenantsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get tenants with pagination, search, and sorting
   *
   * Retrieves a paginated list of all tenant organizations accessible to the requesting user.
   */
  async getTenantsForGetTenants(query?: {
    page?: number;
    pageSize?: number;
    status?: string;
    searchTerm?: string;
  }): Promise<Result<Types.PagedResultTenant, ApiError>> {
    const url = '/v1/tenants';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultTenantSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Create a new tenant organization
   *
   * Creates a new tenant organization within the GameGuild platform.
   */
  async postTenants(body: Types.IdentityTenantsCreateTenantInput): Promise<Result<void, ApiError>> {
    const url = '/v1/tenants';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsCreateTenantInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Bulk activate tenant accounts
   *
   * Activates multiple tenant accounts at once.
   */
  async postTenantsActivateForPostTenantsActivate(body: Types.IdentityTenantsBulkActivateTenantsCommand): Promise<Result<Types.BulkOperationOutput, ApiError>> {
    const url = '/v1/tenants:activate';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsBulkActivateTenantsCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.BulkOperationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk archive tenant accounts
   *
   * Archives multiple tenant accounts at once.
   */
  async postTenantsArchiveForPostTenantsArchive(body: Types.IdentityTenantsBulkArchiveTenantsCommand): Promise<Result<Types.BulkOperationOutput, ApiError>> {
    const url = '/v1/tenants:archive';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsBulkArchiveTenantsCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.BulkOperationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk create tenants
   *
   * Creates multiple tenant organizations at once.
   */
  async postTenantsCreate(body: Types.IdentityTenantsBulkCreateTenantsCommand): Promise<Result<Types.BulkOperationOutput, ApiError>> {
    const url = '/v1/tenants:create';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsBulkCreateTenantsCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.BulkOperationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk deactivate tenant accounts
   *
   * Deactivates multiple tenant accounts at once.
   */
  async postTenantsDeactivateForPostTenantsDeactivate(
    body: Types.IdentityTenantsBulkDeactivateTenantsCommand,
  ): Promise<Result<Types.BulkOperationOutput, ApiError>> {
    const url = '/v1/tenants:deactivate';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsBulkDeactivateTenantsCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.BulkOperationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk soft delete tenants
   *
   * Soft deletes multiple tenants at once.
   */
  async postTenantsDelete(body: Types.IdentityTenantsBulkDeleteTenantsCommand): Promise<Result<Types.BulkOperationOutput, ApiError>> {
    const url = '/v1/tenants:delete';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsBulkDeleteTenantsCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.BulkOperationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk hard delete tenants (irreversible purge)
   *
   * Permanently deletes multiple tenants. Admin operation requiring proper authorization.
   */
  async postTenantsPurgeForPostTenantsPurge(body: Types.IdentityTenantsBulkPurgeTenantsCommand): Promise<Result<Types.BulkOperationOutput, ApiError>> {
    const url = '/v1/tenants:purge';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsBulkPurgeTenantsCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.BulkOperationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk full update tenants
   *
   * Updates multiple tenants with complete data.
   */
  async postTenantsReplace(body: Types.IdentityTenantsBulkUpdateTenantsCommand): Promise<Result<Types.BulkOperationOutput, ApiError>> {
    const url = '/v1/tenants:replace';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsBulkUpdateTenantsCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.BulkOperationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk undelete soft-deleted tenants
   *
   * Restores multiple soft-deleted tenants at once.
   */
  async postTenantsUndeleteForPostTenantsUndelete(body: Types.IdentityTenantsBulkUndeleteTenantsCommand): Promise<Result<Types.BulkOperationOutput, ApiError>> {
    const url = '/v1/tenants:undelete';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsBulkUndeleteTenantsCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.BulkOperationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk partial update tenants
   *
   * Updates multiple tenants with partial data.
   */
  async postTenantsUpdate(body: Types.IdentityTenantsBulkUpdateTenantsCommand): Promise<Result<Types.BulkOperationOutput, ApiError>> {
    const url = '/v1/tenants:update';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsBulkUpdateTenantsCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.BulkOperationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Validate tenant data before creation
   *
   * Validates tenant data without creating. Returns errors, warnings, and suggestions.
   */
  async postTenantsValidate(body: Types.IdentityTenantsValidateTenantInput): Promise<Result<Types.IdentityTenantsTenantValidationOutput, ApiError>> {
    const url = '/v1/tenants:validate';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsValidateTenantInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityTenantsTenantValidationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get tenant by ID
   *
   * Retrieves detailed information for a specific tenant by their unique identifier.
   */
  async getTenantsForGetTenantsByTenantId(tenantId: string): Promise<Result<Types.IdentityTenantsTenant, ApiError>> {
    const url = `/v1/tenants/${tenantId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityTenantsTenantSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update tenant by ID
   *
   * Fully updates a tenant by ID with complete tenant data.
   */
  async putTenants(tenantId: string, body: Types.IdentityTenantsUpdateTenantInput): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsUpdateTenantInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Soft delete tenant by ID
   *
   * Soft deletes a tenant by ID (can be restored).
   */
  async deleteTenants(tenantId: string, body: Types.IdentityTenantsArchiveInput): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsArchiveInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Partially update tenant by ID
   *
   * Updates specific fields of a tenant by ID.
   */
  async patchTenants(tenantId: string, body: Types.IdentityTenantsUpdateTenantInput): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsUpdateTenantInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Check if tenant exists by ID
   *
   * Checks if a tenant exists by ID without returning the body.
   */
  async headTenants(tenantId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}`;

    const result = await this.client.request({
      method: 'HEAD',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Activate tenant account
   *
   * Activates a tenant organization by ID.
   */
  async postTenantsActivateForPostTenantsByTenantIdActivate(tenantId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}:activate`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Archive (soft delete) tenant account
   *
   * Archives a tenant organization by ID.
   */
  async postTenantsArchiveForPostTenantsByTenantIdArchive(tenantId: string, body: Types.IdentityTenantsArchiveInput): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}:archive`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsArchiveInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Deactivate tenant account
   *
   * Deactivates a tenant organization by ID.
   */
  async postTenantsDeactivateForPostTenantsByTenantIdDeactivate(tenantId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}:deactivate`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Permanently delete (hard delete) tenant account
   *
   * Permanently and irreversibly deletes a tenant organization. Admin operation requiring proper authorization.
   */
  async postTenantsPurgeForPostTenantsByTenantIdPurge(tenantId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}:purge`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Undelete a soft-deleted tenant account
   *
   * Undeletes a previously soft-deleted (archived) tenant organization.
   */
  async postTenantsUndeleteForPostTenantsByTenantIdUndelete(tenantId: string, body: Types.IdentityTenantsRecoverInput): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}:undelete`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsRecoverInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get tenant audit log
   *
   * Retrieves the audit log for a tenant showing all changes, actions, and who performed them.
   */
  async getTenantsAuditLog(
    tenantId: string,
    query?: { startDate?: string; endDate?: string; action?: string; actorId?: string; page?: number; pageSize?: number },
  ): Promise<Result<Types.PagedResultTenantAuditLogEntry, ApiError>> {
    const url = `/v1/tenants/${tenantId}/audit-log`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultTenantAuditLogEntrySchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get payment history for tenant
   *
   * Retrieves payment history for a specific tenant with optional date filtering.
   */
  async getTenantsPayments(
    tenantId: string,
    query?: { startDate?: string; endDate?: string },
  ): Promise<Result<Array<Types.CommercePaymentsPaymentResult>, ApiError>> {
    const url = `/v1/tenants/${tenantId}/payments`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.CommercePaymentsPaymentResult>, ApiError>;
  }
}

export function createTenantsModule(client: ApiClient): TenantsModule {
  return new TenantsModule(client);
}
