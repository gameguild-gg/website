/**
 * @game-guild/client - TestingLabTestingEventTemplates Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class TestingLabTestingEventTemplatesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getVTestingTemplates(
    version: string,
    query?: { includeArchived?: boolean },
  ): Promise<Result<Types.TestingLabTestingEventTemplateProjection[], ApiError>> {
    const url = `/v${version}/testing/templates`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.TestingLabTestingEventTemplateProjection[], ApiError>;
  }

  /**
   */
  async postVTestingTemplates(
    version: string,
    body: Types.TestingLabUpsertTestingEventTemplateInput,
  ): Promise<Result<Types.TestingLabTestingEventTemplateProjection, ApiError>> {
    const url = `/v${version}/testing/templates`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabUpsertTestingEventTemplateInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingEventTemplateProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putVTestingTemplates(
    templateId: string,
    version: string,
    body: Types.TestingLabUpsertTestingEventTemplateInput,
  ): Promise<Result<Types.TestingLabTestingEventTemplateProjection, ApiError>> {
    const url = `/v${version}/testing/templates/${templateId}`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabUpsertTestingEventTemplateInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingEventTemplateProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postVTestingTemplatesArchive(templateId: string, version: string): Promise<Result<Types.TestingLabTestingEventTemplateProjection, ApiError>> {
    const url = `/v${version}/testing/templates/${templateId}:archive`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingEventTemplateProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postVTestingTemplatesRestore(templateId: string, version: string): Promise<Result<Types.TestingLabTestingEventTemplateProjection, ApiError>> {
    const url = `/v${version}/testing/templates/${templateId}:restore`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingEventTemplateProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getVTestingTemplatesRevisions(
    templateId: string,
    revisionId: string,
    version: string,
  ): Promise<Result<Types.TestingLabTestingEventTemplateRevisionProjection, ApiError>> {
    const url = `/v${version}/testing/templates/${templateId}/revisions/${revisionId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingEventTemplateRevisionProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createTestingLabTestingEventTemplatesModule(client: ApiClient): TestingLabTestingEventTemplatesModule {
  return new TestingLabTestingEventTemplatesModule(client);
}
