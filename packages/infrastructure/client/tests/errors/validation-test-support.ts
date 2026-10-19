import { ZodError } from 'zod';
import { isApiError } from '../../src/runtime/errors/guards.js';
import type { ApiError } from '../../src/runtime/errors/types.js';
import type { ValidationErrorDetail } from '../../src/runtime/errors/validation.js';

function isValidationErrorDetail(value: unknown): value is ValidationErrorDetail {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    && 'field' in value && typeof value.field === 'string'
    && 'message' in value && typeof value.message === 'string'
    && 'code' in value && typeof value.code === 'string';
}

/** Read actual public output without assuming that unknown metadata is typed. */
export function readValidationDetails(error: ApiError): ValidationErrorDetail[] {
  const details = error.metadata?.errors;
  if (!Array.isArray(details) || !details.every(isValidationErrorDetail)) {
    throw new Error('Expected validation metadata with field, message and code strings');
  }
  return details;
}

export function assertApiError(error: unknown): asserts error is ApiError {
  if (!isApiError(error)) {
    throw new Error('Expected an ApiError from failed validation');
  }
}

/** Deliberately inject unsupported legacy issue shapes to test fallback paths. */
export function createMalformedZodError(issues: readonly unknown[]): ZodError {
  return new ZodError([...issues] as ConstructorParameters<typeof ZodError>[0]);
}
