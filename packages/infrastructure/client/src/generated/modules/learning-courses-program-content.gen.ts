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
   * Get all content for a course with optional filtering (resource-level Read permission required on parent Program)
   *
   * Supports filtering via query parameters:
   * - level=top: Get only top-level content
   */
  async getCoursesContent(programId: string, query?: { level?: string }): Promise<Result<Types.LearningCoursesProgramContentDto[], ApiError>> {
    const url = `/v1/courses/${programId}/content`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    return result as Result<Types.LearningCoursesProgramContentDto[], ApiError>;
  }

  /**
   * Create new program content (resource-level Create permission required on parent Program)
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
   * Get specific program content by ID (resource-level Read permission required on parent Program)
   */
  async getCoursesContentById(programId: string, id: string): Promise<Result<Types.LearningCoursesProgramContentDto, ApiError>> {
    const url = `/v1/courses/${programId}/content/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesProgramContentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update program content (resource-level Edit permission required on parent Program)
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
   * Delete program content (resource-level Delete permission required on parent Program)
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
   * Student view of a coding assignment: Private tests stripped, Private files filtered out.
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
   * Author a coding assignment: UPSERT onto ProgramContent.JsonBody + sync grading to linked Assessment.
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
   * Instructor view of a coding assignment: full content including Private tests and files.
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
   * Move content to a new parent/position (resource-level Edit permission required on parent Program)
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
   * Submit work for the current learner on a course content item.
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
   * Get child content for a specific parent (resource-level Read permission required on parent Program)
   */
  async getCoursesContentChildren(programId: string, parentId: string): Promise<Result<Types.LearningCoursesProgramContentDto[], ApiError>> {
    const url = `/v1/courses/${programId}/content/${parentId}/children`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LearningCoursesProgramContentDto[], ApiError>;
  }

  /**
   * Get content by type (resource-level Read permission required on parent Program)
   */
  async getCoursesContentByType(
    programId: string,
    type: Types.LearningCoursesProgramContentType,
  ): Promise<Result<Types.LearningCoursesProgramContentDto[], ApiError>> {
    const url = `/v1/courses/${programId}/content/by-type/${type}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LearningCoursesProgramContentDto[], ApiError>;
  }

  /**
   * Get content by visibility (resource-level Read permission required on parent Program)
   */
  async getCoursesContentByVisibility(
    programId: string,
    visibility: Types.LearningCoursesVisibility,
  ): Promise<Result<Types.LearningCoursesProgramContentDto[], ApiError>> {
    const url = `/v1/courses/${programId}/content/by-visibility/${visibility}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LearningCoursesProgramContentDto[], ApiError>;
  }

  /**
   * Reorder content within a program (resource-level Edit permission required on parent Program)
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
   * Get required content for a program (resource-level Read permission required on parent Program)
   */
  async getCoursesContentRequired(programId: string): Promise<Result<Types.LearningCoursesProgramContentDto[], ApiError>> {
    const url = `/v1/courses/${programId}/content/required`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LearningCoursesProgramContentDto[], ApiError>;
  }

  /**
   * Search content within a program (resource-level Read permission required on parent Program)
   */
  async postCoursesContentSearch(
    programId: string,
    body: Types.LearningCoursesSearchContentDto,
  ): Promise<Result<Types.LearningCoursesProgramContentDto[], ApiError>> {
    const url = `/v1/courses/${programId}/content/search`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesSearchContentDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<Types.LearningCoursesProgramContentDto[], ApiError>;
  }

  /**
   * Get content statistics for a program (resource-level Read permission required on parent Program)
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
