import { describe, expect, it, vi } from 'vitest';
import { createServer } from 'node:http';
import { once } from 'node:events';
import { generateModules } from '../../scripts/codegen/modules.js';
import { generateEndpoints } from '../../scripts/codegen/endpoints.js';
import type { OpenApiSpec } from '../../scripts/fetch-spec.js';
import { createFetchTransport } from '../../src/runtime/transport/fetch.js';
import { createClient } from '../../src/client.js';
import { ComplianceAuditCompliancePackagingModule } from '../../src/generated/modules/compliance-audit-compliance-packaging.gen.js';

describe('Compliance package ZIP downloads', () => {
  it('generates a Blob result for a ZIP success response in both public surfaces', () => {
    const spec = {
      openapi: '3.0.1',
      info: { title: 'Evidence API', version: '1' },
      paths: {
        '/v1/audit/compliance-packaging/{id}/download': {
          get: {
            operationId: 'downloadEvidence',
            tags: ['Compliance/Audit/CompliancePackaging'],
            parameters: [{ name: 'id', in: 'path', required: true, schema: { type: 'string', format: 'uuid' } }],
            responses: { '200': { description: 'ZIP artifact', content: { 'application/zip': { schema: { type: 'string', format: 'byte' } } } } },
          },
        },
      },
    } as OpenApiSpec;
    const generatedModule = Object.values(generateModules(spec)).join('\n');
    expect(generatedModule).toContain('Promise<Result<Blob, ApiError>>');
    expect(generatedModule).toContain("responseType: 'blob'");
    expect(generatedModule).toContain('Accept: "application/zip"');
    expect(generateEndpoints(spec)).toContain('DownloadEvidenceOutput = Blob;');
  });

  it('preserves ZIP bytes as a Blob even when Content-Disposition is absent', async () => {
    const bytes = new Uint8Array([0x50, 0x4b, 3, 4, 0, 255, 128, 13, 10]);
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(bytes, {
        headers: { 'Content-Type': 'application/zip' },
        status: 200,
      }),
    );
    try {
      const result = await createFetchTransport({ baseUrl: 'https://example.com' }).request<Blob>({
        method: 'GET',
        path: '/v1/audit/compliance-packaging/package/download',
      });
      expect(result.ok).toBe(true);
      if (!result.ok) throw new Error('Expected ZIP download success');
      expect(result.data.data).toBeInstanceOf(Blob);
      expect(new Uint8Array(await result.data.data.arrayBuffer())).toEqual(bytes);
    } finally {
      fetchMock.mockRestore();
    }
  });

  it('downloads binary bytes through real HTTP with an explicit binary response contract', async () => {
    const bytes = new Uint8Array([0x50, 0x4b, 3, 4, 0, 255, 128]);
    let accept: string | undefined;
    const server = createServer((request, response) => {
      accept = request.headers.accept;
      response.writeHead(200, { 'Content-Type': 'application/zip' });
      response.end(bytes);
    });
    const listening = once(server, 'listening');
    server.listen(0, '127.0.0.1');
    await listening;
    try {
      const address = server.address();
      if (!address || typeof address === 'string') throw new Error('Missing test HTTP port');
      const result = await createFetchTransport({ baseUrl: `http://127.0.0.1:${address.port}` }).request<Blob>({
        method: 'GET',
        path: '/evidence/download',
        responseType: 'blob',
        headers: { Accept: 'application/zip' },
      });
      expect(result.ok).toBe(true);
      if (!result.ok) throw new Error('Expected binary success over HTTP');
      expect(new Uint8Array(await result.data.data.arrayBuffer())).toEqual(bytes);
      expect(accept).toBe('application/zip');
    } finally {
      await new Promise<void>((resolve, reject) => server.close((error) => (error ? reject(error) : resolve())));
    }
  });

  it('keeps authorization errors as structured errors for a binary request', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          title: 'Forbidden',
          detail: 'Administrator access is required.',
          status: 403,
        }),
        { status: 403, headers: { 'Content-Type': 'application/problem+json' } },
      ),
    );
    try {
      const result = await createFetchTransport({ baseUrl: 'https://example.com' }).request<Blob>({
        method: 'GET',
        path: '/evidence/download',
        responseType: 'blob',
      });
      expect(result.ok).toBe(false);
      if (result.ok) throw new Error('Expected access denial');
      expect(result.error.status).toBe(403);
      expect(result.error.code).toBe('FORBIDDEN');
      expect(result.error.message).toBe('Forbidden');
      expect(result.error.detail).toContain('Administrator access');
    } finally {
      fetchMock.mockRestore();
    }
  });

  it('downloads through the regenerated authenticated module with the ZIP Accept header', async () => {
    const bytes = new Uint8Array([0x50, 0x4b, 3, 4, 0, 255, 128]);
    const observed: Array<{ path: string; accept?: string; authorization?: string }> = [];
    const server = createServer((request, response) => {
      observed.push({ path: request.url ?? '', accept: request.headers.accept, authorization: request.headers.authorization });
      response.writeHead(200, { 'Content-Type': 'application/zip' });
      response.end(bytes);
    });
    const listening = once(server, 'listening');
    server.listen(0, '127.0.0.1');
    await listening;
    try {
      const address = server.address();
      if (!address || typeof address === 'string') throw new Error('Missing test HTTP port');
      const client = createClient({
        baseUrl: `http://127.0.0.1:${address.port}`,
        auth: { getAccessToken: async () => 'test-only' },
        devtools: { enabled: false },
      });
      const evidence = new ComplianceAuditCompliancePackagingModule(client);
      for (const result of [await evidence.getAuditCompliancePackagingDownload('test-id'), await evidence.getApiAuditCompliancePackagingDownload('test-id')]) {
        expect(result.ok).toBe(true);
        if (!result.ok) throw new Error('Expected regenerated ZIP module success');
        expect(new Uint8Array(await result.data.arrayBuffer())).toEqual(bytes);
      }
      expect(observed.map((request) => request.path)).toEqual([
        '/v1/audit/compliance-packaging/test-id/download',
        '/api/audit/compliance-packaging/test-id/download',
      ]);
      expect(observed.every((request) => request.accept === 'application/zip' && request.authorization === 'Bearer test-only')).toBe(true);
    } finally {
      await new Promise<void>((resolve, reject) => server.close((error) => (error ? reject(error) : resolve())));
    }
  });
});
