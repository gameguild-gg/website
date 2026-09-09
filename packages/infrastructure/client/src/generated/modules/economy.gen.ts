/**
 * @game-guild/client - Economy Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class EconomyModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List payout requests awaiting administrative review
   */
  async getAdminEconomyPayoutRequests(query?: { take?: number }): Promise<Result<Array<Types.EconomyPayoutsQueriesEconomyPayoutRequestReviewDto>, ApiError>> {
    const url = '/api/v1/admin/economy/payout-requests';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.EconomyPayoutsQueriesEconomyPayoutRequestReviewDto>, ApiError>;
  }

  /**
   * Record one independent payout approval
   *
   * The first approval waits for a different tenant administrator. Final approval records a decision only and does not reserve or dispatch value.
   */
  async postAdminEconomyPayoutRequestsApprove(
    requestId: string,
    body: Types.EconomyPayoutsCommandsReviewPayoutRequestInput,
  ): Promise<Result<Types.EconomyPayoutsQueriesEconomyPayoutRequestReviewDto, ApiError>> {
    const url = `/api/v1/admin/economy/payout-requests/${requestId}/approve`;

    // Validate request body
    const validatedBody = safeParse(Types.EconomyPayoutsCommandsReviewPayoutRequestInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyPayoutsQueriesEconomyPayoutRequestReviewDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get the immutable administrative review trail for a payout request
   */
  async getAdminEconomyPayoutRequestsAudit(requestId: string): Promise<Result<Array<Types.EconomyPayoutsQueriesEconomyPayoutRequestReviewAuditDto>, ApiError>> {
    const url = `/api/v1/admin/economy/payout-requests/${requestId}/audit`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.EconomyPayoutsQueriesEconomyPayoutRequestReviewAuditDto>, ApiError>;
  }

  /**
   * Reject a payout request with an immutable reason
   */
  async postAdminEconomyPayoutRequestsReject(
    requestId: string,
    body: Types.EconomyPayoutsCommandsReviewPayoutRequestInput,
  ): Promise<Result<Types.EconomyPayoutsQueriesEconomyPayoutRequestReviewDto, ApiError>> {
    const url = `/api/v1/admin/economy/payout-requests/${requestId}/reject`;

    // Validate request body
    const validatedBody = safeParse(Types.EconomyPayoutsCommandsReviewPayoutRequestInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyPayoutsQueriesEconomyPayoutRequestReviewDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get my Economy capability readiness
   */
  async getEconomyCapabilities(): Promise<Result<Array<Types.APIControllersEconomySelfServiceCapabilityDto>, ApiError>> {
    const url = '/api/v1/economy/capabilities';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.APIControllersEconomySelfServiceCapabilityDto>, ApiError>;
  }

  /**
   * Convert my confirmed HardCoin balance into SoftCoin
   */
  async postEconomyConversionsHardToSoft(
    body: Types.EconomyCommandsConvertMyHardToSoftInput,
  ): Promise<Result<Types.EconomyFundingSelfServiceHardToSoftConversionReceipt, ApiError>> {
    const url = '/api/v1/economy/conversions/hard-to-soft';

    // Validate request body
    const validatedBody = safeParse(Types.EconomyCommandsConvertMyHardToSoftInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyFundingSelfServiceHardToSoftConversionReceiptSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * List my payout requests
   */
  async getEconomyPayoutRequests(query?: { take?: number }): Promise<Result<Array<Types.EconomyPayoutsQueriesEconomyPayoutRequestDto>, ApiError>> {
    const url = '/api/v1/economy/payout-requests';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.EconomyPayoutsQueriesEconomyPayoutRequestDto>, ApiError>;
  }

  /**
   * Submit my payout request
   *
   * Records a withdrawal request only. It does not reserve or transfer value until KYC, risk, provider, and FIFO eligibility checks pass.
   */
  async postEconomyPayoutRequests(
    body: Types.EconomyPayoutsCommandsCreateMyPayoutRequestInput,
  ): Promise<Result<Types.EconomyPayoutsQueriesEconomyPayoutRequestDto, ApiError>> {
    const url = '/api/v1/economy/payout-requests';

    // Validate request body
    const validatedBody = safeParse(Types.EconomyPayoutsCommandsCreateMyPayoutRequestInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyPayoutsQueriesEconomyPayoutRequestDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Cancel my pending payout request
   */
  async postEconomyPayoutRequestsCancel(requestId: string): Promise<Result<Types.EconomyPayoutsQueriesEconomyPayoutRequestDto, ApiError>> {
    const url = `/api/v1/economy/payout-requests/${requestId}/cancel`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyPayoutsQueriesEconomyPayoutRequestDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * List my payout operations
   */
  async getEconomyPayoutsForGetEconomyPayouts(query?: {
    take?: number;
  }): Promise<Result<Array<Types.EconomyPayoutsQueriesEconomyPayoutOperationDto>, ApiError>> {
    const url = '/api/v1/economy/payouts';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.EconomyPayoutsQueriesEconomyPayoutOperationDto>, ApiError>;
  }

  /**
   * Get my payout operation
   */
  async getEconomyPayoutsForGetEconomyPayoutsByOperationId(
    operationId: string,
  ): Promise<Result<Types.EconomyPayoutsQueriesEconomyPayoutOperationDto, ApiError>> {
    const url = `/api/v1/economy/payouts/${operationId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyPayoutsQueriesEconomyPayoutOperationDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get my payout provider account readiness
   */
  async getEconomyPayoutsAccount(): Promise<Result<Types.EconomyPayoutsConnectAccountSnapshot, ApiError>> {
    const url = '/api/v1/economy/payouts/account';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyPayoutsConnectAccountSnapshotSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Create or refresh my payout provider onboarding
   */
  async postEconomyPayoutsOnboarding(): Promise<Result<Types.EconomyPayoutsConnectOnboardingResult, ApiError>> {
    const url = '/api/v1/economy/payouts/onboarding';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyPayoutsConnectOnboardingResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * List my HardCoin top-ups
   */
  async getEconomyTopUpsForGetEconomyTopUps(query?: { take?: number }): Promise<Result<Array<Types.EconomyFundingEconomyTopUpStatusDto>, ApiError>> {
    const url = '/api/v1/economy/top-ups';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.EconomyFundingEconomyTopUpStatusDto>, ApiError>;
  }

  /**
   * Create my HardCoin top-up payment intent
   *
   * The server derives tenant, wallet, jurisdiction, signed quote, amount, provider binding, and idempotency authority.
   */
  async postEconomyTopUps(
    body: Types.EconomyCommandsCreateMyHardCoinTopUpInput,
  ): Promise<Result<Types.EconomyFundingSelfServiceHardCoinTopUpReceipt, ApiError>> {
    const url = '/api/v1/economy/top-ups';

    // Validate request body
    const validatedBody = safeParse(Types.EconomyCommandsCreateMyHardCoinTopUpInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyFundingSelfServiceHardCoinTopUpReceiptSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get one of my HardCoin top-ups
   */
  async getEconomyTopUpsForGetEconomyTopUpsByTopUpId(topUpId: string): Promise<Result<Types.EconomyFundingEconomyTopUpStatusDto, ApiError>> {
    const url = `/api/v1/economy/top-ups/${topUpId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyFundingEconomyTopUpStatusDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Send a typed Economy transfer to another user in my tenant
   *
   * The server resolves wallets, jurisdiction, policy, reserve, risk, and posting authority. The request contains business intent only.
   */
  async postEconomyTransfers(
    body: Types.EconomyTransfersSelfServiceEconomyTransferInput,
  ): Promise<Result<Types.EconomyTransfersSelfServiceEconomyTransferReceipt, ApiError>> {
    const url = '/api/v1/economy/transfers';

    // Validate request body
    const validatedBody = safeParse(Types.EconomyTransfersSelfServiceEconomyTransferInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyTransfersSelfServiceEconomyTransferReceiptSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get my Economy wallet
   */
  async getEconomyWallet(): Promise<Result<Types.EconomyContractsEconomyWalletSummaryDto, ApiError>> {
    const url = '/api/v1/economy/wallet';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyContractsEconomyWalletSummaryDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * List my Economy wallet transactions
   */
  async getEconomyWalletTransactions(query?: { take?: number }): Promise<Result<Array<Types.EconomyContractsEconomyWalletTransactionDto>, ApiError>> {
    const url = '/api/v1/economy/wallet/transactions';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.EconomyContractsEconomyWalletTransactionDto>, ApiError>;
  }
}

export function createEconomyModule(client: ApiClient): EconomyModule {
  return new EconomyModule(client);
}
