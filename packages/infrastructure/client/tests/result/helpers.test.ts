/**
 * Result Helpers Tests
 * 
 * Tests for Result monad helper functions
 */

import { describe, it, expect } from 'vitest';
import { ok, err, isOk, isErr, unwrap, unwrapOr, unwrapOrElse, match, map, mapErr, flatMap, fromPromise, toPromise } from '../../src/runtime/result/helpers.js';
import type { Result } from '../../src/runtime/result/types.js';

describe('Result Helpers', () => {
  describe('ok', () => {
    it('should create successful result', () => {
      const result = ok(42);

      expect(result.ok).toBe(true);
      if (result.ok) {
        expect(result.data).toBe(42);
      }
    });

    it('should create result with object data', () => {
      const data = { id: '123', name: 'Test' };
      const result = ok(data);

      expect(result.ok).toBe(true);
      if (result.ok) {
        expect(result.data).toEqual(data);
      }
    });

    it('should create result with null data', () => {
      const result = ok(null);

      expect(result.ok).toBe(true);
      if (result.ok) {
        expect(result.data).toBeNull();
      }
    });
  });

  describe('err', () => {
    it('should create error result', () => {
      const error = { message: 'Test error', code: 'ERROR' };
      const result = err(error);

      expect(result.ok).toBe(false);
      if (!result.ok) {
        expect(result.error).toEqual(error);
      }
    });

    it('should create result with string error', () => {
      const result = err('Something went wrong');

      expect(result.ok).toBe(false);
      if (!result.ok) {
        expect(result.error).toBe('Something went wrong');
      }
    });
  });

  describe('isOk', () => {
    it('should return true for successful results', () => {
      const result = ok(42);
      expect(isOk(result)).toBe(true);
    });

    it('should return false for error results', () => {
      const result = err('error');
      expect(isOk(result)).toBe(false);
    });

    it('should narrow type correctly', () => {
      const result: Result<number, string> = ok(42);

      if (isOk(result)) {
        // TypeScript should know result.data is number here
        expect(result.data).toBe(42);
      }
    });
  });

  describe('isErr', () => {
    it('should return true for error results', () => {
      const result = err('error');
      expect(isErr(result)).toBe(true);
    });

    it('should return false for successful results', () => {
      const result = ok(42);
      expect(isErr(result)).toBe(false);
    });

    it('should narrow type correctly', () => {
      const result: Result<number, string> = err('failed');

      if (isErr(result)) {
        // TypeScript should know result.error is string here
        expect(result.error).toBe('failed');
      }
    });
  });

  describe('unwrap', () => {
    it('should return data for successful results', () => {
      const result = ok(42);
      expect(unwrap(result)).toBe(42);
    });

    it('should throw for error results', () => {
      const result = err('Something went wrong');
      expect(() => unwrap(result)).toThrow('Something went wrong');
    });

    it('should throw with error object message', () => {
      const result = err({ message: 'Custom error', code: 'ERR' });
      expect(() => unwrap(result)).toThrow();
    });
  });

  describe('unwrapOr', () => {
    it('should return data for successful results', () => {
      const result = ok(42);
      expect(unwrapOr(result, 0)).toBe(42);
    });

    it('should return default for error results', () => {
      const result = err('error');
      expect(unwrapOr(result, 0)).toBe(0);
    });

    it('should work with object defaults', () => {
      const defaultValue = { id: 'default', name: 'Default' };
      const result: Result<{ id: string; name: string }, string> = err('error');

      expect(unwrapOr(result, defaultValue)).toEqual(defaultValue);
    });

    it('should work with null defaults', () => {
      const result: Result<string, string> = err('error');
      expect(unwrapOr(result, null)).toBeNull();
    });
  });

  describe('match', () => {
    it('should call ok handler for successful results', () => {
      const result = ok(42);
      const output = match(result, {
        ok: (data) => `Success: ${data}`,
        err: (error) => `Error: ${error}`,
      });

      expect(output).toBe('Success: 42');
    });

    it('should call err handler for error results', () => {
      const result = err('Something failed');
      const output = match(result, {
        ok: (data) => `Success: ${data}`,
        err: (error) => `Error: ${error}`,
      });

      expect(output).toBe('Error: Something failed');
    });

    it('should work with different return types', () => {
      const result = ok(42);
      const number = match(result, {
        ok: (data) => data * 2,
        err: () => 0,
      });

      expect(number).toBe(84);
    });

    it('should work with complex transformations', () => {
      type User = { id: string; name: string };
      type UserError = { code: string; message: string };

      const result: Result<User, UserError> = ok({ id: '123', name: 'Alice' });

      const greeting = match<User, UserError, string>(result, {
        ok: (user) => `Hello, ${user.name}!`,
        err: (error) => `Error ${error.code}: ${error.message}`,
      });

      expect(greeting).toBe('Hello, Alice!');
    });

    it('should handle void returns', () => {
      let sideEffect = '';

      const result = ok(42);
      match(result, {
        ok: (data) => { sideEffect = `Got ${data}`; },
        err: () => { sideEffect = 'Error'; },
      });

      expect(sideEffect).toBe('Got 42');
    });
  });

  describe('unwrapOrElse', () => {
    it('should return data for successful results', () => {
      const result = ok(42);
      expect(unwrapOrElse(result, () => 0)).toBe(42);
    });

    it('should call fn with error for error results', () => {
      const result: Result<number, string> = err('bad');
      const value = unwrapOrElse(result, (error) => error.length);
      expect(value).toBe(3);
    });

    it('should pass error to the function', () => {
      const result: Result<string, { code: number }> = err({ code: 404 });
      const value = unwrapOrElse(result, (e) => `Error ${e.code}`);
      expect(value).toBe('Error 404');
    });
  });

  describe('map', () => {
    it('should transform success value', () => {
      const result = ok(5);
      const mapped = map(result, (x) => x * 2);
      expect(isOk(mapped)).toBe(true);
      if (mapped.ok) expect(mapped.data).toBe(10);
    });

    it('should pass through error unchanged', () => {
      const result: Result<number, string> = err('error');
      const mapped = map<number, number, string>(result, (x) => x * 2);
      expect(isErr(mapped)).toBe(true);
      if (!mapped.ok) expect(mapped.error).toBe('error');
    });

    it('should transform type', () => {
      const result = ok(42);
      const mapped = map(result, (x) => String(x));
      if (mapped.ok) expect(mapped.data).toBe('42');
    });
  });

  describe('mapErr', () => {
    it('should transform error value', () => {
      const result: Result<number, string> = err('not found');
      const mapped = mapErr(result, (e) => ({ message: e, code: 404 }));
      expect(isErr(mapped)).toBe(true);
      if (!mapped.ok) {
        expect(mapped.error).toEqual({ message: 'not found', code: 404 });
      }
    });

    it('should pass through success unchanged', () => {
      const result: Result<number, string> = ok(42);
      const mapped = mapErr(result, (e) => ({ message: e }));
      expect(isOk(mapped)).toBe(true);
      if (mapped.ok) expect(mapped.data).toBe(42);
    });
  });

  describe('flatMap', () => {
    it('should chain successful results', () => {
      const result = ok(10);
      const chained = flatMap(result, (x) =>
        x > 0 ? ok(x * 2) : err('negative'),
      );
      expect(isOk(chained)).toBe(true);
      if (chained.ok) expect(chained.data).toBe(20);
    });

    it('should return error from fn', () => {
      const result = ok(-5);
      const chained = flatMap(result, (x) =>
        x > 0 ? ok(x * 2) : err('negative'),
      );
      expect(isErr(chained)).toBe(true);
      if (!chained.ok) expect(chained.error).toBe('negative');
    });

    it('should pass through original error', () => {
      const result: Result<number, string> = err('initial error');
      const chained = flatMap<number, number, string>(result, (x) => ok(x * 2));
      expect(isErr(chained)).toBe(true);
      if (!chained.ok) expect(chained.error).toBe('initial error');
    });

    it('should support multi-step chaining', () => {
      const parse = (s: string): Result<number, string> => {
        const n = parseInt(s, 10);
        return isNaN(n) ? err('not a number') : ok(n);
      };
      const double = (n: number): Result<number, string> =>
        n > 100 ? err('too large') : ok(n * 2);

      const result1 = flatMap(parse('21'), double);
      expect(isOk(result1)).toBe(true);
      if (result1.ok) expect(result1.data).toBe(42);

      const result2 = flatMap(parse('abc'), double);
      expect(isErr(result2)).toBe(true);

      const result3 = flatMap(parse('200'), double);
      expect(isErr(result3)).toBe(true);
      if (!result3.ok) expect(result3.error).toBe('too large');
    });
  });

  describe('fromPromise', () => {
    it('should wrap resolved promise as ok', async () => {
      const result = await fromPromise(Promise.resolve(42));
      expect(isOk(result)).toBe(true);
      if (result.ok) expect(result.data).toBe(42);
    });

    it('should wrap rejected promise as err', async () => {
      const result = await fromPromise(Promise.reject(new Error('fail')));
      expect(isErr(result)).toBe(true);
      if (!result.ok) expect(result.error).toBeInstanceOf(Error);
    });

    it('should use custom error mapper', async () => {
      const result = await fromPromise(
        Promise.reject(new Error('fail')),
        (e) => ({ code: 'FAIL', original: e }),
      );
      expect(isErr(result)).toBe(true);
      if (!result.ok) {
        expect(result.error.code).toBe('FAIL');
      }
    });

    it('should handle non-Error rejections', async () => {
      const result = await fromPromise(Promise.reject('string error'));
      expect(isErr(result)).toBe(true);
      if (!result.ok) expect(result.error).toBe('string error');
    });
  });

  describe('toPromise', () => {
    it('should resolve for ok result', async () => {
      const result = ok(42);
      const value = await toPromise(result);
      expect(value).toBe(42);
    });

    it('should reject for err result', async () => {
      const result = err('failure');
      await expect(toPromise(result)).rejects.toBe('failure');
    });

    it('should reject with error object', async () => {
      const error = new Error('something went wrong');
      const result = err(error);
      await expect(toPromise(result)).rejects.toThrow('something went wrong');
    });
  });

  describe('Result Type Guards in Practice', () => {
    it('should work in if-else chains', () => {
      const result: Result<number, string> = ok(42);

      if (isOk(result)) {
        expect(result.data).toBe(42);
      } else {
        throw new Error('Should not reach here');
      }
    });

    it('should work with early returns', () => {
      function processResult(result: Result<number, string>): number {
        if (isErr(result)) {
          return -1;
        }
        return result.data * 2;
      }

      expect(processResult(ok(21))).toBe(42);
      expect(processResult(err('error'))).toBe(-1);
    });

    it('should compose with other helpers', () => {
      const result = ok(42);

      const value = isOk(result) 
        ? unwrap(result) 
        : unwrapOr(result, 0);

      expect(value).toBe(42);
    });
  });
});
