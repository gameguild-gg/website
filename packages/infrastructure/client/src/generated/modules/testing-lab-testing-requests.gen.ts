/**
 * @game-guild/client - TestingLabTestingRequests Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class TestingLabTestingRequestsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getTestingAvailableForTesting(): Promise<Result<Array<Types.TestingLabTestingInput>, ApiError>> {
    const url = '/v1/testing/available-for-testing';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabTestingInput>, ApiError>;
  }

  /**
   */
  async getTestingMyRequests(): Promise<Result<Array<Types.TestingLabTestingInput>, ApiError>> {
    const url = '/v1/testing/my-requests';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabTestingInput>, ApiError>;
  }

  /**
   */
  async getTestingRequestsForGetTestingRequests(query?: {
    skip?: number;
    take?: number;
    includeArchived?: boolean;
  }): Promise<Result<Array<Types.TestingLabTestingRequestDetailProjection>, ApiError>> {
    const url = '/v1/testing/requests';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabTestingRequestDetailProjection>, ApiError>;
  }

  /**
   */
  async postTestingRequests(body: Types.TestingLabCreateTestingRequestDto): Promise<Result<Types.TestingLabTestingInput, ApiError>> {
    const url = '/v1/testing/requests';

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabCreateTestingRequestDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingInputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getTestingRequestsForGetTestingRequestsById(id: string): Promise<Result<Types.TestingLabTestingRequestDetailProjection, ApiError>> {
    const url = `/v1/testing/requests/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingRequestDetailProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putTestingRequests(
    id: string,
    body: Types.TestingLabUpdateTestingRequestDto,
  ): Promise<Result<Types.TestingLabTestingRequestDetailProjection, ApiError>> {
    const url = `/v1/testing/requests/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabUpdateTestingRequestDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingRequestDetailProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteTestingRequests(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/testing/requests/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postTestingRequestsRestore(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/testing/requests/${id}:restore`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getTestingRequestsDetails(id: string): Promise<Result<Types.TestingLabTestingInput, ApiError>> {
    const url = `/v1/testing/requests/${id}/details`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingInputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getTestingRequestsStatistics(requestId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/testing/requests/${requestId}/statistics`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getTestingRequestsByCreator(creatorId: string): Promise<Result<Array<Types.TestingLabTestingInput>, ApiError>> {
    const url = `/v1/testing/requests/by-creator/${creatorId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabTestingInput>, ApiError>;
  }

  /**
   */
  async getTestingRequestsByProjectVersion(projectVersionId: string): Promise<Result<Array<Types.TestingLabTestingInput>, ApiError>> {
    const url = `/v1/testing/requests/by-project-version/${projectVersionId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabTestingInput>, ApiError>;
  }

  /**
   */
  async getTestingRequestsByStatus(status: Types.TestingLabTestingRequestStatus): Promise<Result<Array<Types.TestingLabTestingInput>, ApiError>> {
    const url = `/v1/testing/requests/by-status/${status}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabTestingInput>, ApiError>;
  }

  /**
   */
  async getTestingRequestsSearch(query?: { searchTerm?: string }): Promise<Result<Array<Types.TestingLabTestingInput>, ApiError>> {
    const url = '/v1/testing/requests/search';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabTestingInput>, ApiError>;
  }

  /**
   */
  async postTestingSubmitSimple(
    body: Types.TestingLabCreateSimpleTestingRequestDto,
  ): Promise<Result<Types.TestingLabTestingRequestDetailProjection, ApiError>> {
    const url = '/v1/testing/submit-simple';

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabCreateSimpleTestingRequestDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingRequestDetailProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createTestingLabTestingRequestsModule(client: ApiClient): TestingLabTestingRequestsModule {
  return new TestingLabTestingRequestsModule(client);
}
