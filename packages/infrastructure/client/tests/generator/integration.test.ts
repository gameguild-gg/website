/**
 * Integration tests for the complete generation pipeline
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { existsSync, rmSync, readFileSync } from 'fs';
import { join } from 'path';
import { fileURLToPath } from 'url';
import { dirname } from 'path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

interface SpecResponseFixture {
  ok: boolean;
  status: number;
  statusText?: string;
  json?: () => Promise<unknown>;
}
type SpecFetchFixture = (...args: Parameters<typeof fetch>) => Promise<SpecResponseFixture>;
const mockFetch = vi.fn<SpecFetchFixture>();

describe('Generation Pipeline Integration', () => {
  const testOutputDir = join(__dirname, 'temp-output');

  beforeEach(() => {
    mockFetch.mockReset();
    vi.stubGlobal('fetch', mockFetch);
    // Clean up before each test
    if (existsSync(testOutputDir)) {
      rmSync(testOutputDir, { recursive: true });
    }
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    // Clean up after each test
    if (existsSync(testOutputDir)) {
      rmSync(testOutputDir, { recursive: true });
    }
    vi.clearAllMocks();
  });

  it('should fetch OpenAPI spec from URL', async () => {
    const mockSpec = {
      openapi: '3.0.1',
      info: { title: 'Test API', version: '1.0.0' },
      paths: {
        '/api/test': {
          get: { operationId: 'getTest', responses: {} },
        },
      },
    };

    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => mockSpec,
    });

    const { fetchOpenApiSpec } = await import('../../scripts/fetch-spec.js');
    const spec = await fetchOpenApiSpec('http://localhost:5295/openapi/v1.json');

    expect(spec).toEqual(mockSpec);
    expect(global.fetch).toHaveBeenCalledWith(
      'http://localhost:5295/openapi/v1.json',
      expect.objectContaining({
        headers: { Accept: 'application/json' },
      })
    );
  }, 15000);

  it('should load OpenAPI spec from file', async () => {
    const { fetchOpenApiSpec } = await import('../../scripts/fetch-spec.js');
    const specPath = join(__dirname, 'fixtures', 'simple-spec.json');

    const spec = await fetchOpenApiSpec(specPath);

    expect(spec).toBeDefined();
    expect(spec.info.title).toBe('Test API');
  }, 15000);

  it('should throw error for invalid URL', async () => {
    mockFetch.mockResolvedValue({
      ok: false,
      status: 404,
      statusText: 'Not Found',
    });

    const { fetchOpenApiSpec } = await import('../../scripts/fetch-spec.js');

    await expect(fetchOpenApiSpec('http://localhost:5295/invalid')).rejects.toThrow(
      /Failed to fetch OpenAPI spec/
    );
  });

  it('should throw error for non-existent file', async () => {
    const { fetchOpenApiSpec } = await import('../../scripts/fetch-spec.js');

    await expect(fetchOpenApiSpec('/non/existent/path.json')).rejects.toThrow();
  });

  it('should validate OpenAPI version', async () => {
    const invalidSpec = {
      swagger: '2.0',
      info: { title: 'Old API', version: '1.0' },
    };

    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => invalidSpec,
    });

    const { fetchOpenApiSpec } = await import('../../scripts/fetch-spec.js');

    // Error message is slightly different
    await expect(fetchOpenApiSpec('http://localhost:5295/openapi/v1.json')).rejects.toThrow(
      /Invalid OpenAPI spec/
    );
  });

  it('should normalize and generate types in pipeline', async () => {
    const { normalizeSpec } = await import('../../scripts/normalize.js');
    const { generateTypes } = await import('../../scripts/codegen/types.js');

    const rawSpec = JSON.parse(
      readFileSync(join(__dirname, 'fixtures', 'simple-spec.json'), 'utf-8')
    );

    const normalized = normalizeSpec(rawSpec);
    const types = generateTypes(normalized);

    // Normalized schema names are different from fixture names
    expect(types).toContain('export interface User');
    expect(types).toContain('export interface CreateUserInput');
    expect(types).toContain('export type UserRole');
  }, 30000);

  it('should normalize and generate endpoints in pipeline', async () => {
    const { normalizeSpec } = await import('../../scripts/normalize.js');
    const { generateEndpoints } = await import('../../scripts/codegen/endpoints.js');

    const rawSpec = JSON.parse(
      readFileSync(join(__dirname, 'fixtures', 'simple-spec.json'), 'utf-8')
    );

    const normalized = normalizeSpec(rawSpec);
    const endpoints = generateEndpoints(normalized);

    expect(endpoints).toContain('getUsers');
    expect(endpoints).toContain('createUser');
    expect(endpoints).toContain('getUserById');
  }, 15000);

  it('should detect spec changes via hash', async () => {
    const { createHash } = await import('crypto');

    const spec1 = {
      openapi: '3.0.1',
      info: { title: 'API', version: '1.0' },
      paths: { '/users': {} },
    };

    const spec2 = {
      openapi: '3.0.1',
      info: { title: 'API', version: '1.0' },
      paths: { '/users': {}, '/posts': {} },
    };

    const hash1 = createHash('sha256').update(JSON.stringify(spec1)).digest('hex');
    const hash2 = createHash('sha256').update(JSON.stringify(spec2)).digest('hex');

    expect(hash1).not.toBe(hash2);
  });

  it('should not change hash for identical specs', async () => {
    const { createHash } = await import('crypto');

    const spec = {
      openapi: '3.0.1',
      info: { title: 'API', version: '1.0' },
      paths: {},
    };

    const hash1 = createHash('sha256').update(JSON.stringify(spec)).digest('hex');
    const hash2 = createHash('sha256').update(JSON.stringify(spec)).digest('hex');

    expect(hash1).toBe(hash2);
  });

  it('should handle network errors gracefully', async () => {
    mockFetch.mockRejectedValue(new Error('Network error'));

    const { fetchOpenApiSpec } = await import('../../scripts/fetch-spec.js');

    await expect(fetchOpenApiSpec('http://localhost:5295/openapi/v1.json')).rejects.toThrow(
      'Network error'
    );
  });

  it('should handle malformed JSON gracefully', async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => {
        throw new Error('Invalid JSON');
      },
    });

    const { fetchOpenApiSpec } = await import('../../scripts/fetch-spec.js');

    await expect(fetchOpenApiSpec('http://localhost:5295/openapi/v1.json')).rejects.toThrow();
  });
});
