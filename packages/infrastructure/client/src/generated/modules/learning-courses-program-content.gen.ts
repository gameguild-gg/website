/**
 * @game-guild/client - LearningCoursesProgramContent Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningCoursesProgramContentModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getCoursesContent(programId: string, query?: { level?: string }): Promise<Result<Array<Types.LearningCoursesProgramContentDto>, ApiError>> {
    const url = `/v1/courses/${programId}/content`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesProgramContentDto>, ApiError>;
  }

  /**
   */
  async postCoursesContent(
    programId: string,
    body: Types.LearningCoursesCreateProgramContentDto,
  ): Promise<Result<Types.LearningCoursesProgramContentDto, ApiError>> {
    const url = `/v1/courses/${programId}/content`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesCreateProgramContentDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesProgramContentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesContentById(programId: string, id: string): Promise<Result<Types.LearningCoursesProgramContentDto, ApiError>> {
    const url = `/v1/courses/${programId}/content/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesProgramContentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putCoursesContent(
    programId: string,
    id: string,
    body: Types.LearningCoursesUpdateProgramContentDto,
  ): Promise<Result<Types.LearningCoursesProgramContentDto, ApiError>> {
    const url = `/v1/courses/${programId}/content/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesUpdateProgramContentDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesProgramContentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteCoursesContent(programId: string, id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/courses/${programId}/content/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getCoursesContentCodingAssignment(programId: string, id: string): Promise<Result<Types.LearningCoursesCodingAssignmentContent, ApiError>> {
    const url = `/v1/courses/${programId}/content/${id}/coding-assignment`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesCodingAssignmentContentSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putCoursesContentCodingAssignment(
    programId: string,
    id: string,
    body: Types.LearningCoursesCodingAssignmentContent,
  ): Promise<Result<Types.LearningCoursesCodingAssignmentContent, ApiError>> {
    const url = `/v1/courses/${programId}/content/${id}/coding-assignment`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesCodingAssignmentContentSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesCodingAssignmentContentSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesContentCodingAssignmentFull(programId: string, id: string): Promise<Result<Types.LearningCoursesCodingAssignmentContent, ApiError>> {
    const url = `/v1/courses/${programId}/content/${id}/coding-assignment/full`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesCodingAssignmentContentSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postCoursesContentMove(programId: string, id: string, body: Types.LearningCoursesMoveContentDto): Promise<Result<void, ApiError>> {
    const url = `/v1/courses/${programId}/content/${id}/move`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesMoveContentDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postCoursesContentSubmit(
    programId: string,
    id: string,
    body: Types.LearningCoursesSubmitUserContentDto,
  ): Promise<Result<Types.LearningCoursesContentInteractionDto, ApiError>> {
    const url = `/v1/courses/${programId}/content/${id}/submit`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesSubmitUserContentDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesContentInteractionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesContentChildren(programId: string, parentId: string): Promise<Result<Array<Types.LearningCoursesProgramContentDto>, ApiError>> {
    const url = `/v1/courses/${programId}/content/${parentId}/children`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesProgramContentDto>, ApiError>;
  }

  /**
   */
  async getCoursesContentByType(
    programId: string,
    type: Types.LearningCoursesProgramContentType,
  ): Promise<Result<Array<Types.LearningCoursesProgramContentDto>, ApiError>> {
    const url = `/v1/courses/${programId}/content/by-type/${type}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesProgramContentDto>, ApiError>;
  }

  /**
   */
  async getCoursesContentByVisibility(
    programId: string,
    visibility: Types.LearningCoursesVisibility,
  ): Promise<Result<Array<Types.LearningCoursesProgramContentDto>, ApiError>> {
    const url = `/v1/courses/${programId}/content/by-visibility/${visibility}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesProgramContentDto>, ApiError>;
  }

  /**
   */
  async postCoursesContentReorder(programId: string, body: Types.LearningCoursesReorderContentDto): Promise<Result<void, ApiError>> {
    const url = `/v1/courses/${programId}/content/reorder`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesReorderContentDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getCoursesContentRequired(programId: string): Promise<Result<Array<Types.LearningCoursesProgramContentDto>, ApiError>> {
    const url = `/v1/courses/${programId}/content/required`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesProgramContentDto>, ApiError>;
  }

  /**
   */
  async postCoursesContentSearch(
    programId: string,
    body: Types.LearningCoursesSearchContentDto,
  ): Promise<Result<Array<Types.LearningCoursesProgramContentDto>, ApiError>> {
    const url = `/v1/courses/${programId}/content/search`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesSearchContentDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesProgramContentDto>, ApiError>;
  }

  /**
   */
  async getCoursesContentStats(programId: string): Promise<Result<Types.LearningCoursesContentStatsDto, ApiError>> {
    const url = `/v1/courses/${programId}/content/stats`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesContentStatsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createLearningCoursesProgramContentModule(client: ApiClient): LearningCoursesProgramContentModule {
  return new LearningCoursesProgramContentModule(client);
}
