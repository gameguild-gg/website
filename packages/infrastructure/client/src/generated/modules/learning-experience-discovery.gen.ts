/**
 * @game-guild/client - LearningExperienceDiscovery Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningExperienceDiscoveryModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getDiscoveryCollectionsForGetDiscoveryCollections(query?: {
    tenantId?: string;
    type?: Types.LearningExperienceDiscoveryCollectionType;
    skip?: number;
    take?: number;
  }): Promise<Result<Array<Types.LearningExperienceDiscoveryCourseCollectionDto>, ApiError>> {
    const url = '/v1/discovery/collections';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceDiscoveryCourseCollectionDto>, ApiError>;
  }

  /**
   */
  async postDiscoveryCollections(
    body: Types.LearningExperienceDiscoveryCreateCourseCollectionDto,
    query?: { curatorId?: string; tenantId?: string },
  ): Promise<Result<Types.LearningExperienceDiscoveryCourseCollectionDto, ApiError>> {
    const url = '/v1/discovery/collections';

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceDiscoveryCreateCourseCollectionDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceDiscoveryCourseCollectionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getDiscoveryCollectionsForGetDiscoveryCollectionsById(id: string): Promise<Result<Types.LearningExperienceDiscoveryCourseCollectionDto, ApiError>> {
    const url = `/v1/discovery/collections/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceDiscoveryCourseCollectionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putDiscoveryCollections(
    id: string,
    body: Types.LearningExperienceDiscoveryUpdateCourseCollectionDto,
  ): Promise<Result<Types.LearningExperienceDiscoveryCourseCollectionDto, ApiError>> {
    const url = `/v1/discovery/collections/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceDiscoveryUpdateCourseCollectionDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceDiscoveryCourseCollectionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteDiscoveryCollections(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/discovery/collections/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postDiscoveryCollectionsPublish(id: string): Promise<Result<Types.LearningExperienceDiscoveryCourseCollectionDto, ApiError>> {
    const url = `/v1/discovery/collections/${id}/publish`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceDiscoveryCourseCollectionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postDiscoveryCollectionsUnpublish(id: string): Promise<Result<Types.LearningExperienceDiscoveryCourseCollectionDto, ApiError>> {
    const url = `/v1/discovery/collections/${id}/unpublish`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceDiscoveryCourseCollectionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getDiscoveryCollectionsCurator(
    curatorId: string,
    query?: { includeUnpublished?: boolean; skip?: number; take?: number },
  ): Promise<Result<Array<Types.LearningExperienceDiscoveryCourseCollectionDto>, ApiError>> {
    const url = `/v1/discovery/collections/curator/${curatorId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceDiscoveryCourseCollectionDto>, ApiError>;
  }

  /**
   */
  async getDiscoveryCollectionsFeatured(query?: {
    tenantId?: string;
    take?: number;
  }): Promise<Result<Array<Types.LearningExperienceDiscoveryCourseCollectionDto>, ApiError>> {
    const url = '/v1/discovery/collections/featured';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceDiscoveryCourseCollectionDto>, ApiError>;
  }

  /**
   */
  async getDiscoveryCollectionsSlug(
    slug: string,
    query?: { tenantId?: string },
  ): Promise<Result<Types.LearningExperienceDiscoveryCourseCollectionDto, ApiError>> {
    const url = `/v1/discovery/collections/slug/${slug}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceDiscoveryCourseCollectionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getDiscoveryFeaturedForGetDiscoveryFeatured(query?: {
    tenantId?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Array<Types.LearningExperienceDiscoveryFeaturedContentDto>, ApiError>> {
    const url = '/v1/discovery/featured';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceDiscoveryFeaturedContentDto>, ApiError>;
  }

  /**
   */
  async postDiscoveryFeatured(
    body: Types.LearningExperienceDiscoveryCreateFeaturedContentDto,
    query?: { tenantId?: string },
  ): Promise<Result<Types.LearningExperienceDiscoveryFeaturedContentDto, ApiError>> {
    const url = '/v1/discovery/featured';

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceDiscoveryCreateFeaturedContentDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceDiscoveryFeaturedContentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getDiscoveryFeaturedForGetDiscoveryFeaturedById(id: string): Promise<Result<Types.LearningExperienceDiscoveryFeaturedContentDto, ApiError>> {
    const url = `/v1/discovery/featured/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceDiscoveryFeaturedContentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putDiscoveryFeatured(
    id: string,
    body: Types.LearningExperienceDiscoveryUpdateFeaturedContentDto,
  ): Promise<Result<Types.LearningExperienceDiscoveryFeaturedContentDto, ApiError>> {
    const url = `/v1/discovery/featured/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceDiscoveryUpdateFeaturedContentDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceDiscoveryFeaturedContentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteDiscoveryFeatured(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/discovery/featured/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async patchDiscoveryFeaturedToggle(
    id: string,
    query?: { isActive?: boolean },
  ): Promise<Result<Types.LearningExperienceDiscoveryFeaturedContentDto, ApiError>> {
    const url = `/v1/discovery/featured/${id}/toggle`;

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceDiscoveryFeaturedContentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getDiscoveryFeaturedType(
    type: Types.LearningExperienceDiscoveryFeaturedContentType,
    query?: { tenantId?: string; skip?: number; take?: number },
  ): Promise<Result<Array<Types.LearningExperienceDiscoveryFeaturedContentDto>, ApiError>> {
    const url = `/v1/discovery/featured/type/${type}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceDiscoveryFeaturedContentDto>, ApiError>;
  }

  /**
   */
  async postDiscoverySearchClick(searchId: string, body: Types.LearningExperienceDiscoveryRecordSearchClickDto): Promise<Result<void, ApiError>> {
    const url = `/v1/discovery/search/${searchId}/click`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceDiscoveryRecordSearchClickDtoSchema, body, 'request');

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
  async getDiscoverySearchHistory(
    userId: string,
    query?: { take?: number },
  ): Promise<Result<Array<Types.LearningExperienceDiscoverySearchHistoryDto>, ApiError>> {
    const url = `/v1/discovery/search/history/${userId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceDiscoverySearchHistoryDto>, ApiError>;
  }

  /**
   */
  async getDiscoverySearchPopular(query?: {
    daysBack?: number;
    take?: number;
  }): Promise<Result<Array<Types.LearningExperienceDiscoveryPopularSearchResult>, ApiError>> {
    const url = '/v1/discovery/search/popular';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningExperienceDiscoveryPopularSearchResult>, ApiError>;
  }

  /**
   */
  async postDiscoverySearchRecord(
    body: Types.LearningExperienceDiscoveryRecordSearchDto,
    query?: { userId?: string },
  ): Promise<Result<Types.LearningExperienceDiscoverySearchHistoryDto, ApiError>> {
    const url = '/v1/discovery/search/record';

    // Validate request body
    const validatedBody = safeParse(Types.LearningExperienceDiscoveryRecordSearchDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningExperienceDiscoverySearchHistoryDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createLearningExperienceDiscoveryModule(client: ApiClient): LearningExperienceDiscoveryModule {
  return new LearningExperienceDiscoveryModule(client);
}
