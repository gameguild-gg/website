/**
 * @game-guild/client - LearningExperienceLearningPathsLearningPath Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningExperienceLearningPathsLearningPathModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getLearningPathsForGetLearningPaths(query?: {
    tenantId?: string;
    difficulty?: Types.LearningExperienceLearningPathsLearningPathDifficulty;
    skip?: number;
    take?: number;
  }): Promise<Result<Array<Types.LearningExperienceLearningPathsLearningPathDto>, ApiError>> {
    const url = '/v1/learning-paths';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceLearningPathsLearningPathDto>, ApiError>;
  }

  /**
   */
  async postLearningPaths(
    body: Types.LearningExperienceLearningPathsCreateLearningPathDto,
    query?: { creatorId?: string; tenantId?: string },
  ): Promise<Result<Types.LearningExperienceLearningPathsLearningPathDto, ApiError>> {
    const url = '/v1/learning-paths';

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceLearningPathsCreateLearningPathDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceLearningPathsLearningPathDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getLearningPathsForGetLearningPathsById(id: string): Promise<Result<Types.LearningExperienceLearningPathsLearningPathDetailDto, ApiError>> {
    const url = `/v1/learning-paths/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceLearningPathsLearningPathDetailDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putLearningPaths(
    id: string,
    body: Types.LearningExperienceLearningPathsUpdateLearningPathDto,
  ): Promise<Result<Types.LearningExperienceLearningPathsLearningPathDto, ApiError>> {
    const url = `/v1/learning-paths/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceLearningPathsUpdateLearningPathDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceLearningPathsLearningPathDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteLearningPaths(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/learning-paths/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postLearningPathsAbandon(id: string, query?: { userId?: string }): Promise<Result<void, ApiError>> {
    const url = `/v1/learning-paths/${id}/abandon`;

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
  async postLearningPathsComplete(
    id: string,
    query?: { userId?: string },
  ): Promise<Result<Types.LearningExperienceLearningPathsLearningPathEnrollmentDto, ApiError>> {
    const url = `/v1/learning-paths/${id}/complete`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceLearningPathsLearningPathEnrollmentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postLearningPathsCourses(
    id: string,
    body: Types.LearningExperienceLearningPathsAddCourseToPathDto,
  ): Promise<Result<Types.LearningExperienceLearningPathsLearningPathDetailDto, ApiError>> {
    const url = `/v1/learning-paths/${id}/courses`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceLearningPathsAddCourseToPathDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceLearningPathsLearningPathDetailDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteLearningPathsCourses(id: string, courseId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/learning-paths/${id}/courses/${courseId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async putLearningPathsCoursesOrder(
    id: string,
    body: Types.LearningExperienceLearningPathsReorderCoursesDto,
  ): Promise<Result<Types.LearningExperienceLearningPathsLearningPathDetailDto, ApiError>> {
    const url = `/v1/learning-paths/${id}/courses/order`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceLearningPathsReorderCoursesDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceLearningPathsLearningPathDetailDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postLearningPathsEnroll(
    id: string,
    query?: { userId?: string },
  ): Promise<Result<Types.LearningExperienceLearningPathsLearningPathEnrollmentDto, ApiError>> {
    const url = `/v1/learning-paths/${id}/enroll`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceLearningPathsLearningPathEnrollmentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getLearningPathsEnrollment(id: string, userId: string): Promise<Result<Types.LearningExperienceLearningPathsLearningPathEnrollmentDto, ApiError>> {
    const url = `/v1/learning-paths/${id}/enrollment/${userId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceLearningPathsLearningPathEnrollmentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getLearningPathsEnrollmentCheck(id: string, userId: string): Promise<Result<boolean, ApiError>> {
    const url = `/v1/learning-paths/${id}/enrollment/${userId}/check`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<boolean, ApiError>;
  }

  /**
   */
  async getLearningPathsEnrollments(
    id: string,
    query?: { status?: Types.LearningExperienceLearningPathsLearningPathEnrollmentStatus; skip?: number; take?: number },
  ): Promise<Result<Array<Types.LearningExperienceLearningPathsLearningPathEnrollmentDto>, ApiError>> {
    const url = `/v1/learning-paths/${id}/enrollments`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceLearningPathsLearningPathEnrollmentDto>, ApiError>;
  }

  /**
   */
  async putLearningPathsProgress(
    id: string,
    body: Types.LearningExperienceLearningPathsUpdatePathProgressDto,
    query?: { userId?: string },
  ): Promise<Result<Types.LearningExperienceLearningPathsLearningPathEnrollmentDto, ApiError>> {
    const url = `/v1/learning-paths/${id}/progress`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceLearningPathsUpdatePathProgressDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      params: query,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceLearningPathsLearningPathEnrollmentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postLearningPathsPublish(id: string): Promise<Result<Types.LearningExperienceLearningPathsLearningPathDto, ApiError>> {
    const url = `/v1/learning-paths/${id}/publish`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceLearningPathsLearningPathDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getLearningPathsStatistics(id: string): Promise<Result<Types.LearningExperienceLearningPathsLearningPathStatisticsDto, ApiError>> {
    const url = `/v1/learning-paths/${id}/statistics`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceLearningPathsLearningPathStatisticsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postLearningPathsUnenroll(id: string, query?: { userId?: string }): Promise<Result<void, ApiError>> {
    const url = `/v1/learning-paths/${id}/unenroll`;

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
  async postLearningPathsUnpublish(id: string): Promise<Result<Types.LearningExperienceLearningPathsLearningPathDto, ApiError>> {
    const url = `/v1/learning-paths/${id}/unpublish`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceLearningPathsLearningPathDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getLearningPathsCreator(
    creatorId: string,
    query?: { includeUnpublished?: boolean; skip?: number; take?: number },
  ): Promise<Result<Array<Types.LearningExperienceLearningPathsLearningPathDto>, ApiError>> {
    const url = `/v1/learning-paths/creator/${creatorId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceLearningPathsLearningPathDto>, ApiError>;
  }

  /**
   */
  async getLearningPathsFeatured(query?: {
    tenantId?: string;
    take?: number;
  }): Promise<Result<Array<Types.LearningExperienceLearningPathsLearningPathDto>, ApiError>> {
    const url = '/v1/learning-paths/featured';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceLearningPathsLearningPathDto>, ApiError>;
  }

  /**
   */
  async getLearningPathsPopular(query?: {
    tenantId?: string;
    daysBack?: number;
    take?: number;
  }): Promise<Result<Array<Types.LearningExperienceLearningPathsLearningPathDto>, ApiError>> {
    const url = '/v1/learning-paths/popular';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceLearningPathsLearningPathDto>, ApiError>;
  }

  /**
   */
  async getLearningPathsSearch(query?: {
    q?: string;
    tenantId?: string;
    difficulty?: Types.LearningExperienceLearningPathsLearningPathDifficulty;
    skip?: number;
    take?: number;
  }): Promise<Result<Array<Types.LearningExperienceLearningPathsLearningPathDto>, ApiError>> {
    const url = '/v1/learning-paths/search';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceLearningPathsLearningPathDto>, ApiError>;
  }

  /**
   */
  async getLearningPathsSlug(
    slug: string,
    query?: { tenantId?: string },
  ): Promise<Result<Types.LearningExperienceLearningPathsLearningPathDetailDto, ApiError>> {
    const url = `/v1/learning-paths/slug/${slug}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceLearningPathsLearningPathDetailDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getLearningPathsUserCompleted(
    userId: string,
    query?: { skip?: number; take?: number },
  ): Promise<Result<Array<Types.LearningExperienceLearningPathsLearningPathEnrollmentDto>, ApiError>> {
    const url = `/v1/learning-paths/user/${userId}/completed`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceLearningPathsLearningPathEnrollmentDto>, ApiError>;
  }

  /**
   */
  async getLearningPathsUserEnrollments(
    userId: string,
    query?: { status?: Types.LearningExperienceLearningPathsLearningPathEnrollmentStatus; skip?: number; take?: number },
  ): Promise<Result<Array<Types.LearningExperienceLearningPathsLearningPathEnrollmentDto>, ApiError>> {
    const url = `/v1/learning-paths/user/${userId}/enrollments`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceLearningPathsLearningPathEnrollmentDto>, ApiError>;
  }
}

export function createLearningExperienceLearningPathsLearningPathModule(client: ApiClient): LearningExperienceLearningPathsLearningPathModule {
  return new LearningExperienceLearningPathsLearningPathModule(client);
}
