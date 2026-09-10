/**
 * @game-guild/client - EconomyBounties Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class EconomyBountiesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getEconomyBountiesForGetEconomyBounties(query?: {
    status?: Types.FinanceEconomyBountiesBountyStatus;
  }): Promise<Result<Array<Types.FinanceEconomyBountiesDurableBountyView>, ApiError>> {
    const url = '/api/v1/economy/bounties';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.FinanceEconomyBountiesDurableBountyView>, ApiError>;
  }

  /**
   */
  async postEconomyBounties(body: Types.APIControllersCreateMyBountyInput): Promise<Result<Types.FinanceEconomyBountiesDurableBountyView, ApiError>> {
    const url = '/api/v1/economy/bounties';

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersCreateMyBountyInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyBountiesDurableBountyViewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getEconomyBountiesForGetEconomyBountiesByBountyId(bountyId: string): Promise<Result<Types.FinanceEconomyBountiesDurableBountyView, ApiError>> {
    const url = `/api/v1/economy/bounties/${bountyId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyBountiesDurableBountyViewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postEconomyBountiesClaim(
    bountyId: string,
    body: Types.APIControllersCompleteMyBountyInput,
  ): Promise<Result<Types.FinanceEconomyBountiesDurableBountyView, ApiError>> {
    const url = `/api/v1/economy/bounties/${bountyId}:claim`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersCompleteMyBountyInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyBountiesDurableBountyViewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postEconomyBountiesReclaim(
    bountyId: string,
    body: Types.APIControllersCompleteMyBountyInput,
  ): Promise<Result<Types.FinanceEconomyBountiesDurableBountyView, ApiError>> {
    const url = `/api/v1/economy/bounties/${bountyId}:reclaim`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersCompleteMyBountyInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyBountiesDurableBountyViewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createEconomyBountiesModule(client: ApiClient): EconomyBountiesModule {
  return new EconomyBountiesModule(client);
}
