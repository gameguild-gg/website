/**
 * Extended Transform Tests — 401/403 subtypes, rate-limited, validation, network errors
 */

import { describe, it, expect } from 'vitest';
import { createApiError, createNetworkError } from '../../src/runtime/errors/transform.js';
import { isRateLimitError, isValidationError } from '../../src/runtime/errors/guards.js';

describe('createApiError — status code branches', () => {
  it('should map 401 with "expired" type to TOKEN_EXPIRED', async () => {
    const response = new Response(
      JSON.stringify({ type: 'token_expired', title: 'Token expired', detail: 'JWT expired' }),
      { status: 401 }
    );
    const error = await createApiError(response);
    expect(error.code).toBe('TOKEN_EXPIRED');
    expect(error.status).toBe(401);
  });

  it('should map 401 with "invalid" type to TOKEN_INVALID', async () => {
    const response = new Response(
      JSON.stringify({ type: 'token_invalid', title: 'Invalid token' }),
      { status: 401 }
    );
    const error = await createApiError(response);
    expect(error.code).toBe('TOKEN_INVALID');
  });

  it('should map 401 with "missing" type to TOKEN_MISSING', async () => {
    const response = new Response(
      JSON.stringify({ type: 'token_missing', title: 'Token missing' }),
      { status: 401 }
    );
    const error = await createApiError(response);
    expect(error.code).toBe('TOKEN_MISSING');
  });

  it('should map 401 without specific type to AUTHENTICATION_ERROR', async () => {
    const response = new Response(
      JSON.stringify({ title: 'Unauthorized' }),
      { status: 401 }
    );
    const error = await createApiError(response);
    expect(error.code).toBe('AUTHENTICATION_ERROR');
  });

  it('should map 403 with "permission" type to INSUFFICIENT_PERMISSIONS', async () => {
    const response = new Response(
      JSON.stringify({ type: 'insufficient_permission', title: 'Forbidden' }),
      { status: 403 }
    );
    const error = await createApiError(response);
    expect(error.code).toBe('INSUFFICIENT_PERMISSIONS');
  });

  it('should map 403 with "feature" type to FEATURE_NOT_AVAILABLE', async () => {
    const response = new Response(
      JSON.stringify({ type: 'feature_not_available', title: 'Feature disabled' }),
      { status: 403 }
    );
    const error = await createApiError(response);
    expect(error.code).toBe('FEATURE_NOT_AVAILABLE');
  });

  it('should map 403 without specific type to FORBIDDEN', async () => {
    const response = new Response(
      JSON.stringify({ title: 'Forbidden' }),
      { status: 403 }
    );
    const error = await createApiError(response);
    expect(error.code).toBe('FORBIDDEN');
  });

  it('should map 409 to CONFLICT', async () => {
    const response = new Response(
      JSON.stringify({ title: 'Conflict', detail: 'Resource already exists' }),
      { status: 409 }
    );
    const error = await createApiError(response);
    expect(error.code).toBe('CONFLICT');
  });

  it('should map 429 to RATE_LIMITED', async () => {
    const response = new Response(
      JSON.stringify({ title: 'Too many requests' }),
      { status: 429 }
    );
    const error = await createApiError(response);
    expect(error.code).toBe('RATE_LIMITED');
  });

  it('should map 429 with Retry-After header', async () => {
    const response = new Response(
      JSON.stringify({ title: 'Too many requests' }),
      {
        status: 429,
        headers: { 'Retry-After': '30' },
      }
    );
    const error = await createApiError(response);
    expect(error.code).toBe('RATE_LIMITED');
    if (!isRateLimitError(error)) throw new Error('Expected a rate limit error');
    expect(error.retryAfter).toBe(30);
  });

  it('should map 400 with errors to VALIDATION_ERROR with fieldErrors', async () => {
    const response = new Response(
      JSON.stringify({
        title: 'Validation failed',
        errors: { email: ['Invalid email'], password: ['Too short'] },
      }),
      { status: 400 }
    );
    const error = await createApiError(response);
    expect(error.code).toBe('VALIDATION_ERROR');
    if (!isValidationError(error)) throw new Error('Expected a validation error');
    expect(error.fieldErrors).toEqual({
      email: ['Invalid email'],
      password: ['Too short'],
    });
  });

  it('should map 400 without errors to VALIDATION_ERROR', async () => {
    const response = new Response(
      JSON.stringify({ title: 'Bad request' }),
      { status: 400 }
    );
    const error = await createApiError(response);
    expect(error.code).toBe('VALIDATION_ERROR');
  });

  it('should map 502 to SERVER_ERROR', async () => {
    const response = new Response(
      JSON.stringify({ title: 'Bad gateway' }),
      { status: 502 }
    );
    const error = await createApiError(response);
    expect(error.code).toBe('SERVER_ERROR');
  });

  it('should map 503 to SERVER_ERROR', async () => {
    const response = new Response(
      JSON.stringify({ title: 'Service unavailable' }),
      { status: 503 }
    );
    const error = await createApiError(response);
    expect(error.code).toBe('SERVER_ERROR');
  });

  it('should map 504 to SERVER_ERROR', async () => {
    const response = new Response(
      JSON.stringify({ title: 'Gateway timeout' }),
      { status: 504 }
    );
    const error = await createApiError(response);
    expect(error.code).toBe('SERVER_ERROR');
  });

  it('should map unknown status to UNKNOWN', async () => {
    const response = new Response(
      JSON.stringify({ title: 'Custom error' }),
      { status: 418 }
    );
    const error = await createApiError(response);
    expect(error.code).toBe('UNKNOWN');
  });

  it('should handle response with no JSON body', async () => {
    const response = new Response(null, { status: 500, statusText: 'Internal Server Error' });
    const error = await createApiError(response);
    expect(error.code).toBe('SERVER_ERROR');
    expect(error.message).toBe('Internal Server Error');
  });

  it('should include traceId from ProblemDetails', async () => {
    const response = new Response(
      JSON.stringify({ title: 'Error', traceId: 'trace-123' }),
      { status: 500 }
    );
    const error = await createApiError(response);
    expect(error.traceId).toBe('trace-123');
  });

  it('should use detail as fallback message', async () => {
    const response = new Response(
      JSON.stringify({ detail: 'Detailed error message' }),
      { status: 500 }
    );
    const error = await createApiError(response);
    expect(error.detail).toBe('Detailed error message');
  });
});

describe('createNetworkError — extended', () => {
  it('should create TIMEOUT from AbortError', () => {
    const abortError = new DOMException('The operation was aborted', 'AbortError');
    const error = createNetworkError(abortError);
    expect(error.code).toBe('TIMEOUT');
    expect(error.message).toBe('Request timed out');
  });

  it('should create NETWORK_ERROR from regular Error', () => {
    const error = createNetworkError(new Error('fetch failed'));
    expect(error.code).toBe('NETWORK_ERROR');
    expect(error.message).toBe('Network error');
  });

  it('should create NETWORK_ERROR from non-Error cause', () => {
    const error = createNetworkError('some string error');
    expect(error.code).toBe('NETWORK_ERROR');
    expect(error.cause).toBe('some string error');
  });

  it('should have status 0', () => {
    const error = createNetworkError(new Error('test'));
    expect(error.status).toBe(0);
  });
});
