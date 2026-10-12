/**
 * Transport Types
 *
 * Type definitions for the HTTP transport layer.
 */

import type { Result } from '../result/types.js';
import type { ApiError } from '../errors/types.js';

/**
 * HTTP methods supported by the client
 */
export type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH' | 'HEAD' | 'OPTIONS';

export type QueryParameter = string | number | boolean | undefined;

/**
 * Request configuration
 */
export interface RequestConfig {
  /** HTTP method */
  method: HttpMethod;
  /** URL path (may include path parameters) */
  path: string;
  /** Query parameters */
  params?: Record<string, QueryParameter | QueryParameter[]>;
  /** Request body (will be JSON serialized) */
  body?: unknown;
  /** Additional headers */
  headers?: Record<string, string>;
  /** Parse a binary success response without decoding its bytes as text. */
  responseType?: 'blob';
  /** Request timeout in milliseconds */
  timeout?: number;
  /** Fetch cache policy for this request */
  cache?: RequestCache;
  /** Whether this request requires authentication */
  requiresAuth?: boolean;
  /** AbortSignal for request cancellation */
  signal?: AbortSignal;
  /** Unique request ID for tracing/correlation */
  requestId?: string;
}

/**
 * Response from a request
 */
export interface ApiResponse<T> {
  /** Response data */
  data: T;
  /** HTTP status code */
  status: number;
  /** Response headers */
  headers: Headers;
}

/**
 * Transport interface for making HTTP requests
 */
export interface Transport {
  /** Execute a request and return a Result */
  request<T>(config: RequestConfig): Promise<Result<ApiResponse<T>, ApiError>>;
}

/**
 * Request interceptor
 */
export interface RequestInterceptor {
  /** Called before request is sent */
  onRequest?(config: RequestConfig): RequestConfig | Promise<RequestConfig>;
}

/**
 * Response interceptor
 */
export interface ResponseInterceptor {
  /** Called after successful response */
  onResponse?<T>(response: ApiResponse<T>): ApiResponse<T> | Promise<ApiResponse<T>>;
  /** Called on error - can return modified error or Result for retry */
  onError?(error: ApiError): Result<never, ApiError> | Promise<Result<never, ApiError>>;
}

/**
 * Combined interceptor
 */
export type Interceptor = RequestInterceptor & ResponseInterceptor;

/**
 * Transport configuration
 */
export interface TransportConfig {
  /** Base URL for all requests */
  baseUrl: string;
  /** Default timeout in milliseconds */
  timeout?: number;
  /** Default fetch cache policy */
  cache?: RequestCache;
  /** Default headers for all requests */
  headers?: Record<string, string>;
  /** Request/response interceptors */
  interceptors?: Interceptor[];
  /** Whether to generate request IDs automatically (default: true) */
  generateRequestId?: boolean;
  /** Custom request ID generator function */
  requestIdGenerator?: () => string;
}
