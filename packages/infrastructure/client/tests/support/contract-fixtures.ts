import type { ApiError } from '../../src/runtime/errors/types.js';
import type { Result } from '../../src/runtime/result/types.js';
import type { ApiResponse, RequestConfig, Transport } from '../../src/runtime/transport/types.js';

/** Deliberately exercise handlers with legacy codes outside the public ApiErrorCode domain. */
export function legacyCodeError(input: Omit<ApiError, 'code'> & { readonly code: string }): ApiError {
  return input as ApiError;
}

/**
 * The test caller owns the response shape, as it does at a JSON transport boundary.
 * Keep this generic conversion isolated and retain the original response and error identities.
 */
export function fixtureTransport(
  respond: (config: RequestConfig) => Promise<Result<ApiResponse<unknown>, ApiError>>,
): Transport {
  return {
    async request<T>(config: RequestConfig): Promise<Result<ApiResponse<T>, ApiError>> {
      const result = await respond(config);
      return result as Result<ApiResponse<T>, ApiError>;
    },
  };
}
