/**
 * @game-guild/client - LearningExperienceSocialLikes Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningExperienceSocialLikesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Likes a course
   */
  async postApiSocialCoursesLike(courseId: string): Promise<Result<Types.LearningExperienceSocialServicesCourseLikeDto, ApiError>> {
    const url = `/api/social/courses/${courseId}/like`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesCourseLikeDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Unlikes a course
   */
  async deleteApiSocialCoursesLike(courseId: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/courses/${courseId}/like`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Checks if the current user has liked a course
   */
  async getApiSocialCoursesLikeCheck(courseId: string): Promise<Result<boolean, ApiError>> {
    const url = `/api/social/courses/${courseId}/like/check`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<boolean, ApiError>;
  }

  /**
   * Gets the like count for a course
   */
  async getApiSocialCoursesLikeCount(courseId: string): Promise<Result<number, ApiError>> {
    const url = `/api/social/courses/${courseId}/like/count`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    return result as Result<number, ApiError>;
  }

  /**
   * Gets the current user's liked courses
   */
  async getApiSocialLikesMe(query?: { skip?: number; take?: number }): Promise<Result<Types.LearningExperienceSocialServicesCourseLikeDto[], ApiError>> {
    const url = '/api/social/likes/me';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.LearningExperienceSocialServicesCourseLikeDto[], ApiError>;
  }
}

export function createLearningExperienceSocialLikesModule(client: ApiClient): LearningExperienceSocialLikesModule {
  return new LearningExperienceSocialLikesModule(client);
}
