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
  async getAdminEconomyPayoutRequests(query?: {
    take?: number;
  }): Promise<Result<Array<Types.FinanceEconomyPayoutsQueriesEconomyPayoutRequestReviewDto>, ApiError>> {
    const url = '/api/v1/admin/economy/payout-requests';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.FinanceEconomyPayoutsQueriesEconomyPayoutRequestReviewDto>, ApiError>;
  }

  /**
   * Record one independent payout approval
   *
   * The first approval waits for a different tenant administrator. Final approval records a decision only and does not reserve or dispatch value.
   */
  async postAdminEconomyPayoutRequestsApprove(
    requestId: string,
    body: Types.FinanceEconomyPayoutsCommandsReviewPayoutRequestInput,
  ): Promise<Result<Types.FinanceEconomyPayoutsQueriesEconomyPayoutRequestReviewDto, ApiError>> {
    const url = `/api/v1/admin/economy/payout-requests/${requestId}/approve`;

    // Validate request body
    const validatedBody = safeParse(Types.FinanceEconomyPayoutsCommandsReviewPayoutRequestInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyPayoutsQueriesEconomyPayoutRequestReviewDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get the immutable administrative review trail for a payout request
   */
  async getAdminEconomyPayoutRequestsAudit(
    requestId: string,
  ): Promise<Result<Array<Types.FinanceEconomyPayoutsQueriesEconomyPayoutRequestReviewAuditDto>, ApiError>> {
    const url = `/api/v1/admin/economy/payout-requests/${requestId}/audit`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.FinanceEconomyPayoutsQueriesEconomyPayoutRequestReviewAuditDto>, ApiError>;
  }

  /**
   * Reject a payout request with an immutable reason
   */
  async postAdminEconomyPayoutRequestsReject(
    requestId: string,
    body: Types.FinanceEconomyPayoutsCommandsReviewPayoutRequestInput,
  ): Promise<Result<Types.FinanceEconomyPayoutsQueriesEconomyPayoutRequestReviewDto, ApiError>> {
    const url = `/api/v1/admin/economy/payout-requests/${requestId}/reject`;

    // Validate request body
    const validatedBody = safeParse(Types.FinanceEconomyPayoutsCommandsReviewPayoutRequestInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyPayoutsQueriesEconomyPayoutRequestReviewDtoSchema, result.data, 'response');
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
    body: Types.FinanceEconomyCommandsConvertMyHardToSoftInput,
  ): Promise<Result<Types.FinanceEconomyFundingSelfServiceHardToSoftConversionReceipt, ApiError>> {
    const url = '/api/v1/economy/conversions/hard-to-soft';

    // Validate request body
    const validatedBody = safeParse(Types.FinanceEconomyCommandsConvertMyHardToSoftInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyFundingSelfServiceHardToSoftConversionReceiptSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * List my payout requests
   */
  async getEconomyPayoutRequests(query?: { take?: number }): Promise<Result<Array<Types.FinanceEconomyPayoutsQueriesEconomyPayoutRequestDto>, ApiError>> {
    const url = '/api/v1/economy/payout-requests';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.FinanceEconomyPayoutsQueriesEconomyPayoutRequestDto>, ApiError>;
  }

  /**
   * Submit my payout request
   *
   * Records a withdrawal request only. It does not reserve or transfer value until KYC, risk, provider, and FIFO eligibility checks pass.
   */
  async postEconomyPayoutRequests(
    body: Types.FinanceEconomyPayoutsCommandsCreateMyPayoutRequestInput,
  ): Promise<Result<Types.FinanceEconomyPayoutsQueriesEconomyPayoutRequestDto, ApiError>> {
    const url = '/api/v1/economy/payout-requests';

    // Validate request body
    const validatedBody = safeParse(Types.FinanceEconomyPayoutsCommandsCreateMyPayoutRequestInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyPayoutsQueriesEconomyPayoutRequestDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Cancel my pending payout request
   */
  async postEconomyPayoutRequestsCancel(requestId: string): Promise<Result<Types.FinanceEconomyPayoutsQueriesEconomyPayoutRequestDto, ApiError>> {
    const url = `/api/v1/economy/payout-requests/${requestId}/cancel`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyPayoutsQueriesEconomyPayoutRequestDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * List my payout operations
   */
  async getEconomyPayoutsForGetEconomyPayouts(query?: {
    take?: number;
  }): Promise<Result<Array<Types.FinanceEconomyPayoutsQueriesEconomyPayoutOperationDto>, ApiError>> {
    const url = '/api/v1/economy/payouts';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.FinanceEconomyPayoutsQueriesEconomyPayoutOperationDto>, ApiError>;
  }

  /**
   * Get my payout operation
   */
  async getEconomyPayoutsForGetEconomyPayoutsByOperationId(
    operationId: string,
  ): Promise<Result<Types.FinanceEconomyPayoutsQueriesEconomyPayoutOperationDto, ApiError>> {
    const url = `/api/v1/economy/payouts/${operationId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyPayoutsQueriesEconomyPayoutOperationDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get my payout provider account readiness
   */
  async getEconomyPayoutsAccount(): Promise<Result<Types.FinanceEconomyPayoutsConnectAccountSnapshot, ApiError>> {
    const url = '/api/v1/economy/payouts/account';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyPayoutsConnectAccountSnapshotSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Create or refresh my payout provider onboarding
   */
  async postEconomyPayoutsOnboarding(): Promise<Result<Types.FinanceEconomyPayoutsConnectOnboardingResult, ApiError>> {
    const url = '/api/v1/economy/payouts/onboarding';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyPayoutsConnectOnboardingResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * List my HardCoin top-ups
   */
  async getEconomyTopUpsForGetEconomyTopUps(query?: { take?: number }): Promise<Result<Array<Types.FinanceEconomyFundingEconomyTopUpStatusDto>, ApiError>> {
    const url = '/api/v1/economy/top-ups';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.FinanceEconomyFundingEconomyTopUpStatusDto>, ApiError>;
  }

  /**
   * Create my HardCoin top-up payment intent
   *
   * The server derives tenant, wallet, jurisdiction, signed quote, amount, provider binding, and idempotency authority.
   */
  async postEconomyTopUps(
    body: Types.FinanceEconomyCommandsCreateMyHardCoinTopUpInput,
  ): Promise<Result<Types.FinanceEconomyFundingSelfServiceHardCoinTopUpReceipt, ApiError>> {
    const url = '/api/v1/economy/top-ups';

    // Validate request body
    const validatedBody = safeParse(Types.FinanceEconomyCommandsCreateMyHardCoinTopUpInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyFundingSelfServiceHardCoinTopUpReceiptSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get one of my HardCoin top-ups
   */
  async getEconomyTopUpsForGetEconomyTopUpsByTopUpId(topUpId: string): Promise<Result<Types.FinanceEconomyFundingEconomyTopUpStatusDto, ApiError>> {
    const url = `/api/v1/economy/top-ups/${topUpId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyFundingEconomyTopUpStatusDtoSchema, result.data, 'response');
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
    body: Types.FinanceEconomyTransfersSelfServiceEconomyTransferInput,
  ): Promise<Result<Types.FinanceEconomyTransfersSelfServiceEconomyTransferReceipt, ApiError>> {
    const url = '/api/v1/economy/transfers';

    // Validate request body
    const validatedBody = safeParse(Types.FinanceEconomyTransfersSelfServiceEconomyTransferInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyTransfersSelfServiceEconomyTransferReceiptSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get my Economy wallet
   */
  async getEconomyWallet(): Promise<Result<Types.FinanceEconomyContractsEconomyWalletSummaryDto, ApiError>> {
    const url = '/api/v1/economy/wallet';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyContractsEconomyWalletSummaryDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * List my Economy wallet transactions
   */
  async getEconomyWalletTransactions(query?: { take?: number }): Promise<Result<Array<Types.FinanceEconomyContractsEconomyWalletTransactionDto>, ApiError>> {
    const url = '/api/v1/economy/wallet/transactions';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.FinanceEconomyContractsEconomyWalletTransactionDto>, ApiError>;
  }
}

export function createEconomyModule(client: ApiClient): EconomyModule {
  return new EconomyModule(client);
}
