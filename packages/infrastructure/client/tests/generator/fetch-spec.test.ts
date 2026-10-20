import { mkdtempSync, rmSync, writeFileSync } from 'fs';
import { tmpdir } from 'os';
import { join } from 'path';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { fetchOpenApiSpec, getOperationIds, getTags, type OpenApiSpec } from '../../scripts/fetch-spec.js';

describe('OpenAPI spec fetcher', () => {
  let tempDir: string;
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    tempDir = mkdtempSync(join(tmpdir(), 'modu-client-spec-'));
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    rmSync(tempDir, { recursive: true, force: true });
    vi.unstubAllGlobals();
  });

  it('loads and validates JSON specs from files', async () => {
    const specPath = join(tempDir, 'openapi.json');
    writeFileSync(
      specPath,
      JSON.stringify({
        openapi: '3.0.1',
        info: { title: 'Client API', version: '1.0.0' },
        paths: { '/health': { get: { operationId: 'getHealth', responses: {} } } },
      }),
    );

    await expect(fetchOpenApiSpec(specPath)).resolves.toMatchObject({
      openapi: '3.0.1',
      info: { title: 'Client API' },
    });
  });

  it('loads and validates specs from HTTP and HTTPS URLs', async () => {
    const spec = {
      openapi: '3.1.0',
      info: { title: 'Remote API', version: '2.0.0' },
      paths: { '/users': { get: { operationId: 'getUsers', responses: {} } } },
    };
    fetchMock.mockResolvedValue({ ok: true, status: 200, json: async () => spec });

    await expect(fetchOpenApiSpec('https://api.example.test/openapi.json')).resolves.toEqual(spec);
    await expect(fetchOpenApiSpec('http://api.example.test/openapi.json')).resolves.toEqual(spec);
    expect(fetchMock).toHaveBeenCalledWith(
      'https://api.example.test/openapi.json',
      expect.objectContaining({ headers: { Accept: 'application/json' } }),
    );
  });

  it('rejects failed URL fetches and malformed remote specs', async () => {
    fetchMock.mockResolvedValueOnce({ ok: false, status: 404, statusText: 'Not Found' });
    await expect(fetchOpenApiSpec('https://api.example.test/missing.json')).rejects.toThrow(
      'Failed to fetch OpenAPI spec: HTTP 404 Not Found',
    );

    fetchMock.mockResolvedValueOnce({ ok: true, status: 200, json: async () => ({ swagger: '2.0' }) });
    await expect(fetchOpenApiSpec('https://api.example.test/swagger.json')).rejects.toThrow(
      'Invalid OpenAPI spec: missing "openapi" field',
    );
  });

  it('rejects unsupported file formats and invalid OpenAPI shapes', async () => {
    const yamlPath = join(tempDir, 'openapi.yaml');
    writeFileSync(yamlPath, 'openapi: 3.0.1');
    await expect(fetchOpenApiSpec(yamlPath)).rejects.toThrow('YAML OpenAPI specs not yet supported');

    const unsupportedVersionPath = join(tempDir, 'unsupported.json');
    writeFileSync(
      unsupportedVersionPath,
      JSON.stringify({ openapi: '2.0.0', info: { title: 'Old', version: '1.0.0' }, paths: { '/x': {} } }),
    );
    await expect(fetchOpenApiSpec(unsupportedVersionPath)).rejects.toThrow('Unsupported OpenAPI version');

    const missingInfoPath = join(tempDir, 'missing-info.json');
    writeFileSync(missingInfoPath, JSON.stringify({ openapi: '3.0.1', paths: { '/x': {} } }));
    await expect(fetchOpenApiSpec(missingInfoPath)).rejects.toThrow('Invalid OpenAPI spec: missing "info" field');

    const noPathsPath = join(tempDir, 'no-paths.json');
    writeFileSync(noPathsPath, JSON.stringify({ openapi: '3.0.1', info: { title: 'Empty', version: '1.0.0' }, paths: {} }));
    await expect(fetchOpenApiSpec(noPathsPath)).rejects.toThrow('Invalid OpenAPI spec: no paths defined');
  });

  it('extracts operation ids and sorted tags from top-level and operation metadata', () => {
    const spec: OpenApiSpec = {
      openapi: '3.0.1',
      info: { title: 'Metadata API', version: '1.0.0' },
      tags: [{ name: 'Billing' }],
      paths: {
        '/empty': undefined,
        '/users': {
          get: { operationId: 'getUsers', tags: ['Users'], responses: {} },
          post: { tags: ['Users', 'Admin'], responses: {} },
        },
        '/users/{id}': {
          put: { operationId: 'putUser', tags: ['Users'], responses: {} },
          patch: { operationId: 'patchUser', responses: {} },
          delete: { operationId: 'deleteUser', tags: ['Admin'], responses: {} },
          options: { operationId: 'optionsUser', tags: ['Options'], responses: {} },
          head: { operationId: 'headUser', tags: ['Head'], responses: {} },
        },
      },
    } as OpenApiSpec;

    expect(getOperationIds(spec)).toEqual(['getUsers', 'putUser', 'deleteUser', 'patchUser', 'optionsUser', 'headUser']);
    expect(getTags(spec)).toEqual(['Admin', 'Billing', 'Head', 'Options', 'Users']);
  });

  it('returns empty metadata when paths and tags are absent', () => {
    const spec = {
      openapi: '3.0.1',
      info: { title: 'Metadata API', version: '1.0.0' },
    } as OpenApiSpec;

    expect(getOperationIds(spec)).toEqual([]);
    expect(getTags(spec)).toEqual([]);
  });
});
