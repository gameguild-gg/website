/**
 * @game-guild/client - SocialBlogAi Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class SocialBlogAiModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Lists the author's copilot conversations for the post.
   */
  async getApiSocialBlogPostsAiConversations(postId: string): Promise<Result<Types.SocialBlogAuthoringBlogAiConversationDto[], ApiError>> {
    const url = `/api/social/blog/posts/${postId}/ai/conversations`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.SocialBlogAuthoringBlogAiConversationDto[], ApiError>;
  }

  /**
   * Returns the acting author's AI credit wallet snapshot.
   */
  async getApiSocialBlogPostsAiEntitlement(postId: string): Promise<Result<Types.SocialBlogAuthoringBlogAiEntitlementDto, ApiError>> {
    const url = `/api/social/blog/posts/${postId}/ai/entitlement`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogAuthoringBlogAiEntitlementDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Discards a pending proposal without changing the post.
   */
  async deleteApiSocialBlogPostsAiProposals(postId: string, proposalId: string): Promise<Result<Types.SocialBlogAuthoringBlogAiProposalDto, ApiError>> {
    const url = `/api/social/blog/posts/${postId}/ai/proposals/${proposalId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogAuthoringBlogAiProposalDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Applies a pending proposal to the post (revision-guarded).
   */
  async postApiSocialBlogPostsAiProposalsApply(
    postId: string,
    proposalId: string,
    body: Types.SocialBlogAuthoringApplyBlogAiProposalInput,
  ): Promise<Result<Types.SocialBlogAuthoringBlogPostDto, ApiError>> {
    const url = `/api/social/blog/posts/${postId}/ai/proposals/${proposalId}/apply`;

    // Validate request body
    const validatedBody = safeParse(Types.SocialBlogAuthoringApplyBlogAiProposalInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogAuthoringBlogPostDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Starts (or idempotently replays) an AI run against the post's current revision.
   */
  async postApiSocialBlogPostsAiRuns(
    postId: string,
    body: Types.SocialBlogAuthoringBlogAiRunInput,
  ): Promise<Result<Types.SocialBlogAuthoringBlogAiRunDto, ApiError>> {
    const url = `/api/social/blog/posts/${postId}/ai/runs`;

    // Validate request body
    const validatedBody = safeParse(Types.SocialBlogAuthoringBlogAiRunInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogAuthoringBlogAiRunDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Fetches one run owned by the acting author.
   */
  async getApiSocialBlogPostsAiRuns(postId: string, runId: string): Promise<Result<Types.SocialBlogAuthoringBlogAiRunDto, ApiError>> {
    const url = `/api/social/blog/posts/${postId}/ai/runs/${runId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogAuthoringBlogAiRunDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Cancels a queued, reserved, or running run and releases its reservation.
   */
  async postApiSocialBlogPostsAiRunsCancel(postId: string, runId: string): Promise<Result<Types.SocialBlogAuthoringBlogAiRunDto, ApiError>> {
    const url = `/api/social/blog/posts/${postId}/ai/runs/${runId}/cancel`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialBlogAuthoringBlogAiRunDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Streams persisted run events (SSE) until the run reaches a terminal state.
   */
  async getApiSocialBlogPostsAiRunsStream(postId: string, runId: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/blog/posts/${postId}/ai/runs/${runId}/stream`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createSocialBlogAiModule(client: ApiClient): SocialBlogAiModule {
  return new SocialBlogAiModule(client);
}
