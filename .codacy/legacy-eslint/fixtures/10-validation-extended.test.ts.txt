/**
 * Extended Validation Tests — test unexported functions via public API
 * formatIssueMessage and transformZodIssue are tested through transformZodError and safeParse
 */

import { describe, it, expect } from 'vitest';
import { z, ZodError } from 'zod';
import {
  transformZodError,
  isZodError,
  safeParse,
} from '../../src/runtime/errors/validation.js';

describe('transformZodError — branch coverage', () => {
  it('should format invalid_type issues', () => {
    const schema = z.object({ name: z.string() });
    try {
      schema.parse({ name: 42 });
    } catch (e) {
      const result = transformZodError(e as ZodError);
      expect(result.code).toBe('VALIDATION_ERROR');
      expect(result.metadata?.errors).toBeDefined();
      const errors = result.metadata!.errors as any[];
      expect(errors.length).toBeGreaterThan(0);
      expect(errors[0].message).toContain('string');
    }
  });

  it('should format invalid_string email issues', () => {
    const schema = z.object({ email: z.string().email() });
    try {
      schema.parse({ email: 'not-an-email' });
    } catch (e) {
      const result = transformZodError(e as ZodError);
      const errors = result.metadata!.errors as any[];
      expect(errors[0].message).toContain('email');
    }
  });

  it('should format invalid_string url issues', () => {
    const schema = z.object({ website: z.string().url() });
    try {
      schema.parse({ website: 'not-a-url' });
    } catch (e) {
      const result = transformZodError(e as ZodError);
      const errors = result.metadata!.errors as any[];
      expect(errors[0].message).toContain('URL');
    }
  });

  it('should format invalid_string uuid issues', () => {
    const schema = z.object({ id: z.string().uuid() });
    try {
      schema.parse({ id: 'not-a-uuid' });
    } catch (e) {
      const result = transformZodError(e as ZodError);
      const errors = result.metadata!.errors as any[];
      expect(errors[0].message).toContain('UUID');
    }
  });

  it('should format too_small string issues', () => {
    const schema = z.object({ name: z.string().min(3) });
    try {
      schema.parse({ name: 'ab' });
    } catch (e) {
      const result = transformZodError(e as ZodError);
      const errors = result.metadata!.errors as any[];
      expect(errors[0].message).toContain('3');
      expect(errors[0].message).toContain('character');
    }
  });

  it('should format too_small number issues', () => {
    const schema = z.object({ age: z.number().min(0) });
    try {
      schema.parse({ age: -1 });
    } catch (e) {
      const result = transformZodError(e as ZodError);
      const errors = result.metadata!.errors as any[];
      expect(errors[0].message).toContain('0');
    }
  });

  it('should format too_small array issues', () => {
    const schema = z.object({ items: z.array(z.string()).min(1) });
    try {
      schema.parse({ items: [] });
    } catch (e) {
      const result = transformZodError(e as ZodError);
      const errors = result.metadata!.errors as any[];
      expect(errors[0].message).toContain('1');
    }
  });

  it('should format too_big string issues', () => {
    const schema = z.object({ bio: z.string().max(5) });
    try {
      schema.parse({ bio: 'this is too long' });
    } catch (e) {
      const result = transformZodError(e as ZodError);
      const errors = result.metadata!.errors as any[];
      expect(errors[0].message).toContain('5');
    }
  });

  it('should format too_big number issues', () => {
    const schema = z.object({ score: z.number().max(100) });
    try {
      schema.parse({ score: 101 });
    } catch (e) {
      const result = transformZodError(e as ZodError);
      const errors = result.metadata!.errors as any[];
      expect(errors[0].message).toContain('100');
    }
  });

  it('should format too_big array issues', () => {
    const schema = z.object({ items: z.array(z.string()).max(2) });
    try {
      schema.parse({ items: ['a', 'b', 'c'] });
    } catch (e) {
      const result = transformZodError(e as ZodError);
      const errors = result.metadata!.errors as any[];
      expect(errors[0].message).toContain('2');
    }
  });

  it('should format invalid_enum_value issues', () => {
    const schema = z.object({ color: z.enum(['red', 'green', 'blue']) });
    try {
      schema.parse({ color: 'purple' });
    } catch (e) {
      const result = transformZodError(e as ZodError);
      const errors = result.metadata!.errors as any[];
      expect(errors[0].message).toContain('red');
    }
  });

  it('should format invalid_union issues', () => {
    const schema = z.union([z.string(), z.number()]);
    try {
      schema.parse(true);
    } catch (e) {
      const result = transformZodError(e as ZodError);
      const errors = result.metadata!.errors as any[];
      expect(errors.length).toBeGreaterThan(0);
    }
  });

  it('should format custom validation issues', () => {
    const schema = z.string().refine((v) => v === 'secret', 'Must be secret');
    try {
      schema.parse('nope');
    } catch (e) {
      const result = transformZodError(e as ZodError);
      const errors = result.metadata!.errors as any[];
      expect(errors[0].message).toBe('Must be secret');
    }
  });

  it('should handle multiple issues', () => {
    const schema = z.object({
      name: z.string().min(1),
      email: z.string().email(),
      age: z.number(),
    });
    try {
      schema.parse({ name: '', email: 'bad', age: 'not-number' });
    } catch (e) {
      const result = transformZodError(e as ZodError);
      const errors = result.metadata!.errors as any[];
      expect(errors.length).toBeGreaterThanOrEqual(2);
    }
  });

  it('should set context in error metadata for request', () => {
    const schema = z.string();
    try {
      schema.parse(42);
    } catch (e) {
      const result = transformZodError(e as ZodError, 'request');
      expect(result.message).toContain('Request');
      expect(result.metadata?.context).toBe('request');
    }
  });

  it('should set context in error metadata for response', () => {
    const schema = z.string();
    try {
      schema.parse(42);
    } catch (e) {
      const result = transformZodError(e as ZodError, 'response');
      expect(result.message).toContain('Response');
      expect(result.metadata?.context).toBe('response');
    }
  });

  it('should handle empty path (root field)', () => {
    const schema = z.string();
    try {
      schema.parse(42);
    } catch (e) {
      const result = transformZodError(e as ZodError);
      const errors = result.metadata!.errors as any[];
      expect(errors[0].field).toBe('root');
    }
  });

  it('should include nested field paths', () => {
    const schema = z.object({ user: z.object({ name: z.string() }) });
    try {
      schema.parse({ user: { name: 123 } });
    } catch (e) {
      const result = transformZodError(e as ZodError);
      const errors = result.metadata!.errors as any[];
      expect(errors[0].field).toBe('user.name');
    }
  });
});

describe('isZodError', () => {
  it('should return true for actual ZodError instances', () => {
    const schema = z.string();
    try {
      schema.parse(42);
    } catch (e) {
      expect(isZodError(e)).toBe(true);
    }
  });

  it('should return false for regular Error', () => {
    expect(isZodError(new Error('hello'))).toBe(false);
  });

  it('should return false for null', () => {
    expect(isZodError(null)).toBe(false);
  });

  it('should return false for undefined', () => {
    expect(isZodError(undefined)).toBe(false);
  });

  it('should return false for plain object with issues', () => {
    // Plain objects are not ZodError instances
    expect(isZodError({ issues: [{ code: 'custom', path: [], message: 'x' }] })).toBe(false);
  });
});

describe('safeParse', () => {
  it('should return data for valid schema parse', () => {
    const schema = z.object({ name: z.string() });
    const result = safeParse(schema, { name: 'valid' });
    expect(result).toEqual({ name: 'valid' });
  });

  it('should throw ApiError for invalid data (ZodError)', () => {
    const schema = z.object({ name: z.string() });
    expect(() => safeParse(schema, { name: 42 })).toThrow();

    try {
      safeParse(schema, { name: 42 });
    } catch (e: any) {
      expect(e.code).toBe('VALIDATION_ERROR');
      expect(e.metadata?.errors).toBeDefined();
    }
  });

  it('should re-throw non-Zod errors', () => {
    const schema = {
      parse: () => { throw new Error('Unexpected internal error'); },
    };

    expect(() => safeParse(schema, {})).toThrow('Unexpected internal error');
  });

  it('should re-throw non-Error values', () => {
    const schema = {
      parse: () => { throw 'string error'; },
    };

    expect(() => safeParse(schema, {})).toThrow();
  });

  it('should pass context to transformZodError', () => {
    const schema = z.string();
    try {
      safeParse(schema, 42, 'request');
    } catch (e: any) {
      expect(e.message).toContain('Request');
    }
  });
});

describe('formatIssueMessage — edge cases', () => {
  it('should format invalid_string datetime issues', () => {
    const schema = z.object({ createdAt: z.string().datetime() });
    try {
      schema.parse({ createdAt: 'not-a-date' });
    } catch (e) {
      const result = transformZodError(e as ZodError);
      const errors = result.metadata!.errors as any[];
      expect(errors[0].message).toContain('ISO datetime');
    }
  });

  it('should fallback for unknown invalid_string validation type', () => {
    // Create a ZodError manually with an unrecognized validation type  
    const zodError = new ZodError([
      {
        code: 'invalid_string',
        validation: 'regex' as any,
        path: ['field'],
        message: 'Invalid regex match',
      },
    ]);
    const result = transformZodError(zodError);
    const errors = result.metadata!.errors as any[];
    expect(errors[0].message).toBe('Invalid regex match');
  });

  it('should fallback for too_small with unknown type', () => {
    const zodError = new ZodError([
      {
        code: 'too_small',
        type: 'set' as any,
        minimum: 1,
        inclusive: true,
        exact: false,
        path: ['tags'],
        message: 'Set must have at least 1 element',
      },
    ]);
    const result = transformZodError(zodError);
    const errors = result.metadata!.errors as any[];
    expect(errors[0].message).toBe('Set must have at least 1 element');
  });

  it('should fallback for too_big with unknown type', () => {
    const zodError = new ZodError([
      {
        code: 'too_big',
        type: 'set' as any,
        maximum: 5,
        inclusive: true,
        exact: false,
        path: ['tags'],
        message: 'Set must have at most 5 elements',
      },
    ]);
    const result = transformZodError(zodError);
    const errors = result.metadata!.errors as any[];
    expect(errors[0].message).toBe('Set must have at most 5 elements');
  });

  it('should fallback for custom issue with empty message', () => {
    const zodError = new ZodError([
      {
        code: 'custom',
        path: ['myField'],
        message: '',
      },
    ]);
    const result = transformZodError(zodError);
    const errors = result.metadata!.errors as any[];
    expect(errors[0].message).toBe('myField is invalid');
  });
});
