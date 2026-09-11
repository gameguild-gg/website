/**
 * @game-guild/client - LearningCoursesProgramContentAuthoring Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningCoursesProgramContentAuthoringModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getCoursesContentAuthoring(programId: string, contentId: string): Promise<Result<Types.LearningCoursesAuthoringDraftDto, ApiError>> {
    const url = `/v1/courses/${programId}/content/${contentId}/authoring`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesAuthoringDraftDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putCoursesContentAuthoring(
    programId: string,
    contentId: string,
    body: Types.LearningCoursesSaveAuthoringDraftInput,
  ): Promise<Result<Types.LearningCoursesAuthoringDraftDto, ApiError>> {
    const url = `/v1/courses/${programId}/content/${contentId}/authoring`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesSaveAuthoringDraftInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesAuthoringDraftDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesContentAuthoringAiConversations(
    programId: string,
    contentId: string,
  ): Promise<Result<Array<Types.LearningCoursesAiAuthoringConversationDto>, ApiError>> {
    const url = `/v1/courses/${programId}/content/${contentId}/authoring/ai/conversations`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCoursesAiAuthoringConversationDto>, ApiError>;
  }

  /**
   */
  async getCoursesContentAuthoringAiEntitlement(programId: string, contentId: string): Promise<Result<Types.LearningCoursesAiEntitlementDto, ApiError>> {
    const url = `/v1/courses/${programId}/content/${contentId}/authoring/ai/entitlement`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesAiEntitlementDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteCoursesContentAuthoringAiProposals(
    programId: string,
    contentId: string,
    proposalId: string,
  ): Promise<Result<Types.LearningCoursesAiProposalDto, ApiError>> {
    const url = `/v1/courses/${programId}/content/${contentId}/authoring/ai/proposals/${proposalId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesAiProposalDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postCoursesContentAuthoringAiProposalsApply(
    programId: string,
    contentId: string,
    proposalId: string,
    body: Types.LearningCoursesApplyAiProposalInput,
  ): Promise<Result<Types.LearningCoursesAuthoringDraftDto, ApiError>> {
    const url = `/v1/courses/${programId}/content/${contentId}/authoring/ai/proposals/${proposalId}/apply`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesApplyAiProposalInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesAuthoringDraftDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postCoursesContentAuthoringAiRuns(
    programId: string,
    contentId: string,
    body: Types.LearningCoursesAiAuthoringRunInput,
  ): Promise<Result<Types.LearningCoursesAiAuthoringRunDto, ApiError>> {
    const url = `/v1/courses/${programId}/content/${contentId}/authoring/ai/runs`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesAiAuthoringRunInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesAiAuthoringRunDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesContentAuthoringAiRuns(
    programId: string,
    contentId: string,
    runId: string,
  ): Promise<Result<Types.LearningCoursesAiAuthoringRunDto, ApiError>> {
    const url = `/v1/courses/${programId}/content/${contentId}/authoring/ai/runs/${runId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesAiAuthoringRunDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesContentAuthoringAiRunsStream(programId: string, contentId: string, runId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/courses/${programId}/content/${contentId}/authoring/ai/runs/${runId}/stream`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postCoursesContentAuthoringPublish(
    programId: string,
    contentId: string,
    body: Types.LearningCoursesPublishAuthoringDraftInput,
  ): Promise<Result<Types.LearningCoursesPublishAuthoringResult, ApiError>> {
    const url = `/v1/courses/${programId}/content/${contentId}/authoring/publish`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesPublishAuthoringDraftInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCoursesPublishAuthoringResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createLearningCoursesProgramContentAuthoringModule(client: ApiClient): LearningCoursesProgramContentAuthoringModule {
  return new LearningCoursesProgramContentAuthoringModule(client);
}
