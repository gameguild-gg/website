/** @file Verify stable worker failure labels and exclusion of raw diagnostic details. */

import assert from 'node:assert/strict';
import { test } from 'node:test';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { fileURLToPath } from 'node:url';
import { createWorkerFailureDiagnostic } from './worker-diagnostics.mjs';

const executeFile = promisify(execFile);

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

test('the actual worker fails closed with a safe diagnostic for missing installation arguments', async () => {
  await assert.rejects(
    executeFile(process.execPath, [fileURLToPath(new URL('./worker.mjs', import.meta.url))],
      { encoding: 'utf8', timeout: 10_000 }),
    (error) => {
      assert.equal(error.code, 1);
      assert.equal(error.killed, false);
      assert.equal(error.signal, null);
      assert.equal(error.stdout, '');
      assert.deepEqual(JSON.parse(error.stderr), { schemaVersion: 1, phase: 'input', kind: 'invalid-request' });
      return true;
    },
  );
});
