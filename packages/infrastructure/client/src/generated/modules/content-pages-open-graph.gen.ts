/**
 * @game-guild/client - ContentPagesOpenGraph Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ContentPagesOpenGraphModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getOg(slug: string): Promise<Result<Types.ContentPagesOpenGraphMetadataDto, ApiError>> {
    const url = `/v1/og/${slug}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ContentPagesOpenGraphMetadataDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createContentPagesOpenGraphModule(client: ApiClient): ContentPagesOpenGraphModule {
  return new ContentPagesOpenGraphModule(client);
}
