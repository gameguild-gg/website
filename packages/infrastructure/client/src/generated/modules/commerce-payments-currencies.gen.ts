/**
 * @game-guild/client - CommercePaymentsCurrencies Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommercePaymentsCurrenciesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Lists every catalog currency annotated with the tenant's enablement, base and display
   * preferences plus catalog metadata (symbol, English name, minor-unit exponent) for
   * selector UIs. Enabled currencies come first.
   *
   * Returns all ISO 4217 catalog currencies with the tenant's enablement, base-currency marker and display preferences.
   */
  async getCurrencies(): Promise<Result<Types.CommercePaymentsTenantCurrencyDto[], ApiError>> {
    const url = '/api/v1/currencies';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.CommercePaymentsTenantCurrencyDto[], ApiError>;
  }

  /**
   * Disables a currency. Rejects the base currency outright; a currency still held by
   * active subscriptions requires `confirmActiveSubscriptionImpact=true`.
   *
   * Disables an ISO 4217 currency for the tenant. The base currency is always rejected; currencies held by active subscriptions need explicit confirmation.
   */
  async postCurrenciesDisable(currencyCode: string, body: Types.CommercePaymentsDisableCurrencyInput): Promise<Result<void, ApiError>> {
    const url = `/api/v1/currencies/${currencyCode}/disable`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsDisableCurrencyInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Enables a currency for the tenant (creates the configuration row on first use).
   *
   * Enables an ISO 4217 currency for the tenant. Idempotent. Audit-logged.
   */
  async postCurrenciesEnable(currencyCode: string, body: Types.CommercePaymentsCurrencyDisplayPreferencesInput): Promise<Result<void, ApiError>> {
    const url = `/api/v1/currencies/${currencyCode}/enable`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsCurrencyDisplayPreferencesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Sets the tenant's base (calculation/reporting) currency; enables it if needed and
   * clears the previous base marker.
   *
   * Makes the currency the tenant's base currency for calculation and reporting, clearing the previous marker. Audit-logged.
   */
  async postCurrenciesSetBase(currencyCode: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/currencies/${currencyCode}/set-base`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createCommercePaymentsCurrenciesModule(client: ApiClient): CommercePaymentsCurrenciesModule {
  return new CommercePaymentsCurrenciesModule(client);
}
