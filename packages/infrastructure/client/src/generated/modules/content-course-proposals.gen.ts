/**
 * @game-guild/client - ContentCourseProposals Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ContentCourseProposalsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getContentCourseProposals(query?: {
    status?: string;
    search?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ContentPagesCourseProposalDto[], ApiError>> {
    const url = '/v1/content/course-proposals';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ContentPagesCourseProposalDto[], ApiError>;
  }

  /**
   */
  async postContentCourseProposals(body: Types.ContentPagesCreateCourseProposalDto): Promise<Result<Types.ContentPagesCourseProposalDto, ApiError>> {
    const url = '/v1/content/course-proposals';

    // Validate request body
    const validatedBody = safeParse(Types.ContentPagesCreateCourseProposalDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesCourseProposalDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCourseProposalById(id: string): Promise<Result<Types.ContentPagesCourseProposalDto, ApiError>> {
    const url = `/v1/content/course-proposals/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesCourseProposalDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putContentCourseProposals(id: string, body: Types.ContentPagesUpdateCourseProposalDto): Promise<Result<Types.ContentPagesCourseProposalDto, ApiError>> {
    const url = `/v1/content/course-proposals/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.ContentPagesUpdateCourseProposalDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesCourseProposalDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteContentCourseProposals(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/content/course-proposals/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Public detail lookup for /proposals/[slug]; anonymous callers receive
   * published proposals only, so drafts and rejections stay unlisted.
   */
  async getContentCourseProposalsBySlug(slug: string): Promise<Result<Types.ContentPagesCourseProposalDto, ApiError>> {
    const url = `/v1/content/course-proposals/by-slug/${slug}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesCourseProposalDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createContentCourseProposalsModule(client: ApiClient): ContentCourseProposalsModule {
  return new ContentCourseProposalsModule(client);
}
