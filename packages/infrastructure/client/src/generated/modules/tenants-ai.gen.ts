/**
 * @game-guild/client - TenantsAi Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class TenantsAiModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get tenant AI history
   *
   * Retrieves recent AI conversation history for a specific tenant.
   */
  async getTenantsAiHistory(tenantId: string, query?: { take?: number }): Promise<Result<Array<Types.AIAiConversationHistoryEntryDto>, ApiError>> {
    const url = `/v1/tenants/${tenantId}/ai/history`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.AIAiConversationHistoryEntryDto>, ApiError>;
  }

  /**
   * Export tenant AI history
   */
  async getTenantsAiHistoryExport(tenantId: string, query?: { format?: string; take?: number }): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/ai/history/export`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get tenant AI quotas
   */
  async getTenantsAiQuotas(tenantId: string): Promise<Result<Types.AIAiQuotaStatusOutput, ApiError>> {
    const url = `/v1/tenants/${tenantId}/ai/quotas`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AIAiQuotaStatusOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createTenantsAiModule(client: ApiClient): TenantsAiModule {
  return new TenantsAiModule(client);
}
