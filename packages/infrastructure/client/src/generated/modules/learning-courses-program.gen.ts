/**
 * @game-guild/client - LearningCoursesProgram Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningCoursesProgramModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getCoursesForGetCourses(query?: {
    status?: string;
    category?: Types.ProgramCategory;
    difficulty?: Types.LearningCoursesProgramDifficulty;
    creatorId?: string;
    q?: string;
    sort?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Array<Types.LearningCoursesProgramDto>, ApiError>> {
    const url = '/v1/courses';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesProgramDto>, ApiError>;
  }

  /**
   */
  async postCourses(body: Types.LearningCoursesCreateProgramDto): Promise<Result<Types.LearningCoursesProgramDto, ApiError>> {
    const url = '/v1/courses';

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesCreateProgramDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesProgramDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesForGetCoursesById(id: string): Promise<Result<Types.LearningCoursesProgramDto, ApiError>> {
    const url = `/v1/courses/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesProgramDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putCourses(id: string, body: Types.LearningCoursesUpdateProgramDto): Promise<Result<Types.LearningCoursesProgramDto, ApiError>> {
    const url = `/v1/courses/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesUpdateProgramDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesProgramDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteCourses(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/courses/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postCoursesClone(id: string, body: Types.LearningCoursesCloneProgramDto): Promise<Result<Types.LearningCoursesProgramDto, ApiError>> {
    const url = `/v1/courses/${id}:clone`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesCloneProgramDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesProgramDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postCoursesCreateProduct(id: string, body: Types.LearningCoursesCreateProductFromProgramDto): Promise<Result<string, ApiError>> {
    const url = `/v1/courses/${id}:create-product`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesCreateProductFromProgramDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<string, ApiError>;
  }

  /**
   */
  async postCoursesDisableMonetization(id: string): Promise<Result<Types.LearningCoursesProgramDto, ApiError>> {
    const url = `/v1/courses/${id}:disable-monetization`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesProgramDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postCoursesLinkProduct(id: string, productId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/courses/${id}:link-product/${productId}`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postCoursesMonetize(id: string, body: Types.LearningCoursesMonetizationDto): Promise<Result<Types.LearningCoursesProgramDto, ApiError>> {
    const url = `/v1/courses/${id}:monetize`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesMonetizationDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesProgramDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postCoursesSelfEnroll(id: string): Promise<Result<Types.LearningCoursesUserProgressDto, ApiError>> {
    const url = `/v1/courses/${id}:self-enroll`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesUserProgressDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteCoursesUnlinkProduct(id: string, productId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/courses/${id}:unlink-product/${productId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getCoursesAnalytics(id: string): Promise<Result<Types.LearningCoursesProgramAnalyticsDto, ApiError>> {
    const url = `/v1/courses/${id}/analytics`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesProgramAnalyticsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesAnalyticsCompletionRates(id: string): Promise<Result<Types.LearningCoursesCompletionRatesDto, ApiError>> {
    const url = `/v1/courses/${id}/analytics/completion-rates`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesCompletionRatesDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesAnalyticsEngagement(id: string): Promise<Result<Types.LearningCoursesEngagementMetricsDto, ApiError>> {
    const url = `/v1/courses/${id}/analytics/engagement`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesEngagementMetricsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesAnalyticsRevenue(id: string): Promise<Result<Types.LearningCoursesRevenueAnalyticsDto, ApiError>> {
    const url = `/v1/courses/${id}/analytics/revenue`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesRevenueAnalyticsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postCoursesMeContentComplete(id: string, contentId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/courses/${id}/me/content/${contentId}:complete`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getCoursesMeProgress(id: string): Promise<Result<Types.LearningCoursesUserProgressDto, ApiError>> {
    const url = `/v1/courses/${id}/me/progress`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesUserProgressDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putCoursesMeProgress(id: string, body: Types.LearningCoursesUpdateProgressDto): Promise<Result<Types.LearningCoursesUserProgressDto, ApiError>> {
    const url = `/v1/courses/${id}/me/progress`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesUpdateProgressDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesUserProgressDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesPricing(id: string): Promise<Result<Types.LearningCoursesPricingDto, ApiError>> {
    const url = `/v1/courses/${id}/pricing`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesPricingDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putCoursesPricing(id: string, body: Types.LearningCoursesUpdatePricingDto): Promise<Result<Types.LearningCoursesPricingDto, ApiError>> {
    const url = `/v1/courses/${id}/pricing`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesUpdatePricingDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesPricingDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesProducts(id: string): Promise<Result<Array<string>, ApiError>> {
    const url = `/v1/courses/${id}/products`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<string>, ApiError>;
  }

  /**
   */
  async getCoursesUsers(id: string, query?: { skip?: number; take?: number }): Promise<Result<Array<Types.LearningCoursesUserProgressDto>, ApiError>> {
    const url = `/v1/courses/${id}/users`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesUserProgressDto>, ApiError>;
  }

  /**
   */
  async postCoursesUsersEnroll(id: string, body: Types.LearningCoursesEnrollProgramUserInput): Promise<Result<Types.LearningCoursesUserProgressDto, ApiError>> {
    const url = `/v1/courses/${id}/users:enroll`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesEnrollProgramUserInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesUserProgressDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postCoursesUsers(id: string, userId: string): Promise<Result<Types.LearningCoursesUserProgressDto, ApiError>> {
    const url = `/v1/courses/${id}/users/${userId}`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesUserProgressDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteCoursesUsers(id: string, userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/courses/${id}/users/${userId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postCoursesUsersReset(id: string, userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/courses/${id}/users/${userId}:reset`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postCoursesUsersContentComplete(id: string, userId: string, contentId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/courses/${id}/users/${userId}/content/${contentId}:complete`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getCoursesUsersProgress(id: string, userId: string): Promise<Result<Types.LearningCoursesUserProgressDto, ApiError>> {
    const url = `/v1/courses/${id}/users/${userId}/progress`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesUserProgressDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putCoursesUsersProgress(
    id: string,
    userId: string,
    body: Types.LearningCoursesUpdateProgressDto,
  ): Promise<Result<Types.LearningCoursesUserProgressDto, ApiError>> {
    const url = `/v1/courses/${id}/users/${userId}/progress`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesUpdateProgressDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesUserProgressDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesWithContent(id: string): Promise<Result<Types.LearningCoursesProgramDto, ApiError>> {
    const url = `/v1/courses/${id}/with-content`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesProgramDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesMe(): Promise<Result<Array<Types.LearningCoursesProgramDto>, ApiError>> {
    const url = '/v1/courses/me';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesProgramDto>, ApiError>;
  }

  /**
   */
  async getCoursesPublic(query?: { skip?: number; take?: number }): Promise<Result<Array<Types.LearningCoursesProgramDto>, ApiError>> {
    const url = '/v1/courses/public';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesProgramDto>, ApiError>;
  }

  /**
   */
  async getCoursesSlug(slug: string): Promise<Result<Types.LearningCoursesProgramDto, ApiError>> {
    const url = `/v1/courses/slug/${slug}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesProgramDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createLearningCoursesProgramModule(client: ApiClient): LearningCoursesProgramModule {
  return new LearningCoursesProgramModule(client);
}
