import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

import { resolveBashExecutable } from '../../../apps/web/scripts/run-testing-lab-browser-e2e.mjs';

const helper = fileURLToPath(new URL('../pull-ci-image.sh', import.meta.url)).replaceAll('\\', '/');
const bashHelper = /^[A-Za-z]:\//.test(helper) ? '/' + helper[0].toLowerCase() + helper.slice(2) : helper;
const script = `
set -euo pipefail
source "$1"
failures="$2"
image="$3"
cached="$4"
stalled="$5"
declare -a calls=() delays=() limits=() kills=() inspections=()
docker() {
  if [[ "$#" == 3 && "$1" == image && "$2" == inspect ]]; then
    inspections+=("$3")
    (( cached )) && return 0
    return 1
  fi
  [[ "$#" == 2 && "$1" == pull ]] || return 97
  calls+=("$2")
  (( stalled )) && return 124
  if (( \${#calls[@]} <= failures )); then return 1; fi
  return 0
}
timeout() {
  [[ "$1" =~ ^--kill-after=([0-9]+)s$ ]] || return 97
  kills+=("\${BASH_REMATCH[1]}")
  [[ "$2" =~ ^([0-9]+)s$ ]] || return 97
  limits+=("\${BASH_REMATCH[1]}")
  shift 2
  "$@"
}
sleep() { delays+=("$1"); }
status=0
pull_ci_image "$image" || status=$?
for value in "\${limits[@]}"; do printf 'LIMIT=%s\\n' "$value"; done
for value in "\${kills[@]}"; do printf 'KILL=%s\\n' "$value"; done
for value in "\${inspections[@]}"; do printf 'INSPECT=%s\\n' "$value"; done
printf 'STATUS=%s\\n' "$status"
for value in "\${calls[@]}"; do printf 'IMAGE=%s\\n' "$value"; done
for value in "\${delays[@]}"; do printf 'DELAY=%s\\n' "$value"; done
`;

function mockedPull(failures, image, cached = false, stalled = false) {
  // Docker, sleep and timeout are shell functions; no daemon, signal, download or real delay occurs.
  const result = spawnSync(resolveBashExecutable(), ['-c', script, 'mock-pull', bashHelper, String(failures), image, cached ? '1' : '0', stalled ? '1' : '0'], {
    encoding: 'utf8', timeout: 5000, windowsHide: true,
  });
  assert.equal(result.error, undefined);
  assert.equal(result.status, 0, result.stderr);
  return {
    status: Number(/^STATUS=(\d+)$/m.exec(result.stdout)?.[1]),
    images: [...result.stdout.matchAll(/^IMAGE=(.*)$/gm)].map(match => match[1]),
    delays: [...result.stdout.matchAll(/^DELAY=(\d+)$/gm)].map(match => Number(match[1])),
    limits: [...result.stdout.matchAll(/^LIMIT=(\d+)$/gm)].map(match => Number(match[1])),
    kills: [...result.stdout.matchAll(/^KILL=(\d+)$/gm)].map(match => Number(match[1])),
    inspections: [...result.stdout.matchAll(/^INSPECT=(.*)$/gm)].map(match => match[1]),
    stderr: result.stderr,
  };
}

const image = 'mirror.gcr.io/library/postgres:17-alpine@sha256:b0f9560a2de083e2cc7382e75f808c7381a32852a7ec49117deedb300e552b24';

test('first pull success performs exactly one request and no backoff', () => {
  assert.deepEqual(mockedPull(0, image), {
    status: 0, images: [image], delays: [], limits: [3, 25], kills: [1, 5], inspections: [image], stderr: '',
  });
});

test('temporary registry failure retries the same digest and then succeeds', () => {
  const result = mockedPull(2, image);
  assert.equal(result.status, 0);
  assert.deepEqual(result.images, [image, image, image]);
  assert.deepEqual(result.delays, [5, 10]);
});

test('exhausted retries remain a failure with a bounded attempt count', () => {
  const result = mockedPull(99, image);
  assert.equal(result.status, 1);
  assert.deepEqual(result.images, Array(3).fill(image));
  assert.deepEqual(
    result.delays,
    [5, 10],
  );
  assert.match(result.stderr, /still failing after 3 attempts/);
});

test('configured image is a single literal argument, not shell code', () => {
  const literal = 'repository:tag $(exit 90); exit 91';
  const result = mockedPull(0, literal);
  assert.equal(result.status, 0);
  assert.deepEqual(result.images, [literal]);
});

test('registry retry backoff leaves the job budget available for required tests', () => {
  const result = mockedPull(99, image);
  assert.equal(result.status, 1);
  assert.ok(result.delays.reduce((sum, delay) => sum + delay, 0) <= 15);
  const budget = [...result.limits, ...result.kills, ...result.delays].reduce((sum, duration) => sum + duration, 0);
  assert.ok(budget < 120, `registry acquisition can take ${budget}s`);
});

test('an exact locally available digest needs no network request', () => {
  const result = mockedPull(0, image, true);
  assert.equal(result.status, 0);
  assert.deepEqual(result.images, []);
  assert.deepEqual(result.inspections, [image]);
  assert.deepEqual(result.limits, [3]);
  assert.deepEqual(result.delays, []);
});

test('a mutable tag is refreshed even when a local image exists', () => {
  const tag = 'public.ecr.aws/docker/library/postgres:17-alpine';
  const result = mockedPull(0, tag, true);
  assert.equal(result.status, 0);
  assert.deepEqual(result.inspections, []);
  assert.deepEqual(result.images, [tag]);
});

test('stalled pulls are bounded and remain a failure', () => {
  const result = mockedPull(0, image, false, true);
  assert.equal(result.status, 1);
  assert.deepEqual(result.images, Array(3).fill(image));
  assert.deepEqual(result.limits, [3, 25, 25, 25]);
  assert.deepEqual(result.kills, [1, 5, 5, 5]);
  assert.deepEqual(result.delays, [5, 10]);
  assert.match(result.stderr, /last exit 124/);
});
