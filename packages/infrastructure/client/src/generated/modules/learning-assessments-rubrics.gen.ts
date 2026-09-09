/**
 * @game-guild/client - LearningAssessmentsRubrics Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningAssessmentsRubricsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getAssessmentsRubric(assessmentId: string): Promise<Result<Types.LearningAssessmentsRubricDto, ApiError>> {
    const url = `/v1/assessments/${assessmentId}/rubric`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsRubricDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putAssessmentsRubric(
    assessmentId: string,
    body: Types.LearningAssessmentsSaveRubricInput,
  ): Promise<Result<Types.LearningAssessmentsRubricDto, ApiError>> {
    const url = `/v1/assessments/${assessmentId}/rubric`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsSaveRubricInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsRubricDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteAssessmentsRubric(assessmentId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/assessments/${assessmentId}/rubric`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createLearningAssessmentsRubricsModule(client: ApiClient): LearningAssessmentsRubricsModule {
  return new LearningAssessmentsRubricsModule(client);
}
