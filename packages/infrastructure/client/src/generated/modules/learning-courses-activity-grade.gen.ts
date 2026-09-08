/**
 * @game-guild/client - LearningCoursesActivityGrade Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningCoursesActivityGradeModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async postCoursesActivityGrades(
    programId: string,
    body: Types.LearningCoursesCreateActivityGradeDto,
  ): Promise<Result<Types.LearningCoursesActivityGradeDto, ApiError>> {
    const url = `/v1/courses/${programId}/activity-grades`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesCreateActivityGradeDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesActivityGradeDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putCoursesActivityGrades(
    programId: string,
    gradeId: string,
    body: Types.LearningCoursesUpdateActivityGradeDto,
  ): Promise<Result<Types.LearningCoursesActivityGradeDto, ApiError>> {
    const url = `/v1/courses/${programId}/activity-grades/${gradeId}`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesUpdateActivityGradeDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesActivityGradeDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteCoursesActivityGrades(programId: string, gradeId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/courses/${programId}/activity-grades/${gradeId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getCoursesActivityGradesContent(programId: string, contentId: string): Promise<Result<Array<Types.LearningCoursesActivityGradeDto>, ApiError>> {
    const url = `/v1/courses/${programId}/activity-grades/content/${contentId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesActivityGradeDto>, ApiError>;
  }

  /**
   */
  async getCoursesActivityGradesGrader(
    programId: string,
    graderProgramUserId: string,
  ): Promise<Result<Array<Types.LearningCoursesActivityGradeDto>, ApiError>> {
    const url = `/v1/courses/${programId}/activity-grades/grader/${graderProgramUserId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesActivityGradeDto>, ApiError>;
  }

  /**
   */
  async getCoursesActivityGradesInteraction(programId: string, contentInteractionId: string): Promise<Result<Types.LearningCoursesActivityGradeDto, ApiError>> {
    const url = `/v1/courses/${programId}/activity-grades/interaction/${contentInteractionId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesActivityGradeDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesActivityGradesPending(programId: string): Promise<Result<Array<Types.LearningCoursesContentInteractionDto>, ApiError>> {
    const url = `/v1/courses/${programId}/activity-grades/pending`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesContentInteractionDto>, ApiError>;
  }

  /**
   */
  async getCoursesActivityGradesStatistics(programId: string): Promise<Result<Types.LearningCoursesGradeStatisticsDto, ApiError>> {
    const url = `/v1/courses/${programId}/activity-grades/statistics`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesGradeStatisticsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesActivityGradesStudent(programId: string, programUserId: string): Promise<Result<Array<Types.LearningCoursesActivityGradeDto>, ApiError>> {
    const url = `/v1/courses/${programId}/activity-grades/student/${programUserId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesActivityGradeDto>, ApiError>;
  }
}

export function createLearningCoursesActivityGradeModule(client: ApiClient): LearningCoursesActivityGradeModule {
  return new LearningCoursesActivityGradeModule(client);
}
