/**
 * @game-guild/client - LearningEnrollments Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningEnrollmentsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async postApiLearningEnrollments(body: Types.LearningEnrollmentsEnrollUserInput): Promise<Result<Types.LearningEnrollmentsEnrollmentDto, ApiError>> {
    const url = '/api/learning/enrollments';

    // Validate request body
    const validatedBody = safeParse(Types.LearningEnrollmentsEnrollUserInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningEnrollmentsEnrollmentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiLearningEnrollments(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/learning/enrollments/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async patchApiLearningEnrollmentsProgress(id: string, body: Types.LearningEnrollmentsUpdateEnrollmentProgressInput): Promise<Result<void, ApiError>> {
    const url = `/api/learning/enrollments/${id}/progress`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningEnrollmentsUpdateEnrollmentProgressInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postApiLearningEnrollmentsStatus(id: string, status: Types.LearningEnrollmentsEnrollmentStatus): Promise<Result<void, ApiError>> {
    const url = `/api/learning/enrollments/${id}/status/${status}`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getApiLearningEnrollmentsCourses(
    courseId: string,
    query?: { status?: Types.LearningEnrollmentsEnrollmentStatus },
  ): Promise<Result<Array<Types.LearningEnrollmentsEnrollmentDto>, ApiError>> {
    const url = `/api/learning/enrollments/courses/${courseId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningEnrollmentsEnrollmentDto>, ApiError>;
  }

  /**
   */
  async getApiLearningEnrollmentsUsers(
    userId: string,
    query?: { status?: Types.LearningEnrollmentsEnrollmentStatus },
  ): Promise<Result<Array<Types.LearningEnrollmentsEnrollmentDto>, ApiError>> {
    const url = `/api/learning/enrollments/users/${userId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningEnrollmentsEnrollmentDto>, ApiError>;
  }
}

export function createLearningEnrollmentsModule(client: ApiClient): LearningEnrollmentsModule {
  return new LearningEnrollmentsModule(client);
}
