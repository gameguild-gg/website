/**
 * @game-guild/client - CommercePayments Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommercePaymentsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Retrieve all payment transactions with optional filtering
   *
   * Retrieves a paginated list of all payment transactions with support for filtering by tenant, status,
   * and date range. This is the primary endpoint for payment administration and reporting.
   * Supported status filters:
   * - pending: Payments currently being processed
   * - completed: Successfully processed payments
   * - failed: Payments that encountered errors
   * - cancelled: Payments cancelled before completion
   * - refunded: Payments that have been refunded
   * Query Parameters:
   * - tenantId: Filter payments for specific tenant
   * - status: Filter by payment status
   * - startDate: Include payments from this date onwards (ISO 8601 format)
   * - endDate: Include payments up to this date (ISO 8601 format)
   * - page: Pagination page number (1-based)
   * - pageSize: Items per page (1-100)
   * Use cases:
   * - Financial reporting and reconciliation
   * - Payment monitoring and analytics
   * - Administrative payment management
   * - Audit trail generation
   */
  async getPayments(query?: {
    tenantId?: string;
    status?: string;
    startDate?: string;
    endDate?: string;
    page?: number;
    pageSize?: number;
  }): Promise<Result<Types.CommercePaymentsPaymentResult[], ApiError>> {
    const url = '/api/v1/payments';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.CommercePaymentsPaymentResult[], ApiError>;
  }

  /**
   * Process a new payment transaction
   *
   * Initiates a new payment transaction for a subscription. This endpoint handles the complete payment
   * processing workflow including payment method validation, amount verification, and transaction execution.
   * Returns the payment result immediately with a transaction ID that can be used to track payment status.
   * Processing workflow:
   * 1. Validate payment method and amount
   * 2. Verify subscription and tenant information
   * 3. Process payment through configured payment gateway
   * 4. Update subscription status based on result
   * 5. Generate transaction record
   * Request body must include:
   * - TenantId: Organization identifier
   * - SubscriptionId: Target subscription
   * - Amount: Payment amount in base currency units
   * - PaymentMethodId: Stripe payment method identifier starting with pm_
   * Returns CreatedAtRoute with payment details for successful transactions.
   */
  async postPayments(body: Types.CommercePaymentsPaymentsControllerProcessPaymentInput): Promise<Result<Types.CommercePaymentsPaymentResult, ApiError>> {
    const url = '/api/v1/payments';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsPaymentsControllerProcessPaymentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPaymentResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Retrieve a specific payment by its unique identifier
   *
   * Retrieves detailed information about a specific payment transaction, including its current status,
   * amount, payment method, and processing details. Use this endpoint to track payment progress
   * and verify transaction completion.
   * Response includes:
   * - Payment ID and transaction details
   * - Current payment status (pending, completed, failed, etc.)
   * - Payment method information
   * - Amount and currency
   * - Timestamps for creation and updates
   * - Associated subscription and tenant information
   */
  async getPaymentById(paymentId: string): Promise<Result<Types.CommercePaymentsPaymentResult, ApiError>> {
    const url = `/api/v1/payments/${paymentId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPaymentResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Cancel a payment transaction
   *
   * Cancels a payment transaction that is in progress or pending. This endpoint can be used to
   * cancel payments before they are processed, or to handle user-initiated cancellations during checkout.
   * Once canceled, a payment cannot be processed and may require a new payment attempt.
   * Cancellation scenarios:
   * - User abandons checkout process
   * - Administrative cancellation
   * - Fraud prevention trigger
   * - Duplicate transaction prevention
   * - Session timeout or expiration
   * - Payment method validation failure
   * Cancellation handling:
   * - Updates payment status to "canceled"
   * - Records cancellation reason and timestamp
   * - Releases any held resources or reservations
   * - Notifies relevant systems of cancellation
   * - Provides audit trail for investigation
   * Request body options:
   * - CancellationReason: Required reason for audit trail
   * - CanceledBy: Optional user ID for tracking
   * - Notes: Optional additional context
   * Important notes:
   * - Only pending or processing payments can be canceled
   * - Completed payments cannot be canceled (use refund instead)
   * - Cancellation is immediate and irreversible
   * - Some payment methods may have specific cancellation rules
   */
  async postPaymentsCancel(
    paymentId: string,
    body: Types.CommercePaymentsPaymentsControllerCancelPaymentInput,
  ): Promise<Result<Types.CommercePaymentsPaymentCancellationResult, ApiError>> {
    const url = `/api/v1/payments/${paymentId}:cancel`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsPaymentsControllerCancelPaymentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPaymentCancellationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Process a refund for a completed payment
   *
   * Processes a full or partial refund for a previously completed payment transaction.
   * If no amount is specified, a full refund will be processed. The refund reason is optional
   * but recommended for record keeping and customer service purposes. Refunds are processed
   * back to the original payment method and may take several business days to appear.
   * Refund types:
   * - Full refund: Refunds the entire payment amount
   * - Partial refund: Refunds a specified portion of the payment
   * Request body options:
   * - Amount: Specific refund amount (null for full refund)
   * - Reason: Optional reason for audit and customer service
   * Processing notes:
   * - Refunds are processed to the original payment method
   * - Processing time varies by payment processor (2-10 business days)
   * - Refund fees may apply depending on payment method
   * - Only successful payments can be refunded
   * - Multiple partial refunds allowed up to original amount
   * Response includes refund ID for tracking and customer communication.
   */
  async postPaymentsRefund(
    paymentId: string,
    body: Types.CommercePaymentsPaymentsControllerRefundInput,
  ): Promise<Result<Types.CommercePaymentsProcessRefundResult, ApiError>> {
    const url = `/api/v1/payments/${paymentId}:refund`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsPaymentsControllerRefundInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsProcessRefundResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Retry a failed payment transaction
   *
   * Attempts to reprocess a previously failed payment transaction using the same payment method and amount.
   * This is useful when payments fail due to temporary issues like network problems or insufficient funds
   * that have since been resolved. The retry operation creates a new transaction attempt while maintaining
   * the link to the original payment record.
   * Retry scenarios:
   * - Temporary network connectivity issues resolved
   * - Insufficient funds now available
   * - Payment gateway was temporarily unavailable
   * - Rate limiting issues have cleared
   * - Card issuer temporary restrictions lifted
   * Important notes:
   * - Only failed payments can be retried
   * - Successful payments will return a 400 Bad Request
   * - Retry preserves original payment details
   * - New transaction ID is generated for the retry attempt
   * - Original payment record maintains audit trail
   */
  async postPaymentsRetry(paymentId: string): Promise<Result<Types.CommercePaymentsPaymentRetryResult, ApiError>> {
    const url = `/api/v1/payments/${paymentId}:retry`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPaymentRetryResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Payment success and failed-payment recovery metrics for an inclusive window.
   *
   * Aggregates the recovery analytics that billing administrators need to steer retry and
   * dunning strategy (issue #403): first-attempt success rate, retry-recovery rate, recovered
   * amounts grouped by currency, and dunning outcome counters (pending retry, due for retry,
   * exhausted). Non-admin actors are scoped to their own tenant.
   */
  async getPaymentsRecoveryMetrics(query?: {
    fromUtc?: string;
    toUtc?: string;
    tenantId?: string;
  }): Promise<Result<Types.CommercePaymentsPaymentRecoveryMetrics, ApiError>> {
    const url = '/api/v1/payments/recovery-metrics';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPaymentRecoveryMetricsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Failed payments that are due for a retry right now (retry-queue candidates).
   *
   * Returns the candidate set an automated retry queue would consume (issue #403; the queue
   * itself is tracked in #415): failed payments with retry budget left whose configured
   * backoff has elapsed, ordered by the scheduled next-retry time. Non-admin actors are
   * scoped to their own tenant.
   */
  async getPaymentsRetryingCandidates(query?: { tenantId?: string; take?: number }): Promise<Result<Types.CommercePaymentsPaymentResult[], ApiError>> {
    const url = '/api/v1/payments/retrying-candidates';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.CommercePaymentsPaymentResult[], ApiError>;
  }

  /**
   * Creates a Stripe SetupIntent for a subscription checkout.
   *
   * Creates or reuses a Stripe customer for the subscription and returns a SetupIntent client secret for PaymentElement-based card collection.
   */
  async postPaymentsSetupIntents(
    body: Types.CommercePaymentsPaymentsControllerCreateSetupIntentInput,
  ): Promise<Result<Types.CommercePaymentsPaymentsControllerCreateSetupIntentOutput, ApiError>> {
    const url = '/api/v1/payments/setup-intents';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsPaymentsControllerCreateSetupIntentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPaymentsControllerCreateSetupIntentOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Completes a subscription checkout after Stripe confirms the SetupIntent.
   *
   * Sets the confirmed Stripe payment method as the customer's default and processes the first subscription charge.
   */
  async postPaymentsSubscriptionCheckoutsComplete(
    body: Types.CommercePaymentsPaymentsControllerCompleteSubscriptionCheckoutInput,
  ): Promise<Result<Types.CommercePaymentsPaymentResult, ApiError>> {
    const url = '/api/v1/payments/subscription-checkouts:complete';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsPaymentsControllerCompleteSubscriptionCheckoutInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPaymentResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommercePaymentsModule(client: ApiClient): CommercePaymentsModule {
  return new CommercePaymentsModule(client);
}
