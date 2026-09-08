/**
 * @game-guild/client - LearningCohortsSchedules Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningCohortsSchedulesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getCoursesCohortsSchedule(courseId: string, cohortId: string): Promise<Result<Types.LearningCohortsCohortScheduleDto, ApiError>> {
    const url = `/v1/courses/${courseId}/cohorts/${cohortId}/schedule`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCohortsCohortScheduleDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putCoursesCohortsSchedule(
    courseId: string,
    cohortId: string,
    body: Types.LearningCohortsApplyCohortScheduleInput,
  ): Promise<Result<Types.LearningCohortsCohortScheduleDto, ApiError>> {
    const url = `/v1/courses/${courseId}/cohorts/${cohortId}/schedule`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCohortsApplyCohortScheduleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCohortsCohortScheduleDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesCohortsScheduleAvailableContent(
    courseId: string,
    cohortId: string,
  ): Promise<Result<Array<Types.LearningCohortsAvailableCohortContentDto>, ApiError>> {
    const url = `/v1/courses/${courseId}/cohorts/${cohortId}/schedule/available-content`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCohortsAvailableCohortContentDto>, ApiError>;
  }

  /**
   */
  async patchCoursesCohortsScheduleItems(
    courseId: string,
    cohortId: string,
    itemId: string,
    body: Types.LearningCohortsUpdateCohortScheduleInput,
  ): Promise<Result<Types.LearningCohortsCohortScheduleDto, ApiError>> {
    const url = `/v1/courses/${courseId}/cohorts/${cohortId}/schedule/items/${itemId}`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCohortsUpdateCohortScheduleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCohortsCohortScheduleDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postCoursesCohortsScheduleItemsShift(
    courseId: string,
    cohortId: string,
    itemId: string,
    body: Types.LearningCohortsShiftCohortScheduleInput,
  ): Promise<Result<Types.LearningCohortsCohortScheduleDto, ApiError>> {
    const url = `/v1/courses/${courseId}/cohorts/${cohortId}/schedule/items/${itemId}/shift`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCohortsShiftCohortScheduleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCohortsCohortScheduleDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postCoursesCohortsSchedulePreview(
    courseId: string,
    cohortId: string,
    body: Types.LearningCohortsPreviewCohortScheduleInput,
  ): Promise<Result<Types.LearningCohortsCohortSchedulePreviewDto, ApiError>> {
    const url = `/v1/courses/${courseId}/cohorts/${cohortId}/schedule/preview`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCohortsPreviewCohortScheduleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCohortsCohortSchedulePreviewDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesCohortsCalendar(
    courseId: string,
    query?: { cohortId?: string; from?: string; to?: string },
  ): Promise<Result<Types.LearningCohortsCourseCohortCalendarDto, ApiError>> {
    const url = `/v1/courses/${courseId}/cohorts/calendar`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCohortsCourseCohortCalendarDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createLearningCohortsSchedulesModule(client: ApiClient): LearningCohortsSchedulesModule {
  return new LearningCohortsSchedulesModule(client);
}
