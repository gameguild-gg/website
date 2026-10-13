import type { ApiError, ApiResponse, RequestConfig, Result } from '@game-guild/client';
import { AuditReplayTimeline, parseReplayTimestamp } from './replay-timeline';
import { importAuditReplaySnapshot, MAX_REPLAY_SNAPSHOT_BYTES, type ImportedAuditReplaySnapshot } from './replay-snapshot-import';

export interface AuditReplayCaptureClient {
  requestRaw<T>(config: RequestConfig): Promise<Result<ApiResponse<T>, ApiError>>;
}
export type AuditReplayCaptureRange = Readonly<{ start: string; end: string }>;
export type AuditReplayAccessScope = Readonly<{ subjectId: string; tenantId: string | null }>;
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function scope(value: AuditReplayAccessScope | null): AuditReplayAccessScope {
  if (value === null || typeof value !== 'object' ||
      typeof value.subjectId !== 'string' || !GUID.test(value.subjectId) ||
      value.subjectId.toLowerCase() === '00000000-0000-0000-0000-000000000000') {
    throw new TypeError('An authenticated replay context is required.');
  }
  const tenantId = new AuditReplayTimeline([], { tenantId: value.tenantId, totalCount: 0 }).tenantId;
  return Object.freeze({ subjectId: value.subjectId.toLowerCase(), tenantId });
}

/** Uses the configured client's existing authentication/tenant transport. Scope changes
 * or cancellation discard the result. Server authorization and durable actor-bound
 * investigations remain authoritative; this does not revoke a downloaded copy. */
export async function captureAuditReplaySnapshot(
  client: AuditReplayCaptureClient,
  range: AuditReplayCaptureRange,
  getCurrentScope: () => AuditReplayAccessScope | null,
  signal?: AbortSignal,
): Promise<ImportedAuditReplaySnapshot> {
  signal?.throwIfAborted();
  const startNs = parseReplayTimestamp(range.start), endNs = parseReplayTimestamp(range.end);
  if (startNs > endNs) {
    throw new RangeError('Replay start must not follow its end.');
  }
  const expected = scope(getCurrentScope());
  const checkContext = () => {
    signal?.throwIfAborted();
    const current = scope(getCurrentScope());
    if (current.subjectId !== expected.subjectId || current.tenantId !== expected.tenantId) {
      throw new DOMException('Replay context changed; capture again.', 'AbortError');
    }
  };
  const response = await client.requestRaw<Blob>({
    method: 'POST',
    path: '/v1/admin/audit-logs/replay/snapshots',
    body: Object.freeze({ start: range.start, end: range.end }),
    headers: { Accept: 'application/json' },
    responseType: 'blob',
    requiresAuth: true,
    cache: 'no-store',
    ...(signal === undefined ? {} : { signal }),
  });
  checkContext();
  if (!response.ok) {
    throw new Error('Audit replay capture failed.', { cause: response.error });
  }
  const { data, headers, status } = response.data;
  if (status !== 200 || !(data instanceof Blob) || data.size > MAX_REPLAY_SNAPSHOT_BYTES ||
      headers.get('Content-Type')?.split(';')[0]?.trim().toLowerCase() !== 'application/json') {
    throw new TypeError('Audit replay capture returned an invalid response.');
  }
  const digest = headers.get('X-Audit-Snapshot-SHA256');
  if (digest === null) {
    throw new TypeError('Audit replay capture did not return its integrity receipt.');
  }
  // Preserve the original UTF-8 bytes. Fatal decoding rejects malformed input; a BOM
  // remains part of the input and is not silently stripped before digest validation.
  const bytes = await data.arrayBuffer();
  checkContext();
  const json = new TextDecoder('utf-8', { fatal: true, ignoreBOM: true }).decode(bytes);
  const imported = await importAuditReplaySnapshot(json, digest, expected.tenantId);
  checkContext();
  return imported;
}
