/**
 * @game-guild/client - SocialPostsInteractions Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class SocialPostsInteractionsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getPostsFollow(postId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/posts/${postId}/follow`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postPostsFollow(postId: string, body: Types.SocialPostsControllersFollowPostInput): Promise<Result<void, ApiError>> {
    const url = `/api/v1/posts/${postId}/follow`;

    // Validate request body
    const validatedBody = safeParse(Types.SocialPostsControllersFollowPostInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async deletePostsFollow(postId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/posts/${postId}/follow`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postPostsLike(postId: string, query?: { reactionType?: string }): Promise<Result<void, ApiError>> {
    const url = `/api/v1/posts/${postId}/like`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postPostsPin(postId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/posts/${postId}/pin`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postPostsReposts(postId: string, body: Types.SocialPostsControllersCreateRepostInput): Promise<Result<void, ApiError>> {
    const url = `/api/v1/posts/${postId}/reposts`;

    // Validate request body
    const validatedBody = safeParse(Types.SocialPostsControllersCreateRepostInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postPostsShare(postId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/posts/${postId}/share`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getPostsStatistics(postId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/posts/${postId}/statistics`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postPostsView(postId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/posts/${postId}/view`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createSocialPostsInteractionsModule(client: ApiClient): SocialPostsInteractionsModule {
  return new SocialPostsInteractionsModule(client);
}
