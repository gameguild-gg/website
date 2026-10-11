/**
 * @game-guild/client - SocialFollowsFollowers Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class SocialFollowsFollowersModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Batch get follower counts for multiple entities
   */
  async postApiFollowersBatchCounts(body: Types.SocialFollowsControllersBatchCountsInput): Promise<Result<Record<string, number>, ApiError>> {
    const url = '/api/followers/batch/counts';

    // Validate request body
    const validatedBody = safeParse(Types.SocialFollowsControllersBatchCountsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    return result as Result<Record<string, number>, ApiError>;
  }

  /**
   * Batch get follow status for multiple entities
   */
  async postApiFollowersBatchStatus(body: Types.SocialFollowsControllersBatchStatusInput): Promise<Result<Record<string, boolean>, ApiError>> {
    const url = '/api/followers/batch/status';

    // Validate request body
    const validatedBody = safeParse(Types.SocialFollowsControllersBatchStatusInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<Record<string, boolean>, ApiError>;
  }

  /**
   * Block a user
   */
  async postApiFollowersBlock(body: Types.SocialFollowsControllersBlockInput): Promise<Result<Types.SocialFollowsControllersBlockDto, ApiError>> {
    const url = '/api/followers/block';

    // Validate request body
    const validatedBody = safeParse(Types.SocialFollowsControllersBlockInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialFollowsControllersBlockDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get current user's blocked users list
   */
  async getApiFollowersBlockedUsers(query?: { skip?: number; take?: number }): Promise<Result<Types.SocialFollowsControllersBlockDto[], ApiError>> {
    const url = '/api/followers/blocked-users';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.SocialFollowsControllersBlockDto[], ApiError>;
  }

  /**
   * Get follower count for an entity
   */
  async getApiFollowersCountFollowers(entityId: string, query?: { entityType?: string }): Promise<Result<number, ApiError>> {
    const url = `/api/followers/count/followers/${entityId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    return result as Result<number, ApiError>;
  }

  /**
   * Get following count for current user
   */
  async getApiFollowersCountFollowing(query?: { entityType?: string }): Promise<Result<number, ApiError>> {
    const url = '/api/followers/count/following';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<number, ApiError>;
  }

  /**
   * Follow an entity
   */
  async postApiFollowersFollow(body: Types.SocialFollowsControllersFollowInput): Promise<Result<Types.SocialFollowsControllersFollowDto, ApiError>> {
    const url = '/api/followers/follow';

    // Validate request body
    const validatedBody = safeParse(Types.SocialFollowsControllersFollowInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialFollowsControllersFollowDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get followers for an entity
   */
  async getApiFollowersFollowers(
    entityId: string,
    query?: { entityType?: string; skip?: number; take?: number },
  ): Promise<Result<Types.SocialFollowsControllersFollowDto[], ApiError>> {
    const url = `/api/followers/followers/${entityId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    return result as Result<Types.SocialFollowsControllersFollowDto[], ApiError>;
  }

  /**
   * Get entities the current user is following
   */
  async getApiFollowersFollowing(query?: {
    entityType?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.SocialFollowsControllersFollowDto[], ApiError>> {
    const url = '/api/followers/following';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.SocialFollowsControllersFollowDto[], ApiError>;
  }

  /**
   * Check if current user has blocked a user
   */
  async getApiFollowersIsBlocked(blockedUserId: string): Promise<Result<boolean, ApiError>> {
    const url = `/api/followers/is-blocked/${blockedUserId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<boolean, ApiError>;
  }

  /**
   * Check if current user is following an entity
   */
  async getApiFollowersIsFollowing(query?: { entityId?: string; entityType?: string }): Promise<Result<boolean, ApiError>> {
    const url = '/api/followers/is-following';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<boolean, ApiError>;
  }

  /**
   * Check if current user has muted a user
   */
  async getApiFollowersIsMuted(mutedUserId: string): Promise<Result<boolean, ApiError>> {
    const url = `/api/followers/is-muted/${mutedUserId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<boolean, ApiError>;
  }

  /**
   * Mute a user
   */
  async postApiFollowersMute(body: Types.SocialFollowsControllersMuteInput): Promise<Result<Types.SocialFollowsControllersMuteDto, ApiError>> {
    const url = '/api/followers/mute';

    // Validate request body
    const validatedBody = safeParse(Types.SocialFollowsControllersMuteInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialFollowsControllersMuteDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get current user's muted users list
   */
  async getApiFollowersMutedUsers(query?: { skip?: number; take?: number }): Promise<Result<Types.SocialFollowsControllersMuteDto[], ApiError>> {
    const url = '/api/followers/muted-users';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.SocialFollowsControllersMuteDto[], ApiError>;
  }

  /**
   * Check if two users are mutual followers
   */
  async getApiFollowersMutual(query?: { userId1?: string; userId2?: string }): Promise<Result<boolean, ApiError>> {
    const url = '/api/followers/mutual';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<boolean, ApiError>;
  }

  /**
   * Update notification settings for a follow relationship
   */
  async putApiFollowersNotifications(
    body: Types.SocialFollowsControllersUpdateNotificationsInput,
  ): Promise<Result<Types.SocialFollowsControllersFollowDto, ApiError>> {
    const url = '/api/followers/notifications';

    // Validate request body
    const validatedBody = safeParse(Types.SocialFollowsControllersUpdateNotificationsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialFollowsControllersFollowDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get current user's privacy settings
   */
  async getApiFollowersPrivacySettings(): Promise<Result<Types.SocialFollowsControllersFollowPrivacySettingsDto, ApiError>> {
    const url = '/api/followers/privacy-settings';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialFollowsControllersFollowPrivacySettingsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update current user's privacy settings
   */
  async putApiFollowersPrivacySettings(
    body: Types.SocialFollowsControllersUpdatePrivacySettingsInput,
  ): Promise<Result<Types.SocialFollowsControllersFollowPrivacySettingsDto, ApiError>> {
    const url = '/api/followers/privacy-settings';

    // Validate request body
    const validatedBody = safeParse(Types.SocialFollowsControllersUpdatePrivacySettingsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialFollowsControllersFollowPrivacySettingsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Unblock a user
   */
  async deleteApiFollowersUnblock(blockedUserId: string): Promise<Result<void, ApiError>> {
    const url = `/api/followers/unblock/${blockedUserId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Unfollow an entity
   */
  async deleteApiFollowersUnfollow(query?: { entityId?: string; entityType?: string }): Promise<Result<void, ApiError>> {
    const url = '/api/followers/unfollow';

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Unmute a user
   */
  async deleteApiFollowersUnmute(mutedUserId: string): Promise<Result<void, ApiError>> {
    const url = `/api/followers/unmute/${mutedUserId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createSocialFollowsFollowersModule(client: ApiClient): SocialFollowsFollowersModule {
  return new SocialFollowsFollowersModule(client);
}
