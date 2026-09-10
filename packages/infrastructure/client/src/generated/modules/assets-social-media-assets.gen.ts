/**
 * @game-guild/client - AssetsSocialMediaAssets Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AssetsSocialMediaAssetsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async postAssetsSocialMedia(): Promise<Result<Types.AssetsSocialMediaSocialMediaAssetDescriptor, ApiError>> {
    const url = '/v1/assets/social-media';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AssetsSocialMediaSocialMediaAssetDescriptorSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAssetsSocialMedia(assetReferenceId: string): Promise<Result<Types.AssetsSocialMediaSocialMediaAssetDescriptor, ApiError>> {
    const url = `/v1/assets/social-media/${assetReferenceId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AssetsSocialMediaSocialMediaAssetDescriptorSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAssetsSocialMediaAssetsModule(client: ApiClient): AssetsSocialMediaAssetsModule {
  return new AssetsSocialMediaAssetsModule(client);
}
