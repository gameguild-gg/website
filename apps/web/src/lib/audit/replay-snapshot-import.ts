import { AuditReplayTimeline, parseReplayTimestamp, MAX_REPLAY_EVENTS, type ReplayEventInput } from './replay-timeline';

export const MAX_REPLAY_SNAPSHOT_BYTES = 16 * 1024 * 1024;
const ROOT_FIELDS = ['schemaVersion', 'tenantId', 'totalCount', 'startUtc', 'endUtc', 'capturedAtUtc', 'events'] as const;
const EVENT_FIELDS = ['id', 'tenantId', 'createdAt', 'actionType', 'resourceType', 'resourceId', 'userId', 'sessionId', 'correlationId', 'success'] as const;
const UTC_TICKS = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$/;

function record(value: unknown, fields: readonly string[], label: string): Record<string, unknown> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    throw new TypeError(label + ' must be an object.');
  }
  const result = value as Record<string, unknown>;
  const names = Object.keys(result);
  if (names.length !== fields.length || names.some(name => !fields.includes(name))) {
    throw new TypeError(label + ' must contain exactly the safe snapshot fields.');
  }
  return result;
}

function timestamp(value: unknown, label: string): { text: string; ns: bigint } {
  if (typeof value !== 'string' || !UTC_TICKS.test(value)) {
    throw new TypeError(label + ' must be a UTC timestamp with seven fraction digits.');
  }
  return { text: value, ns: parseReplayTimestamp(value) };
}

export type ImportedAuditReplaySnapshot = Readonly<{
  schemaVersion: 1;
  tenantId: string | null;
  totalCount: number;
  startUtc: string;
  endUtc: string;
  capturedAtUtc: string;
  sha256: string;
  timeline: AuditReplayTimeline;
}>;

/** Reads the guarded API's bounded safe projection. The caller supplies its current
 * authorized tenant and expected digest; a digest alone does not prove authorization,
 * provenance, actor attribution, database completeness or synchronized source clocks.
 * Hash the original response text before parsing; do not hash a reserialized object. */
export async function importAuditReplaySnapshot(
  json: string, expectedSha256: string, authorizedTenantId: string | null,
): Promise<ImportedAuditReplaySnapshot> {
  if (typeof json !== 'string' || json.length > MAX_REPLAY_SNAPSHOT_BYTES) {
    throw new RangeError('Replay snapshot exceeds its 16 MiB input bound.');
  }
  if (typeof expectedSha256 !== 'string' || !/^[0-9a-f]{64}$/.test(expectedSha256)) {
    throw new TypeError('Expected snapshot digest must be 64 lowercase hexadecimal characters.');
  }
  const bytes = new TextEncoder().encode(json);
  if (bytes.byteLength > MAX_REPLAY_SNAPSHOT_BYTES) {
    throw new RangeError('Replay snapshot exceeds its 16 MiB UTF-8 bound.');
  }
  const digest = new Uint8Array(await globalThis.crypto.subtle.digest('SHA-256', bytes));
  const actualSha256 = Array.from(digest, byte => byte.toString(16).padStart(2, '0')).join('');
  if (actualSha256 !== expectedSha256) {
    throw new RangeError('Replay snapshot does not match the expected digest.');
  }

  const document = record(JSON.parse(json) as unknown, ROOT_FIELDS, 'Replay snapshot');
  if (document.schemaVersion !== 1 || !Array.isArray(document.events) ||
      !Number.isSafeInteger(document.totalCount) ||
      typeof document.totalCount !== 'number' || document.totalCount < 0 ||
      document.totalCount > MAX_REPLAY_EVENTS || document.events.length !== document.totalCount) {
    throw new TypeError('Replay snapshot version, count or events are invalid.');
  }
  const start = timestamp(document.startUtc, 'Replay start');
  const end = timestamp(document.endUtc, 'Replay end');
  const captured = timestamp(document.capturedAtUtc, 'Replay capture time');
  if (start.ns > end.ns || end.ns > captured.ns) {
    throw new RangeError('Replay range must be ordered and cannot exceed capture time.');
  }

  // The existing core validates GUIDs, explicit null tenant scope, safe field types,
  // duplicate IDs, stable ordering and complete declared counts before any filtering.
  const authorizedTenant = new AuditReplayTimeline([], { tenantId: authorizedTenantId, totalCount: 0 }).tenantId;
  const events = document.events.map((input: unknown) => {
    const event = record(input, EVENT_FIELDS, 'Replay event');
    const at = timestamp(event.createdAt, 'Replay event time');
    if (at.ns < start.ns || at.ns > end.ns) {
      throw new RangeError('Replay event is outside the captured range.');
    }
    return event as unknown as ReplayEventInput;
  });
  const timeline = new AuditReplayTimeline(events, {
    tenantId: document.tenantId as string | null,
    totalCount: document.totalCount,
  });
  if (timeline.tenantId !== authorizedTenant) {
    throw new RangeError('Replay snapshot does not match the current authorized tenant.');
  }
  return Object.freeze({
    schemaVersion: 1, tenantId: timeline.tenantId, totalCount: timeline.sourceCount,
    startUtc: start.text, endUtc: end.text, capturedAtUtc: captured.text,
    sha256: actualSha256, timeline,
  });
}
