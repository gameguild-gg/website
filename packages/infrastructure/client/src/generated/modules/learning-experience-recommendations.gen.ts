/**
 * @game-guild/client - LearningExperienceRecommendations Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningExperienceRecommendationsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async postRecommendationsDismiss(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/recommendations/${id}/dismiss`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postRecommendationsViewed(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/recommendations/${id}/viewed`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getRecommendationsCoursesSimilar(
    courseId: string,
    query?: { tenantId?: string; maxResults?: number },
  ): Promise<Result<Array<Types.LearningExperienceRecommendationsSimilarCourseDto>, ApiError>> {
    const url = `/v1/recommendations/courses/${courseId}/similar`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceRecommendationsSimilarCourseDto>, ApiError>;
  }

  /**
   */
  async getRecommendationsMe(query?: {
    tenantId?: string;
    type?: Types.LearningExperienceRecommendationsRecommendationType;
    includeViewed?: boolean;
    skip?: number;
    take?: number;
  }): Promise<Result<Array<Types.LearningExperienceRecommendationsRecommendationDto>, ApiError>> {
    const url = '/v1/recommendations/me';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceRecommendationsRecommendationDto>, ApiError>;
  }

  /**
   */
  async postRecommendationsMeGenerate(query?: {
    tenantId?: string;
    maxResults?: number;
  }): Promise<Result<Array<Types.LearningExperienceRecommendationsRecommendationDto>, ApiError>> {
    const url = '/v1/recommendations/me/generate';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceRecommendationsRecommendationDto>, ApiError>;
  }

  /**
   */
  async getRecommendationsMeProfile(): Promise<Result<Types.LearningExperienceRecommendationsUserLearningProfileDto, ApiError>> {
    const url = '/v1/recommendations/me/profile';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceRecommendationsUserLearningProfileDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putRecommendationsMeProfile(
    body: Types.LearningExperienceRecommendationsCreateOrUpdateLearningProfileDto,
  ): Promise<Result<Types.LearningExperienceRecommendationsUserLearningProfileDto, ApiError>> {
    const url = '/v1/recommendations/me/profile';

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceRecommendationsCreateOrUpdateLearningProfileDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceRecommendationsUserLearningProfileDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postRecommendationsMeProfileSkills(
    body: Types.LearningExperienceRecommendationsAddSkillInput,
  ): Promise<Result<Types.LearningExperienceRecommendationsUserLearningProfileDto, ApiError>> {
    const url = '/v1/recommendations/me/profile/skills';

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceRecommendationsAddSkillInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceRecommendationsUserLearningProfileDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteRecommendationsMeProfileSkills(skill: string): Promise<Result<Types.LearningExperienceRecommendationsUserLearningProfileDto, ApiError>> {
    const url = `/v1/recommendations/me/profile/skills/${skill}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceRecommendationsUserLearningProfileDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postRecommendationsMeRefresh(query?: { tenantId?: string }): Promise<Result<void, ApiError>> {
    const url = '/v1/recommendations/me/refresh';

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
  async getRecommendationsMeStatistics(): Promise<Result<Types.LearningExperienceRecommendationsRecommendationStatisticsDto, ApiError>> {
    const url = '/v1/recommendations/me/statistics';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceRecommendationsRecommendationStatisticsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getRecommendationsPopular(query?: {
    tenantId?: string;
    category?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Array<Types.LearningExperienceRecommendationsPopularCourseDto>, ApiError>> {
    const url = '/v1/recommendations/popular';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceRecommendationsPopularCourseDto>, ApiError>;
  }

  /**
   */
  async getRecommendationsTrending(query?: {
    tenantId?: string;
    daysWindow?: number;
    skip?: number;
    take?: number;
  }): Promise<Result<Array<Types.LearningExperienceRecommendationsTrendingCourseDto>, ApiError>> {
    const url = '/v1/recommendations/trending';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceRecommendationsTrendingCourseDto>, ApiError>;
  }
}

export function createLearningExperienceRecommendationsModule(client: ApiClient): LearningExperienceRecommendationsModule {
  return new LearningExperienceRecommendationsModule(client);
}
