/**
 * @game-guild/client - LearningCoursesContentInteraction Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningCoursesContentInteractionModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async postCourseInteractions(
    body: Types.LearningCoursesStartContentInput,
    query?: { programId?: string },
  ): Promise<Result<Types.LearningCoursesContentInteractionDto, ApiError>> {
    const url = '/v1/course-interactions';

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesStartContentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
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
  async postCourseInteractionsComplete(
    interactionId: string,
    body: Types.LearningCoursesCompleteContentInput,
    query?: { programId?: string },
  ): Promise<Result<Types.LearningCoursesContentInteractionDto, ApiError>> {
    const url = `/v1/course-interactions/${interactionId}/complete`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesCompleteContentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
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
  async putCourseInteractionsProgress(
    interactionId: string,
    body: Types.LearningCoursesUpdateProgressInput,
    query?: { programId?: string },
  ): Promise<Result<Types.LearningCoursesContentInteractionDto, ApiError>> {
    const url = `/v1/course-interactions/${interactionId}/progress`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesUpdateProgressInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      params: query,
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
  async postCourseInteractionsSubmit(
    interactionId: string,
    body: Types.LearningCoursesSubmitContentInput,
    query?: { programId?: string },
  ): Promise<Result<Types.LearningCoursesContentInteractionDto, ApiError>> {
    const url = `/v1/course-interactions/${interactionId}/submit`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesSubmitContentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
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
  async putCourseInteractionsTimeSpent(
    interactionId: string,
    body: Types.LearningCoursesUpdateTimeSpentInput,
    query?: { programId?: string },
  ): Promise<Result<Types.LearningCoursesContentInteractionDto, ApiError>> {
    const url = `/v1/course-interactions/${interactionId}/time-spent`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesUpdateTimeSpentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      params: query,
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
  async getCourseInteractionsContentReflectionResponses(
    contentId: string,
    query?: { programId?: string },
  ): Promise<Result<Array<Types.LearningCoursesReflectionResponseResultDto>, ApiError>> {
    const url = `/v1/course-interactions/content/${contentId}/reflection-responses`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesReflectionResponseResultDto>, ApiError>;
  }

  /**
   */
  async getCourseInteractionsContentReflectionResponsesVisible(
    contentId: string,
    query?: { programId?: string },
  ): Promise<Result<Array<Types.LearningCoursesReflectionResponseResultDto>, ApiError>> {
    const url = `/v1/course-interactions/content/${contentId}/reflection-responses/visible`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesReflectionResponseResultDto>, ApiError>;
  }

  /**
   */
  async getCourseInteractionsContentSurveyResults(
    contentId: string,
    query?: { programId?: string },
  ): Promise<Result<Array<Types.LearningCoursesSurveyResponseResultDto>, ApiError>> {
    const url = `/v1/course-interactions/content/${contentId}/survey-results`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesSurveyResponseResultDto>, ApiError>;
  }

  /**
   */
  async getCourseInteractionsContentSurveyResultsVisible(
    contentId: string,
    query?: { programId?: string },
  ): Promise<Result<Array<Types.LearningCoursesSurveyResponseResultDto>, ApiError>> {
    const url = `/v1/course-interactions/content/${contentId}/survey-results/visible`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesSurveyResponseResultDto>, ApiError>;
  }

  /**
   */
  async getCourseInteractionsUser(
    programUserId: string,
    query?: { programId?: string },
  ): Promise<Result<Array<Types.LearningCoursesContentInteractionDto>, ApiError>> {
    const url = `/v1/course-interactions/user/${programUserId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesContentInteractionDto>, ApiError>;
  }

  /**
   */
  async getCourseInteractionsUserContent(
    programUserId: string,
    contentId: string,
    query?: { programId?: string },
  ): Promise<Result<Types.LearningCoursesContentInteractionDto, ApiError>> {
    const url = `/v1/course-interactions/user/${programUserId}/content/${contentId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesContentInteractionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createLearningCoursesContentInteractionModule(client: ApiClient): LearningCoursesContentInteractionModule {
  return new LearningCoursesContentInteractionModule(client);
}
