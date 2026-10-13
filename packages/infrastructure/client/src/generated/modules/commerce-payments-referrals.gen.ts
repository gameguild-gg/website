/**
 * @game-guild/client - CommercePaymentsReferrals Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommercePaymentsReferralsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Gets the caller's referral code and trackable link, creating them on first use.
   *
   * Returns the calling user's opaque unique referral code and the trackable share link built from it (AccountCredits:ReferralLinkBaseUrl base). Idempotent: every call returns the same per-user code.
   */
  async getReferralsCode(): Promise<Result<Types.CommercePaymentsReferralsControllerReferralCodeOutput, ApiError>> {
    const url = '/api/v1/referrals/code';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsReferralsControllerReferralCodeOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Tracking rows of the caller's referrals, newest first.
   *
   * Returns a paged list of the calling user's referral tracking rows, newest first, with funnel timestamps and status.
   */
  async getReferralsHistory(query?: { skip?: number; take?: number }): Promise<Result<Types.PagedResultReferral, ApiError>> {
    const url = '/api/v1/referrals/history';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultReferralSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Funnel stats of the caller's referrals.
   *
   * Returns the calling user's referral funnel counters: shared rows, clicks, signups, conversions (first paid invoice), rewards issued, fraudulent blocks and expiries.
   */
  async getReferralsStats(): Promise<Result<Types.CommercePaymentsReferralStats, ApiError>> {
    const url = '/api/v1/referrals/stats';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsReferralStatsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Records a referral link click for funnel tracking.
   *
   * Records a click against a referral code (first click time kept on idempotent replays). The caller IP is hashed (SHA-256) for the same-IP fraud block and never persisted in clear.
   */
  async postReferralsTrackClick(
    body: Types.CommercePaymentsReferralsControllerTrackReferralClickInput,
  ): Promise<Result<Types.CommercePaymentsReferral, ApiError>> {
    const url = '/api/v1/referrals/track-click';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsReferralsControllerTrackReferralClickInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsReferralSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * The signed-up caller applies a referral code to their account.
   *
   * Attaches the calling user as the referee of the referral code. Fail-closed blocks: self-referral, referral cap (AccountCredits:MaxReferralsPerUser) and same-IP repeats under the same referrer are rejected or recorded as fraudulent without reward eligibility.
   */
  async postReferralsTrackSignup(
    body: Types.CommercePaymentsReferralsControllerTrackReferralSignupInput,
  ): Promise<Result<Types.CommercePaymentsReferralSignupResult, ApiError>> {
    const url = '/api/v1/referrals/track-signup';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsReferralsControllerTrackReferralSignupInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsReferralSignupResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommercePaymentsReferralsModule(client: ApiClient): CommercePaymentsReferralsModule {
  return new CommercePaymentsReferralsModule(client);
}
