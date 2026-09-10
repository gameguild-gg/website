/**
 * @game-guild/client - SocialSavedPosts Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class SocialSavedPostsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getApiSocialSavedPosts(postId: string): Promise<Result<Types.SocialFeedSavedPostStateDto, ApiError>> {
    const url = `/api/social/saved-posts/${postId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialFeedSavedPostStateDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putApiSocialSavedPosts(postId: string): Promise<Result<Types.SocialFeedSavedPostStateDto, ApiError>> {
    const url = `/api/social/saved-posts/${postId}`;

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialFeedSavedPostStateDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteApiSocialSavedPosts(postId: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/saved-posts/${postId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createSocialSavedPostsModule(client: ApiClient): SocialSavedPostsModule {
  return new SocialSavedPostsModule(client);
}
