/**
 * @game-guild/client - ContentMarketingLeads Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ContentMarketingLeadsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getMarketingLeads(query?: {
    source?: string;
    status?: string;
    topic?: string;
    search?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Array<Types.ContentPagesMarketingLeadDto>, ApiError>> {
    const url = '/v1/marketing/leads';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.ContentPagesMarketingLeadDto>, ApiError>;
  }

  /**
   */
  async postMarketingLeads(body: Types.ContentPagesCreateMarketingLeadDto): Promise<Result<Types.ContentPagesMarketingLeadDto, ApiError>> {
    const url = '/v1/marketing/leads';

    // Validate request body
    const validatedBody = safeParse(Types.ContentPagesCreateMarketingLeadDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesMarketingLeadDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getMarketingLeadById(id: string): Promise<Result<Types.ContentPagesMarketingLeadDto, ApiError>> {
    const url = `/v1/marketing/leads/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesMarketingLeadDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createContentMarketingLeadsModule(client: ApiClient): ContentMarketingLeadsModule {
  return new ContentMarketingLeadsModule(client);
}
