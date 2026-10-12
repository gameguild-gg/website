/**
 * @game-guild/client - LearningExperienceSocialWishlists Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningExperienceSocialWishlistsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Adds a course to the current user's wishlist
   */
  async postApiSocialWishlist(
    courseId: string,
    query?: { notifyOnSale?: boolean; notifyOnUpdate?: boolean },
  ): Promise<Result<Types.LearningExperienceSocialServicesCourseWishlistDto, ApiError>> {
    const url = `/api/social/wishlist/${courseId}`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesCourseWishlistDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Removes a course from the current user's wishlist
   */
  async deleteApiSocialWishlist(courseId: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/wishlist/${courseId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Checks if a course is in the current user's wishlist
   */
  async getApiSocialWishlistCheck(courseId: string): Promise<Result<boolean, ApiError>> {
    const url = `/api/social/wishlist/${courseId}/check`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<boolean, ApiError>;
  }

  /**
   * Updates wishlist notification preferences
   */
  async putApiSocialWishlistPreferences(
    courseId: string,
    body: Types.LearningExperienceSocialServicesWishlistPreferencesInput,
  ): Promise<Result<Types.LearningExperienceSocialServicesCourseWishlistDto, ApiError>> {
    const url = `/api/social/wishlist/${courseId}/preferences`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceSocialServicesWishlistPreferencesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesCourseWishlistDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Gets the current user's wishlist
   */
  async getApiSocialWishlistMe(query?: { skip?: number; take?: number }): Promise<Result<Types.LearningExperienceSocialServicesCourseWishlistDto[], ApiError>> {
    const url = '/api/social/wishlist/me';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.LearningExperienceSocialServicesCourseWishlistDto[], ApiError>;
  }
}

export function createLearningExperienceSocialWishlistsModule(client: ApiClient): LearningExperienceSocialWishlistsModule {
  return new LearningExperienceSocialWishlistsModule(client);
}
