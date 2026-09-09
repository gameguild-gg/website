/**
 * @game-guild/client - ContentPages Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ContentPagesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getPagesForGetPages(query?: {
    type?: Types.ContentPagesPageType;
    status?: Types.ContentPagesPageStatus;
    locale?: string;
    parentId?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Array<Types.ContentPagesPageDto>, ApiError>> {
    const url = '/v1/pages';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.ContentPagesPageDto>, ApiError>;
  }

  /**
   */
  async postPages(body: Types.ContentPagesCreatePageDto): Promise<Result<Types.ContentPagesPageDto, ApiError>> {
    const url = '/v1/pages';

    // Validate request body
    const validatedBody = safeParse(Types.ContentPagesCreatePageDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesPageDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getPagesForGetPagesById(id: string): Promise<Result<Types.ContentPagesPageDto, ApiError>> {
    const url = `/v1/pages/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesPageDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putPages(id: string, body: Types.ContentPagesUpdatePageDto): Promise<Result<Types.ContentPagesPageDto, ApiError>> {
    const url = `/v1/pages/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.ContentPagesUpdatePageDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesPageDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deletePages(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/pages/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postPagesPublish(id: string): Promise<Result<Types.ContentPagesPageDto, ApiError>> {
    const url = `/v1/pages/${id}/publish`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesPageDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postPagesUnpublish(id: string): Promise<Result<Types.ContentPagesPageDto, ApiError>> {
    const url = `/v1/pages/${id}/unpublish`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesPageDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getPagesSectionsForGetPagesByPageIdSections(pageId: string): Promise<Result<Array<Types.ContentPagesPageSectionDto>, ApiError>> {
    const url = `/v1/pages/${pageId}/sections`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.ContentPagesPageSectionDto>, ApiError>;
  }

  /**
   */
  async postPagesSections(pageId: string, body: Types.ContentPagesCreatePageSectionDto): Promise<Result<Types.ContentPagesPageSectionDto, ApiError>> {
    const url = `/v1/pages/${pageId}/sections`;

    // Validate request body
    const validatedBody = safeParse(Types.ContentPagesCreatePageSectionDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesPageSectionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getPagesSectionsForGetPagesByPageIdSectionsBySectionId(pageId: string, sectionId: string): Promise<Result<Types.ContentPagesPageSectionDto, ApiError>> {
    const url = `/v1/pages/${pageId}/sections/${sectionId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesPageSectionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putPagesSections(
    pageId: string,
    sectionId: string,
    body: Types.ContentPagesUpdatePageSectionDto,
  ): Promise<Result<Types.ContentPagesPageSectionDto, ApiError>> {
    const url = `/v1/pages/${pageId}/sections/${sectionId}`;

    // Validate request body
    const validatedBody = safeParse(Types.ContentPagesUpdatePageSectionDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesPageSectionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deletePagesSections(pageId: string, sectionId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/pages/${pageId}/sections/${sectionId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postPagesSectionsReorder(pageId: string, body: Array<string>): Promise<Result<void, ApiError>> {
    const url = `/v1/pages/${pageId}/sections/reorder`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: body,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getPagesBySlug(slug: string): Promise<Result<Types.ContentPagesPageDto, ApiError>> {
    const url = `/v1/pages/by-slug/${slug}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesPageDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getPagesSitemap(query?: { locale?: string }): Promise<Result<Array<Types.ContentPagesSitemapEntryDto>, ApiError>> {
    const url = '/v1/pages/sitemap';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.ContentPagesSitemapEntryDto>, ApiError>;
  }
}

export function createContentPagesModule(client: ApiClient): ContentPagesModule {
  return new ContentPagesModule(client);
}
