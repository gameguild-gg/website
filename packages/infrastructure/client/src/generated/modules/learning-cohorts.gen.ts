/**
 * @game-guild/client - LearningCohorts Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningCohortsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async postApiCohorts(body: Types.LearningCohortsCreateCohortInput): Promise<Result<Types.LearningCohortsCohortDto, ApiError>> {
    const url = '/api/cohorts';

    // Validate request body
    const validatedBody = safeParse(Types.LearningCohortsCreateCohortInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCohortsCohortDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiCohorts(id: string): Promise<Result<Types.LearningCohortsCohortDto, ApiError>> {
    const url = `/api/cohorts/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCohortsCohortDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putApiCohorts(id: string, body: Types.LearningCohortsUpdateCohortInput): Promise<Result<Types.LearningCohortsCohortDto, ApiError>> {
    const url = `/api/cohorts/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCohortsUpdateCohortInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCohortsCohortDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteApiCohorts(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/cohorts/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postApiCohortsCancel(id: string): Promise<Result<Types.LearningCohortsCohortDto, ApiError>> {
    const url = `/api/cohorts/${id}/cancel`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCohortsCohortDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postApiCohortsClose(id: string): Promise<Result<Types.LearningCohortsCohortDto, ApiError>> {
    const url = `/api/cohorts/${id}/close`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCohortsCohortDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postApiCohortsComplete(id: string): Promise<Result<Types.LearningCohortsCohortDto, ApiError>> {
    const url = `/api/cohorts/${id}/complete`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCohortsCohortDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postApiCohortsOpen(id: string): Promise<Result<Types.LearningCohortsCohortDto, ApiError>> {
    const url = `/api/cohorts/${id}/open`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCohortsCohortDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiCohortsCourse(courseId: string): Promise<Result<Array<Types.LearningCohortsCohortDto>, ApiError>> {
    const url = `/api/cohorts/course/${courseId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCohortsCohortDto>, ApiError>;
  }

  /**
   */
  async getApiCohortsCourseActive(courseId: string): Promise<Result<Array<Types.LearningCohortsCohortDto>, ApiError>> {
    const url = `/api/cohorts/course/${courseId}/active`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCohortsCohortDto>, ApiError>;
  }

  /**
   */
  async getApiCohortsCourseEnrollable(courseId: string): Promise<Result<Array<Types.LearningCohortsCohortDto>, ApiError>> {
    const url = `/api/cohorts/course/${courseId}/enrollable`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCohortsCohortDto>, ApiError>;
  }
}

export function createLearningCohortsModule(client: ApiClient): LearningCohortsModule {
  return new LearningCohortsModule(client);
}
