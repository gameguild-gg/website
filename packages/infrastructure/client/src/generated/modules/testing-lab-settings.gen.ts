/**
 * @game-guild/client - TestingLabSettings Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class TestingLabSettingsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getApiTestingLabSettings(): Promise<Result<Types.TestingLabTestingLabSettingsDto, ApiError>> {
    const url = '/api/testing-lab/settings';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingLabSettingsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putApiTestingLabSettings(body: Types.TestingLabCreateTestingLabSettingsDto): Promise<Result<Types.TestingLabTestingLabSettingsDto, ApiError>> {
    const url = '/api/testing-lab/settings';

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabCreateTestingLabSettingsDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingLabSettingsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async patchApiTestingLabSettings(body: Types.TestingLabUpdateTestingLabSettingsDto): Promise<Result<Types.TestingLabTestingLabSettingsDto, ApiError>> {
    const url = '/api/testing-lab/settings';

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabUpdateTestingLabSettingsDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingLabSettingsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiTestingLabSettingsExists(): Promise<Result<boolean, ApiError>> {
    const url = '/api/testing-lab/settings/exists';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<boolean, ApiError>;
  }

  /**
   */
  async postApiTestingLabSettingsReset(): Promise<Result<Types.TestingLabTestingLabSettingsDto, ApiError>> {
    const url = '/api/testing-lab/settings/reset';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingLabSettingsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createTestingLabSettingsModule(client: ApiClient): TestingLabSettingsModule {
  return new TestingLabSettingsModule(client);
}
