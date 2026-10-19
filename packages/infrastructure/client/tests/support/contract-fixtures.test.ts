import { describe, expect, it, vi } from 'vitest';
import { fixtureTransport, legacyCodeError } from './contract-fixtures.js';
import { err, ok } from '../../src/runtime/result/helpers.js';
import type { RequestConfig } from '../../src/runtime/transport/types.js';

describe('legacy error fixture boundary', () => {
  it.each(['API_ERROR', 'SERVICE_UNAVAILABLE', 'CUSTOM', 'INTERNAL', 'X', 'UNAUTHORIZED', 'ERROR'])(
    'retains the intentionally unsupported code %s without normalizing its payload',
    (code) => {
      const input = { name: 'ApiError' as const, message: 'fixture', status: 503, code };
      const result = legacyCodeError(input);
      expect(result).toBe(input);
      expect(result.code).toBe(code);
    },
  );

  it('retains metadata, cause and trace identity without cloning the error', () => {
    const metadata = { fixture: true };
    const cause = new Error('original');
    const input = { name: 'ApiError' as const, message: 'fixture', status: 503, code: 'CUSTOM', metadata, cause, traceId: 'fixture-trace' };
    const result = legacyCodeError(input);
    expect(result).toBe(input);
    expect(result.metadata).toBe(metadata);
    expect(result.cause).toBe(cause);
    expect(result.traceId).toBe('fixture-trace');
  });
});

describe('generic transport fixture boundary', () => {
  it('forwards the exact request and response objects once', async () => {
    const response = ok({ data: { value: 'fixture' }, status: 200, headers: new Headers() });
    const config: RequestConfig = { method: 'POST', path: '/fixture', body: { value: 'request' } };
    const respond = vi.fn(async (actualConfig: RequestConfig) => {
      expect(actualConfig).toBe(config);
      return response;
    });
    const transport = fixtureTransport(respond);
    expect(await transport.request<{ value: string }>(config)).toBe(response);
    expect(respond).toHaveBeenCalledTimes(1);
    expect(respond).toHaveBeenCalledWith(config);
  });

  it('retains the exact failure result and its legacy error', async () => {
    const failure = err(legacyCodeError({ name: 'ApiError', message: 'fixture', code: 'CUSTOM', status: 503 }));
    const result = await fixtureTransport(async () => failure).request({ method: 'GET', path: '/fixture' });
    expect(result).toBe(failure);
    if (result.ok) throw new Error('Expected the failure fixture');
    expect(result.error).toBe(failure.error);
  });

  it('propagates the original thrown failure', async () => {
    const failure = new Error('original response failure');
    const transport = fixtureTransport(async () => { throw failure; });
    await expect(transport.request({ method: 'GET', path: '/fixture' })).rejects.toBe(failure);
  });

  it('calls the scripted response for every request in order', async () => {
    let index = 0;
    const transport = fixtureTransport(async () => ok({ data: ++index, status: 200, headers: new Headers() }));
    const first = await transport.request<number>({ method: 'GET', path: '/one' });
    const second = await transport.request<number>({ method: 'GET', path: '/two' });
    expect(first.ok && first.data.data).toBe(1);
    expect(second.ok && second.data.data).toBe(2);
    expect(index).toBe(2);
  });

  it('retains binary payload and response header identity', async () => {
    const data = new Uint8Array([0, 255, 17]);
    const headers = new Headers({ 'Content-Type': 'application/octet-stream' });
    const response = ok({ data, status: 200, headers });
    const result = await fixtureTransport(async () => response).request<Uint8Array>({ method: 'GET', path: '/fixture' });
    if (!result.ok) throw new Error('Expected the binary fixture');
    expect(result.data.data).toBe(data);
    expect(result.data.headers).toBe(headers);
  });
});
