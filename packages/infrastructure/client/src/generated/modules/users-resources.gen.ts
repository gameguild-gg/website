/**
 * @game-guild/client - UsersResources Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class UsersResourcesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Record resource usage for a user
   *
   * Records a new resource usage entry for the specified user.
   */
  async postUsersResourcesRecord(userId: string, body: Types.ResourcesRecordUserResourceUsageInput): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/resources:record`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesRecordUserResourceUsageInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Record resource usage with quota enforcement for a user
   *
   * Records a new resource usage entry after verifying it doesn't exceed configured quotas. Returns 429 if quota would be exceeded.
   */
  async postUsersResourcesRecordWithQuotaCheck(userId: string, body: Types.ResourcesRecordUserResourceUsageInput): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/resources:record-with-quota-check`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesRecordUserResourceUsageInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Reset resource usage for a user
   *
   * Resets the resource usage counters for a specific user and resource type to zero.
   */
  async postUsersResourcesReset(userId: string, query?: { usageType?: Types.ResourcesResourceUsageType }): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/resources:reset`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Check resource limits for a user
   *
   * Checks current resource usage against configured limits for a specific user.
   */
  async getUsersResourcesLimits(
    userId: string,
    query?: { usageType?: Types.ResourcesResourceUsageType },
  ): Promise<
    Result<
      {
        AbacPolicies?: boolean;
        AccessReviewCampaigns?: boolean;
        AiRequests?: boolean;
        AiTokens?: boolean;
        ApiCalls?: boolean;
        AssetDownloads?: boolean;
        Assets?: boolean;
        AssetStorage?: boolean;
        AssetTransformations?: boolean;
        AuditEntries?: boolean;
        ConditionalPolicies?: boolean;
        Courses?: boolean;
        Disputes?: boolean;
        FeatureFlags?: boolean;
        Orders?: boolean;
        Products?: boolean;
        Programs?: boolean;
        Projects?: boolean;
        PromoCodes?: boolean;
        Roles?: boolean;
        SLOs?: boolean;
        SoDRules?: boolean;
        Storage?: boolean;
        SubscriptionPlans?: boolean;
        Subscriptions?: boolean;
        Teams?: boolean;
        Tenants?: boolean;
        TestingSessions?: boolean;
        Users?: boolean;
        Wallets?: boolean;
      },
      ApiError
    >
  > {
    const url = `/v1/users/${userId}/resources/limits`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<
      {
        AbacPolicies?: boolean;
        AccessReviewCampaigns?: boolean;
        AiRequests?: boolean;
        AiTokens?: boolean;
        ApiCalls?: boolean;
        AssetDownloads?: boolean;
        Assets?: boolean;
        AssetStorage?: boolean;
        AssetTransformations?: boolean;
        AuditEntries?: boolean;
        ConditionalPolicies?: boolean;
        Courses?: boolean;
        Disputes?: boolean;
        FeatureFlags?: boolean;
        Orders?: boolean;
        Products?: boolean;
        Programs?: boolean;
        Projects?: boolean;
        PromoCodes?: boolean;
        Roles?: boolean;
        SLOs?: boolean;
        SoDRules?: boolean;
        Storage?: boolean;
        SubscriptionPlans?: boolean;
        Subscriptions?: boolean;
        Teams?: boolean;
        Tenants?: boolean;
        TestingSessions?: boolean;
        Users?: boolean;
        Wallets?: boolean;
      },
      ApiError
    >;
  }

  /**
   * Get usage records for a user
   *
   * Retrieves resource usage records for a specific user with optional filtering by type and date range.
   */
  async getUsersResourcesUsageRecords(
    userId: string,
    query?: { usageType?: Types.ResourcesResourceUsageType; startDate?: string; endDate?: string },
  ): Promise<Result<Array<Types.ResourcesUsageRecord>, ApiError>> {
    const url = `/v1/users/${userId}/resources/usage-records`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.ResourcesUsageRecord>, ApiError>;
  }

  /**
   * Get current usage summary for a user
   *
   * Retrieves the current aggregated resource usage summary for a specific user.
   */
  async getUsersResourcesUsageSummary(
    userId: string,
  ): Promise<
    Result<
      {
        AbacPolicies?: number;
        AccessReviewCampaigns?: number;
        AiRequests?: number;
        AiTokens?: number;
        ApiCalls?: number;
        AssetDownloads?: number;
        Assets?: number;
        AssetStorage?: number;
        AssetTransformations?: number;
        AuditEntries?: number;
        ConditionalPolicies?: number;
        Courses?: number;
        Disputes?: number;
        FeatureFlags?: number;
        Orders?: number;
        Products?: number;
        Programs?: number;
        Projects?: number;
        PromoCodes?: number;
        Roles?: number;
        SLOs?: number;
        SoDRules?: number;
        Storage?: number;
        SubscriptionPlans?: number;
        Subscriptions?: number;
        Teams?: number;
        Tenants?: number;
        TestingSessions?: number;
        Users?: number;
        Wallets?: number;
      },
      ApiError
    >
  > {
    const url = `/v1/users/${userId}/resources/usage-summary`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<
      {
        AbacPolicies?: number;
        AccessReviewCampaigns?: number;
        AiRequests?: number;
        AiTokens?: number;
        ApiCalls?: number;
        AssetDownloads?: number;
        Assets?: number;
        AssetStorage?: number;
        AssetTransformations?: number;
        AuditEntries?: number;
        ConditionalPolicies?: number;
        Courses?: number;
        Disputes?: number;
        FeatureFlags?: number;
        Orders?: number;
        Products?: number;
        Programs?: number;
        Projects?: number;
        PromoCodes?: number;
        Roles?: number;
        SLOs?: number;
        SoDRules?: number;
        Storage?: number;
        SubscriptionPlans?: number;
        Subscriptions?: number;
        Teams?: number;
        Tenants?: number;
        TestingSessions?: number;
        Users?: number;
        Wallets?: number;
      },
      ApiError
    >;
  }
}

export function createUsersResourcesModule(client: ApiClient): UsersResourcesModule {
  return new UsersResourcesModule(client);
}
