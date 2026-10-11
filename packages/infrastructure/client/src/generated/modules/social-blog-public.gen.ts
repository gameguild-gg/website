/**
 * @game-guild/client - SocialBlogPublic Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class SocialBlogPublicModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Public author listing by handle (published only), keyset-paged newest-first.
   */
  async getApiSocialBlogPublicAuthorsForGetApiSocialBlogPublicAuthorsByHandle(
    handle: string,
    query?: { beforePublishedAt?: string; beforeId?: string },
  ): Promise<Result<Types.SocialBlogQueriesBlogPostSummaryPage, ApiError>> {
    const url = `/api/social/blog/public/authors/${handle}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogQueriesBlogPostSummaryPageSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Public post detail by (handle, slug); unpublished/missing → indistinguishable 404.
   */
  async getApiSocialBlogPublicAuthorsForGetApiSocialBlogPublicAuthorsByHandleBySlug(
    handle: string,
    slug: string,
  ): Promise<Result<Types.SocialBlogQueriesBlogPostDetailDto, ApiError>> {
    const url = `/api/social/blog/public/authors/${handle}/${slug}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogQueriesBlogPostDetailDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Global public blog index (published only), keyset-paged newest-first.
   */
  async getApiSocialBlogPublicPosts(query?: {
    beforePublishedAt?: string;
    beforeId?: string;
  }): Promise<Result<Types.SocialBlogQueriesBlogPostSummaryPage, ApiError>> {
    const url = '/api/social/blog/public/posts';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogQueriesBlogPostSummaryPageSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Public comments of a published post, oldest-first; unpublished/missing post → 404.
   */
  async getApiSocialBlogPublicPostsComments(
    id: string,
    query?: { afterCreatedAt?: string; afterId?: string },
  ): Promise<Result<Types.SocialBlogQueriesBlogCommentPage, ApiError>> {
    const url = `/api/social/blog/public/posts/${id}/comments`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogQueriesBlogCommentPageSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * View beacon: atomically increments the published post's counter; PerIp rate-limited.
   */
  async postApiSocialBlogPublicPostsViews(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/blog/public/posts/${id}/views`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: false,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Resolves a possibly-stale (handle, slug) route to the canonical route, or 404.
   */
  async getApiSocialBlogPublicResolve(handle: string, slug: string): Promise<Result<Types.SocialBlogQueriesBlogRouteResolutionDto, ApiError>> {
    const url = `/api/social/blog/public/resolve/${handle}/${slug}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogQueriesBlogRouteResolutionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createSocialBlogPublicModule(client: ApiClient): SocialBlogPublicModule {
  return new SocialBlogPublicModule(client);
}
