/**
 * @game-guild/client - LearningAssessmentsPeerReviews Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningAssessmentsPeerReviewsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async postAssessmentsPeerReviewsClaim(assessmentId: string): Promise<Result<Types.LearningAssessmentsPeerReviewClaimDto, ApiError>> {
    const url = `/v1/assessments/${assessmentId}/peer-reviews/claim`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsPeerReviewClaimDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAssessmentsPeerReviews(reviewId: string): Promise<Result<Types.LearningAssessmentsAnonymousReviewSubmissionDto, ApiError>> {
    const url = `/v1/assessments/peer-reviews/${reviewId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsAnonymousReviewSubmissionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAssessmentsPeerReviewsSubmit(reviewId: string, body: Types.LearningAssessmentsPeerReviewSubmitInput): Promise<Result<void, ApiError>> {
    const url = `/v1/assessments/peer-reviews/${reviewId}/submit`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsPeerReviewSubmitInputSchema, body, 'request');

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
  async getAssessmentsSubmissionsPeerReviews(submissionId: string): Promise<Result<Array<Types.LearningAssessmentsInstructorPeerReviewDto>, ApiError>> {
    const url = `/v1/assessments/submissions/${submissionId}/peer-reviews`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningAssessmentsInstructorPeerReviewDto>, ApiError>;
  }

  /**
   */
  async getAssessmentsSubmissionsReceivedPeerReviews(submissionId: string): Promise<Result<Array<Types.LearningAssessmentsReceivedPeerReviewDto>, ApiError>> {
    const url = `/v1/assessments/submissions/${submissionId}/received-peer-reviews`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningAssessmentsReceivedPeerReviewDto>, ApiError>;
  }
}

export function createLearningAssessmentsPeerReviewsModule(client: ApiClient): LearningAssessmentsPeerReviewsModule {
  return new LearningAssessmentsPeerReviewsModule(client);
}
