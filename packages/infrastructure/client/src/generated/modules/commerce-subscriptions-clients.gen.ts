/**
 * @game-guild/client - CommerceSubscriptionsClients Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceSubscriptionsClientsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List B2B client accounts
   *
   * Lists client accounts through the canonical tenant page query.
   */
  async getClients(query?: { page?: number; pageSize?: number; status?: string; searchTerm?: string }): Promise<Result<Types.PagedResultTenant, ApiError>> {
    const url = '/v1/clients';

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
   * Create a B2B client account
   *
   * Creates a client account using the canonical tenant creation workflow.
   */
  async postClients(body: Types.CommerceSubscriptionsCreateClientInput): Promise<Result<void, ApiError>> {
    const url = '/v1/clients';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsCreateClientInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get a B2B client account
   */
  async getClientById(clientId: string): Promise<Result<Types.IdentityTenantsTenant, ApiError>> {
    const url = `/v1/clients/${clientId}`;

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
   * Update a B2B client account
   */
  async putClients(clientId: string, body: Types.IdentityTenantsUpdateTenantInput): Promise<Result<void, ApiError>> {
    const url = `/v1/clients/${clientId}`;

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
   * Archive a B2B client account
   */
  async deleteClients(clientId: string, body: Types.IdentityTenantsArchiveInput): Promise<Result<void, ApiError>> {
    const url = `/v1/clients/${clientId}`;

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
   * List contracted modules for a B2B client
   *
   * Returns subscription-backed modules plus tenant feature flags for a client account.
   */
  async getClientsModules(
    clientId: string,
    query?: { page?: number; pageSize?: number; status?: Types.CommerceSubscriptionsSubscriptionStatus },
  ): Promise<Result<Types.CommerceSubscriptionsClientModulesOutput, ApiError>> {
    const url = `/v1/clients/${clientId}/modules`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceSubscriptionsClientModulesOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update contracted module toggles for a B2B client
   */
  async putClientsModules(clientId: string, body: Types.IdentityTenantsUpdateTenantFeatureFlagsInput): Promise<Result<void, ApiError>> {
    const url = `/v1/clients/${clientId}/modules`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsUpdateTenantFeatureFlagsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Update contracted module toggles for a B2B client
   */
  async patchClientsModules(clientId: string, body: Types.IdentityTenantsUpdateTenantFeatureFlagsInput): Promise<Result<void, ApiError>> {
    const url = `/v1/clients/${clientId}/modules`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsUpdateTenantFeatureFlagsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createCommerceSubscriptionsClientsModule(client: ApiClient): CommerceSubscriptionsClientsModule {
  return new CommerceSubscriptionsClientsModule(client);
}
