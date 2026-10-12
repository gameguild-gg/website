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
   * Create a new assessment for a course
   */
  async postAssessments(body: Types.LearningAssessmentsCreateAssessmentInput): Promise<Result<Types.LearningAssessmentsAssessmentDto, ApiError>> {
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
      const validatedData = safeParse(Types.LearningAssessmentsAssessmentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Check if user can attempt an assessment
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
   * Get the SpeedGrader navigation queue for an assessment (instructor-only):
   * one item per student/group representing the target's latest gradeable attempt,
   * excluding InProgress-only targets.
   */
  async getAssessmentsGradingQueue(assessmentId: string): Promise<Result<Types.LearningAssessmentsGradingQueueDto, ApiError>> {
    const url = `/v1/assessments/${assessmentId}/grading-queue`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingQueueDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Gets delivery-safe active cues for an enrolled learner and one video content item.
   */
  async getAssessmentsInteractiveVideoCuesContentEnrollments(
    assessmentId: string,
    contentId: string,
    enrollmentId: string,
  ): Promise<Result<Types.LearningAssessmentsLearnerInteractiveVideoAssessmentCueDto[], ApiError>> {
    const url = `/v1/assessments/${assessmentId}/interactive-video-cues/content/${contentId}/enrollments/${enrollmentId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LearningAssessmentsLearnerInteractiveVideoAssessmentCueDto[], ApiError>;
  }

  /**
   * Get all submissions for an assessment
   */
  async getAssessmentsSubmissionsForGetAssessmentsByAssessmentIdSubmissions(
    assessmentId: string,
  ): Promise<Result<Types.LearningAssessmentsAssessmentSubmissionDto[], ApiError>> {
    const url = `/v1/assessments/${assessmentId}/submissions`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LearningAssessmentsAssessmentSubmissionDto[], ApiError>;
  }

  /**
   * Start a new assessment attempt
   */
  async postAssessmentsSubmissionsStart(
    assessmentId: string,
    body: Types.LearningAssessmentsStartSubmissionInput,
  ): Promise<Result<Types.LearningAssessmentsLearnerAssessmentAttemptDto, ApiError>> {
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
      const validatedData = safeParse(Types.LearningAssessmentsLearnerAssessmentAttemptDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get an assessment by ID
   */
  async getAssessments(id: string): Promise<Result<Types.LearningAssessmentsAssessmentDto, ApiError>> {
    const url = `/v1/assessments/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsAssessmentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update an assessment
   */
  async putAssessments(id: string, body: Types.LearningAssessmentsUpdateAssessmentInput): Promise<Result<Types.LearningAssessmentsAssessmentDto, ApiError>> {
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
      const validatedData = safeParse(Types.LearningAssessmentsAssessmentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Delete an assessment
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
   * Prepares an immutable candidate revision for instructor testing.
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
   * Assign an assessment to a weighted group or clear the assignment.
   */
  async putAssessmentsGroup(
    id: string,
    body: Types.LearningAssessmentsAssignAssessmentGroupInput,
  ): Promise<Result<Types.LearningAssessmentsAssessmentDto, ApiError>> {
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
      const validatedData = safeParse(Types.LearningAssessmentsAssessmentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Gets the interactive-video cue links for this assessment.
   */
  async getAssessmentsInteractiveVideoCues(id: string): Promise<Result<Types.LearningAssessmentsInteractiveVideoAssessmentCueDto[], ApiError>> {
    const url = `/v1/assessments/${id}/interactive-video-cues`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LearningAssessmentsInteractiveVideoAssessmentCueDto[], ApiError>;
  }

  /**
   * Links this assessment to a cue in an interactive-video lesson.
   */
  async postAssessmentsInteractiveVideoCues(
    id: string,
    body: Types.LearningAssessmentsLinkInteractiveVideoCueInput,
  ): Promise<Result<Types.LearningAssessmentsInteractiveVideoAssessmentCueDto, ApiError>> {
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
      const validatedData = safeParse(Types.LearningAssessmentsInteractiveVideoAssessmentCueDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Removes a manager-configured interactive-video cue link.
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
   * Restore a soft-deleted assessment
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
   * Prepares an immutable candidate revision for instructor testing.
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
   * Publishes exactly the prepared revision after official capability validation.
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
   * Stops new official starts without deleting revisions or existing executions.
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
  async postAssessmentsRuntimeSubmissionsCollective(
    id: string,
    body: Types.LearningAssessmentsStartCollectiveRuntimeSubmissionInput,
  ): Promise<Result<Types.LearningAssessmentsGradingRuntimeAssessmentSubmissionViewV1, ApiError>> {
    const url = `/v1/assessments/${id}/runtime-submissions/collective`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsStartCollectiveRuntimeSubmissionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingRuntimeAssessmentSubmissionViewV1Schema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAssessmentsRuntimeSubmissionsIndividual(
    id: string,
    body: Types.LearningAssessmentsStartIndividualRuntimeSubmissionInput,
  ): Promise<Result<Types.LearningAssessmentsGradingRuntimeAssessmentSubmissionViewV1, ApiError>> {
    const url = `/v1/assessments/${id}/runtime-submissions/individual`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsStartIndividualRuntimeSubmissionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingRuntimeAssessmentSubmissionViewV1Schema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAssessmentsTestRuns(
    id: string,
    body: Types.LearningAssessmentsStartAssessmentTestRunInput,
  ): Promise<Result<Types.LearningAssessmentsGradingRuntimeAssessmentTestRunViewV1, ApiError>> {
    const url = `/v1/assessments/${id}/test-runs`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsStartAssessmentTestRunInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingRuntimeAssessmentTestRunViewV1Schema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Starts or resumes the current learner's official attempt for graded content.
   * The server resolves both the linked assessment and the active course membership.
   */
  async postAssessmentsContentRuntimeSubmissionsIndividual(
    contentId: string,
    body: Types.LearningAssessmentsStartContentRuntimeSubmissionInput,
  ): Promise<Result<Types.LearningAssessmentsGradingRuntimeAssessmentSubmissionViewV1, ApiError>> {
    const url = `/v1/assessments/content/${contentId}/runtime-submissions/individual`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsStartContentRuntimeSubmissionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingRuntimeAssessmentSubmissionViewV1Schema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get all assessments for a course
   */
  async getAssessmentsCourse(courseId: string): Promise<Result<Types.LearningAssessmentsAssessmentDto[], ApiError>> {
    const url = `/v1/assessments/course/${courseId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LearningAssessmentsAssessmentDto[], ApiError>;
  }

  /**
   * Get assessment score distribution and weighted group performance for a course.
   */
  async getAssessmentsCourseAnalytics(courseId: string): Promise<Result<Types.LearningAssessmentsCourseAssessmentAnalyticsDto, ApiError>> {
    const url = `/v1/assessments/course/${courseId}/analytics`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsCourseAssessmentAnalyticsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Atomically saves assessable content and its assessment policy.
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
   * Returns the single canonical gradebook projection for one course enrollment.
   */
  async getAssessmentsCourseGradebook(
    courseId: string,
    enrollmentId: string,
  ): Promise<Result<Types.LearningAssessmentsGradingRuntimeGradebookCourseProjectionV1, ApiError>> {
    const url = `/v1/assessments/course/${courseId}/gradebook/${enrollmentId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingRuntimeGradebookCourseProjectionV1Schema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get weighted assessment groups for a course.
   */
  async getAssessmentsCourseGroups(courseId: string): Promise<Result<Types.LearningAssessmentsAssessmentGroupDto[], ApiError>> {
    const url = `/v1/assessments/course/${courseId}/groups`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LearningAssessmentsAssessmentGroupDto[], ApiError>;
  }

  /**
   * Create a weighted assessment group.
   */
  async postAssessmentsGroups(
    body: Types.LearningAssessmentsCreateAssessmentGroupInput,
  ): Promise<Result<Types.LearningAssessmentsAssessmentGroupDto, ApiError>> {
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
      const validatedData = safeParse(Types.LearningAssessmentsAssessmentGroupDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update a weighted assessment group.
   */
  async putAssessmentsGroups(
    id: string,
    body: Types.LearningAssessmentsUpdateAssessmentGroupInput,
  ): Promise<Result<Types.LearningAssessmentsAssessmentGroupDto, ApiError>> {
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
      const validatedData = safeParse(Types.LearningAssessmentsAssessmentGroupDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Delete a weighted assessment group.
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
   * Get my submissions for an enrollment
   */
  async getAssessmentsMySubmissions(enrollmentId: string): Promise<Result<Types.LearningAssessmentsLearnerAssessmentSubmissionDto[], ApiError>> {
    const url = `/v1/assessments/my-submissions/${enrollmentId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LearningAssessmentsLearnerAssessmentSubmissionDto[], ApiError>;
  }

  /**
   */
  async getAssessmentsRuntimeSubmissions(submissionId: string): Promise<Result<Types.LearningAssessmentsGradingRuntimeAssessmentSubmissionViewV1, ApiError>> {
    const url = `/v1/assessments/runtime-submissions/${submissionId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingRuntimeAssessmentSubmissionViewV1Schema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putAssessmentsRuntimeSubmissionsDraft(
    submissionId: string,
    body: Types.LearningAssessmentsSaveCollectiveRuntimeDraftInput,
  ): Promise<Result<Types.LearningAssessmentsGradingRuntimeAssessmentSubmissionViewV1, ApiError>> {
    const url = `/v1/assessments/runtime-submissions/${submissionId}/draft`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsSaveCollectiveRuntimeDraftInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingRuntimeAssessmentSubmissionViewV1Schema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAssessmentsRuntimeSubmissionsInstructorReview(
    submissionId: string,
    body: Types.LearningAssessmentsResolveInstructorReviewInput,
  ): Promise<Result<Types.LearningAssessmentsGradingRuntimeAssessmentSubmissionViewV1, ApiError>> {
    const url = `/v1/assessments/runtime-submissions/${submissionId}/instructor-review`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsResolveInstructorReviewInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingRuntimeAssessmentSubmissionViewV1Schema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAssessmentsRuntimeSubmissionsRegrade(
    submissionId: string,
    body: Types.LearningAssessmentsGradingRuntimeRegradeExecutionCommand,
  ): Promise<Result<Types.LearningAssessmentsGradingRuntimeAssessmentSubmissionViewV1, ApiError>> {
    const url = `/v1/assessments/runtime-submissions/${submissionId}/regrade`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsGradingRuntimeRegradeExecutionCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingRuntimeAssessmentSubmissionViewV1Schema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAssessmentsRuntimeSubmissionsRelease(
    submissionId: string,
    body: Types.LearningAssessmentsGradingRuntimeReleaseGradeResultCommand,
  ): Promise<Result<Types.LearningAssessmentsGradeResultReleaseOutput, ApiError>> {
    const url = `/v1/assessments/runtime-submissions/${submissionId}/release`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsGradingRuntimeReleaseGradeResultCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradeResultReleaseOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAssessmentsRuntimeSubmissionsSubmit(
    submissionId: string,
    body: Types.LearningAssessmentsSubmitAssessmentRuntimeInput,
  ): Promise<Result<Types.LearningAssessmentsGradingRuntimeAssessmentSubmissionViewV1, ApiError>> {
    const url = `/v1/assessments/runtime-submissions/${submissionId}/submit`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsSubmitAssessmentRuntimeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingRuntimeAssessmentSubmissionViewV1Schema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get a submission by ID
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
   * Submit a completed assessment
   */
  async postAssessmentsSubmissionsSubmit(
    submissionId: string,
    body: Types.LearningAssessmentsSubmitAssessmentInput,
  ): Promise<Result<Types.LearningAssessmentsLearnerAssessmentSubmissionDto, ApiError>> {
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
      const validatedData = safeParse(Types.LearningAssessmentsLearnerAssessmentSubmissionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAssessmentsTestRuns(testRunId: string): Promise<Result<Types.LearningAssessmentsGradingRuntimeAssessmentTestRunViewV1, ApiError>> {
    const url = `/v1/assessments/test-runs/${testRunId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingRuntimeAssessmentTestRunViewV1Schema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAssessmentsTestRunsInstructorReview(
    testRunId: string,
    body: Types.LearningAssessmentsResolveInstructorReviewInput,
  ): Promise<Result<Types.LearningAssessmentsGradingRuntimeAssessmentTestRunViewV1, ApiError>> {
    const url = `/v1/assessments/test-runs/${testRunId}/instructor-review`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsResolveInstructorReviewInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingRuntimeAssessmentTestRunViewV1Schema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAssessmentsTestRunsRestart(
    testRunId: string,
    body: Types.LearningAssessmentsIdempotentRuntimeInput,
  ): Promise<Result<Types.LearningAssessmentsGradingRuntimeAssessmentTestRunViewV1, ApiError>> {
    const url = `/v1/assessments/test-runs/${testRunId}/restart`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsIdempotentRuntimeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingRuntimeAssessmentTestRunViewV1Schema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAssessmentsTestRunsSubmit(
    testRunId: string,
    body: Types.LearningAssessmentsSubmitAssessmentRuntimeInput,
  ): Promise<Result<Types.LearningAssessmentsGradingRuntimeAssessmentTestRunViewV1, ApiError>> {
    const url = `/v1/assessments/test-runs/${testRunId}/submit`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsSubmitAssessmentRuntimeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGradingRuntimeAssessmentTestRunViewV1Schema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createLearningAssessmentsModule(client: ApiClient): LearningAssessmentsModule {
  return new LearningAssessmentsModule(client);
}
