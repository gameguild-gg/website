import { createServer } from 'node:http';
import { once } from 'node:events';
import { describe, expect, it } from 'vitest';
import { createClient } from '../../src/client.js';
import { ComplianceAuditModule } from '../../src/generated/modules/compliance-audit.gen.js';

describe('Generated audit export contracts', () => {
  it('downloads CSV bytes through all compatible authenticated methods with CSV Accept', async () => {
    const csv = 'Description\r\n"ação, ""quoted"""\r\n';
    const observed: Array<{ path?: string; accept?: string; authorization?: string }> = [];
    const server = createServer((request, response) => {
      observed.push({ path: request.url, accept: request.headers.accept, authorization: request.headers.authorization });
      if (request.headers.accept !== 'text/csv') {
        response.writeHead(406, { 'Content-Type': 'application/problem+json' });
        response.end(JSON.stringify({ status: 406, title: 'CSV Accept required' }));
        return;
      }
      response.writeHead(200, { 'Content-Type': 'text/csv;charset=utf-8' });
      response.end(csv);
    });
    const listening = once(server, 'listening');
    server.listen(0, '127.0.0.1');
    await listening;
    try {
      const address = server.address();
      if (!address || typeof address === 'string') throw new Error('Missing local test port');
      const client = createClient({
        baseUrl: 'http://127.0.0.1:' + address.port,
        auth: { getAccessToken: async () => 'test-only' },
        devtools: { enabled: false },
      });
      const audit = new ComplianceAuditModule(client);
      const methods = [
        () => audit.postApiAuditExportCsv({ columns: ['Description'] }),
        () => audit.postAdminAuditLogsExportCsv({ columns: ['Description'] }),
        () => audit.postAdminAuditLogsExport({ columns: ['Description'] }),
      ];
      for (const exportCsv of methods) {
        const result = await exportCsv();
        expect(result.ok).toBe(true);
        if (!result.ok) throw new Error('Expected CSV download: ' + result.error.message);
        expect(result.data).toBeInstanceOf(Blob);
        expect(await result.data.text()).toBe(csv);
      }
      expect(observed.map((request) => request.path)).toEqual(['/api/audit/export/csv', '/v1/admin/audit-logs/export/csv', '/v1/admin/audit-logs/:export']);
      expect(observed.every((request) => request.accept === 'text/csv' && request.authorization === 'Bearer test-only')).toBe(true);
    } finally {
      await new Promise<void>((resolve, reject) => server.close((error) => (error ? reject(error) : resolve())));
    }
  });

  it('validates the same JSON export schema through the requested and versioned methods', async () => {
    const document = { schemaVersion: '1.0', pagination: { pageNumber: 2, pageSize: 10, totalRecords: 0, totalPages: 0 }, records: [] };
    const observed: string[] = [];
    const server = createServer((request, response) => {
      observed.push(request.url ?? '');
      response.writeHead(200, { 'Content-Type': 'application/json' });
      response.end(JSON.stringify(document));
    });
    const listening = once(server, 'listening');
    server.listen(0, '127.0.0.1');
    await listening;
    try {
      const address = server.address();
      if (!address || typeof address === 'string') throw new Error('Missing local test port');
      const audit = new ComplianceAuditModule(
        createClient({
          baseUrl: 'http://127.0.0.1:' + address.port,
          auth: { getAccessToken: async () => 'test-only' },
          devtools: { enabled: false },
        }),
      );
      for (const result of [
        await audit.postApiAuditExportJson({ pageNumber: 2, pageSize: 10 }),
        await audit.postAdminAuditLogsExportJson({ pageNumber: 2, pageSize: 10 }),
      ]) {
        expect(result).toEqual({ ok: true, data: document });
      }
      expect(observed).toEqual(['/api/audit/export/json', '/v1/admin/audit-logs/export/json']);
    } finally {
      await new Promise<void>((resolve, reject) => server.close((error) => (error ? reject(error) : resolve())));
    }
  });
});
