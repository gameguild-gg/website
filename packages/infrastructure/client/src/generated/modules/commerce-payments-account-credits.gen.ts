/**
 * @game-guild/client - CommercePaymentsAccountCredits Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommercePaymentsAccountCreditsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Revokes the remaining balance of a credit grant with an auditable reason. SystemAdmin only.
   *
   * Revokes the remaining balance of an account credit grant; the reason is captured on the grant and in the credit ledger. Requires the SystemAdmin role.
   */
  async postAccountCreditsRevoke(grantId: string, body: Types.CommercePaymentsAccountCreditsControllerRevokeCreditInput): Promise<Result<void, ApiError>> {
    const url = `/api/v1/account-credits/${grantId}:revoke`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsAccountCreditsControllerRevokeCreditInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Per-currency spendable credit balances of the calling user.
   *
   * Returns the spendable account credit balance of the calling user per ISO 4217 currency (Active grants, expiry-aware). User and tenant are derived from the authenticated actor.
   */
  async getAccountCreditsBalance(): Promise<Result<Types.CommercePaymentsAccountCreditBalance[], ApiError>> {
    const url = '/api/v1/account-credits/balance';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.CommercePaymentsAccountCreditBalance[], ApiError>;
  }

  /**
   * Manually issues account credit to a user of the calling tenant. SystemAdmin only.
   *
   * Manually grants account credit to a tenant user with an optional expiry. The credit type restricts what the credit may be applied to. Requires the SystemAdmin role; the tenant is derived from the authenticated actor.
   */
  async postAccountCreditsGrants(
    body: Types.CommercePaymentsAccountCreditsControllerIssueCreditInput,
  ): Promise<Result<Types.CommercePaymentsAccountCreditGrant, ApiError>> {
    const url = '/api/v1/account-credits/grants';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsAccountCreditsControllerIssueCreditInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsAccountCreditGrantSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Paged credit ledger history of the calling user (grants, consumption, revocation, expiry).
   *
   * Returns a paged list of the calling user's account credit ledger rows, newest first. User and tenant are derived from the authenticated actor.
   */
  async getAccountCreditsHistory(query?: { skip?: number; take?: number }): Promise<Result<Types.PagedResultAccountCreditTransaction, ApiError>> {
    const url = '/api/v1/account-credits/history';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultAccountCreditTransactionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Growth metrics: credit issuance/usage/expiry rates, referral funnel and top referrers. SystemAdmin only.
   *
   * Credit ledger aggregates per currency (issued, applied, expired-unused waste, revoked, active balance, usage and expiration-waste rates), the referral conversion funnel (clicks → signups → conversions → rewards) and the top-referrers leaderboard. Requires the SystemAdmin role.
   */
  async getAccountCreditsMetrics(): Promise<Result<Types.CommercePaymentsAccountCreditMetricsReport, ApiError>> {
    const url = '/api/v1/account-credits/metrics';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsAccountCreditMetricsReportSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommercePaymentsAccountCreditsModule(client: ApiClient): CommercePaymentsAccountCreditsModule {
  return new CommercePaymentsAccountCreditsModule(client);
}
