/**
 * @game-guild/client - ResourcesContentsVersioning Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ResourcesContentsVersioningModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get a specific version
   */
  async getApiContentsVersioning(versionId: string): Promise<Result<Types.ResourcesContentsContentVersionDto, ApiError>> {
    const url = `/api/contents/versioning/${versionId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesContentsContentVersionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Approve a version
   */
  async postApiContentsVersioningApprove(
    versionId: string,
    body: Types.ResourcesContentsReviewInput,
  ): Promise<Result<Types.ResourcesContentsContentVersionDto, ApiError>> {
    const url = `/api/contents/versioning/${versionId}/approve`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesContentsReviewInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesContentsContentVersionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Cancel scheduled publishing
   */
  async postApiContentsVersioningCancelSchedule(versionId: string): Promise<Result<Types.ResourcesContentsContentVersionDto, ApiError>> {
    const url = `/api/contents/versioning/${versionId}/cancel-schedule`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesContentsContentVersionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Publish a version
   */
  async postApiContentsVersioningPublish(versionId: string): Promise<Result<Types.ResourcesContentsContentVersionDto, ApiError>> {
    const url = `/api/contents/versioning/${versionId}/publish`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesContentsContentVersionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Reject a version
   */
  async postApiContentsVersioningReject(
    versionId: string,
    body: Types.ResourcesContentsReviewInput,
  ): Promise<Result<Types.ResourcesContentsContentVersionDto, ApiError>> {
    const url = `/api/contents/versioning/${versionId}/reject`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesContentsReviewInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesContentsContentVersionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Add a review to a version
   */
  async postApiContentsVersioningReviews(
    versionId: string,
    body: Types.ResourcesContentsAddReviewInput,
  ): Promise<Result<Types.ResourcesContentsContentVersionReviewDto, ApiError>> {
    const url = `/api/contents/versioning/${versionId}/reviews`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesContentsAddReviewInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesContentsContentVersionReviewDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Schedule a version for publishing
   */
  async postApiContentsVersioningSchedule(
    versionId: string,
    body: Types.ResourcesContentsScheduleInput,
  ): Promise<Result<Types.ResourcesContentsContentVersionDto, ApiError>> {
    const url = `/api/contents/versioning/${versionId}/schedule`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesContentsScheduleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesContentsContentVersionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Submit a draft for review
   */
  async postApiContentsVersioningSubmitForReview(versionId: string): Promise<Result<Types.ResourcesContentsContentVersionDto, ApiError>> {
    const url = `/api/contents/versioning/${versionId}/submit-for-review`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesContentsContentVersionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Compare two versions
   */
  async getApiContentsVersioningCompare(query?: {
    versionId1?: string;
    versionId2?: string;
  }): Promise<Result<Types.ResourcesContentsContentVersionDiff, ApiError>> {
    const url = '/api/contents/versioning/compare';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesContentsContentVersionDiffSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Create a new draft version
   */
  async postApiContentsVersioningDrafts(body: Types.ResourcesContentsCreateDraftInput): Promise<Result<Types.ResourcesContentsContentVersionDto, ApiError>> {
    const url = '/api/contents/versioning/drafts';

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesContentsCreateDraftInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesContentsContentVersionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update a draft version
   */
  async putApiContentsVersioningDrafts(
    versionId: string,
    body: Types.ResourcesContentsUpdateDraftInput,
  ): Promise<Result<Types.ResourcesContentsContentVersionDto, ApiError>> {
    const url = `/api/contents/versioning/drafts/${versionId}`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesContentsUpdateDraftInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesContentsContentVersionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get the current published version for an entity
   */
  async getApiContentsVersioningEntityCurrent(entityType: string, entityId: string): Promise<Result<Types.ResourcesContentsContentVersionDto, ApiError>> {
    const url = `/api/contents/versioning/entity/${entityType}/${entityId}/current`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesContentsContentVersionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get version history for an entity
   */
  async getApiContentsVersioningEntityHistory(entityType: string, entityId: string): Promise<Result<Types.ResourcesContentsContentVersionDto[], ApiError>> {
    const url = `/api/contents/versioning/entity/${entityType}/${entityId}/history`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ResourcesContentsContentVersionDto[], ApiError>;
  }

  /**
   * Rollback to a previous version
   */
  async postApiContentsVersioningEntityRollback(
    entityType: string,
    entityId: string,
    body: Types.ResourcesContentsRollbackInput,
  ): Promise<Result<Types.ResourcesContentsContentVersionDto, ApiError>> {
    const url = `/api/contents/versioning/entity/${entityType}/${entityId}/rollback`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesContentsRollbackInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesContentsContentVersionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get a specific version by number
   */
  async getApiContentsVersioningEntityVersion(
    entityType: string,
    entityId: string,
    versionNumber: number,
  ): Promise<Result<Types.ResourcesContentsContentVersionDto, ApiError>> {
    const url = `/api/contents/versioning/entity/${entityType}/${entityId}/version/${versionNumber}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesContentsContentVersionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get versions pending review
   */
  async getApiContentsVersioningPendingReview(query?: {
    entityType?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ResourcesContentsContentVersionDto[], ApiError>> {
    const url = '/api/contents/versioning/pending-review';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ResourcesContentsContentVersionDto[], ApiError>;
  }
}

export function createResourcesContentsVersioningModule(client: ApiClient): ResourcesContentsVersioningModule {
  return new ResourcesContentsVersioningModule(client);
}
