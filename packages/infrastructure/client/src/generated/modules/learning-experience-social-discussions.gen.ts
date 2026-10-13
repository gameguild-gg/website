/**
 * @game-guild/client - LearningExperienceSocialDiscussions Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningExperienceSocialDiscussionsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Gets discussions for specific content within a course
   */
  async getApiSocialCoursesContentDiscussions(
    courseId: string,
    contentId: string,
    query?: { skip?: number; take?: number },
  ): Promise<Result<Types.LearningExperienceSocialServicesCourseDiscussionDto[], ApiError>> {
    const url = `/api/social/courses/${courseId}/content/${contentId}/discussions`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    return result as Result<Types.LearningExperienceSocialServicesCourseDiscussionDto[], ApiError>;
  }

  /**
   * Gets discussions for a course
   */
  async getApiSocialCoursesDiscussions(
    courseId: string,
    query?: { skip?: number; take?: number; pinnedFirst?: boolean },
  ): Promise<Result<Types.LearningExperienceSocialServicesCourseDiscussionDto[], ApiError>> {
    const url = `/api/social/courses/${courseId}/discussions`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    return result as Result<Types.LearningExperienceSocialServicesCourseDiscussionDto[], ApiError>;
  }

  /**
   * Creates a new discussion thread
   */
  async postApiSocialDiscussions(
    body: Types.LearningExperienceSocialServicesCreateDiscussionInput,
  ): Promise<Result<Types.LearningExperienceSocialServicesCourseDiscussionDto, ApiError>> {
    const url = '/api/social/discussions';

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceSocialServicesCreateDiscussionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesCourseDiscussionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Gets a discussion by ID
   */
  async getApiSocialDiscussions(id: string): Promise<Result<Types.LearningExperienceSocialServicesCourseDiscussionDto, ApiError>> {
    const url = `/api/social/discussions/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesCourseDiscussionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Deletes a discussion (owner only)
   */
  async deleteApiSocialDiscussions(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/discussions/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Pins a discussion (instructor/admin only)
   */
  async postApiSocialDiscussionsPin(id: string): Promise<Result<Types.LearningExperienceSocialServicesCourseDiscussionDto, ApiError>> {
    const url = `/api/social/discussions/${id}/pin`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesCourseDiscussionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Marks a discussion as resolved
   */
  async postApiSocialDiscussionsResolve(id: string): Promise<Result<Types.LearningExperienceSocialServicesCourseDiscussionDto, ApiError>> {
    const url = `/api/social/discussions/${id}/resolve`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesCourseDiscussionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Unpins a discussion
   */
  async postApiSocialDiscussionsUnpin(id: string): Promise<Result<Types.LearningExperienceSocialServicesCourseDiscussionDto, ApiError>> {
    const url = `/api/social/discussions/${id}/unpin`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesCourseDiscussionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createLearningExperienceSocialDiscussionsModule(client: ApiClient): LearningExperienceSocialDiscussionsModule {
  return new LearningExperienceSocialDiscussionsModule(client);
}
