import type { ApiError, ApiResponse, RequestConfig, Result } from '@game-guild/client';
import { parseReplayTimestamp, type AuditReplayTimeline } from './replay-timeline';
import { importAuditReplaySnapshot, type ImportedAuditReplaySnapshot } from './replay-snapshot-import';

export interface AuditReplayCaptureClient {
  requestRaw<T>(config: RequestConfig): Promise<Result<ApiResponse<T>, ApiError>>;
}
export type AuditReplayAccessScope = Readonly<{ subjectId: string; tenantId: string | null }>;
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const ROOT_FIELDS = ['sha256', 'tenantId', 'totalCount', 'failedCount', 'startUtc', 'endUtc', 'capturedAtUtc', 'events', 'users', 'sessions'] as const;
const USER_FIELDS = ['userId', 'eventCount', 'failedCount', 'firstSeenAtUtc', 'lastSeenAtUtc', 'sessionIds'] as const;
const SESSION_FIELDS = ['sessionId', 'eventCount', 'failedCount', 'firstAtUtc', 'lastAtUtc', 'userIds'] as const;

export type AuditReplayUserJourney = Readonly<{
  userId: string
  eventCount: number
  failedCount: number
  firstSeenAtUtc: string
  lastSeenAtUtc: string
  sessionIds: readonly string[]
}>;
export type AuditReplaySessionSummary = Readonly<{
  sessionId: string
  eventCount: number
  failedCount: number
  firstAtUtc: string
  lastAtUtc: string
  userIds: readonly string[]
}>;
export type ReconstructedAuditReplay = Readonly<{
  sha256: string
  tenantId: string | null
  totalCount: number
  failedCount: number
  startUtc: string
  endUtc: string
  capturedAtUtc: string
  timeline: AuditReplayTimeline
  users: readonly AuditReplayUserJourney[]
  sessions: readonly AuditReplaySessionSummary[]
}>;

function record(value: unknown, fields: readonly string[], label: string): Record<string, unknown> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    throw new TypeError(label + ' must be an object.');
  }
  const result = value as Record<string, unknown>;
  const names = Object.keys(result);
  if (names.length !== fields.length || names.some(name => !fields.includes(name))) {
    throw new TypeError(label + ' must contain exactly the safe reconstruction fields.');
  }
  return result;
}

function count(value: unknown, label: string, maximum: number): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < 0 || value > maximum) {
    throw new TypeError(label + ' is outside its bounds.');
  }
  return value;
}

function guidList(value: unknown, label: string, maximum: number): readonly string[] {
  if (!Array.isArray(value) || value.length > maximum ||
      value.some(item => typeof item !== 'string' || !GUID.test(item))) {
    throw new TypeError(label + ' must be a bounded list of identifiers.');
  }
  return Object.freeze(value.map(item => (item as string).toLowerCase()));
}

/** Sends one client-held snapshot for server-side timeline reconstruction (issue #169).
 * The bytes are re-validated locally (digest, shape, tenant) before any request, the
 * request context is re-checked after every await, and the reconstruction is only
 * accepted when the server's integrity receipt matches the digest of the sent bytes.
 * Journeys and session summaries are investigation aids, not attribution evidence. */
export async function reconstructAuditReplaySnapshot(
  client: AuditReplayCaptureClient,
  json: string,
  expectedSha256: string,
  authorizedTenantId: string | null,
  signal?: AbortSignal,
): Promise<ReconstructedAuditReplay> {
  signal?.throwIfAborted();
  const imported: ImportedAuditReplaySnapshot =
    await importAuditReplaySnapshot(json, expectedSha256, authorizedTenantId);
  signal?.throwIfAborted();

  const document: unknown = JSON.parse(json);
  const request = Object.freeze({ sha256: imported.sha256, document });
  const response = await client.requestRaw<unknown>({
    method: 'POST',
    path: '/v1/admin/audit-logs/replay/snapshots:reconstruct',
    body: request,
    headers: { Accept: 'application/json' },
    responseType: 'json',
    requiresAuth: true,
    cache: 'no-store',
    ...(signal === undefined ? {} : { signal }),
  });
  signal?.throwIfAborted();
  if (!response.ok) {
    throw new Error('Audit replay reconstruction failed.', { cause: response.error });
  }
  const { data, headers, status } = response.data;
  if (status !== 200 ||
      headers.get('Content-Type')?.split(';')[0]?.trim().toLowerCase() !== 'application/json') {
    throw new TypeError('Audit replay reconstruction returned an invalid response.');
  }
  const receipt = headers.get('X-Audit-Snapshot-SHA256');
  if (receipt === null || receipt.toLowerCase() !== imported.sha256) {
    throw new TypeError('Audit replay reconstruction did not confirm the snapshot digest.');
  }
  return validateReconstruction(data, imported);
}

function validateReconstruction(
  value: unknown,
  imported: ImportedAuditReplaySnapshot,
): ReconstructedAuditReplay {
  const body = record(value, ROOT_FIELDS, 'Replay reconstruction');
  if (typeof body.sha256 !== 'string' || body.sha256.toLowerCase() !== imported.sha256) {
    throw new RangeError('Replay reconstruction does not match the requested snapshot digest.');
  }
  const tenantId = body.tenantId === null
    ? null
    : typeof body.tenantId === 'string' && GUID.test(body.tenantId)
      ? body.tenantId.toLowerCase()
      : undefined;
  if (tenantId === undefined || tenantId !== imported.tenantId) {
    throw new RangeError('Replay reconstruction does not match the imported tenant scope.');
  }
  const totalCount = count(body.totalCount, 'Replay reconstruction total', 10_000);
  if (totalCount !== imported.totalCount) {
    throw new RangeError('Replay reconstruction disagrees with the imported event count.');
  }
  const failedCount = count(body.failedCount, 'Replay reconstruction failed', 10_000);
  if (failedCount > totalCount) {
    throw new RangeError('Replay reconstruction cannot fail more events than it holds.');
  }
  const start = parseReplayTimestamp(text(body.startUtc, 'Replay start'));
  const end = parseReplayTimestamp(text(body.endUtc, 'Replay end'));
  const capturedAt = parseReplayTimestamp(text(body.capturedAtUtc, 'Replay capture time'));
  if (start !== parseReplayTimestamp(imported.startUtc) ||
      end !== parseReplayTimestamp(imported.endUtc) ||
      capturedAt !== parseReplayTimestamp(imported.capturedAtUtc)) {
    throw new RangeError('Replay reconstruction changed the captured range.');
  }
  if (!Array.isArray(body.events) || body.events.length !== totalCount ||
      !Array.isArray(body.users) || !Array.isArray(body.sessions)) {
    throw new TypeError('Replay reconstruction collections are invalid.');
  }
  const users = body.users.map((input: unknown): AuditReplayUserJourney => {
    const user = record(input, USER_FIELDS, 'Replay user journey');
    return Object.freeze({
      userId: identifier(user.userId, 'Replay user journey'),
      eventCount: count(user.eventCount, 'Replay user journey events', 10_000),
      failedCount: count(user.failedCount, 'Replay user journey failures', 10_000),
      firstSeenAtUtc: within(text(user.firstSeenAtUtc, 'Replay user journey start'), start, end),
      lastSeenAtUtc: within(text(user.lastSeenAtUtc, 'Replay user journey end'), start, end),
      sessionIds: guidList(user.sessionIds, 'Replay user journey sessions', 10_000),
    });
  });
  const sessions = body.sessions.map((input: unknown): AuditReplaySessionSummary => {
    const session = record(input, SESSION_FIELDS, 'Replay session summary');
    return Object.freeze({
      sessionId: identifier(session.sessionId, 'Replay session summary'),
      eventCount: count(session.eventCount, 'Replay session summary events', 10_000),
      failedCount: count(session.failedCount, 'Replay session summary failures', 10_000),
      firstAtUtc: within(text(session.firstAtUtc, 'Replay session summary start'), start, end),
      lastAtUtc: within(text(session.lastAtUtc, 'Replay session summary end'), start, end),
      userIds: guidList(session.userIds, 'Replay session summary users', 10_000),
    });
  });
  return Object.freeze({
    sha256: imported.sha256, tenantId, totalCount, failedCount,
    startUtc: imported.startUtc, endUtc: imported.endUtc, capturedAtUtc: imported.capturedAtUtc,
    timeline: imported.timeline, users: Object.freeze(users), sessions: Object.freeze(sessions),
  });
}

function text(value: unknown, label: string): string {
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$/.test(value)) {
    throw new TypeError(label + ' must be a UTC timestamp with seven fraction digits.');
  }
  return value;
}

function within(value: string, start: bigint, end: bigint): string {
  const ns = parseReplayTimestamp(value);
  if (ns < start || ns > end) {
    throw new RangeError('Replay reconstruction summary is outside the captured range.');
  }
  return value;
}

function identifier(value: unknown, label: string): string {
  if (typeof value !== 'string' || !GUID.test(value) ||
      value.toLowerCase() === '00000000-0000-0000-0000-000000000000') {
    throw new TypeError(label + ' must be a non-empty GUID.');
  }
  return value.toLowerCase();
}
