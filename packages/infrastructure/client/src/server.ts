/**
 * Server Client Factory
 *
 * Creates API client instances for server-side usage (RSC, Server Actions).
 */

import { createFetchTransport, createHeaderInterceptor } from './runtime/transport/fetch.js';
import { ok, err } from './runtime/result/helpers.js';
import type { Result } from './runtime/result/types.js';
import type { ApiError } from './runtime/errors/types.js';
import type { TokenProvider } from './runtime/auth/types.js';
import type { TenantProvider } from './runtime/tenant/types.js';
import type { ApiResponse, Interceptor, RequestConfig, Transport } from './runtime/transport/types.js';
import type { ApiClient } from './runtime/client.js';

/**
 * Server client configuration options
 */
export interface ServerClientConfig {
  /** Base URL for API requests */
  baseUrl: string;

  /** Token provider for authentication */
  auth?: TokenProvider;

  /** Tenant provider for multi-tenancy */
  tenant?: TenantProvider;

  /** Default request timeout in milliseconds */
  timeout?: number;

  /** Default fetch cache policy */
  cache?: RequestCache;

  /** Additional interceptors */
  interceptors?: Interceptor[];
}

/**
 * Create an API client for server-side usage
 *
 * This client is designed for use in:
 * - Server Components (RSC)
 * - Server Actions
 * - API Routes
 * - Proxy
 *
 * Key differences from browser client:
 * - No automatic token refresh (handled by auth provider)
 * - No client-side state
 * - Safe for concurrent requests
 */
export function createServerClient(config: ServerClientConfig): ApiClient {
  const interceptors: Interceptor[] = [...(config.interceptors || [])];

  // Shared token cache for the current request cycle.
  // This prevents calling getAccessToken() multiple times per request,
  // which is critical when the backend does refresh-token rotation
  // (the first call revokes the old refresh token, causing subsequent calls to fail).
  let cachedTokenPromise: Promise<string | null> | null = null;

  function getCachedAccessToken(): Promise<string | null> {
    if (!cachedTokenPromise) {
      cachedTokenPromise = config.auth!.getAccessToken();
    }
    return cachedTokenPromise;
  }

  // Add auth interceptor
  if (config.auth) {
    interceptors.push(
      createHeaderInterceptor(async (): Promise<Record<string, string>> => {
        const token = await getCachedAccessToken();
        if (token) {
          return { Authorization: `Bearer ${token}` };
        }
        return {};
      }),
    );
  }

  // Add tenant interceptor
  if (config.tenant) {
    interceptors.push(
      createHeaderInterceptor(async (): Promise<Record<string, string>> => {
        const tenantId = await config.tenant!.getTenantId();
        if (tenantId) {
          return { 'X-Tenant-Id': tenantId };
        }
        return {};
      }),
    );
  }

  // Create transport
  const transport: Transport = createFetchTransport({
    baseUrl: config.baseUrl,
    timeout: config.timeout,
    cache: config.cache,
    interceptors,
  });

  async function requestWithResponse<T>(
    requestConfig: RequestConfig
  ): Promise<Result<ApiResponse<T>, ApiError>> {
    // Reset token cache for each request
    cachedTokenPromise = null;

    // Check auth requirement
    if (requestConfig.requiresAuth && config.auth) {
      const token = await getCachedAccessToken();
      if (!token) {
        await config.auth.onAuthenticationRequired?.();
        return err({
          name: 'ApiError' as const,
          message: 'Authentication required',
          status: 401,
          code: 'TOKEN_MISSING' as const,
        });
      }
    }

    return transport.request<T>(requestConfig);
  }

  // Create client
  const client: ApiClient = {
    async request<T>(requestConfig: RequestConfig): Promise<Result<T, ApiError>> {
      const result = await requestWithResponse<T>(requestConfig);

      if (result.ok) {
        return ok(result.data.data);
      }

      return result;
    },

    async requestRaw<T>(requestConfig: RequestConfig): Promise<Result<ApiResponse<T>, ApiError>> {
      return requestWithResponse<T>(requestConfig);
    },

    getBaseUrl() {
      return config.baseUrl;
    },
  };

  return client;
}
