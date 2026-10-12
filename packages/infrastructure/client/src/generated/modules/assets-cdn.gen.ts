/**
 * @game-guild/client - AssetsCdn Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AssetsCdnModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Serve asset content with path-based token (CDN-friendly).
   *
   * URL format: /assets/{referenceId}/{token}
   * This format is more CDN-friendly than query-string tokens because:
   * - Path-based URLs are consistently cached
   * - No query string parsing issues
   * - Works with CDNs that strip query strings
   */
  async getAssetsForGetAssetsByReferenceIdByToken(referenceId: string, token: string): Promise<Result<void, ApiError>> {
    const url = `/assets/${referenceId}/${token}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Serve ephemeral asset (short-lived URL with embedded reference).
   *
   * URL format: /e/{token}
   * The token contains the encrypted asset reference ID and expiration.
   * Useful for temporary share links and secure downloads.
   */
  async getE(token: string): Promise<Result<void, ApiError>> {
    const url = `/e/${token}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Serve transformed asset (resized, cropped, etc.) with CDN caching.
   *
   * URL format: /t/{transformation}/{referenceId}/{token}
   * Transformations use standard format: w=100,h=100,fit=cover
   */
  async getT(transformation: string, referenceId: string, token: string): Promise<Result<void, ApiError>> {
    const url = `/t/${transformation}/${referenceId}/${token}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    return result as Result<void, ApiError>;
  }
}

export function createAssetsCdnModule(client: ApiClient): AssetsCdnModule {
  return new AssetsCdnModule(client);
}
