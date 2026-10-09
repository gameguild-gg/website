import assert from 'node:assert/strict';
import { test } from 'node:test';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { createWorkerFailureDiagnostic } from './worker-diagnostics.mjs';

for (const [phase, kind] of Object.entries({
  input: 'invalid-request', 'artifact verification': 'artifact-binding-failed',
  'runtime server': 'runtime-server-failed', 'browser startup': 'browser-startup-failed',
  'WebAssembly evaluation': 'execution-failed',
})) {
  test(`reports only a safe infrastructure diagnostic during ${phase}`, () => {
    const serialized = createWorkerFailureDiagnostic(phase, new Error('private-test-name /private/source.cpp secret-value'));
    assert.deepEqual(JSON.parse(serialized), { schemaVersion: 1, phase, kind });
    assert.ok(!serialized.includes('private') && !serialized.includes('secret-value'));
  });
}

test('distinguishes an unavailable Chromium sandbox without emitting the browser log', () => {
  assert.deepEqual(JSON.parse(createWorkerFailureDiagnostic('browser startup',
    new Error('No usable sandbox! /home/runner/private-build-location'))),
  { schemaVersion: 1, phase: 'browser startup', kind: 'browser-sandbox-unavailable' });
});

test('never interprets student output as a browser startup diagnostic', () => {
  assert.equal(JSON.parse(createWorkerFailureDiagnostic('WebAssembly evaluation',
    new Error('No usable sandbox!'))).kind, 'execution-failed');
});

test('redacts an unexpected phase instead of publishing it', () => {
  assert.deepEqual(JSON.parse(createWorkerFailureDiagnostic('secret-value', new Error('private-source'))),
    { schemaVersion: 1, phase: 'unavailable', kind: 'unknown' });
});

test('the actual worker fails closed with a safe diagnostic for missing installation arguments', () => {
  const result = spawnSync(process.execPath, [fileURLToPath(new URL('./worker.mjs', import.meta.url))],
    { encoding: 'utf8', timeout: 10_000 });
  assert.equal(result.status, 1);
  assert.equal(result.stdout, '');
  assert.deepEqual(JSON.parse(result.stderr), { schemaVersion: 1, phase: 'input', kind: 'invalid-request' });
});
