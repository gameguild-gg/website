/**
 * @game-guild/client - EconomyComplianceHoldAdministration Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class EconomyComplianceHoldAdministrationModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getAdminEconomyComplianceHoldsForGetAdminEconomyComplianceHolds(query?: {
    active?: boolean;
    capability?: Types.FinanceEconomyRiskEconomyValueMovementCapability;
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.FinanceEconomyRiskComplianceHoldPage, ApiError>> {
    const url = '/api/v1/admin/economy/compliance/holds';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyRiskComplianceHoldPageSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyComplianceHoldsForGetAdminEconomyComplianceHoldsByHoldId(
    holdId: string,
  ): Promise<Result<Types.FinanceEconomyRiskComplianceHoldAdministrationState, ApiError>> {
    const url = `/api/v1/admin/economy/compliance/holds/${holdId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyRiskComplianceHoldAdministrationStateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyComplianceHoldsAudit(holdId: string): Promise<Result<Array<Types.FinanceEconomyRiskComplianceHoldEvent>, ApiError>> {
    const url = `/api/v1/admin/economy/compliance/holds/${holdId}/audit`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.FinanceEconomyRiskComplianceHoldEvent>, ApiError>;
  }

  /**
   */
  async postAdminEconomyComplianceHoldsReleaseApprovals(
    holdId: string,
    body: Types.APIControllersEconomyStepUpInput,
  ): Promise<Result<Types.FinanceEconomyRiskComplianceHoldAdministrationState, ApiError>> {
    const url = `/api/v1/admin/economy/compliance/holds/${holdId}/release-approvals`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersEconomyStepUpInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyRiskComplianceHoldAdministrationStateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyComplianceHoldsReleaseProposals(
    holdId: string,
    body: Types.APIControllersEconomyStepUpInput,
  ): Promise<Result<Types.FinanceEconomyRiskComplianceHoldAdministrationState, ApiError>> {
    const url = `/api/v1/admin/economy/compliance/holds/${holdId}/release-proposals`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersEconomyStepUpInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyRiskComplianceHoldAdministrationStateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createEconomyComplianceHoldAdministrationModule(client: ApiClient): EconomyComplianceHoldAdministrationModule {
  return new EconomyComplianceHoldAdministrationModule(client);
}
