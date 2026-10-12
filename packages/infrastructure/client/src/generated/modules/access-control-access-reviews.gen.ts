/**
 * @game-guild/client - AccessControlAccessReviews Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AccessControlAccessReviewsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Create a new access review campaign
   */
  async postAccessReviewsCampaigns(
    body: Types.IdentityAuthorizationCommandsCreateAccessReviewCampaignCommand,
  ): Promise<Result<Types.IdentityAuthorizationAccessReviewCampaign, ApiError>> {
    const url = '/v1/access-reviews/campaigns';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationCommandsCreateAccessReviewCampaignCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationAccessReviewCampaignSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Process expired campaigns (admin only)
   */
  async postAccessReviewsCampaignsProcessExpired(): Promise<Result<number, ApiError>> {
    const url = '/v1/access-reviews/campaigns:process-expired';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<number, ApiError>;
  }

  /**
   * Get a campaign by ID
   */
  async getAccessReviewsCampaigns(id: string): Promise<Result<Types.IdentityAuthorizationAccessReviewCampaign, ApiError>> {
    const url = `/v1/access-reviews/campaigns/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationAccessReviewCampaignSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Cancel a campaign
   */
  async postAccessReviewsCampaignsCancel(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/access-reviews/campaigns/${id}:cancel`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Complete a campaign
   */
  async postAccessReviewsCampaignsComplete(id: string, body: Types.IdentityAuthorizationControllersCompleteCampaignInput): Promise<Result<void, ApiError>> {
    const url = `/v1/access-reviews/campaigns/${id}:complete`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationControllersCompleteCampaignInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Send reminders for a campaign
   */
  async postAccessReviewsCampaignsSendReminders(id: string): Promise<Result<number, ApiError>> {
    const url = `/v1/access-reviews/campaigns/${id}:send-reminders`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<number, ApiError>;
  }

  /**
   * Start a campaign
   */
  async postAccessReviewsCampaignsStart(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/access-reviews/campaigns/${id}:start`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get active campaigns
   */
  async getAccessReviewsCampaignsActive(query?: { tenantId?: string }): Promise<Result<Types.IdentityAuthorizationAccessReviewCampaign[], ApiError>> {
    const url = '/v1/access-reviews/campaigns/active';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityAuthorizationAccessReviewCampaign[], ApiError>;
  }

  /**
   * Approve an access review item
   */
  async postAccessReviewsItemsApprove(
    id: string,
    body: Types.IdentityAuthorizationControllersApproveItemInput,
  ): Promise<Result<Types.IdentityAuthorizationAccessReviewItem, ApiError>> {
    const url = `/v1/access-reviews/items/${id}:approve`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationControllersApproveItemInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationAccessReviewItemSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Revoke access for a review item
   */
  async postAccessReviewsItemsRevoke(
    id: string,
    body: Types.IdentityAuthorizationControllersRevokeItemInput,
  ): Promise<Result<Types.IdentityAuthorizationAccessReviewItem, ApiError>> {
    const url = `/v1/access-reviews/items/${id}:revoke`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationControllersRevokeItemInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationAccessReviewItemSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get pending review items for a reviewer
   */
  async getAccessReviewsItemsPending(query?: {
    reviewerId?: string;
    tenantId?: string;
  }): Promise<Result<Types.IdentityAuthorizationAccessReviewItem[], ApiError>> {
    const url = '/v1/access-reviews/items/pending';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityAuthorizationAccessReviewItem[], ApiError>;
  }
}

export function createAccessControlAccessReviewsModule(client: ApiClient): AccessControlAccessReviewsModule {
  return new AccessControlAccessReviewsModule(client);
}
