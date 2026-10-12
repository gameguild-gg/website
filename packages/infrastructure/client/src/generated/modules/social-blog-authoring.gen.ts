/**
 * @game-guild/client - SocialBlogAuthoring Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class SocialBlogAuthoringModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Creates a draft post; the actor becomes the primary author.
   */
  async postApiSocialBlogPosts(body: Types.SocialBlogControllersCreateBlogPostInput): Promise<Result<Types.SocialBlogBlogPost, ApiError>> {
    const url = '/api/social/blog/posts';

    // Validate request body
    const validatedBody = safeParse(Types.SocialBlogControllersCreateBlogPostInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogBlogPostSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Fetches one post for an author (primary or co-author) with full draft access.
   */
  async getApiSocialBlogPosts(id: string): Promise<Result<Types.SocialBlogBlogPost, ApiError>> {
    const url = `/api/social/blog/posts/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogBlogPostSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Revision-guarded draft update; any author (primary or co-author). Stale revision → 409.
   */
  async putApiSocialBlogPosts(id: string, body: Types.SocialBlogControllersUpdateBlogPostDraftInput): Promise<Result<Types.SocialBlogBlogPost, ApiError>> {
    const url = `/api/social/blog/posts/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.SocialBlogControllersUpdateBlogPostDraftInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogBlogPostSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Soft-deletes the post (primary only).
   */
  async deleteApiSocialBlogPosts(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/blog/posts/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Adds a co-author (primary only).
   */
  async postApiSocialBlogPostsCoauthors(id: string, body: Types.SocialBlogControllersBlogCoauthorInput): Promise<Result<void, ApiError>> {
    const url = `/api/social/blog/posts/${id}/coauthors`;

    // Validate request body
    const validatedBody = safeParse(Types.SocialBlogControllersBlogCoauthorInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Removes a co-author (primary only).
   */
  async deleteApiSocialBlogPostsCoauthors(id: string, userId: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/blog/posts/${id}/coauthors/${userId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Publishes the post (primary only); fans out the publication announcement.
   */
  async postApiSocialBlogPostsPublish(id: string): Promise<Result<Types.SocialBlogBlogPost, ApiError>> {
    const url = `/api/social/blog/posts/${id}/publish`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogBlogPostSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Changes the post slug (primary only); the old route 301-redirects forever.
   */
  async postApiSocialBlogPostsSlug(id: string, body: Types.SocialBlogControllersChangeBlogPostSlugInput): Promise<Result<Types.SocialBlogBlogPost, ApiError>> {
    const url = `/api/social/blog/posts/${id}/slug`;

    // Validate request body
    const validatedBody = safeParse(Types.SocialBlogControllersChangeBlogPostSlugInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogBlogPostSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Transfers primary authorship to a current co-author (primary only).
   */
  async postApiSocialBlogPostsTransferPrimary(
    id: string,
    body: Types.SocialBlogControllersTransferBlogPrimaryInput,
  ): Promise<Result<Types.SocialBlogBlogPost, ApiError>> {
    const url = `/api/social/blog/posts/${id}/transfer-primary`;

    // Validate request body
    const validatedBody = safeParse(Types.SocialBlogControllersTransferBlogPrimaryInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogBlogPostSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Unpublishes the post back to draft (primary only).
   */
  async postApiSocialBlogPostsUnpublish(id: string): Promise<Result<Types.SocialBlogBlogPost, ApiError>> {
    const url = `/api/social/blog/posts/${id}/unpublish`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogBlogPostSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lists the acting user's posts (authored + co-authored), newest edit first.
   */
  async getApiSocialBlogPostsMine(query?: { page?: number }): Promise<Result<Types.SocialBlogBlogPost[], ApiError>> {
    const url = '/api/social/blog/posts/mine';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.SocialBlogBlogPost[], ApiError>;
  }
}

export function createSocialBlogAuthoringModule(client: ApiClient): SocialBlogAuthoringModule {
  return new SocialBlogAuthoringModule(client);
}
