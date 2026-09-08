/**
 * @game-guild/client - EconomyIntegrations Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class EconomyIntegrationsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async postIntegrationsEconomyStripeConnectWebhook(): Promise<Result<Types.APIControllersEconomyPayoutExecutionOperationDto, ApiError>> {
    const url = '/api/v1/integrations/economy/stripe-connect/webhook';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIControllersEconomyPayoutExecutionOperationDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postIntegrationsEconomySumsubWebhook(): Promise<Result<Types.ComplianceKYCSumSubWebhookIngestionResult, ApiError>> {
    const url = '/api/v1/integrations/economy/sumsub/webhook';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceKYCSumSubWebhookIngestionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createEconomyIntegrationsModule(client: ApiClient): EconomyIntegrationsModule {
  return new EconomyIntegrationsModule(client);
}
