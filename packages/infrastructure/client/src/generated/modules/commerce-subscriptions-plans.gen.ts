/**
 * @game-guild/client - CommerceSubscriptionsPlans Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceSubscriptionsPlansModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Activate subscription plan
   *
   * Activates a subscription plan by ID.
   */
  async postSubscriptionPlansActivate(planId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscription-plans/${planId}:activate`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Archive subscription plan
   *
   * Archives a subscription plan, making it unavailable for new subscriptions while preserving existing subscriptions.
   */
  async postSubscriptionPlansArchive(planId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscription-plans/${planId}:archive`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Clone subscription plan
   *
   * Creates a copy of an existing subscription plan with a new name and slug.
   */
  async postSubscriptionPlansClone(
    planId: string,
    body: Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerCloneSubscriptionPlanInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscription-plans/${planId}:clone`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerCloneSubscriptionPlanInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Deactivate subscription plan
   *
   * Deactivates a subscription plan by ID.
   */
  async postSubscriptionPlansDeactivate(planId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscription-plans/${planId}:deactivate`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Set subscription plan external ID
   *
   * Sets the external system ID for subscription plan integration.
   */
  async postSubscriptionPlansExternalId(
    planId: string,
    body: Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerSetExternalIdInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscription-plans/${planId}:external-id`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerSetExternalIdInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Set subscription plan featured status
   *
   * Sets whether a subscription plan is featured or not.
   */
  async postSubscriptionPlansFeatured(
    planId: string,
    body: Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerSetFeaturedInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscription-plans/${planId}:featured`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerSetFeaturedInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Validate subscription plan limits
   *
   * Validates whether the specified usage fits within the plan limits. Custom action per Google API guidelines.
   */
  async postSubscriptionPlansValidateLimits(
    planId: string,
    body: Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerValidateLimitsInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscription-plans/${planId}:validate-limits`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerValidateLimitsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Partially update subscription plan details
   *
   * Updates specific fields of a subscription plan's details.
   */
  async patchSubscriptionPlansDetails(
    planId: string,
    body: Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateDetailsInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscription-plans/${planId}/details`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateDetailsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Update subscription plan features
   *
   * Updates the features for a subscription plan.
   */
  async patchSubscriptionPlansFeatures(
    planId: string,
    body: Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateFeaturesInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscription-plans/${planId}/features`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateFeaturesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Update subscription plan limits
   *
   * Updates the limits for a subscription plan.
   */
  async patchSubscriptionPlansLimits(
    planId: string,
    body: Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateLimitsInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscription-plans/${planId}/limits`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateLimitsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Calculate pricing for a subscription plan
   *
   * Calculates the total cost for a subscription plan including all applicable taxes, fees, and discounts.
   */
  async getSubscriptionPlansPricing(planId: string, query?: { tenantId?: string; discountCode?: string }): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscription-plans/${planId}/pricing`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Update subscription plan pricing
   *
   * Updates the pricing for a subscription plan.
   */
  async patchSubscriptionPlansPricing(
    planId: string,
    body: Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdatePricingInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscription-plans/${planId}/pricing`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdatePricingInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get suggested plan upgrades
   *
   * Suggests upgrade plans based on current usage requirements.
   */
  async getSubscriptionPlansSuggestUpgrades(
    planId: string,
    query?: { users?: number; storageMb?: number; apiCalls?: number },
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscription-plans/${planId}/suggest-upgrades`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get subscription plan usage statistics
   *
   * Retrieves usage statistics for a specific subscription plan.
   */
  async getSubscriptionPlansUsage(planId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/subscription-plans/${planId}/usage`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get subscription plans with pagination and filtering
   *
   * Retrieves a paginated list of subscription plans with optional filtering. Use query parameters: featured=true for featured plans, q=searchTerm for search, slug=value for slug lookup, minPrice/maxPrice for price range.
   */
  async getSubscriptionPlansForGetSubscriptionPlans(query?: {
    page?: number;
    pageSize?: number;
    activeOnly?: boolean;
    isActive?: boolean;
    featured?: boolean;
    q?: string;
    slug?: string;
    minPrice?: number;
    maxPrice?: number;
  }): Promise<Result<void, ApiError>> {
    const url = '/v1/subscription-plans';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Create a new subscription plan
   *
   * Creates a new subscription plan with the provided information.
   */
  async postSubscriptionPlans(body: Types.CommerceSubscriptionsSubscriptionPlansCrudControllerCreatePlanInput): Promise<Result<void, ApiError>> {
    const url = '/v1/subscription-plans';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionPlansCrudControllerCreatePlanInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Compare subscription plans
   *
   * Compares multiple subscription plans side by side. Custom action per Google API guidelines.
   */
  async postSubscriptionPlansCompare(body: Types.CommerceSubscriptionsSubscriptionPlansCrudControllerComparePlansInput): Promise<Result<void, ApiError>> {
    const url = '/v1/subscription-plans:compare';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionPlansCrudControllerComparePlansInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get subscription plan by ID
   *
   * Retrieves detailed information for a specific subscription plan.
   */
  async getSubscriptionPlansForGetSubscriptionPlansByPlanId(planId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/subscription-plans/${planId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Full update of a subscription plan
   *
   * Performs a full replacement of subscription plan data. All fields will be updated.
   */
  async putSubscriptionPlans(
    planId: string,
    body: Types.CommerceSubscriptionsSubscriptionPlansCrudControllerPutSubscriptionPlanInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/v1/subscription-plans/${planId}`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionPlansCrudControllerPutSubscriptionPlanInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Delete subscription plan
   *
   * Deletes a subscription plan by ID.
   */
  async deleteSubscriptionPlans(planId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/subscription-plans/${planId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Check if subscription plan exists by ID
   *
   * Checks if a subscription plan exists by ID without returning the body.
   */
  async headSubscriptionPlans(planId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/subscription-plans/${planId}`;

    const result = await this.client.request({
      method: 'HEAD',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createCommerceSubscriptionsPlansModule(client: ApiClient): CommerceSubscriptionsPlansModule {
  return new CommerceSubscriptionsPlansModule(client);
}
