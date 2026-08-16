/**
 * API Client
 *
 * Main client interface for making API requests.
 */

import type { Result } from './result/types.js';
import type { ApiError } from './errors/types.js';
import type { RequestConfig, ApiResponse } from './transport/types.js';

/**
 * API Client interface
 *
 * Used by generated modules to make requests.
 */
export interface ApiClient {
  /**
   * Make an API request
   */
  request<T>(config: RequestConfig): Promise<Result<T, ApiError>>;

  /**
   * Make an API request returning the full response, with status and headers
   * (needed for metadata-only contracts such as HEAD endpoints)
   */
  requestRaw<T>(config: RequestConfig): Promise<Result<ApiResponse<T>, ApiError>>;

  /**
   * Get the base URL
   */
  getBaseUrl(): string;
}

/**
 * Internal request executor
 */
export type RequestExecutor = <T>(config: RequestConfig) => Promise<Result<ApiResponse<T>, ApiError>>;
