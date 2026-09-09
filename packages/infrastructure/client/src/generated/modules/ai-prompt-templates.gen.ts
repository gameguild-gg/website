/**
 * @game-guild/client - AiPromptTemplates Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AiPromptTemplatesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getAiPromptTemplatesForGetAiPromptTemplates(query?: {
    category?: string;
    includeInactive?: boolean;
  }): Promise<Result<Array<Types.AIAiPromptTemplateDto>, ApiError>> {
    const url = '/v1/ai/prompt-templates';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.AIAiPromptTemplateDto>, ApiError>;
  }

  /**
   */
  async postAiPromptTemplates(body: Types.AICreateAiPromptTemplateInput): Promise<Result<Types.AIAiPromptTemplateDto, ApiError>> {
    const url = '/v1/ai/prompt-templates';

    // Validate request body
    const validatedBody = safeParse(Types.AICreateAiPromptTemplateInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AIAiPromptTemplateDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAiPromptTemplatesForGetAiPromptTemplatesById(id: string): Promise<Result<Types.AIAiPromptTemplateDto, ApiError>> {
    const url = `/v1/ai/prompt-templates/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AIAiPromptTemplateDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putAiPromptTemplates(id: string, body: Types.AIUpdateAiPromptTemplateInput): Promise<Result<Types.AIAiPromptTemplateDto, ApiError>> {
    const url = `/v1/ai/prompt-templates/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.AIUpdateAiPromptTemplateInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AIAiPromptTemplateDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteAiPromptTemplates(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/ai/prompt-templates/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postAiPromptTemplatesGenerate(id: string, body: Types.AIAiPromptTemplateGenerateInput): Promise<Result<Types.AIAiCompletionOutput, ApiError>> {
    const url = `/v1/ai/prompt-templates/${id}/generate`;

    // Validate request body
    const validatedBody = safeParse(Types.AIAiPromptTemplateGenerateInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AIAiCompletionOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAiPromptTemplatesRender(id: string, body: Types.AIAiPromptTemplateRenderInput): Promise<Result<Types.AIAiPromptTemplateRenderOutput, ApiError>> {
    const url = `/v1/ai/prompt-templates/${id}/render`;

    // Validate request body
    const validatedBody = safeParse(Types.AIAiPromptTemplateRenderInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AIAiPromptTemplateRenderOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAiPromptTemplatesModule(client: ApiClient): AiPromptTemplatesModule {
  return new AiPromptTemplatesModule(client);
}
