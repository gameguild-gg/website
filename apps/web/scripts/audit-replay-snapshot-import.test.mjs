import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { stripTypeScriptTypes } from 'node:module';

const sourceUrl = process.env.AUDIT_REPLAY_IMPORT_SOURCE
  ? new URL('file:///' + process.env.AUDIT_REPLAY_IMPORT_SOURCE.replaceAll('\\', '/'))
  : new URL('../src/lib/audit/replay-snapshot-import.ts', import.meta.url);
const coreUrl = new URL('./replay-timeline.ts', sourceUrl);
const source = await fs.readFile(sourceUrl, 'utf8');
const core = await fs.readFile(coreUrl, 'utf8');
const coreModule = 'data:text/javascript;base64,' + Buffer.from(stripTypeScriptTypes(core, { mode: 'strip' })).toString('base64');
const compiled = stripTypeScriptTypes(source, { mode: 'strip' }).replace("from './replay-timeline'", "from '" + coreModule + "'");
const { importAuditReplaySnapshot: read, MAX_REPLAY_SNAPSHOT_BYTES: MAX_BYTES } =
  await import('data:text/javascript;base64,' + Buffer.from(compiled).toString('base64'));
const id = n => '10000000-0000-0000-0000-' + String(n).padStart(12, '0');
const tenant = id(90), start = '2026-10-11T00:00:00.0000000Z', end = '2026-10-11T00:00:01.0000000Z';
const event = (n = 1, scope = tenant) => ({
  id: id(n), tenantId: scope, createdAt: start, actionType: 'Read', resourceType: 'Document',
  resourceId: 'doc-1', userId: id(91), sessionId: id(92), correlationId: 'request-1', success: true,
});
const doc = (events = [event()], scope = tenant) => ({
  schemaVersion: 1, tenantId: scope, totalCount: events.length, startUtc: start, endUtc: end,
  capturedAtUtc: '2026-10-11T00:00:02.0000000Z', events,
});
const hash = value => createHash('sha256').update(value, 'utf8').digest('hex');
const importDoc = (value, scope = tenant) => { const json = JSON.stringify(value); return read(json, hash(json), scope); };

test('imports safe data with exact tick ordering and stable ID ties', async () => {
  const value = doc([event(3), event(2), event(1)]);
  value.events[0].createdAt = '2026-10-11T00:00:00.0000001Z';
  const result = await importDoc(value);
  assert.equal(result.totalCount, 3);
  assert.deepEqual(result.timeline.events.map(e => e.id), [id(1), id(2), id(3)]);
  assert.equal(result.timeline.events[2].timestampNs - result.timeline.events[0].timestampNs, 100n);
  assert.equal(Object.isFrozen(result), true);
  assert.equal(Object.isFrozen(result.timeline.events), true);
});
test('accepts an explicitly global complete empty selection', async () => {
  const result = await importDoc(doc([], null), null);
  assert.equal(result.tenantId, null); assert.equal(result.timeline.events.length, 0);
});
test('normalizes authorized tenant GUID casing through the existing core', async () => {
  const lower = 'abcdef01-0000-0000-0000-000000000090';
  const result = await importDoc(doc([event(1, lower)], lower), lower.toUpperCase());
  assert.equal(result.tenantId, lower);
});
for (const [label, value, scope] of [
  ['another tenant', doc(), id(89)],
  ['tenant selection in global context', doc(), null],
  ['global selection in tenant context', doc([], null), tenant],
  ['empty selection in another tenant', doc([]), id(89)],
  ['mixed tenant events', doc([event(1), event(2, id(89))]), tenant],
]) {
  test('rejects ' + label, async () => { await assert.rejects(() => importDoc(value, scope)); });
}
test('checks the original response bytes rather than reserialized JSON', async () => {
  const value = doc(); value.events[0].actionType = "Read café <item> café <item> 😀";
  const json = JSON.stringify(value).replaceAll('é', '\\u00E9').replaceAll('<', '\\u003C').replaceAll('>', '\\u003E');
  assert.doesNotMatch(json, /[é<>]/);
  const result = await read(json, hash(json), tenant);
  assert.equal(result.timeline.events[0].actionType, value.events[0].actionType);
  await assert.rejects(() => read(json, hash(JSON.stringify(value)), tenant), /digest/);
});
test('rejects content changed after hashing', async () => {
  const json = JSON.stringify(doc());
  await assert.rejects(() => read(json.replace('"Read"', '"Write"'), hash(json), tenant), /digest/);
});
for (const invalid of ['', 'a'.repeat(63), 'A'.repeat(64), 'g'.repeat(64), null]) {
  test('rejects invalid expected digest ' + String(invalid).slice(0, 12), async () => {
    await assert.rejects(() => read(JSON.stringify(doc()), invalid, tenant), /digest/);
  });
}
for (const [label, change] of [
  ['schema version', v => { v.schemaVersion = 2; }],
  ['string schema version', v => { v.schemaVersion = '1'; }],
  ['negative count', v => { v.totalCount = -1; }],
  ['fractional count', v => { v.totalCount = 1.5; }],
  ['string count', v => { v.totalCount = '1'; }],
  ['incomplete count', v => { v.totalCount = 2; }],
  ['event overflow', v => { v.events = Array.from({length:10001}, (_, i) => event(i + 1)); v.totalCount = v.events.length; }],
  ['non-array events', v => { v.events = {}; }],
  ['extra root content', v => { v.metadata = 'PRIVATE'; }],
  ['missing nullable event field', v => { delete v.events[0].sessionId; }],
  ['wrong success type', v => { v.events[0].success = 'true'; }],
  ['empty event GUID', v => { v.events[0].id = '00000000-0000-0000-0000-000000000000'; }],
  ['duplicate event ID', v => { v.events.push(event()); v.totalCount++; }],
  ['reversed range', v => { v.startUtc = end; v.endUtc = start; }],
  ['end after capture', v => { v.capturedAtUtc = start; }],
  ['event before range', v => { v.events[0].createdAt = '2026-10-10T23:59:59.9999999Z'; }],
  ['event after range', v => { v.events[0].createdAt = '2026-10-11T00:00:01.0000001Z'; }],
  ['event without tick precision', v => { v.events[0].createdAt = '2026-10-11T00:00:00Z'; }],
  ['event without UTC', v => { v.events[0].createdAt = '2026-10-11T00:00:00.0000000+00:00'; }],
  ['invalid calendar date', v => { v.startUtc = '2026-02-30T00:00:00.0000000Z'; }],
]) {
  test('rejects ' + label, async () => { const v = doc(); change(v); await assert.rejects(() => importDoc(v)); });
}
for (const field of ['description', 'ipAddress', 'userAgent', 'errorMessage', 'metadata']) {
  test('rejects private event field ' + field, async () => {
    const v = doc(); v.events[0][field] = 'PRIVATE'; await assert.rejects(() => importDoc(v), /safe snapshot fields/);
  });
}
for (const root of [null, [], true]) {
  test('rejects non-object root ' + JSON.stringify(root), async () => { await assert.rejects(() => importDoc(root)); });
}
test('rejects malformed JSON even with a matching digest', async () => {
  const json = '{"schemaVersion":';
  await assert.rejects(() => read(json, hash(json), tenant), SyntaxError);
});
test('includes both range boundaries', async () => {
  const last = event(2); last.createdAt = end;
  const result = await importDoc(doc([last, event(1)]));
  assert.deepEqual(result.timeline.events.map(e => e.createdAt), [start, end]);
});
test('preserves null identifiers without attributing anonymous actors', async () => {
  const e = event(); e.userId = null; e.sessionId = null; e.correlationId = null; e.resourceId = null;
  const result = await importDoc(doc([e]));
  assert.equal(result.timeline.events[0].userId, null);
  assert.equal(result.timeline.events[0].sessionId, null);
});
test('accepts the complete maximum event selection', async () => {
  const result = await importDoc(doc(Array.from({length:10000}, (_, i) => event(i + 1))));
  assert.equal(result.timeline.sourceCount, 10000);
});
test('rejects oversized character input before encoding or parsing', async () => {
  const json = ' '.repeat(MAX_BYTES + 1);
  await assert.rejects(() => read(json, 'a'.repeat(64), tenant), /input bound/);
});
test('rejects excessive UTF-8 bytes even when character length fits', async () => {
  const json = '😀'.repeat(MAX_BYTES / 4 + 1);
  assert(json.length < MAX_BYTES);
  await assert.rejects(() => read(json, 'a'.repeat(64), tenant), /UTF-8 bound/);
});

for (const invalidScope of [undefined, '00000000-0000-0000-0000-000000000000']) {
  test('rejects invalid authorized scope ' + String(invalidScope), async () => {
    const json = JSON.stringify(doc());
    await assert.rejects(() => read(json, hash(json), invalidScope));
  });
}
test('rejects missing explicit tenant field', async () => {
  const value = doc(); delete value.tenantId; await assert.rejects(() => importDoc(value));
});
