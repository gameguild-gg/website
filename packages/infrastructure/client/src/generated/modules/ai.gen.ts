/**
 * @game-guild/client - Ai Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AiModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async postAiChat(body: Types.AIAiChatInput): Promise<Result<Types.AIAiCompletionOutput, ApiError>> {
    const url = '/v1/ai/chat';

    // Validate request body
    const validatedBody = safeParse(Types.AIAiChatInputSchema, body, 'request');

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
  async postAiEmail(body: Types.AIAiGeneratedContentDraftInput): Promise<Result<Types.AIAiCompletionOutput, ApiError>> {
    const url = '/v1/ai/email';

    // Validate request body
    const validatedBody = safeParse(Types.AIAiGeneratedContentDraftInputSchema, body, 'request');

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
  async postAiGenerate(body: Types.AIAiGenerateInput): Promise<Result<Types.AIAiCompletionOutput, ApiError>> {
    const url = '/v1/ai/generate';

    // Validate request body
    const validatedBody = safeParse(Types.AIAiGenerateInputSchema, body, 'request');

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
  async postAiGenerateContent(body: Types.AIAiGeneratedContentInput): Promise<Result<Types.AIAiCompletionOutput, ApiError>> {
    const url = '/v1/ai/generate-content';

    // Validate request body
    const validatedBody = safeParse(Types.AIAiGeneratedContentInputSchema, body, 'request');

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
  async postAiGenerateContentEmail(body: Types.AIAiGeneratedContentDraftInput): Promise<Result<Types.AIAiCompletionOutput, ApiError>> {
    const url = '/v1/ai/generate-content/email';

    // Validate request body
    const validatedBody = safeParse(Types.AIAiGeneratedContentDraftInputSchema, body, 'request');

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
  async postAiGenerateContentListingDescription(body: Types.AIAiGeneratedContentDraftInput): Promise<Result<Types.AIAiCompletionOutput, ApiError>> {
    const url = '/v1/ai/generate-content/listing-description';

    // Validate request body
    const validatedBody = safeParse(Types.AIAiGeneratedContentDraftInputSchema, body, 'request');

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
  async postAiGenerateContentReport(body: Types.AIAiGeneratedContentDraftInput): Promise<Result<Types.AIAiCompletionOutput, ApiError>> {
    const url = '/v1/ai/generate-content/report';

    // Validate request body
    const validatedBody = safeParse(Types.AIAiGeneratedContentDraftInputSchema, body, 'request');

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
  async getAiHistory(query?: { take?: number }): Promise<Result<Array<Types.AIAiConversationHistoryEntryDto>, ApiError>> {
    const url = '/v1/ai/history';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.AIAiConversationHistoryEntryDto>, ApiError>;
  }

  /**
   */
  async getAiHistoryExport(query?: { format?: string; take?: number }): Promise<Result<void, ApiError>> {
    const url = '/v1/ai/history/export';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getAiQuotas(): Promise<Result<Types.AIAiQuotaStatusOutput, ApiError>> {
    const url = '/v1/ai/quotas';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AIAiQuotaStatusOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAiReport(body: Types.AIAiGeneratedContentDraftInput): Promise<Result<Types.AIAiCompletionOutput, ApiError>> {
    const url = '/v1/ai/report';

    // Validate request body
    const validatedBody = safeParse(Types.AIAiGeneratedContentDraftInputSchema, body, 'request');

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
  async getAiStatus(): Promise<Result<Types.AIAiStatusOutput, ApiError>> {
    const url = '/v1/ai/status';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AIAiStatusOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAiModule(client: ApiClient): AiModule {
  return new AiModule(client);
}
