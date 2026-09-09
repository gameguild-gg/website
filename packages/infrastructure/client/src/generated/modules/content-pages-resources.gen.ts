/**
 * @game-guild/client - ContentPagesResources Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ContentPagesResourcesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getContentResourcesForGetContentResources(query?: {
    type?: Types.ContentPagesContentResourceType;
    status?: Types.ContentPagesContentResourceStatus;
    locale?: string;
    category?: string;
    featured?: boolean;
    q?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Array<Types.ContentPagesContentResourceDto>, ApiError>> {
    const url = '/v1/content-resources';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.ContentPagesContentResourceDto>, ApiError>;
  }

  /**
   */
  async postContentResources(body: Types.ContentPagesCreateContentResourceDto): Promise<Result<Types.ContentPagesContentResourceDto, ApiError>> {
    const url = '/v1/content-resources';

    // Validate request body
    const validatedBody = safeParse(Types.ContentPagesCreateContentResourceDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesContentResourceDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getContentResourcesForGetContentResourcesById(id: string): Promise<Result<Types.ContentPagesContentResourceDto, ApiError>> {
    const url = `/v1/content-resources/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesContentResourceDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putContentResources(id: string, body: Types.ContentPagesUpdateContentResourceDto): Promise<Result<Types.ContentPagesContentResourceDto, ApiError>> {
    const url = `/v1/content-resources/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.ContentPagesUpdateContentResourceDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesContentResourceDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteContentResources(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/content-resources/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postContentResourcesPublish(id: string): Promise<Result<Types.ContentPagesContentResourceDto, ApiError>> {
    const url = `/v1/content-resources/${id}/publish`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesContentResourceDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getContentResourcesBySlug(slug: string): Promise<Result<Types.ContentPagesContentResourceDto, ApiError>> {
    const url = `/v1/content-resources/by-slug/${slug}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesContentResourceDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createContentPagesResourcesModule(client: ApiClient): ContentPagesResourcesModule {
  return new ContentPagesResourcesModule(client);
}
