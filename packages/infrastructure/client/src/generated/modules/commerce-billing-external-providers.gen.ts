/**
 * @game-guild/client - CommerceBillingExternalProviders Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceBillingExternalProvidersModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List the configured external billing providers with health and enabled state
   *
   * Returns one entry per supported external billing provider (stripe, paypal, applepay,
   * apple_app_store, googlepay, google_play_store) with read-only configuration health
   * (credentials configured, configuration valid, webhook endpoint verification material
   * present) and the runtime enabled state (defaults to enabled; an explicit administrator
   * disable persists a management override).
   */
  async getBillingExternalProvidersForGetBillingExternalProviders(): Promise<Result<Types.CommerceBillingExternalBillingProviderStatusDto[], ApiError>> {
    const url = '/api/v1/billing/external-providers';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.CommerceBillingExternalBillingProviderStatusDto[], ApiError>;
  }

  /**
   * Get the health and enabled state of a single external billing provider
   *
   * Fails closed with 404 for unknown provider keys.
   */
  async getBillingExternalProvidersForGetBillingExternalProvidersByProviderKey(
    providerKey: string,
  ): Promise<Result<Types.CommerceBillingExternalBillingProviderStatusDto, ApiError>> {
    const url = `/api/v1/billing/external-providers/${providerKey}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceBillingExternalBillingProviderStatusDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Disable an external billing provider at runtime
   *
   * Persists an explicit disable decision (idempotent) and writes an audit event.
   * Fails closed with 404 for unknown provider keys.
   */
  async postBillingExternalProvidersDisable(providerKey: string): Promise<Result<Types.CommerceBillingExternalBillingProviderStatusDto, ApiError>> {
    const url = `/api/v1/billing/external-providers/${providerKey}:disable`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceBillingExternalBillingProviderStatusDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Enable an external billing provider at runtime
   *
   * Persists an explicit enable decision (idempotent) and writes an audit event.
   * Fails closed with 404 for unknown provider keys. Availability still requires
   * valid provider configuration.
   */
  async postBillingExternalProvidersEnable(providerKey: string): Promise<Result<Types.CommerceBillingExternalBillingProviderStatusDto, ApiError>> {
    const url = `/api/v1/billing/external-providers/${providerKey}:enable`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceBillingExternalBillingProviderStatusDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Produce a report-only migration dry-run between two external billing providers
   *
   * Scans subscriptions and classifies each by its external-provider binding: bound to the
   * source provider while lacking a target-provider external identifier (the migration work
   * list), already on the target provider, unattributable external identifiers (manual
   * review), and subscriptions without external identifiers. The report never mutates
   * state; executing provider switching is out of scope and gateway routing/failover is
   * tracked in issue #413. Fails closed with 400 for unknown or identical provider keys.
   */
  async postBillingExternalProvidersMigrationDryRun(
    body: Types.CommerceBillingBillingExternalProvidersControllerMigrationDryRunInput,
  ): Promise<Result<Types.CommerceBillingBillingProviderMigrationReport, ApiError>> {
    const url = '/api/v1/billing/external-providers/migration:dry-run';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceBillingBillingExternalProvidersControllerMigrationDryRunInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceBillingBillingProviderMigrationReportSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommerceBillingExternalProvidersModule(client: ApiClient): CommerceBillingExternalProvidersModule {
  return new CommerceBillingExternalProvidersModule(client);
}
