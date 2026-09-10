/**
 * @game-guild/client - SocialStories Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class SocialStoriesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getApiSocialStories(): Promise<Result<Array<Types.SocialFeedStoryDto>, ApiError>> {
    const url = '/api/social/stories';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.SocialFeedStoryDto>, ApiError>;
  }

  /**
   */
  async postApiSocialStories(body: Types.SocialFeedCreateStoryInput): Promise<Result<Types.SocialFeedStoryDto, ApiError>> {
    const url = '/api/social/stories';

    // Validate request body
    const validatedBody = safeParse(Types.SocialFeedCreateStoryInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialFeedStoryDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteApiSocialStories(storyId: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/stories/${storyId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postApiSocialStoriesViews(storyId: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/stories/${storyId}/views`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createSocialStoriesModule(client: ApiClient): SocialStoriesModule {
  return new SocialStoriesModule(client);
}
