/**
 * @game-guild/client - LearningExperienceSocialReplies Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningExperienceSocialRepliesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getApiSocialDiscussionsReplies(
    discussionId: string,
    query?: { skip?: number; take?: number },
  ): Promise<Result<Array<Types.LearningExperienceSocialServicesDiscussionReplyDto>, ApiError>> {
    const url = `/api/social/discussions/${discussionId}/replies`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceSocialServicesDiscussionReplyDto>, ApiError>;
  }

  /**
   */
  async postApiSocialDiscussionsReplies(
    discussionId: string,
    body: Types.LearningExperienceSocialServicesCreateReplyInput,
  ): Promise<Result<Types.LearningExperienceSocialServicesDiscussionReplyDto, ApiError>> {
    const url = `/api/social/discussions/${discussionId}/replies`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceSocialServicesCreateReplyInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesDiscussionReplyDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteApiSocialReplies(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/replies/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postApiSocialRepliesAccept(id: string): Promise<Result<Types.LearningExperienceSocialServicesDiscussionReplyDto, ApiError>> {
    const url = `/api/social/replies/${id}/accept`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesDiscussionReplyDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postApiSocialRepliesUpvote(id: string): Promise<Result<Types.LearningExperienceSocialServicesDiscussionReplyDto, ApiError>> {
    const url = `/api/social/replies/${id}/upvote`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceSocialServicesDiscussionReplyDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createLearningExperienceSocialRepliesModule(client: ApiClient): LearningExperienceSocialRepliesModule {
  return new LearningExperienceSocialRepliesModule(client);
}
