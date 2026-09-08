/**
 * @game-guild/client - SocialFeed Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class SocialFeedModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async postApiSocialFeed(body: Types.SocialFeedAddFeedItemInput): Promise<Result<Types.SocialFeedFeedItemDto, ApiError>> {
    const url = '/api/social/feed';

    // Validate request body
    const validatedBody = safeParse(Types.SocialFeedAddFeedItemInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialFeedFeedItemDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postApiSocialFeedHide(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/feed/${id}/hide`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postApiSocialFeedRead(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/feed/${id}/read`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getApiSocialFeedUsers(
    userId: string,
    query?: { skip?: number; take?: number; includeRead?: boolean },
  ): Promise<Result<Array<Types.SocialFeedFeedItemDto>, ApiError>> {
    const url = `/api/social/feed/users/${userId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.SocialFeedFeedItemDto>, ApiError>;
  }
}

export function createSocialFeedModule(client: ApiClient): SocialFeedModule {
  return new SocialFeedModule(client);
}
