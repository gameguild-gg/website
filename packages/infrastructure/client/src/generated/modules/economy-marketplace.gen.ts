/**
 * @game-guild/client - EconomyMarketplace Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class EconomyMarketplaceModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async postEconomyMarketplaceOrdersSettle(
    orderId: string,
    body: Types.APIControllersSettleMyMarketplaceOrderInput,
  ): Promise<Result<Types.FinanceEconomyMarketplaceDurableMarketplaceSettlementResult, ApiError>> {
    const url = `/api/v1/economy/marketplace/orders/${orderId}:settle`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersSettleMyMarketplaceOrderInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyMarketplaceDurableMarketplaceSettlementResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postEconomyMarketplaceSettlementsRefund(
    settlementId: string,
    body: Types.APIControllersRefundMarketplaceSettlementInput,
  ): Promise<Result<Types.FinanceEconomyMarketplaceDurableMarketplaceRefundResult, ApiError>> {
    const url = `/api/v1/economy/marketplace/settlements/${settlementId}:refund`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersRefundMarketplaceSettlementInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyMarketplaceDurableMarketplaceRefundResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createEconomyMarketplaceModule(client: ApiClient): EconomyMarketplaceModule {
  return new EconomyMarketplaceModule(client);
}
