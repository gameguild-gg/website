/**
 * @game-guild/client - CommercePaymentsWallets Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommercePaymentsWalletsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get my wallet
   */
  async getWallet(): Promise<Result<Types.CommercePaymentsUserWallet, ApiError>> {
    const url = '/api/v1/wallet';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsUserWalletSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Create my wallet
   */
  async postWallet(body: Types.CommercePaymentsCreateWalletInput): Promise<Result<Types.CommercePaymentsUserWallet, ApiError>> {
    const url = '/api/v1/wallet';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsCreateWalletInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsUserWalletSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lock my wallet
   */
  async postWalletLock(body: Types.CommercePaymentsLockWalletInput): Promise<Result<void, ApiError>> {
    const url = '/api/v1/wallet:lock';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsLockWalletInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Unlock my wallet
   */
  async postWalletUnlock(): Promise<Result<void, ApiError>> {
    const url = '/api/v1/wallet:unlock';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get my wallet balance
   */
  async getWalletBalance(): Promise<Result<void, ApiError>> {
    const url = '/api/v1/wallet/balance';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * List all wallets
   */
  async getWalletsForGetWallets(query?: { page?: number; pageSize?: number; currency?: string; isFrozen?: boolean }): Promise<Result<void, ApiError>> {
    const url = '/api/v1/wallets';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Wallet-to-wallet transfer (issue #406): moves funds between two same-currency wallets
   * through the audited TransferFunds command. A duplicate ReferenceId replays
   * idempotently.
   */
  async postWalletsTransfer(
    body: Types.CommercePaymentsModelsTransferWalletsInput,
  ): Promise<Result<Types.CommercePaymentsModelsTransferWalletsOutput, ApiError>> {
    const url = '/api/v1/wallets:transfer';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsModelsTransferWalletsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsModelsTransferWalletsOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get wallet by ID
   */
  async getWalletsForGetWalletsByWalletId(walletId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/wallets/${walletId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Close wallet
   */
  async deleteWallets(walletId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/wallets/${walletId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Update wallet settings
   */
  async patchWallets(walletId: string, body: Types.CommercePaymentsModelsPatchWalletInput): Promise<Result<void, ApiError>> {
    const url = `/api/v1/wallets/${walletId}`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsModelsPatchWalletInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Check if wallet exists
   */
  async headWallets(walletId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/wallets/${walletId}`;

    const result = await this.client.request({
      method: 'HEAD',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Administrative balance adjustment (issue #406): credits or debits a wallet through
   * the audited AddFunds/DeductFunds commands. A duplicate ReferenceId replays
   * idempotently and returns the original transaction.
   */
  async postWalletsAdjust(
    walletId: string,
    body: Types.CommercePaymentsModelsAdjustWalletInput,
  ): Promise<Result<Types.CommercePaymentsWalletTransaction, ApiError>> {
    const url = `/api/v1/wallets/${walletId}:adjust`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsModelsAdjustWalletInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsWalletTransactionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Freeze wallet
   */
  async postWalletsFreeze(walletId: string, body: Types.CommercePaymentsModelsFreezeWalletInput): Promise<Result<void, ApiError>> {
    const url = `/api/v1/wallets/${walletId}:freeze`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsModelsFreezeWalletInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Unfreeze wallet
   */
  async postWalletsUnfreeze(walletId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/wallets/${walletId}:unfreeze`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get wallet audit log
   */
  async getWalletsAuditLog(walletId: string, query?: { page?: number; pageSize?: number }): Promise<Result<void, ApiError>> {
    const url = `/api/v1/wallets/${walletId}/audit-log`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Wallet balance analytics (issue #406): total balances by currency, active/frozen
   * wallet counts and completed transaction volume by type and day.
   */
  async getWalletsMetrics(query?: { days?: number }): Promise<Result<Types.CommercePaymentsWalletMetricsOutput, ApiError>> {
    const url = '/api/v1/wallets/metrics';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsWalletMetricsOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Report-only balance reconciliation (issue #406): per wallet, verifies Balance against
   * the latest BalanceAfter and the signed sum of completed transactions. Never mutates state.
   */
  async getWalletsReconciliation(query?: { walletId?: string }): Promise<Result<Types.CommercePaymentsWalletReconciliationReportOutput, ApiError>> {
    const url = '/api/v1/wallets/reconciliation';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsWalletReconciliationReportOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommercePaymentsWalletsModule(client: ApiClient): CommercePaymentsWalletsModule {
  return new CommercePaymentsWalletsModule(client);
}
