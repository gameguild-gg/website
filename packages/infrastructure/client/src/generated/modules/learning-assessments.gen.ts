/**
 * @game-guild/client - LearningAssessments Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningAssessmentsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async postAssessments(body: Types.LearningAssessmentsCreateAssessmentInput): Promise<Result<Types.LearningAssessmentsAssessment, ApiError>> {
    const url = '/v1/assessments';

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsCreateAssessmentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsAssessmentSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAssessmentsCanAttempt(assessmentId: string, enrollmentId: string): Promise<Result<Types.LearningAssessmentsCanAttemptOutput, ApiError>> {
    const url = `/v1/assessments/${assessmentId}/can-attempt/${enrollmentId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsCanAttemptOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAssessmentsGradingQueue(assessmentId: string): Promise<Result<Types.LearningAssessmentsGradingQueue, ApiError>> {
    const url = `/v1/assessments/${assessmentId}/grading-queue`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingQueueSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAssessmentsInteractiveVideoCuesContentEnrollments(
    assessmentId: string,
    contentId: string,
    enrollmentId: string,
  ): Promise<Result<Array<Types.LearningAssessmentsLearnerInteractiveVideoAssessmentCue>, ApiError>> {
    const url = `/v1/assessments/${assessmentId}/interactive-video-cues/content/${contentId}/enrollments/${enrollmentId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningAssessmentsLearnerInteractiveVideoAssessmentCue>, ApiError>;
  }

  /**
   */
  async getAssessmentsSubmissionsForGetAssessmentsByAssessmentIdSubmissions(
    assessmentId: string,
  ): Promise<Result<Array<Types.LearningAssessmentsAssessmentSubmission>, ApiError>> {
    const url = `/v1/assessments/${assessmentId}/submissions`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningAssessmentsAssessmentSubmission>, ApiError>;
  }

  /**
   */
  async postAssessmentsSubmissionsStart(
    assessmentId: string,
    body: Types.LearningAssessmentsStartSubmissionInput,
  ): Promise<Result<Types.LearningAssessmentsLearnerAssessmentAttempt, ApiError>> {
    const url = `/v1/assessments/${assessmentId}/submissions/start`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsStartSubmissionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsLearnerAssessmentAttemptSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAssessments(id: string): Promise<Result<Types.LearningAssessmentsAssessment, ApiError>> {
    const url = `/v1/assessments/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsAssessmentSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putAssessments(id: string, body: Types.LearningAssessmentsUpdateAssessmentInput): Promise<Result<Types.LearningAssessmentsAssessment, ApiError>> {
    const url = `/v1/assessments/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsUpdateAssessmentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsAssessmentSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteAssessments(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/assessments/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getAssessmentsAuthoringState(id: string): Promise<Result<Types.LearningAssessmentsGradingAuthoringAssessmentAuthoringStateResult, ApiError>> {
    const url = `/v1/assessments/${id}/authoring-state`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingAuthoringAssessmentAuthoringStateResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putAssessmentsGroup(
    id: string,
    body: Types.LearningAssessmentsAssignAssessmentGroupInput,
  ): Promise<Result<Types.LearningAssessmentsAssessment, ApiError>> {
    const url = `/v1/assessments/${id}/group`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsAssignAssessmentGroupInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsAssessmentSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAssessmentsInteractiveVideoCues(id: string): Promise<Result<Array<Types.LearningAssessmentsInteractiveVideoAssessmentCue>, ApiError>> {
    const url = `/v1/assessments/${id}/interactive-video-cues`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningAssessmentsInteractiveVideoAssessmentCue>, ApiError>;
  }

  /**
   */
  async postAssessmentsInteractiveVideoCues(
    id: string,
    body: Types.LearningAssessmentsLinkInteractiveVideoCueInput,
  ): Promise<Result<Types.LearningAssessmentsInteractiveVideoAssessmentCue, ApiError>> {
    const url = `/v1/assessments/${id}/interactive-video-cues`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsLinkInteractiveVideoCueInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsInteractiveVideoAssessmentCueSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteAssessmentsInteractiveVideoCues(id: string, cueId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/assessments/${id}/interactive-video-cues/${cueId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postAssessmentsRestore(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/assessments/${id}/restore`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postAssessmentsRevisionsPrepare(
    id: string,
    body: Types.LearningAssessmentsGradingAuthoringPrepareAssessmentRevisionInput,
  ): Promise<Result<Types.LearningAssessmentsGradingAuthoringPreparedAssessmentRevisionResult, ApiError>> {
    const url = `/v1/assessments/${id}/revisions/prepare`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsGradingAuthoringPrepareAssessmentRevisionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingAuthoringPreparedAssessmentRevisionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAssessmentsRevisionsPublish(
    id: string,
    body: Types.LearningAssessmentsGradingAuthoringPublishAssessmentRevisionInput,
  ): Promise<Result<Types.LearningAssessmentsGradingAuthoringPreparedAssessmentRevisionResult, ApiError>> {
    const url = `/v1/assessments/${id}/revisions/publish`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsGradingAuthoringPublishAssessmentRevisionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingAuthoringPreparedAssessmentRevisionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAssessmentsRevisionsUnpublish(
    id: string,
    body: Types.LearningAssessmentsGradingAuthoringUnpublishAssessmentRevisionInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/v1/assessments/${id}/revisions/unpublish`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsGradingAuthoringUnpublishAssessmentRevisionInputSchema, body, 'request');

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
  async getAssessmentsCourse(courseId: string): Promise<Result<Array<Types.LearningAssessmentsAssessment>, ApiError>> {
    const url = `/v1/assessments/course/${courseId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningAssessmentsAssessment>, ApiError>;
  }

  /**
   */
  async getAssessmentsCourseAnalytics(courseId: string): Promise<Result<Types.LearningAssessmentsCourseAssessmentAnalytics, ApiError>> {
    const url = `/v1/assessments/course/${courseId}/analytics`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsCourseAssessmentAnalyticsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putAssessmentsCourseContentDraft(
    courseId: string,
    contentId: string,
    body: Types.LearningAssessmentsGradingAuthoringSaveAssessmentDraftInput,
  ): Promise<Result<Types.LearningAssessmentsGradingAuthoringAssessmentDraftResult, ApiError>> {
    const url = `/v1/assessments/course/${courseId}/content/${contentId}/draft`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsGradingAuthoringSaveAssessmentDraftInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingAuthoringAssessmentDraftResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAssessmentsCourseGroups(courseId: string): Promise<Result<Array<Types.LearningAssessmentsAssessmentGroup>, ApiError>> {
    const url = `/v1/assessments/course/${courseId}/groups`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningAssessmentsAssessmentGroup>, ApiError>;
  }

  /**
   */
  async postAssessmentsGroups(body: Types.LearningAssessmentsCreateAssessmentGroupInput): Promise<Result<Types.LearningAssessmentsAssessmentGroup, ApiError>> {
    const url = '/v1/assessments/groups';

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsCreateAssessmentGroupInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsAssessmentGroupSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putAssessmentsGroups(
    id: string,
    body: Types.LearningAssessmentsUpdateAssessmentGroupInput,
  ): Promise<Result<Types.LearningAssessmentsAssessmentGroup, ApiError>> {
    const url = `/v1/assessments/groups/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsUpdateAssessmentGroupInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsAssessmentGroupSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteAssessmentsGroups(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/assessments/groups/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getAssessmentsMySubmissions(enrollmentId: string): Promise<Result<Array<Types.LearningAssessmentsLearnerAssessmentSubmission>, ApiError>> {
    const url = `/v1/assessments/my-submissions/${enrollmentId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningAssessmentsLearnerAssessmentSubmission>, ApiError>;
  }

  /**
   */
  async getAssessmentsSubmissionsForGetAssessmentsSubmissionsBySubmissionId(submissionId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/assessments/submissions/${submissionId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postAssessmentsSubmissionsGrade(
    submissionId: string,
    body: Types.LearningAssessmentsGradeSubmissionInput,
  ): Promise<Result<Types.LearningAssessmentsAssessmentSubmission, ApiError>> {
    const url = `/v1/assessments/submissions/${submissionId}/grade`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsGradeSubmissionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsAssessmentSubmissionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAssessmentsSubmissionsSubmit(
    submissionId: string,
    body: Types.LearningAssessmentsSubmitAssessmentInput,
  ): Promise<Result<Types.LearningAssessmentsLearnerAssessmentSubmission, ApiError>> {
    const url = `/v1/assessments/submissions/${submissionId}/submit`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsSubmitAssessmentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsLearnerAssessmentSubmissionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createLearningAssessmentsModule(client: ApiClient): LearningAssessmentsModule {
  return new LearningAssessmentsModule(client);
}
