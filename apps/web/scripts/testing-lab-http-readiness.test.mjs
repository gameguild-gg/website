import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import { performance } from 'node:perf_hooks';
import test from 'node:test';
import { resolveBashExecutable } from './run-testing-lab-browser-e2e.mjs';

const runner = await readFile(new URL('./testing-lab-browser-e2e.sh', import.meta.url), 'utf8');
const functionSource = runner.match(/wait_for_http\(\) \{[\s\S]*?\r?\n\}/)?.[0]?.replace(/\r\n/g, '\n');
assert.ok(functionSource, 'the readiness helper must be present');

function runReadiness({ budget = '2', probeBody = 'return 0', pid = '', alive = true } = {}) {
  // Only the extracted readiness helper runs. curl and kill are shell doubles;
  // no network, application, database, container or SDK is started.
  const script = [
    'set -eu',
    functionSource,
    'probe_calls=0',
    'probe_timeouts=()',
    `kill() { return ${alive ? 0 : 1}; }`,
    `curl() {
      local probe_timeout=''
      while (($# > 0)); do
        if [[ "$1" == '--max-time' ]]; then
          probe_timeout="$2"
          shift 2
        else
          shift
        fi
      done
      probe_calls=$((probe_calls + 1))
      probe_timeouts+=("$probe_timeout")
      ${probeBody}
    }`,
    'if wait_for_http "http://readiness.invalid/ready" "fixture" "/fixture/no-log" "$1" "$2"; then result=0; else result=$?; fi',
    'printf "PROBES:%s\\nTIMEOUTS:%s\\n" "$probe_calls" "${probe_timeouts[*]}"',
    'exit "$result"',
  ].join('\n');
  const env = { ...process.env };
  delete env.BASH_ENV;
  delete env.ENV;
  const started = performance.now();
  const result = spawnSync(resolveBashExecutable(), [
    '--noprofile', '--norc', '-c', script, 'readiness-fixture', budget, pid,
  ], { env, encoding: 'utf8', timeout: 6_000 });
  const elapsedMs = performance.now() - started;
  assert.ifError(result.error);
  const calls = Number(result.stdout.match(/^PROBES:(\d+)$/m)?.[1]);
  assert.ok(Number.isInteger(calls));
  const timeouts = (result.stdout.match(/^TIMEOUTS:(.*)$/m)?.[1] ?? '').trim()
    .split(/\s+/).filter(Boolean).map(Number);
  assert.equal(timeouts.length, calls);
  return { ...result, calls, timeouts, elapsedMs };
}

test('returns immediately when HTTP is ready and caps each probe at five seconds', () => {
  const result = runReadiness({ budget: '600' });
  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.calls, 1);
  assert.deepEqual(result.timeouts, [5]);
});

test('retains retries and succeeds when a later HTTP probe is ready', () => {
  const result = runReadiness({ budget: '4', probeBody: 'if ((probe_calls == 1)); then return 1; fi; return 0' });
  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.calls, 2);
  assert.ok(result.timeouts.every((seconds) => seconds > 0 && seconds <= 4));
});

test('bounds a slow failed probe by the remaining elapsed budget', () => {
  const result = runReadiness({ probeBody: 'command sleep "$probe_timeout"; return 1' });
  assert.equal(result.status, 1);
  assert.equal(result.calls, 1);
  assert.ok(result.timeouts[0] > 0 && result.timeouts[0] <= 2);
  assert.ok(result.elapsedMs < 4_000, `two-second budget consumed ${result.elapsedMs}ms`);
  assert.match(result.stderr, /within 2 seconds/);
});

test('charges failed probes and retry sleeps to the same elapsed budget', () => {
  const result = runReadiness({ probeBody: 'command sleep 1; return 1' });
  assert.equal(result.status, 1);
  assert.equal(result.calls, 1);
  assert.ok(result.elapsedMs < 4_000, `shared two-second budget consumed ${result.elapsedMs}ms`);
});

test('fails before probing when the owned process has exited', () => {
  const result = runReadiness({ pid: '812345', alive: false });
  assert.equal(result.status, 1);
  assert.equal(result.calls, 0);
  assert.match(result.stderr, /process 812345 exited before readiness/);
});

test('does not accept a success reported after the readiness deadline', () => {
  const result = runReadiness({ budget: '1', probeBody: 'command sleep 2; return 0' });
  assert.equal(result.status, 1);
  assert.equal(result.calls, 1);
  assert.match(result.stderr, /within 1 seconds/);
});

for (const budget of ['0', '-1', '1.5', 'invalid']) {
  test(`rejects invalid readiness budget ${budget} before probing`, () => {
    const result = runReadiness({ budget });
    assert.equal(result.status, 2);
    assert.equal(result.calls, 0);
    assert.match(result.stderr, /positive whole seconds/);
  });
}
