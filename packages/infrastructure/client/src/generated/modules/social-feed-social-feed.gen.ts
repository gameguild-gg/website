/**
 * @game-guild/client - SocialFeedSocialFeed Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class SocialFeedSocialFeedModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getApiSocialFeed(query?: {
    scope?: string;
    cursor?: string;
    take?: number;
    tag?: string;
  }): Promise<Result<Types.SocialFeedSocialFeedPageDto, ApiError>> {
    const url = '/api/social/feed';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialFeedSocialFeedPageDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiSocialFeedPosts(postId: string): Promise<Result<Types.SocialFeedSocialFeedItemDto, ApiError>> {
    const url = `/api/social/feed/posts/${postId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialFeedSocialFeedItemDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiSocialFeedProfiles(handle: string): Promise<Result<Types.SocialFeedSocialFeedProfileDto, ApiError>> {
    const url = `/api/social/feed/profiles/${handle}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialFeedSocialFeedProfileDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiSocialFeedProfilesUsers(userId: string): Promise<Result<Types.SocialFeedSocialFeedProfileDto, ApiError>> {
    const url = `/api/social/feed/profiles/users/${userId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialFeedSocialFeedProfileDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createSocialFeedSocialFeedModule(client: ApiClient): SocialFeedSocialFeedModule {
  return new SocialFeedSocialFeedModule(client);
}
