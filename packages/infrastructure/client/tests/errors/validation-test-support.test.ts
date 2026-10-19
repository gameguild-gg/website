import { describe, expect, it } from 'vitest';
import type { ApiError } from '../../src/runtime/errors/types.js';
import { assertApiError, createMalformedZodError, readValidationDetails } from './validation-test-support.js';

function errorWith(metadata?: Record<string, unknown>): ApiError {
  return { name: 'ApiError', code: 'VALIDATION_ERROR', status: 400, message: 'invalid', metadata };
}

describe('validation output assertions', () => {
  it('preserves the actual detail array and opaque value identity', () => {
    const value = { nested: ['original'] };
    const details = [{ field: 'name', message: 'invalid', code: 'custom', value }];
    expect(readValidationDetails(errorWith({ errors: details }))).toBe(details);
    expect(details[0]?.value).toBe(value);
  });

  it('accepts an empty issue list', () => {
    expect(readValidationDetails(errorWith({ errors: [] }))).toEqual([]);
  });

  it.each([
    { name: 'missing metadata', metadata: undefined },
    { name: 'missing errors', metadata: {} },
    { name: 'null errors', metadata: { errors: null } },
    { name: 'object errors', metadata: { errors: {} } },
    { name: 'text entry', metadata: { errors: ['invalid'] } },
    { name: 'null entry', metadata: { errors: [null] } },
    { name: 'array entry', metadata: { errors: [[]] } },
    { name: 'missing field', metadata: { errors: [{ message: 'invalid', code: 'custom' }] } },
    { name: 'numeric field', metadata: { errors: [{ field: 7, message: 'invalid', code: 'custom' }] } },
    { name: 'numeric message', metadata: { errors: [{ field: 'x', message: 7, code: 'custom' }] } },
    { name: 'numeric code', metadata: { errors: [{ field: 'x', message: 'invalid', code: 7 }] } },
  ])('rejects $name instead of concealing an invalid output', ({ metadata }) => {
    expect(() => readValidationDetails(errorWith(metadata)))
      .toThrowError('Expected validation metadata');
  });

  it('narrows a real API error without changing it', () => {
    const source: unknown = errorWith({ errors: [] });
    assertApiError(source);
    expect(source.code).toBe('VALIDATION_ERROR');
  });

  it.each([
    { name: 'undefined', value: undefined },
    { name: 'null', value: null },
    { name: 'boolean', value: false },
    { name: 'number', value: 7 },
    { name: 'text', value: 'error' },
    { name: 'Error', value: new Error('wrong error') },
  ])(
    'rejects a non-API $name failure',
    ({ value }) => {
      expect(() => assertApiError(value)).toThrowError('Expected an ApiError');
    },
  );

  it('retains every deliberately unsupported legacy issue object', () => {
    const issue = { code: 'invalid_string', validation: 'regex', path: ['field'], message: 'legacy fallback' };
    const error = createMalformedZodError([issue]);
    expect(error.issues).toHaveLength(1);
    expect(error.issues[0]).toEqual(issue);
  });
});
