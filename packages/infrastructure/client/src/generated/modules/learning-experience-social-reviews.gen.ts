/**
 * @game-guild/client - LearningExperienceSocialReviews Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningExperienceSocialReviewsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Gets rating statistics for a course
   */
  async getApiSocialCoursesRatingStats(courseId: string): Promise<Result<Types.LearningExperienceSocialServicesCourseRatingStats, ApiError>> {
    const url = `/api/social/courses/${courseId}/rating-stats`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesCourseRatingStatsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Gets all reviews for a course
   */
  async getApiSocialCoursesReviews(
    courseId: string,
    query?: { skip?: number; take?: number; approvedOnly?: boolean },
  ): Promise<Result<Types.LearningExperienceSocialServicesCourseReviewDto[], ApiError>> {
    const url = `/api/social/courses/${courseId}/reviews`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    return result as Result<Types.LearningExperienceSocialServicesCourseReviewDto[], ApiError>;
  }

  /**
   * Creates a new course review
   */
  async postApiSocialReviews(
    body: Types.LearningExperienceSocialServicesCreateReviewInput,
  ): Promise<Result<Types.LearningExperienceSocialServicesCourseReviewDto, ApiError>> {
    const url = '/api/social/reviews';

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceSocialServicesCreateReviewInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesCourseReviewDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Gets a review by ID
   */
  async getApiSocialReviews(id: string): Promise<Result<Types.LearningExperienceSocialServicesCourseReviewDto, ApiError>> {
    const url = `/api/social/reviews/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesCourseReviewDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Deletes a review (owner only)
   */
  async deleteApiSocialReviews(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/reviews/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Approves a review (admin only)
   */
  async postApiSocialReviewsApprove(id: string): Promise<Result<Types.LearningExperienceSocialServicesCourseReviewDto, ApiError>> {
    const url = `/api/social/reviews/${id}/approve`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesCourseReviewDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Features a review (admin only)
   */
  async postApiSocialReviewsFeature(id: string): Promise<Result<Types.LearningExperienceSocialServicesCourseReviewDto, ApiError>> {
    const url = `/api/social/reviews/${id}/feature`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesCourseReviewDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Marks a review as helpful
   */
  async postApiSocialReviewsHelpful(id: string): Promise<Result<Types.LearningExperienceSocialServicesCourseReviewDto, ApiError>> {
    const url = `/api/social/reviews/${id}/helpful`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesCourseReviewDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Updates review approval and storefront featured state.
   */
  async patchApiSocialReviewsModeration(
    id: string,
    body: Types.LearningExperienceSocialControllersUpdateReviewModerationInput,
  ): Promise<Result<Types.LearningExperienceSocialServicesCourseReviewDto, ApiError>> {
    const url = `/api/social/reviews/${id}/moderation`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceSocialControllersUpdateReviewModerationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesCourseReviewDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Gets the current user's reviews
   */
  async getApiSocialReviewsMe(query?: { skip?: number; take?: number }): Promise<Result<Types.LearningExperienceSocialServicesCourseReviewDto[], ApiError>> {
    const url = '/api/social/reviews/me';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.LearningExperienceSocialServicesCourseReviewDto[], ApiError>;
  }
}

export function createLearningExperienceSocialReviewsModule(client: ApiClient): LearningExperienceSocialReviewsModule {
  return new LearningExperienceSocialReviewsModule(client);
}
