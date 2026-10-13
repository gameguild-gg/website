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
   * Get all courses with optional filtering (content-type level read permission). Non-manage actors are DAC-scoped to their own courses.
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
  }): Promise<Result<Types.LearningCoursesProgramDto[], ApiError>> {
    const url = '/v1/courses';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.LearningCoursesProgramDto[], ApiError>;
  }

  /**
   * Create a new program (content-type level draft permission)
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
   * Get a specific program by ID (resource-level read permission)
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
   * Update a program (resource-level edit permission)
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
   * Delete a program (resource-level delete permission)
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
   * Clone/duplicate a program (resource-level clone permission)
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
   * Create a product from a program (resource-level edit permission for program, content-type level draft permission for product)
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
   * Disable monetization for a program (resource-level monetize permission)
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
   * Link a program to an existing product (resource-level edit permission)
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
   * Enable monetization for a program (resource-level monetize permission)
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
   * Self-enroll the current authenticated user in a published public course.
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
   * Unlink a program from a product (resource-level edit permission)
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
   * Get program analytics (resource-level analytics permission)
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
   * Get user completion rates for a program (resource-level analytics permission)
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
   * Get program engagement metrics (resource-level analytics permission)
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
   * Get program revenue analytics (resource-level revenue permission)
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
   * Mark content as completed for the current learner.
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
   * Get the current learner's progress in a program.
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
   * Update the current learner's progress in a program.
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
   * Get program pricing information (resource-level read permission)
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
   * Update program pricing (resource-level pricing permission)
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
   * Get all products linked to a program (resource-level read permission)
   */
  async getCoursesProducts(id: string): Promise<Result<string[], ApiError>> {
    const url = `/v1/courses/${id}/products`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    return result as Result<string[], ApiError>;
  }

  /**
   * Get all users in a program (resource-level read permission)
   */
  async getCoursesUsers(id: string, query?: { skip?: number; take?: number }): Promise<Result<Types.LearningCoursesUserProgressDto[], ApiError>> {
    const url = `/v1/courses/${id}/users`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.LearningCoursesUserProgressDto[], ApiError>;
  }

  /**
   * Resolve a tenant-scoped user reference and add that user to a program.
   * This keeps user discovery behind the program's resource-level edit permission,
   * so course owners do not need tenant-wide user administration privileges.
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
   * Add a user to a program (resource-level edit permission)
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
   * Remove a user from a program (resource-level edit permission)
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
   * Reset user progress in a program (resource-level edit permission)
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
   * Mark content as completed for a user (resource-level edit permission)
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
   * Get a specific user's progress in a program (resource-level read permission)
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
   * Update a user's progress in a program (resource-level edit permission)
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
   * Get a specific program with all content included (resource-level read permission)
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
   * Get every course in which the current user has an active enrollment.
   */
  async getCoursesMe(): Promise<Result<Types.LearningCoursesProgramDto[], ApiError>> {
    const url = '/v1/courses/me';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LearningCoursesProgramDto[], ApiError>;
  }

  /**
   * Get published public courses for the public catalog.
   */
  async getCoursesPublic(query?: { skip?: number; take?: number }): Promise<Result<Types.LearningCoursesProgramDto[], ApiError>> {
    const url = '/v1/courses/public';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    return result as Result<Types.LearningCoursesProgramDto[], ApiError>;
  }

  /**
   * Get a specific program by slug (public access for published programs)
   */
  async getCoursesSlug(slug: string): Promise<Result<Types.LearningCoursesProgramDto, ApiError>> {
    const url = `/v1/courses/slug/${slug}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
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
