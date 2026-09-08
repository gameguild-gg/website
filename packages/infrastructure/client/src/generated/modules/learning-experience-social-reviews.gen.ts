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
   */
  async getApiSocialCoursesRatingStats(courseId: string): Promise<Result<Types.LearningExperienceSocialServicesCourseRatingStats, ApiError>> {
    const url = `/api/social/courses/${courseId}/rating-stats`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesCourseRatingStatsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiSocialCoursesReviews(
    courseId: string,
    query?: { skip?: number; take?: number; approvedOnly?: boolean },
  ): Promise<Result<Array<Types.LearningExperienceSocialServicesCourseReviewDto>, ApiError>> {
    const url = `/api/social/courses/${courseId}/reviews`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceSocialServicesCourseReviewDto>, ApiError>;
  }

  /**
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
   */
  async getApiSocialReviews(id: string): Promise<Result<Types.LearningExperienceSocialServicesCourseReviewDto, ApiError>> {
    const url = `/api/social/reviews/${id}`;

    const result = await this.client.request({
      method: 'GET',
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
   */
  async getApiSocialReviewsMe(query?: {
    skip?: number;
    take?: number;
  }): Promise<Result<Array<Types.LearningExperienceSocialServicesCourseReviewDto>, ApiError>> {
    const url = '/api/social/reviews/me';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceSocialServicesCourseReviewDto>, ApiError>;
  }
}

export function createLearningExperienceSocialReviewsModule(client: ApiClient): LearningExperienceSocialReviewsModule {
  return new LearningExperienceSocialReviewsModule(client);
}
