/**
 * @game-guild/client - TestingLabTestingLocations Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class TestingLabTestingLocationsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getTestingLocationsForGetTestingLocations(query?: {
    skip?: number;
    take?: number;
    includeArchived?: boolean;
  }): Promise<Result<Array<Types.TestingLabTestingLocation>, ApiError>> {
    const url = '/v1/testing/locations';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabTestingLocation>, ApiError>;
  }

  /**
   */
  async postTestingLocations(body: Types.TestingLabCreateTestingLocationDto): Promise<Result<Types.TestingLabTestingLocation, ApiError>> {
    const url = '/v1/testing/locations';

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabCreateTestingLocationDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingLocationSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getTestingLocationsForGetTestingLocationsById(id: string): Promise<Result<Types.TestingLabTestingLocation, ApiError>> {
    const url = `/v1/testing/locations/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingLocationSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putTestingLocations(id: string, body: Types.TestingLabUpdateTestingLocationDto): Promise<Result<Types.TestingLabTestingLocation, ApiError>> {
    const url = `/v1/testing/locations/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabUpdateTestingLocationDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingLocationSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteTestingLocations(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/testing/locations/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postTestingLocationsRestore(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/testing/locations/${id}/restore`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createTestingLabTestingLocationsModule(client: ApiClient): TestingLabTestingLocationsModule {
  return new TestingLabTestingLocationsModule(client);
}
