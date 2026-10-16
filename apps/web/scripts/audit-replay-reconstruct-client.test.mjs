import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { stripTypeScriptTypes } from 'node:module';

const sourceUrl = process.env.AUDIT_REPLAY_RECONSTRUCT_SOURCE
  ? new URL('file:///' + process.env.AUDIT_REPLAY_RECONSTRUCT_SOURCE.replaceAll('\\', '/'))
  : new URL('../src/lib/audit/replay-reconstruct-client.ts', import.meta.url);
const coreSource = await fs.readFile(new URL('./replay-timeline.ts', sourceUrl), 'utf8');
const importerSource = await fs.readFile(new URL('./replay-snapshot-import.ts', sourceUrl), 'utf8');
const clientSource = await fs.readFile(sourceUrl, 'utf8');
const moduleUrl = text => 'data:text/javascript;base64,' + Buffer.from(stripTypeScriptTypes(text, { mode: 'strip' })).toString('base64');
const core = moduleUrl(coreSource);
const importer = moduleUrl(importerSource.replace("from './replay-timeline'", "from '" + core + "'"));
const client = moduleUrl(clientSource
  .replace("from './replay-timeline'", "from '" + core + "'")
  .replace("from './replay-snapshot-import'", "from '" + importer + "'"));
const { reconstructAuditReplaySnapshot: reconstruct } = await import(client);
const id = n => '10000000-0000-0000-0000-' + String(n).padStart(12, '0');
const tenant = id(90);
const range = { start: '2026-10-11T00:00:00.0000000Z', end: '2026-10-11T00:00:01.0000000Z' };
const event = (n, at) => ({
  id: id(n), tenantId: tenant, createdAt: at, actionType: 'read', resourceType: 'audit',
  resourceId: id(n + 400), userId: id(91), sessionId: id(92), correlationId: null, success: true,
});
const events = [event(1, range.start), event(2, range.end)];
const document = (tenantId = tenant) => ({
  schemaVersion: 1, tenantId, totalCount: events.length, startUtc: range.start, endUtc: range.end,
  capturedAtUtc: '2026-10-11T00:00:02.0000000Z',
  events: events.map(e => ({ ...e, tenantId })),
});
const hash = value => createHash('sha256').update(value).digest('hex');
const journeys = () => [{
  userId: id(91), eventCount: 2, failedCount: 0,
  firstSeenAtUtc: range.start, lastSeenAtUtc: range.end, sessionIds: [id(92)],
}];
const sessionSummaries = () => [{
  sessionId: id(92), eventCount: 2, failedCount: 0,
  firstAtUtc: range.start, lastAtUtc: range.end, userIds: [id(91)],
}];
const reconstruction = (tenantId = tenant) => ({
  sha256: '', tenantId, totalCount: events.length, failedCount: 0,
  startUtc: range.start, endUtc: range.end, capturedAtUtc: '2026-10-11T00:00:02.0000000Z',
  events, users: journeys(), sessions: sessionSummaries(),
});
const json = JSON.stringify(document());
const digest = hash(json);
const response = value => {
  const body = typeof value === 'string' ? value : JSON.stringify({ ...value, sha256: digest });
  return { ok: true, data: { data: JSON.parse(body), status: 200,
    headers: new Headers({ 'Content-Type': 'application/json; charset=utf-8', 'X-Audit-Snapshot-SHA256': digest }) } };
};
const stub = value => ({ async requestRaw() { return value; } });

test('uses authenticated POST transport with a frozen document and validates the integrity receipt', async () => {
  let config; const abort = new AbortController();
  const value = await reconstruct({ async requestRaw(c) { config = c; return response(reconstruction()); } },
    json, digest, tenant, abort.signal);
  assert.equal(value.totalCount, 2);
  assert.equal(value.sha256, digest);
  assert.equal(value.tenantId, tenant);
  assert.equal(value.timeline.sourceCount, 2);
  assert.deepEqual(value.users[0].sessionIds, [id(92)]);
  assert.deepEqual(value.sessions[0].userIds, [id(91)]);
  assert.equal(Object.isFrozen(config.body), true);
  assert.equal(config.body.sha256, digest);
  assert.deepEqual(config.body.document, document());
  assert.equal(config.path, '/v1/admin/audit-logs/replay/snapshots:reconstruct');
  assert.equal(config.method, 'POST'); assert.equal(config.responseType, undefined);
  assert.equal(config.requiresAuth, true); assert.equal(config.cache, 'no-store');
  assert.equal(config.headers.Accept, 'application/json'); assert.equal(config.signal, abort.signal);
});
test('accepts global-only reconstruction in an explicit global context', async () => {
  const globalJson = JSON.stringify(document(null));
  const globalDigest = hash(globalJson);
  const body = { ...reconstruction(null), sha256: globalDigest };
  const value = await reconstruct(stub({ ok: true, data: { data: body, status: 200,
    headers: new Headers({ 'Content-Type': 'application/json', 'X-Audit-Snapshot-SHA256': globalDigest }) } }),
  globalJson, globalDigest, null);
  assert.equal(value.tenantId, null);
});
test('normalizes an uppercase receipt before comparing the digest', async () => {
  const r = response(reconstruction());
  r.data.headers.set('X-Audit-Snapshot-SHA256', digest.toUpperCase());
  const value = await reconstruct(stub(r), json, digest, tenant);
  assert.equal(value.totalCount, 2);
});
for (const [name, mutate] of [
  ['missing receipt', r => r.data.headers.delete('X-Audit-Snapshot-SHA256')],
  ['wrong receipt', r => r.data.headers.set('X-Audit-Snapshot-SHA256', 'b'.repeat(64))],
  ['wrong content type', r => r.data.headers.set('Content-Type', 'text/html')],
  ['wrong status', r => { r.data.status = 201; }],
  ['body digest mismatch', r => { r.data.data.sha256 = 'c'.repeat(64); }],
  ['wrong tenant', r => { r.data.data.tenantId = id(99); }],
  ['count disagreement', r => { r.data.data.totalCount = 1; }],
  ['failed above total', r => { r.data.data.failedCount = 3; }],
  ['changed range', r => { r.data.data.startUtc = '2026-10-10T00:00:00.0000000Z'; }],
  ['private field in body', r => { r.data.data.metadata = 'PRIVATE'; }],
  ['invalid users entry', r => { r.data.data.users = [{ userId: id(91) }]; }],
  ['non-guid session id', r => { r.data.data.sessions[0].sessionId = 'not-a-guid'; }],
  ['summary outside range', r => { r.data.data.users[0].firstSeenAtUtc = '2026-10-12T00:00:00.0000000Z'; }],
  ['non-array users', r => { r.data.data.users = {}; }],
]) {
  test('rejects ' + name, async () => {
    const r = response(reconstruction()); mutate(r);
    await assert.rejects(() => reconstruct(stub(r), json, digest, tenant));
  });
}
for (const [name, badJson, badDigest, badTenant] of [
  ['malformed digest', json, 'zz', tenant],
  ['oversized snapshot', ' '.repeat(16 * 1024 * 1024 + 1), digest, tenant],
  ['tenant mismatch', json, digest, id(89)],
  ['invalid json', '{', digest, tenant],
]) {
  test('rejects ' + name + ' before requesting', async () => {
    let calls = 0;
    await assert.rejects(() => reconstruct({ async requestRaw() { calls++; return response(reconstruction()); } },
      badJson, badDigest, badTenant));
    assert.equal(calls, 0);
  });
}
test('aborts before requesting', async () => {
  const abort = new AbortController(); abort.abort(); let calls = 0;
  await assert.rejects(() => reconstruct({ async requestRaw() { calls++; return response(reconstruction()); } },
    json, digest, tenant, abort.signal));
  assert.equal(calls, 0);
});
test('discards response on cancellation during request', async () => {
  const abort = new AbortController();
  await assert.rejects(() => reconstruct({ async requestRaw() { abort.abort(); return response(reconstruction()); } },
    json, digest, tenant, abort.signal));
});
test('preserves structured SDK failure without exposing its message as the reconstruction error', async () => {
  const error = { name: 'ApiError', code: 'FORBIDDEN', status: 403, message: 'PRIVATE_DIAGNOSTIC' };
  await assert.rejects(() => reconstruct(stub({ ok: false, error }), json, digest, tenant),
    e => e.message === 'Audit replay reconstruction failed.' && e.cause === error);
});
test('returns frozen journeys and session summaries', async () => {
  const value = await reconstruct(stub(response(reconstruction())), json, digest, tenant);
  assert.equal(Object.isFrozen(value), true);
  assert.equal(Object.isFrozen(value.users), true);
  assert.equal(Object.isFrozen(value.users[0]), true);
  assert.equal(Object.isFrozen(value.sessions[0]), true);
});
