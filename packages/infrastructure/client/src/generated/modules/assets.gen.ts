/**
 * @game-guild/client - Assets Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AssetsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List assets with optional filtering.
   * Use owner=me to get current user's assets.
   * Use parentType and parentId to filter by parent resource.
   */
  async getAssetsForGetAssets(query?: {
    owner?: string;
    parentType?: string;
    parentId?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<void, ApiError>> {
    const url = '/v1/assets';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Upload a new asset.
   */
  async postAssets(query?: {
    displayName?: string;
    accessPolicy?: Types.AssetsAssetAccessPolicy;
    parentResourceType?: string;
    parentResourceId?: string;
    folderId?: string;
    referenceId?: string;
  }): Promise<Result<void, ApiError>> {
    const url = '/v1/assets';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get an asset by ID.
   */
  async getAssetsForGetAssetsById(id: string, query?: { includeContent?: boolean }): Promise<Result<void, ApiError>> {
    const url = `/v1/assets/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Delete an asset.
   */
  async deleteAssets(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/assets/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Update asset metadata.
   */
  async patchAssets(id: string, body: Types.AssetsControllersUpdateAssetInput): Promise<Result<void, ApiError>> {
    const url = `/v1/assets/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.AssetsControllersUpdateAssetInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get extracted searchable text for an asset.
   */
  async getSignedAssetExtractedText(id: string, query?: { token?: string }): Promise<Result<void, ApiError>> {
    const url = `/v1/assets/${id}:extracted-text`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Generate an access URL for an asset.
   */
  async postAssetsGenerateAccessUrl(
    id: string,
    query?: { width?: number; height?: number; fit?: Types.AssetsImageFit; format?: Types.AssetsImageFormat; quality?: number; direct?: boolean },
  ): Promise<Result<void, ApiError>> {
    const url = `/v1/assets/${id}:generate-access-url`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      requiresAuth: false,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Report an asset for moderation.
   */
  async postAssetsReport(id: string, body: Types.AssetsControllersReportAssetInput): Promise<Result<void, ApiError>> {
    const url = `/v1/assets/${id}:report`;

    // Validate request body
    const validatedBody = safeParse(Types.AssetsControllersReportAssetInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get asset content (serve the actual file).
   */
  async getAssetsContent(id: string, query?: { token?: string; transform?: string }): Promise<Result<void, ApiError>> {
    const url = `/v1/assets/${id}/content`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Extract text from an asset when the MIME type supports direct parsing or OCR.
   */
  async getAssetExtractedText(id: string): Promise<Result<Types.AssetsControllersAssetExtractedTextOutput, ApiError>> {
    const url = `/v1/assets/${id}/extracted-text`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AssetsControllersAssetExtractedTextOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get the inline preview contract for a document or media asset.
   */
  async getAssetsPreview(
    id: string,
    query?: { includeExtractedText?: boolean; thumbnailWidth?: number; thumbnailHeight?: number },
  ): Promise<Result<Types.AssetsQueriesAssetPreviewOutput, ApiError>> {
    const url = `/v1/assets/${id}/preview`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AssetsQueriesAssetPreviewOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Delete multiple asset references in one request.
   */
  async postAssetsBulkDelete(body: Types.AssetsControllersBulkDeleteAssetsInput): Promise<Result<Types.AssetsCommandsBulkDeleteAssetsOutput, ApiError>> {
    const url = '/v1/assets/bulk-delete';

    // Validate request body
    const validatedBody = safeParse(Types.AssetsControllersBulkDeleteAssetsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AssetsCommandsBulkDeleteAssetsOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Generate secure access URLs for multiple assets.
   */
  async postAssetsBulkDownload(body: Types.AssetsControllersBulkAssetAccessUrlInput): Promise<Result<Types.AssetsQueriesBulkAssetAccessUrlsOutput, ApiError>> {
    const url = '/v1/assets/bulk-download';

    // Validate request body
    const validatedBody = safeParse(Types.AssetsControllersBulkAssetAccessUrlInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AssetsQueriesBulkAssetAccessUrlsOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Upload multiple assets in one request.
   */
  async postAssetsBulkUpload(query?: {
    accessPolicy?: Types.AssetsAssetAccessPolicy;
    parentResourceType?: string;
    parentResourceId?: string;
    folderId?: string;
  }): Promise<Result<Types.AssetsCommandsBulkUploadAssetsOutput, ApiError>> {
    const url = '/v1/assets/bulk-upload';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AssetsCommandsBulkUploadAssetsOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Initialize a chunked upload for large files.
   */
  async postAssetsChunkedUploads(query?: {
    fileName?: string;
    mimeType?: string;
    totalSize?: number;
  }): Promise<Result<Types.AssetsChunkedUploadSession, ApiError>> {
    const url = '/v1/assets/chunked-uploads';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AssetsChunkedUploadSessionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Abort an in-progress chunked upload.
   */
  async deleteAssetsChunkedUploads(uploadId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/assets/chunked-uploads/${uploadId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Complete a chunked upload and create the asset.
   */
  async postAssetsChunkedUploadsComplete(
    uploadId: string,
    query?: { displayName?: string; accessPolicy?: Types.AssetsAssetAccessPolicy; parentResourceType?: string; parentResourceId?: string; folderId?: string },
  ): Promise<Result<Types.AssetsAssetUploadResult, ApiError>> {
    const url = `/v1/assets/chunked-uploads/${uploadId}:complete`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AssetsAssetUploadResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Upload a chunk for an in-progress chunked upload.
   */
  async postAssetsChunkedUploadsParts(uploadId: string, query?: { chunkIndex?: number }): Promise<Result<void, ApiError>> {
    const url = `/v1/assets/chunked-uploads/${uploadId}/parts`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Search document and media assets by metadata, parent, MIME type, and storage key.
   */
  async getAssetsSearch(query?: {
    q?: string;
    kind?: Types.AssetsAssetKind;
    parentType?: string;
    parentId?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.AssetsQueriesAssetSearchOutput, ApiError>> {
    const url = '/v1/assets/search';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AssetsQueriesAssetSearchOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAssetsModule(client: ApiClient): AssetsModule {
  return new AssetsModule(client);
}
