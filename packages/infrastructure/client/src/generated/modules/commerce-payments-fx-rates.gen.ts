/**
 * @game-guild/client - CommercePaymentsFxRates Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommercePaymentsFxRatesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Lists persisted FX quotations (market snapshots and overrides) with optional pair and
   * effective-date-window filters for historical lookup.
   *
   * Persisted FX quotations with pair and effective-date filters, newest effective date first.
   */
  async getFxRates(query?: {
    baseCurrency?: string;
    quoteCurrency?: string;
    fromEffectiveDateUtc?: string;
    toEffectiveDateUtc?: string;
    limit?: number;
  }): Promise<Result<Types.CommercePaymentsFxRateDto[], ApiError>> {
    const url = '/api/v1/fx-rates';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.CommercePaymentsFxRateDto[], ApiError>;
  }

  /**
   * Sets or replaces an operator manual FX rate override for an exact direction
   * (audit-logged with reason; effective immediately).
   *
   * Persists an audited manual override for one exact currency pair direction; overrides win over synced market rates until replaced or expired.
   */
  async postFxRatesOverrides(body: Types.CommercePaymentsSetManualFxRateOverrideInput): Promise<Result<void, ApiError>> {
    const url = '/api/v1/fx-rates/overrides';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsSetManualFxRateOverrideInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Triggers one FX rate sync pass immediately (same code path as the scheduled worker).
   * Rejected while no provider is configured.
   *
   * Runs one FX sync pass now through the primary (or fallback) provider and persists the rate snapshot.
   */
  async postFxRatesSync(): Promise<Result<Types.CommercePaymentsFxRateSyncResult, ApiError>> {
    const url = '/api/v1/fx-rates/sync';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsFxRateSyncResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * FX sync status: recent runs, consecutive failures, staleness assessment and active
   * grid/override counts.
   *
   * Recent FX sync runs with the consecutive-failure counter and the staleness assessment of the newest persisted rates.
   */
  async getFxRatesSyncStatus(query?: { runLimit?: number }): Promise<Result<Types.CommercePaymentsFxSyncStatusDto, ApiError>> {
    const url = '/api/v1/fx-rates/sync-status';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsFxSyncStatusDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommercePaymentsFxRatesModule(client: ApiClient): CommercePaymentsFxRatesModule {
  return new CommercePaymentsFxRatesModule(client);
}
