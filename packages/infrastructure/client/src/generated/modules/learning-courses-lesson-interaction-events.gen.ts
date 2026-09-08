/**
 * @game-guild/client - LearningCoursesLessonInteractionEvents Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningCoursesLessonInteractionEventsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getCoursesInteractionsEvents(
    programId: string,
    interactionId: string,
  ): Promise<Result<Array<Types.LearningCoursesContentInteractionEventDto>, ApiError>> {
    const url = `/v1/courses/${programId}/interactions/${interactionId}/events`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesContentInteractionEventDto>, ApiError>;
  }

  /**
   */
  async postCoursesInteractionsEvents(
    programId: string,
    interactionId: string,
    body: Types.LearningCoursesRecordContentInteractionEventInput,
  ): Promise<Result<Types.LearningCoursesContentInteractionEventDto, ApiError>> {
    const url = `/v1/courses/${programId}/interactions/${interactionId}/events`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesRecordContentInteractionEventInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesContentInteractionEventDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createLearningCoursesLessonInteractionEventsModule(client: ApiClient): LearningCoursesLessonInteractionEventsModule {
  return new LearningCoursesLessonInteractionEventsModule(client);
}
