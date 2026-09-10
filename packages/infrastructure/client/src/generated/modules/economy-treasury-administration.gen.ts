/**
 * @game-guild/client - EconomyTreasuryAdministration Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class EconomyTreasuryAdministrationModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getAdminEconomyTreasuryWithdrawalsForGetAdminEconomyTreasuryWithdrawals(query?: {
    limit?: number;
  }): Promise<Result<Array<Types.FinanceEconomyTreasuryAdminWithdrawalRun>, ApiError>> {
    const url = '/api/v1/admin/economy/treasury/withdrawals';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.FinanceEconomyTreasuryAdminWithdrawalRun>, ApiError>;
  }

  /**
   */
  async postAdminEconomyTreasuryWithdrawals(
    body: Types.APIControllersProposeTreasuryWithdrawalInput,
  ): Promise<Result<Types.FinanceEconomyTreasuryAdminWithdrawalRun, ApiError>> {
    const url = '/api/v1/admin/economy/treasury/withdrawals';

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersProposeTreasuryWithdrawalInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyTreasuryAdminWithdrawalRunSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyTreasuryWithdrawalsForGetAdminEconomyTreasuryWithdrawalsByRunId(
    runId: string,
  ): Promise<Result<Types.FinanceEconomyTreasuryAdminWithdrawalRun, ApiError>> {
    const url = `/api/v1/admin/economy/treasury/withdrawals/${runId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyTreasuryAdminWithdrawalRunSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyTreasuryWithdrawalsApprove(
    runId: string,
    body: Types.APIControllersApproveTreasuryWithdrawalInput,
  ): Promise<Result<Types.FinanceEconomyTreasuryAdminWithdrawalRun, ApiError>> {
    const url = `/api/v1/admin/economy/treasury/withdrawals/${runId}/approve`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersApproveTreasuryWithdrawalInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyTreasuryAdminWithdrawalRunSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyTreasuryWithdrawalsAudit(runId: string): Promise<Result<Types.FinanceEconomyTreasuryAdminWithdrawalAuditView, ApiError>> {
    const url = `/api/v1/admin/economy/treasury/withdrawals/${runId}/audit`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyTreasuryAdminWithdrawalAuditViewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyTreasuryWithdrawalsDispatch(
    runId: string,
    body: Types.APIControllersDispatchTreasuryWithdrawalInput,
  ): Promise<Result<Types.FinanceEconomyTreasuryAdminWithdrawalRun, ApiError>> {
    const url = `/api/v1/admin/economy/treasury/withdrawals/${runId}/dispatch`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersDispatchTreasuryWithdrawalInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyTreasuryAdminWithdrawalRunSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyTreasuryWithdrawalsReconcile(runId: string): Promise<Result<Types.FinanceEconomyTreasuryAdminWithdrawalRun, ApiError>> {
    const url = `/api/v1/admin/economy/treasury/withdrawals/${runId}/reconcile`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyTreasuryAdminWithdrawalRunSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createEconomyTreasuryAdministrationModule(client: ApiClient): EconomyTreasuryAdministrationModule {
  return new EconomyTreasuryAdministrationModule(client);
}
