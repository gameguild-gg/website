/**
 * @game-guild/client - CommerceSubscriptions Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceSubscriptionsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get subscriptions with pagination, search, and filtering
   *
   * Retrieves a paginated list of subscriptions with optional filtering. Use query parameters: status (active, trialing, cancelled, etc.), tenantId, planId, and expiring=true for expiring subscriptions.
   */
  async getSubscriptionsForGetSubscriptions(query?: {
    page?: number;
    pageSize?: number;
    status?: Types.CommerceSubscriptionsSubscriptionStatus;
    tenantId?: string;
    planId?: string;
    expiring?: boolean;
    expiringDays?: number;
  }): Promise<Result<void, ApiError>> {
    const url = '/api/v1/subscriptions';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Create a new subscription
   *
   * Creates a new subscription with the provided information.
   */
  async postSubscriptions(body: Types.CommerceSubscriptionsSubscriptionsControllerCreateSubscriptionInput): Promise<Result<void, ApiError>> {
    const url = '/api/v1/subscriptions';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionsControllerCreateSubscriptionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get subscription metrics
   *
   * Retrieves subscription metrics and analytics.
   */
  async getSubscriptionsGetMetrics(): Promise<Result<void, ApiError>> {
    const url = '/api/v1/subscriptions:get-metrics';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get subscription by ID
   *
   * Retrieves detailed information for a specific subscription.
   */
  async getSubscriptionsForGetSubscriptionsBySubscriptionId(subscriptionId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Full update subscription
   *
   * Performs a full replacement of subscription data. All fields will be updated.
   */
  async putSubscriptions(
    subscriptionId: string,
    body: Types.CommerceSubscriptionsSubscriptionsControllerPutSubscriptionInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionsControllerPutSubscriptionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Delete subscription
   *
   * Permanently deletes a subscription. Use cancel action for soft removal.
   */
  async deleteSubscriptions(subscriptionId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Partially update subscription
   *
   * Updates specific fields of a subscription. Only provided fields are updated.
   */
  async patchSubscriptions(
    subscriptionId: string,
    body: Types.CommerceSubscriptionsSubscriptionsControllerPatchSubscriptionInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionsControllerPatchSubscriptionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Check if subscription exists by ID
   *
   * Checks if a subscription exists by ID without returning the body.
   */
  async headSubscriptions(subscriptionId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}`;

    const result = await this.client.request({
      method: 'HEAD',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Activate subscription
   *
   * Activates a subscription by ID.
   */
  async postSubscriptionsActivate(subscriptionId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}:activate`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Set subscription auto-renew
   *
   * Enables or disables auto-renewal for a subscription.
   */
  async postSubscriptionsAutoRenew(
    subscriptionId: string,
    body: Types.CommerceSubscriptionsSubscriptionLifecycleControllerAutoRenewInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}:auto-renew`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionLifecycleControllerAutoRenewInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Cancel subscription
   *
   * Cancels a subscription with specified reason and effective date.
   */
  async postSubscriptionsCancel(
    subscriptionId: string,
    body: Types.CommerceSubscriptionsSubscriptionLifecycleControllerCancelInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}:cancel`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionLifecycleControllerCancelInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Downgrade subscription plan
   *
   * Downgrades a subscription to a lower-tier plan.
   */
  async postSubscriptionsDowngrade(
    subscriptionId: string,
    body: Types.CommerceSubscriptionsSubscriptionLifecycleControllerDowngradeInput,
  ): Promise<Result<Types.CommerceSubscriptionsSubscriptionDowngradeResult, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}:downgrade`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionLifecycleControllerDowngradeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceSubscriptionsSubscriptionDowngradeResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * End subscription trial
   *
   * Ends a trial period for a subscription.
   */
  async postSubscriptionsEndTrial(
    subscriptionId: string,
    body: Types.CommerceSubscriptionsSubscriptionLifecycleControllerEndTrialInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}:end-trial`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionLifecycleControllerEndTrialInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Set subscription external IDs
   *
   * Sets external system IDs for subscription integration.
   */
  async postSubscriptionsExternalIds(
    subscriptionId: string,
    body: Types.CommerceSubscriptionsSubscriptionLifecycleControllerExternalIdsInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}:external-ids`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionLifecycleControllerExternalIdsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Pause subscription billing
   *
   * Pauses billing for a subscription while keeping the subscription active. Useful for temporary payment holds.
   */
  async postSubscriptionsPause(
    subscriptionId: string,
    body: Types.CommerceSubscriptionsSubscriptionLifecycleControllerPauseSubscriptionInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}:pause`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionLifecycleControllerPauseSubscriptionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Reactivate subscription
   *
   * Reactivates a suspended or cancelled subscription.
   */
  async postSubscriptionsReactivate(subscriptionId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}:reactivate`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Renew subscription
   *
   * Manually renews a subscription for another billing cycle.
   */
  async postSubscriptionsRenew(subscriptionId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}:renew`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Resume subscription billing
   *
   * Resumes billing for a paused subscription.
   */
  async postSubscriptionsResume(subscriptionId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}:resume`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Start subscription trial
   *
   * Starts a trial period for a subscription.
   */
  async postSubscriptionsStartTrial(
    subscriptionId: string,
    body: Types.CommerceSubscriptionsSubscriptionLifecycleControllerStartTrialInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}:start-trial`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionLifecycleControllerStartTrialInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Suspend subscription
   *
   * Suspends a subscription temporarily.
   */
  async postSubscriptionsSuspend(
    subscriptionId: string,
    body: Types.CommerceSubscriptionsSubscriptionLifecycleControllerSuspendInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}:suspend`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionLifecycleControllerSuspendInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Upgrade subscription plan
   *
   * Upgrades a subscription to a higher-tier plan.
   */
  async postSubscriptionsUpgrade(
    subscriptionId: string,
    body: Types.CommerceSubscriptionsSubscriptionLifecycleControllerUpgradeInput,
  ): Promise<Result<Types.CommerceSubscriptionsSubscriptionUpgradeResult, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}:upgrade`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionLifecycleControllerUpgradeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceSubscriptionsSubscriptionUpgradeResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get subscription billing history
   *
   * Retrieves billing history for a specific subscription.
   */
  async getSubscriptionsBillingHistory(subscriptionId: string): Promise<Result<Array<Types.CommerceSubscriptionsBillingHistoryDto>, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}/billing-history`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.CommerceSubscriptionsBillingHistoryDto>, ApiError>;
  }

  /**
   * Get subscription invoices
   *
   * Retrieves the invoice history for a specific subscription.
   */
  async getSubscriptionsInvoices(subscriptionId: string, query?: { page?: number; pageSize?: number }): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}/invoices`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get subscription usage and limits
   *
   * Retrieves usage information and limits for a specific subscription.
   */
  async getSubscriptionsUsage(subscriptionId: string): Promise<Result<Types.CommerceSubscriptionsSubscriptionUsageDto, ApiError>> {
    const url = `/api/v1/subscriptions/${subscriptionId}/usage`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceSubscriptionsSubscriptionUsageDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommerceSubscriptionsModule(client: ApiClient): CommerceSubscriptionsModule {
  return new CommerceSubscriptionsModule(client);
}
