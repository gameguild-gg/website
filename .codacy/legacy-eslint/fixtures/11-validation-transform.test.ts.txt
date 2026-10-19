/**
 * Validation Error Transformation Tests
 * 
 * Tests for Zod error transformation to user-friendly format
 */

import { describe, it, expect } from 'vitest';
import { ZodError, z } from 'zod';
import { transformZodError, isZodError, safeParse } from '../../src/runtime/errors/validation.js';
import type { ApiError } from '../../src/runtime/errors/types.js';

describe('Validation Error Transformation', () => {
  describe('transformZodError', () => {
    it('should transform Zod error to ApiError format', () => {
      const schema = z.object({
        email: z.string().email(),
        age: z.number().min(18),
      });

      try {
        schema.parse({
          email: 'invalid-email',
          age: 15,
        });
      } catch (error) {
        if (error instanceof ZodError) {
          const apiError = transformZodError(error, 'request');

          expect(apiError.code).toBe('VALIDATION_ERROR');
          expect(apiError.status).toBe(400);
          expect(apiError.message).toBe('Request validation failed');
          expect(apiError.metadata?.errors).toBeDefined();
          expect(apiError.metadata?.context).toBe('request');
        }
      }
    });

    it('should include field-specific error details', () => {
      const schema = z.object({
        username: z.string().min(3),
        password: z.string().min(8),
      });

      try {
        schema.parse({
          username: 'ab',
          password: 'short',
        });
      } catch (error) {
        if (error instanceof ZodError) {
          const apiError = transformZodError(error, 'request');
          const errors = apiError.metadata?.errors as any[];

          expect(errors).toHaveLength(2);
          expect(errors[0].field).toBe('username');
          expect(errors[1].field).toBe('password');
        }
      }
    });

    it('should format invalid_type errors', () => {
      const schema = z.object({
        count: z.number(),
      });

      try {
        schema.parse({ count: 'not-a-number' });
      } catch (error) {
        if (error instanceof ZodError) {
          const apiError = transformZodError(error, 'request');
          const errors = apiError.metadata?.errors as any[];

          expect(errors[0].message).toContain('must be');
          expect(errors[0].message).toContain('number');
        }
      }
    });

    it('should format email validation errors', () => {
      const schema = z.object({
        email: z.string().email(),
      });

      try {
        schema.parse({ email: 'not-an-email' });
      } catch (error) {
        if (error instanceof ZodError) {
          const apiError = transformZodError(error, 'request');
          const errors = apiError.metadata?.errors as any[];

          expect(errors[0].message).toContain('valid email');
        }
      }
    });

    it('should format too_small errors for strings', () => {
      const schema = z.string().min(5);

      try {
        schema.parse('abc');
      } catch (error) {
        if (error instanceof ZodError) {
          const apiError = transformZodError(error, 'request');
          const errors = apiError.metadata?.errors as any[];

          expect(errors[0].message).toContain('at least 5 characters');
        }
      }
    });

    it('should format too_big errors for numbers', () => {
      const schema = z.number().max(100);

      try {
        schema.parse(150);
      } catch (error) {
        if (error instanceof ZodError) {
          const apiError = transformZodError(error, 'request');
          const errors = apiError.metadata?.errors as any[];

          expect(errors[0].message).toContain('at most 100');
        }
      }
    });

    it('should format enum validation errors', () => {
      const schema = z.enum(['admin', 'user', 'guest']);

      try {
        schema.parse('invalid-role');
      } catch (error) {
        if (error instanceof ZodError) {
          const apiError = transformZodError(error, 'request');
          const errors = apiError.metadata?.errors as any[];

          expect(errors[0].message).toContain('must be one of');
          expect(errors[0].message).toContain('admin');
        }
      }
    });

    it('should handle nested field paths', () => {
      const schema = z.object({
        user: z.object({
          profile: z.object({
            name: z.string().min(2),
          }),
        }),
      });

      try {
        schema.parse({
          user: {
            profile: {
              name: 'a',
            },
          },
        });
      } catch (error) {
        if (error instanceof ZodError) {
          const apiError = transformZodError(error, 'request');
          const errors = apiError.metadata?.errors as any[];

          expect(errors[0].field).toBe('user.profile.name');
        }
      }
    });

    it('should differentiate request vs response context', () => {
      const schema = z.string();

      try {
        schema.parse(123);
      } catch (error) {
        if (error instanceof ZodError) {
          const requestError = transformZodError(error, 'request');
          expect(requestError.message).toBe('Request validation failed');

          const responseError = transformZodError(error, 'response');
          expect(responseError.message).toBe('Response validation failed');
        }
      }
    });
  });

  describe('isZodError', () => {
    it('should identify ZodError instances', () => {
      const schema = z.string();
      
      try {
        schema.parse(123);
      } catch (error) {
        expect(isZodError(error)).toBe(true);
      }
    });

    it('should return false for non-Zod errors', () => {
      const error = new Error('Regular error');
      expect(isZodError(error)).toBe(false);

      const apiError: ApiError = {
        name: 'ApiError',
        code: 'NETWORK_ERROR',
        message: 'Network failed',
        status: 0,
      };
      expect(isZodError(apiError)).toBe(false);
    });
  });

  describe('safeParse', () => {
    it('should parse valid data successfully', () => {
      const schema = z.object({
        name: z.string(),
        age: z.number(),
      });

      const result = safeParse(schema, { name: 'Alice', age: 30 }, 'request');

      expect(result).toEqual({ name: 'Alice', age: 30 });
    });

    it('should throw ApiError for invalid data', () => {
      const schema = z.object({
        email: z.string().email(),
      });

      expect(() => {
        safeParse(schema, { email: 'invalid' }, 'request');
      }).toThrow();

      try {
        safeParse(schema, { email: 'invalid' }, 'request');
      } catch (error) {
        const apiError = error as ApiError;
        expect(apiError.code).toBe('VALIDATION_ERROR');
        expect(apiError.status).toBe(400);
      }
    });

    it('should preserve non-Zod errors', () => {
      const schema = {
        parse: () => {
          throw new Error('Custom error');
        },
      };

      expect(() => {
        safeParse(schema, {}, 'request');
      }).toThrow('Custom error');
    });

    it('should include context in error metadata', () => {
      const schema = z.string();

      try {
        safeParse(schema, 123, 'response');
      } catch (error) {
        const apiError = error as ApiError;
        expect(apiError.metadata?.context).toBe('response');
      }
    });
  });

  describe('Complex Validation Scenarios', () => {
    it('should handle array validation errors', () => {
      const schema = z.array(z.number()).min(2);

      try {
        schema.parse([1]);
      } catch (error) {
        if (error instanceof ZodError) {
          const apiError = transformZodError(error, 'request');
          const errors = apiError.metadata?.errors as any[];

          expect(errors[0].message).toContain('at least 2 items');
        }
      }
    });

    it('should handle union type errors', () => {
      const schema = z.union([z.string(), z.number()]);

      try {
        schema.parse(true);
      } catch (error) {
        if (error instanceof ZodError) {
          const apiError = transformZodError(error, 'request');
          const errors = apiError.metadata?.errors as any[];

          expect(errors[0].message).toContain('does not match');
        }
      }
    });

    it('should include error codes', () => {
      const schema = z.string().email();

      try {
        schema.parse('invalid');
      } catch (error) {
        if (error instanceof ZodError) {
          const apiError = transformZodError(error, 'request');
          const errors = apiError.metadata?.errors as any[];

          expect(errors[0].code).toBeDefined();
        }
      }
    });
  });
});
