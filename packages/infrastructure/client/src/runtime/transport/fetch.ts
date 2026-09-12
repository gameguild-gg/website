/**
 * Fetch Transport
 *
 * HTTP transport implementation using the Fetch API.
 */

import { ok, err } from '../result/helpers.js';
import { createApiError, createNetworkError } from '../errors/transform.js';
import type { Result } from '../result/types.js';
import type { ApiError } from '../errors/types.js';
import type { ApiResponse, RequestConfig, Transport, TransportConfig, Interceptor } from './types.js';

/**
 * Generate a unique request ID (UUID v4 format)
 */
function generateRequestId(): string {
  // Use crypto.randomUUID if available, otherwise fallback
  if (typeof crypto !== 'undefined' && crypto.randomUUID) {
    return crypto.randomUUID();
  }
  // Fallback for older environments
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

/**
 * Create a fetch-based transport
 */
export function createFetchTransport(config: TransportConfig): Transport {
  const shouldGenerateRequestId = config.generateRequestId !== false;
  const idGenerator = config.requestIdGenerator ?? generateRequestId;

  return {
    async request<T>(requestConfig: RequestConfig): Promise<Result<ApiResponse<T>, ApiError>> {
      let processedConfig = requestConfig;

      // Add request ID if not present and generation is enabled
      if (shouldGenerateRequestId && !processedConfig.requestId) {
        processedConfig = {
          ...processedConfig,
          requestId: idGenerator(),
        };
      }

      // Store request ID in a way that plugins can access
      (processedConfig as RequestConfig & { _requestId?: string })._requestId = processedConfig.requestId;

      // Apply request interceptors
      for (const interceptor of config.interceptors || []) {
        if (interceptor.onRequest) {
          processedConfig = await interceptor.onRequest(processedConfig);
        }
      }

      try {
        const response = await executeRequest<T>(config, processedConfig);

        if (response.ok) {
          // Apply response interceptors
          let processedResponse = response.data;
          for (const interceptor of config.interceptors || []) {
            if (interceptor.onResponse) {
              processedResponse = await interceptor.onResponse(processedResponse);
            }
          }
          return ok(processedResponse);
        } else {
          // Apply error interceptors
          let errorResult: Result<never, ApiError> = err(response.error);
          for (const interceptor of config.interceptors || []) {
            /* v8 ignore start */
            if (interceptor.onError && !errorResult.ok) {
              /* v8 ignore stop */
              errorResult = await interceptor.onError(errorResult.error);
            }
          }
          return errorResult;
        }
      } catch (error) {
        const apiError = createNetworkError(error);

        // Apply error interceptors
        let errorResult: Result<never, ApiError> = err(apiError);
        for (const interceptor of config.interceptors || []) {
          /* v8 ignore start */
          if (interceptor.onError && !errorResult.ok) {
            /* v8 ignore stop */
            errorResult = await interceptor.onError(errorResult.error);
          }
        }
        return errorResult;
      }
    },
  };
}

/**
 * Execute the HTTP request
 */
async function executeRequest<T>(transportConfig: TransportConfig, requestConfig: RequestConfig): Promise<Result<ApiResponse<T>, ApiError>> {
  // Build URL
  const url = buildUrl(transportConfig.baseUrl, requestConfig.path, requestConfig.params);

  const multipartBody = typeof FormData !== 'undefined' && requestConfig.body instanceof FormData ? requestConfig.body : null;
  const isMultipartBody = multipartBody !== null;

  // Build headers
  const headers = new Headers({
    Accept: 'application/json',
    ...transportConfig.headers,
    ...requestConfig.headers,
  });
  if (!isMultipartBody && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  } else if (isMultipartBody) {
    // Fetch must generate the multipart boundary itself. A caller-provided
    // content type would omit that boundary and make the payload unreadable.
    headers.delete('Content-Type');
  }

  // Add request ID header for distributed tracing
  if (requestConfig.requestId) {
    headers.set('X-Request-Id', requestConfig.requestId);
  }

  // Build request options
  const options: RequestInit = {
    method: requestConfig.method,
    cache: requestConfig.cache ?? transportConfig.cache,
    headers,
    signal: requestConfig.signal,
  };

  // Add body for non-GET requests
  if (requestConfig.body !== undefined && requestConfig.method !== 'GET' && requestConfig.method !== 'HEAD') {
    options.body = multipartBody ? multipartBody : JSON.stringify(requestConfig.body);
  }

  // Add timeout
  const timeout = requestConfig.timeout || transportConfig.timeout;
  let timeoutId: ReturnType<typeof setTimeout> | undefined;
  let abortController: AbortController | undefined;

  if (timeout && !requestConfig.signal) {
    abortController = new AbortController();
    options.signal = abortController.signal;
    timeoutId = setTimeout(() => abortController!.abort(), timeout);
  }

  try {
    const response = await fetch(url, options);

    if (timeoutId) {
      clearTimeout(timeoutId);
    }

    // Check for HTTP errors
    if (!response.ok) {
      const error = await createApiError(response);
      // Copy metrics key if present
      const metricsKey = (requestConfig as RequestConfig & { _metricsKey?: string })._metricsKey;
      if (metricsKey) {
        (error as ApiError & { _metricsKey?: string })._metricsKey = metricsKey;
      }
      return err(error);
    }

    // Parse response
    let data: T;

    if (response.status === 204 || response.headers.get('Content-Length') === '0') {
      // No content
      data = undefined as T;
    } else {
      const contentType = response.headers.get('Content-Type');
      if (contentType?.includes('application/json')) {
        try {
          data = await response.json();
        } catch {
          // Invalid JSON response - return error instead of throwing
          const text = await response.text().catch(() => '');
          const truncated = text.length > 100 ? `${text.slice(0, 100)}...` : text;
          const error: ApiError = {
            name: 'ApiError',
            code: 'PARSE_ERROR',
            message: `Failed to parse JSON response: ${truncated}`,
            status: response.status,
          };
          // Copy metrics key if present
          const metricsKey = (requestConfig as RequestConfig & { _metricsKey?: string })._metricsKey;
          if (metricsKey) {
            (error as ApiError & { _metricsKey?: string })._metricsKey = metricsKey;
          }
          return err(error);
        }
      } else {
        // Non-JSON response
        const contentDisposition = response.headers.get('Content-Disposition');
        data = contentDisposition?.toLowerCase().includes('attachment') ? ((await response.blob()) as T) : ((await response.text()) as T);
      }
    }

    const apiResponse: ApiResponse<T> = {
      data,
      status: response.status,
      headers: response.headers,
    };

    // Copy metrics key if present for interceptor tracking
    const metricsKey = (requestConfig as RequestConfig & { _metricsKey?: string })._metricsKey;
    if (metricsKey) {
      (apiResponse as ApiResponse<T> & { _metricsKey?: string })._metricsKey = metricsKey;
    }

    return ok(apiResponse);
  } catch (error) {
    if (timeoutId) {
      clearTimeout(timeoutId);
    }
    throw error;
  }
}

/**
 * Build full URL with base URL, path, and query parameters
 */
function buildUrl(
  baseUrl: string,
  path: string,
  query?: Record<string, string | number | boolean | undefined | Array<string | number | boolean | undefined>>,
): string {
  // Remove trailing slash from base and leading slash from path
  const base = baseUrl.replace(/\/$/, '');
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;

  let url = `${base}${normalizedPath}`;

  // Add query parameters
  if (query) {
    const params = new URLSearchParams();

    for (const [key, value] of Object.entries(query)) {
      if (Array.isArray(value)) {
        value.forEach((item) => {
          if (item !== undefined) params.append(key, String(item));
        });
      } else if (value !== undefined && value !== null) {
        params.append(key, String(value));
      }
    }

    const queryString = params.toString();
    if (queryString) {
      url += `?${queryString}`;
    }
  }

  return url;
}

/**
 * Create an interceptor that adds headers
 */
export function createHeaderInterceptor(getHeaders: () => Record<string, string> | Promise<Record<string, string>>): Interceptor {
  return {
    async onRequest(config) {
      const headers = await getHeaders();
      return {
        ...config,
        headers: {
          ...config.headers,
          ...headers,
        },
      };
    },
  };
}
