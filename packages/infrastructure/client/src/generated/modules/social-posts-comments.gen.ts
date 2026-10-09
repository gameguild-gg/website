/**
 * @game-guild/client - SocialPostsComments Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class SocialPostsCommentsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get comments for a post
   */
  async getPostsComments(postId: string, query?: { skip?: number; take?: number; parentCommentId?: string }): Promise<Result<void, ApiError>> {
    const url = `/api/v1/posts/${postId}/comments`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Add a comment to a post
   */
  async postPostsComments(postId: string, body: Types.SocialPostsControllersAddCommentInput): Promise<Result<void, ApiError>> {
    const url = `/api/v1/posts/${postId}/comments`;

    // Validate request body
    const validatedBody = safeParse(Types.SocialPostsControllersAddCommentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Update a comment
   */
  async putPostsComments(postId: string, commentId: string, body: Types.SocialPostsControllersUpdateCommentInput): Promise<Result<void, ApiError>> {
    const url = `/api/v1/posts/${postId}/comments/${commentId}`;

    // Validate request body
    const validatedBody = safeParse(Types.SocialPostsControllersUpdateCommentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Delete a comment
   */
  async deletePostsComments(postId: string, commentId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/posts/${postId}/comments/${commentId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get tags for a post
   */
  async getPostsTags(postId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/posts/${postId}/tags`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get popular tags
   */
  async getPostsTagsPopular(query?: { count?: number }): Promise<Result<void, ApiError>> {
    const url = '/api/v1/posts/tags/popular';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Search posts by tags
   */
  async getPostsTagsSearch(query?: { tags?: string[]; skip?: number; take?: number }): Promise<Result<void, ApiError>> {
    const url = '/api/v1/posts/tags/search';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createSocialPostsCommentsModule(client: ApiClient): SocialPostsCommentsModule {
  return new SocialPostsCommentsModule(client);
}
