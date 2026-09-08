/**
 * @game-guild/client - SocialBlogPosts Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class SocialBlogPostsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getApiSocialBlogForGetApiSocialBlog(query?: {
    authorId?: string;
    status?: Types.SocialBlogBlogPostStatus;
    featured?: boolean;
    skip?: number;
    take?: number;
  }): Promise<Result<Array<Types.SocialBlogBlogPostDto>, ApiError>> {
    const url = '/api/social/blog';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.SocialBlogBlogPostDto>, ApiError>;
  }

  /**
   */
  async postApiSocialBlog(body: Types.SocialBlogCreateBlogPostInput): Promise<Result<Types.SocialBlogBlogPostDto, ApiError>> {
    const url = '/api/social/blog';

    // Validate request body
    const validatedBody = safeParse(Types.SocialBlogCreateBlogPostInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogBlogPostDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiSocialBlogForGetApiSocialBlogById(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/blog/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postApiSocialBlogFeature(id: string, query?: { featured?: boolean }): Promise<Result<void, ApiError>> {
    const url = `/api/social/blog/${id}/feature`;

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
  async postApiSocialBlogPublish(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/blog/${id}/publish`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postApiSocialBlogUnpublish(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/blog/${id}/unpublish`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postApiSocialBlogViews(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/blog/${id}/views`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createSocialBlogPostsModule(client: ApiClient): SocialBlogPostsModule {
  return new SocialBlogPostsModule(client);
}
