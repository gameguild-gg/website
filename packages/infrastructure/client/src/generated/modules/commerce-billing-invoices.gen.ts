/**
 * @game-guild/client - CommerceBillingInvoices Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceBillingInvoicesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Applies the calling user's account credits to one of their invoices (issue #410
   * multi-tender settlement). Requires an Idempotency-Key header; the optional body
   * caps the credit tender amount. User and tenant are derived from the actor.
   *
   * Settles part or all of an open/past-due invoice with the calling user's account credits, consumed FIFO by expiry in one atomic settlement (credit ledger rows + settlement record + invoice update). Requires the Idempotency-Key header; replays return the recorded settlement. The invoice must belong to the calling user; credits must match the invoice currency. The remaining balance stays owed to provider tenders.
   */
  async postBillingInvoicesAccountCredits(
    invoiceId: string,
    body: Types.CommerceBillingBillingInvoicesControllerApplyInvoiceCreditsInput,
  ): Promise<Result<Types.CommerceBillingInvoiceAccountCreditSettlementResult, ApiError>> {
    const url = `/api/v1/billing/invoices/${invoiceId}/account-credits`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceBillingBillingInvoicesControllerApplyInvoiceCreditsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceBillingInvoiceAccountCreditSettlementResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Retry invoice payment
   *
   * Accepts a local retry scheduling request for open or past-due invoices. External gateway capture requires configured payment-provider credentials.
   */
  async postBillingInvoicesRetry(invoiceId: string): Promise<Result<Types.CommerceBillingInvoicePaymentRetryResult, ApiError>> {
    const url = `/api/v1/billing/invoices/${invoiceId}/retry`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceBillingInvoicePaymentRetryResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommerceBillingInvoicesModule(client: ApiClient): CommerceBillingInvoicesModule {
  return new CommerceBillingInvoicesModule(client);
}
