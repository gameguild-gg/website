/**
 * @game-guild/client - CommercePaymentsDisputes Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommercePaymentsDisputesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List disputes with optional filters, newest first.
   *
   * Lists payment disputes with optional status, payment and user filters, newest first. Non-admin actors only see their own tenant's disputes.
   */
  async getDisputes(query?: {
    status?: Types.CommercePaymentsDisputeStatus;
    paymentId?: string;
    userId?: string;
    page?: number;
    pageSize?: number;
  }): Promise<Result<Types.CommercePaymentsDisputeListPage, ApiError>> {
    const url = '/api/v1/disputes';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsDisputeListPageSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Create a dispute for a payment. The disputing user is the authenticated actor.
   *
   * Creates a dispute against a payment. The disputing identity comes from the authenticated actor, never the request body.
   */
  async postDisputes(body: Types.CommercePaymentsDisputesControllerCreateDisputeInput): Promise<Result<Types.CommercePaymentsPaymentDispute, ApiError>> {
    const url = '/api/v1/disputes';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsDisputesControllerCreateDisputeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPaymentDisputeSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Retrieve a single dispute by id.
   *
   * Retrieves a single payment dispute with its attached evidence.
   */
  async getDisputeById(disputeId: string): Promise<Result<Types.CommercePaymentsPaymentDispute, ApiError>> {
    const url = `/api/v1/disputes/${disputeId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPaymentDisputeSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Accept a dispute: closes it as lost at the gateway and records the refund through the
   * approval-aware refund path. Requires the `payments:admin` permission.
   *
   * Accepts the dispute (lost) and records the refund through the refund approval workflow. Requires the payments:admin permission.
   */
  async postDisputesAccept(
    disputeId: string,
    body: Types.CommercePaymentsDisputesControllerAcceptDisputeInput,
  ): Promise<Result<Types.CommercePaymentsDisputeActionResult, ApiError>> {
    const url = `/api/v1/disputes/${disputeId}:accept`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsDisputesControllerAcceptDisputeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsDisputeActionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Cancel a dispute.
   *
   * Cancels an open dispute with a reason. Custom action per Google API guidelines.
   */
  async postDisputesCancel(disputeId: string, body: Types.CommercePaymentsDisputesControllerCancelDisputeInput): Promise<Result<void, ApiError>> {
    const url = `/api/v1/disputes/${disputeId}:cancel`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsDisputesControllerCancelDisputeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Challenge a dispute by submitting the merchant evidence to the gateway. Requires the
   * `payments:admin` permission.
   *
   * Submits the merchant evidence to the gateway to contest the chargeback. Requires the payments:admin permission.
   */
  async postDisputesChallenge(
    disputeId: string,
    body: Types.CommercePaymentsDisputesControllerChallengeDisputeInput,
  ): Promise<Result<Types.CommercePaymentsDisputeActionResult, ApiError>> {
    const url = `/api/v1/disputes/${disputeId}:challenge`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsDisputesControllerChallengeDisputeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsDisputeActionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Resolve a dispute with an explicit outcome.
   *
   * Resolves a dispute with an explicit outcome (won, lost, partial refund, ...). Custom action per Google API guidelines.
   */
  async postDisputesResolve(disputeId: string, body: Types.CommercePaymentsDisputesControllerResolveDisputeInput): Promise<Result<void, ApiError>> {
    const url = `/api/v1/disputes/${disputeId}:resolve`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsDisputesControllerResolveDisputeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Respond to a dispute by advancing its status (review, customer or merchant response).
   *
   * Advances a dispute's status (under review, pending customer or merchant response). Custom action per Google API guidelines.
   */
  async postDisputesRespond(disputeId: string, body: Types.CommercePaymentsDisputesControllerRespondDisputeInput): Promise<Result<void, ApiError>> {
    const url = `/api/v1/disputes/${disputeId}:respond`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsDisputesControllerRespondDisputeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Submit the dispute evidence bundle to the gateway: assembles every attached evidence
   * row into one bundle, delivers it through the gateway and records the submission status
   * on the dispute and each evidence item. Requires the `payments:admin` permission.
   *
   * Assembles every evidence row attached to the dispute into one bundle, submits it to the gateway and records the submission status. Requires the payments:admin permission.
   */
  async postDisputesSubmitEvidence(
    disputeId: string,
    body: Types.CommercePaymentsDisputesControllerSubmitDisputeEvidenceInput,
  ): Promise<Result<Types.CommercePaymentsDisputeActionResult, ApiError>> {
    const url = `/api/v1/disputes/${disputeId}:submit-evidence`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsDisputesControllerSubmitDisputeEvidenceInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsDisputeActionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * List the evidence attached to a dispute.
   *
   * Lists the evidence files attached to a payment dispute, newest first.
   */
  async getDisputesEvidence(disputeId: string): Promise<Result<Types.CommercePaymentsDisputeEvidence[], ApiError>> {
    const url = `/api/v1/disputes/${disputeId}/evidence`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.CommercePaymentsDisputeEvidence[], ApiError>;
  }

  /**
   * Attach evidence to a dispute. The submitting user is the authenticated actor.
   *
   * Attaches an evidence entry to a payment dispute. The submitting identity comes from the authenticated actor, never the request body.
   */
  async postDisputesEvidence(
    disputeId: string,
    body: Types.CommercePaymentsDisputesControllerAddDisputeEvidenceInput,
  ): Promise<Result<Types.CommercePaymentsDisputeEvidence, ApiError>> {
    const url = `/api/v1/disputes/${disputeId}/evidence`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsDisputesControllerAddDisputeEvidenceInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsDisputeEvidenceSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Dispute statistics for an inclusive window.
   *
   * Returns dispute counts by status, win/loss ratio for decided disputes and the average time to resolution for an inclusive window.
   */
  async getDisputesStatistics(query?: { fromUtc?: string; toUtc?: string }): Promise<Result<Types.CommercePaymentsDisputeStatistics, ApiError>> {
    const url = '/api/v1/disputes/statistics';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsDisputeStatisticsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommercePaymentsDisputesModule(client: ApiClient): CommercePaymentsDisputesModule {
  return new CommercePaymentsDisputesModule(client);
}
