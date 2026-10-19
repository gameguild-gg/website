/**
 * Error Guards Tests - Comprehensive coverage
 */

import { describe, it, expect } from 'vitest';
import {
  isApiError,
  isValidationError,
  isAuthenticationError,
  isUnauthorized,
  isTokenExpired,
  isAuthorizationError,
  isForbidden,
  isInsufficientPermissions,
  isFeatureNotAvailable,
  isNotFoundError,
  isConflictError,
  isRateLimitError,
  isNetworkError,
  isServerError,
  isRetryableError,
  getRequiredPermissions,
  getRequiredFeature,
  getRetryAfter,
} from '../../src/runtime/errors/guards.js';
import type { ApiError, AuthorizationError, RateLimitError } from '../../src/runtime/errors/types.js';

describe('Error Guards', () => {
  describe('isApiError', () => {
    it('should identify ApiError objects', () => {
      const error = {
        name: 'ApiError',
        code: 'API_ERROR',
        message: 'Test',
        status: 500,
      };
      expect(isApiError(error)).toBe(true);
    });

    it('should reject non-ApiError objects', () => {
      expect(isApiError(new Error('Test'))).toBe(false);
      expect(isApiError(null)).toBe(false);
      expect(isApiError(undefined)).toBe(false);
    });
  });

  describe('isValidationError', () => {
    it('should identify validation errors', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'VALIDATION_ERROR',
        message: 'Invalid',
        status: 400,
      };
      expect(isValidationError(error)).toBe(true);
    });
  });

  describe('isAuthenticationError', () => {
    it('should identify 401 errors', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'AUTHENTICATION_ERROR',
        message: 'Auth required',
        status: 401,
      };
      expect(isAuthenticationError(error)).toBe(true);
    });
  });

  describe('isUnauthorized', () => {
    it('should identify unauthorized errors', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'AUTHENTICATION_ERROR',
        message: 'Unauthorized',
        status: 401,
      };
      expect(isUnauthorized(error)).toBe(true);
    });
  });

  describe('isTokenExpired', () => {
    it('should identify TOKEN_EXPIRED errors', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'TOKEN_EXPIRED',
        message: 'Token expired',
        status: 401,
      };
      expect(isTokenExpired(error)).toBe(true);
    });
  });

  describe('isAuthorizationError', () => {
    it('should identify 403 errors', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'FORBIDDEN',
        message: 'Forbidden',
        status: 403,
      };
      expect(isAuthorizationError(error)).toBe(true);
    });
  });

  describe('isForbidden', () => {
    it('should identify forbidden errors', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'FORBIDDEN',
        message: 'Forbidden',
        status: 403,
      };
      expect(isForbidden(error)).toBe(true);
    });
  });

  describe('isInsufficientPermissions', () => {
    it('should identify INSUFFICIENT_PERMISSIONS code', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'INSUFFICIENT_PERMISSIONS',
        message: 'Insufficient permissions',
        status: 403,
      };
      expect(isInsufficientPermissions(error)).toBe(true);
    });
  });

  describe('isFeatureNotAvailable', () => {
    it('should identify FEATURE_NOT_AVAILABLE code', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'FEATURE_NOT_AVAILABLE',
        message: 'Feature not available',
        status: 403,
      };
      expect(isFeatureNotAvailable(error)).toBe(true);
    });
  });

  describe('isNotFoundError', () => {
    it('should identify 404 NOT_FOUND errors', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'NOT_FOUND',
        message: 'Not found',
        status: 404,
      };
      expect(isNotFoundError(error)).toBe(true);
    });
  });

  describe('isConflictError', () => {
    it('should identify 409 CONFLICT errors', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'CONFLICT',
        message: 'Conflict',
        status: 409,
      };
      expect(isConflictError(error)).toBe(true);
    });
  });

  describe('isRateLimitError', () => {
    it('should identify 429 RATE_LIMITED errors', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'RATE_LIMITED',
        message: 'Too many requests',
        status: 429,
      };
      expect(isRateLimitError(error)).toBe(true);
    });
  });

  describe('isNetworkError', () => {
    it('should identify NETWORK_ERROR code', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'NETWORK_ERROR',
        message: 'Network failed',
        status: 0,
      };
      expect(isNetworkError(error)).toBe(true);
    });

    it('should identify TIMEOUT code', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'TIMEOUT',
        message: 'Request timed out',
        status: 0,
      };
      expect(isNetworkError(error)).toBe(true);
    });
  });

  describe('isServerError', () => {
    it('should identify 500 errors', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'SERVER_ERROR',
        message: 'Internal server error',
        status: 500,
      };
      expect(isServerError(error)).toBe(true);
    });

    it('should identify 502 errors', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'SERVER_ERROR',
        message: 'Bad gateway',
        status: 502,
      };
      expect(isServerError(error)).toBe(true);
    });
  });

  describe('isRetryableError', () => {
    it('should mark network errors as retryable', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'NETWORK_ERROR',
        message: 'Network failed',
        status: 0,
      };
      expect(isRetryableError(error)).toBe(true);
    });

    it('should mark TIMEOUT as retryable', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'TIMEOUT',
        message: 'Timed out',
        status: 0,
      };
      expect(isRetryableError(error)).toBe(true);
    });

    it('should mark rate limit as retryable', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'RATE_LIMITED',
        message: 'Too many requests',
        status: 429,
      };
      expect(isRetryableError(error)).toBe(true);
    });

    it('should mark 502 as retryable', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'SERVER_ERROR',
        message: 'Bad gateway',
        status: 502,
      };
      expect(isRetryableError(error)).toBe(true);
    });

    it('should mark 503 as retryable', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'SERVER_ERROR',
        message: 'Service unavailable',
        status: 503,
      };
      expect(isRetryableError(error)).toBe(true);
    });

    it('should mark 504 as retryable', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'SERVER_ERROR',
        message: 'Gateway timeout',
        status: 504,
      };
      expect(isRetryableError(error)).toBe(true);
    });

    it('should NOT mark 400 as retryable', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'VALIDATION_ERROR',
        message: 'Validation failed',
        status: 400,
      };
      expect(isRetryableError(error)).toBe(false);
    });
  });

  describe('getRequiredPermissions', () => {
    it('should extract required permissions from AuthorizationError', () => {
      const error: AuthorizationError = {
        name: 'ApiError',
        code: 'INSUFFICIENT_PERMISSIONS',
        message: 'Insufficient permissions',
        status: 403,
        requiredPermissions: ['admin', 'write'],
      };
      expect(getRequiredPermissions(error)).toEqual(['admin', 'write']);
    });

    it('should return undefined for non-AuthorizationError', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'SERVER_ERROR',
        message: 'Server error',
        status: 500,
      };
      expect(getRequiredPermissions(error)).toBeUndefined();
    });
  });

  describe('getRequiredFeature', () => {
    it('should extract required feature from FEATURE_NOT_AVAILABLE', () => {
      const error: AuthorizationError = {
        name: 'ApiError',
        code: 'FEATURE_NOT_AVAILABLE',
        message: 'Feature not available',
        status: 403,
        requiredFeature: 'premium',
      };
      expect(getRequiredFeature(error)).toBe('premium');
    });

    it('should return undefined for non-feature errors', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'FORBIDDEN',
        message: 'Forbidden',
        status: 403,
      };
      expect(getRequiredFeature(error)).toBeUndefined();
    });
  });

  describe('getRetryAfter', () => {
    it('should extract retryAfter from RateLimitError', () => {
      const error: RateLimitError = {
        name: 'ApiError',
        code: 'RATE_LIMITED',
        message: 'Too many requests',
        status: 429,
        retryAfter: 60,
      };
      expect(getRetryAfter(error)).toBe(60);
    });

    it('should return undefined for non-rate-limit errors', () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'SERVER_ERROR',
        message: 'Server error',
        status: 500,
      };
      expect(getRetryAfter(error)).toBeUndefined();
    });
  });
});
