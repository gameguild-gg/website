/**
 * @game-guild/client - EconomyLegacyMigrationAdministration Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class EconomyLegacyMigrationAdministrationModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getAdminEconomyLegacyMigrationBatchesForGetAdminEconomyLegacyMigrationBatches(query?: {
    state?: Types.FinanceEconomyOperationsLegacyEconomyShadowState;
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.FinanceEconomyOperationsEconomyOperationalPageLegacyEconomyShadowBatchSummary, ApiError>> {
    const url = '/api/v1/admin/economy/legacy-migration/batches';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsEconomyOperationalPageLegacyEconomyShadowBatchSummarySchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyLegacyMigrationBatches(
    body: Types.APIControllersCaptureLegacyEconomyMigrationInput,
  ): Promise<Result<Types.FinanceEconomyOperationsLegacyEconomyShadowBatchView, ApiError>> {
    const url = '/api/v1/admin/economy/legacy-migration/batches';

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersCaptureLegacyEconomyMigrationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsLegacyEconomyShadowBatchViewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyLegacyMigrationBatchesForGetAdminEconomyLegacyMigrationBatchesByBatchId(
    batchId: string,
  ): Promise<Result<Types.FinanceEconomyOperationsLegacyEconomyShadowBatchView, ApiError>> {
    const url = `/api/v1/admin/economy/legacy-migration/batches/${batchId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsLegacyEconomyShadowBatchViewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyLegacyMigrationBatchesReconcile(
    batchId: string,
  ): Promise<Result<Types.FinanceEconomyOperationsLegacyEconomyShadowBatchView, ApiError>> {
    const url = `/api/v1/admin/economy/legacy-migration/batches/${batchId}:reconcile`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsLegacyEconomyShadowBatchViewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyLegacyMigrationBatchesCutoverApprove(
    batchId: string,
    body: Types.APIControllersApproveLegacyEconomyCutoverInput,
  ): Promise<Result<Types.FinanceEconomyOperationsLegacyEconomyShadowBatchView, ApiError>> {
    const url = `/api/v1/admin/economy/legacy-migration/batches/${batchId}/cutover:approve`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersApproveLegacyEconomyCutoverInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsLegacyEconomyShadowBatchViewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyLegacyMigrationBatchesCutoverPropose(
    batchId: string,
    body: Types.APIControllersProposeLegacyEconomyCutoverInput,
  ): Promise<Result<Types.FinanceEconomyOperationsLegacyEconomyShadowBatchView, ApiError>> {
    const url = `/api/v1/admin/economy/legacy-migration/batches/${batchId}/cutover:propose`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersProposeLegacyEconomyCutoverInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsLegacyEconomyShadowBatchViewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyLegacyMigrationBatchesCutoverRollback(
    batchId: string,
    body: Types.APIControllersRollbackLegacyEconomyCutoverInput,
  ): Promise<Result<Types.FinanceEconomyOperationsLegacyEconomyShadowBatchView, ApiError>> {
    const url = `/api/v1/admin/economy/legacy-migration/batches/${batchId}/cutover:rollback`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersRollbackLegacyEconomyCutoverInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsLegacyEconomyShadowBatchViewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyLegacyMigrationBatchesWalletsBackfill(
    batchId: string,
    body: Types.APIControllersBackfillLegacyEconomyWalletInput,
  ): Promise<Result<Types.FinanceEconomyOperationsLegacyEconomyShadowBatchView, ApiError>> {
    const url = `/api/v1/admin/economy/legacy-migration/batches/${batchId}/wallets:backfill`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersBackfillLegacyEconomyWalletInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsLegacyEconomyShadowBatchViewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createEconomyLegacyMigrationAdministrationModule(client: ApiClient): EconomyLegacyMigrationAdministrationModule {
  return new EconomyLegacyMigrationAdministrationModule(client);
}
