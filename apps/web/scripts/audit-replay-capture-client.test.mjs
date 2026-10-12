import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { stripTypeScriptTypes } from 'node:module';

const sourceUrl = process.env.AUDIT_REPLAY_CAPTURE_SOURCE
  ? new URL('file:///' + process.env.AUDIT_REPLAY_CAPTURE_SOURCE.replaceAll('\\', '/'))
  : new URL('../src/lib/audit/replay-capture-client.ts', import.meta.url);
const coreSource = await fs.readFile(new URL('./replay-timeline.ts', sourceUrl), 'utf8');
const importerSource = await fs.readFile(new URL('./replay-snapshot-import.ts', sourceUrl), 'utf8');
const clientSource = await fs.readFile(sourceUrl, 'utf8');
const moduleUrl = text => 'data:text/javascript;base64,' + Buffer.from(stripTypeScriptTypes(text, { mode: 'strip' })).toString('base64');
const core = moduleUrl(coreSource);
const importer = moduleUrl(importerSource.replace("from './replay-timeline'", "from '" + core + "'"));
const client = moduleUrl(clientSource.replace("from './replay-timeline'", "from '" + core + "'").replace("from './replay-snapshot-import'", "from '" + importer + "'"));
const { captureAuditReplaySnapshot: capture } = await import(client);
const id = n => '10000000-0000-0000-0000-' + String(n).padStart(12, '0');
const actor = { subjectId: id(91), tenantId: id(90) };
const range = { start: '2026-10-11T00:00:00.0000000Z', end: '2026-10-11T00:00:01.0000000Z' };
const document = (tenantId = actor.tenantId) => ({
  schemaVersion: 1, tenantId, totalCount: 0, startUtc: range.start, endUtc: range.end,
  capturedAtUtc: '2026-10-11T00:00:02.0000000Z', events: [],
});
const hash = value => createHash('sha256').update(value).digest('hex');
const response = (value = document()) => {
  const json = typeof value === 'string' ? value : JSON.stringify(value);
  return { ok: true, data: { data: new Blob([json], {type:'application/json'}), status:200,
    headers:new Headers({'Content-Type':'application/json; charset=utf-8','X-Audit-Snapshot-SHA256':hash(json)}) } };
};
const stub = value => ({ async requestRaw() { return value; } });

test('uses existing raw authenticated POST transport and preserves bytes and headers', async () => {
  let config; const abort = new AbortController();
  const value = await capture({async requestRaw(c){config=c;return response();}}, range, () => actor, abort.signal);
  assert.equal(value.totalCount, 0);
  assert.deepEqual(config.body, range); assert.equal(Object.isFrozen(config.body), true);
  assert.equal(config.path, '/v1/admin/audit-logs/replay/snapshots');
  assert.equal(config.method, 'POST'); assert.equal(config.responseType, 'blob');
  assert.equal(config.requiresAuth, true); assert.equal(config.cache, 'no-store');
  assert.equal(config.headers.Accept, 'application/json'); assert.equal(config.signal, abort.signal);
});
test('accepts global-only capture in explicit global context', async () => {
  const value = await capture(stub(response(document(null))), range, () => ({...actor,tenantId:null}));
  assert.equal(value.tenantId, null);
});
for (const [name, mutate] of [
  ['missing digest', r=>r.data.headers.delete('X-Audit-Snapshot-SHA256')],
  ['wrong digest', r=>r.data.headers.set('X-Audit-Snapshot-SHA256','a'.repeat(64))],
  ['wrong content type', r=>r.data.headers.set('Content-Type','text/html')],
  ['wrong status', r=>{r.data.status=201;}],
  ['non-blob body', r=>{r.data.data={};}],
  ['oversized blob', r=>{r.data.data=new Blob([' '.repeat(16*1024*1024+1)]);}],
  ['invalid UTF-8', r=>{r.data.data=new Blob([new Uint8Array([0xff])]);}],
]) {
  test('rejects '+name, async()=>{const r=response();mutate(r);await assert.rejects(()=>capture(stub(r),range,()=>actor));});
}
for (const [name, badRange, badScope] of [
  ['reversed range',{start:range.end,end:range.start},actor],
  ['implicit timezone',{start:'2026-10-11T00:00:00',end:range.end},actor],
  ['empty subject',range,{...actor,subjectId:'00000000-0000-0000-0000-000000000000'}],
  ['missing authentication',range,null],
  ['missing explicit tenant',range,{subjectId:actor.subjectId}],
]) {
  test('rejects '+name+' before requesting',async()=>{let calls=0;await assert.rejects(()=>capture({async requestRaw(){calls++;return response();}},badRange,()=>badScope));assert.equal(calls,0);});
}
test('aborts before requesting',async()=>{const abort=new AbortController();abort.abort();let calls=0;await assert.rejects(()=>capture({async requestRaw(){calls++;return response();}},range,()=>actor,abort.signal));assert.equal(calls,0);});
for (const [name, change] of [
  ['subject change',()=>({...actor,subjectId:id(99)})],
  ['tenant change',()=>({...actor,tenantId:id(99)})],
  ['logout',()=>null],
]) {
  test('discards pending response on '+name,async()=>{let current=actor;await assert.rejects(()=>capture({async requestRaw(){current=change();return response();}},range,()=>current));});
}
test('discards response on cancellation during request',async()=>{const abort=new AbortController();await assert.rejects(()=>capture({async requestRaw(){abort.abort();return response();}},range,()=>actor,abort.signal));});
test('discards context changed while reading response bytes',async()=>{let current=actor;class ChangingBlob extends Blob{async arrayBuffer(){current={...actor,tenantId:id(99)};return super.arrayBuffer();}}const r=response();r.data.data=new ChangingBlob([JSON.stringify(document())]);await assert.rejects(()=>capture(stub(r),range,()=>current));});
test('normalizes scope GUID casing across checks',async()=>{const lower={subjectId:'abcdef01-0000-0000-0000-000000000091',tenantId:'abcdef01-0000-0000-0000-000000000090'};let count=0;const result=await capture(stub(response(document(lower.tenantId))),range,()=>++count===1?lower:{subjectId:lower.subjectId.toUpperCase(),tenantId:lower.tenantId.toUpperCase()});assert.equal(result.tenantId,lower.tenantId);});
test('preserves structured SDK failure without exposing its message as the capture error',async()=>{const error={name:'ApiError',code:'FORBIDDEN',status:403,message:'PRIVATE_DIAGNOSTIC'};await assert.rejects(()=>capture(stub({ok:false,error}),range,()=>actor),e=>e.message==='Audit replay capture failed.'&&e.cause===error);});
test('does not silently strip a response BOM before integrity and JSON validation',async()=>{await assert.rejects(()=>capture(stub(response('\ufeff'+JSON.stringify(document()))),range,()=>actor));});
test('rejects an otherwise hashed response containing a private field',async()=>{const d=document();d.metadata='PRIVATE';await assert.rejects(()=>capture(stub(response(d)),range,()=>actor));});
test('freezes request bounds before awaiting the transport',async()=>{const mutable={...range};let config;const result=await capture({async requestRaw(c){config=c;mutable.end='2099-01-01T00:00:00Z';return response();}},mutable,()=>actor);assert.equal(config.body.end,range.end);assert.equal(result.endUtc,range.end);});
