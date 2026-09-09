/**
 * @game-guild/client - CommerceSubscriptionsBillingSubscriptions Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceSubscriptionsBillingSubscriptionsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List billing subscriptions
   *
   * Compatibility billing endpoint backed by the subscription query model.
   */
  async getBillingSubscriptions(query?: {
    tenantId?: string;
    status?: Types.CommerceSubscriptionsSubscriptionStatus;
    planId?: string;
    page?: number;
    pageSize?: number;
  }): Promise<Result<Types.PagedResultSubscription, ApiError>> {
    const url = '/api/v1/billing/subscriptions';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultSubscriptionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Create billing subscription
   */
  async postBillingSubscriptions(
    body: Types.CommerceSubscriptionsBillingSubscriptionsControllerCreateBillingSubscriptionInput,
  ): Promise<Result<void, ApiError>> {
    const url = '/api/v1/billing/subscriptions';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsBillingSubscriptionsControllerCreateBillingSubscriptionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get billing subscription
   */
  async getBillingSubscriptionById(subscriptionId: string): Promise<Result<Types.CommerceSubscriptionsSubscription, ApiError>> {
    const url = `/api/v1/billing/subscriptions/${subscriptionId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceSubscriptionsSubscriptionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Cancel billing subscription
   */
  async postBillingSubscriptionsCancel(
    subscriptionId: string,
    body: Types.CommerceSubscriptionsBillingSubscriptionsControllerCancelBillingSubscriptionInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/billing/subscriptions/${subscriptionId}:cancel`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsBillingSubscriptionsControllerCancelBillingSubscriptionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Renew billing subscription
   */
  async postBillingSubscriptionsRenew(subscriptionId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/billing/subscriptions/${subscriptionId}:renew`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createCommerceSubscriptionsBillingSubscriptionsModule(client: ApiClient): CommerceSubscriptionsBillingSubscriptionsModule {
  return new CommerceSubscriptionsBillingSubscriptionsModule(client);
}
