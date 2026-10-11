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
declare -a calls=() delays=()
docker() {
  [[ "$#" == 2 && "$1" == pull ]] || return 97
  calls+=("$2")
  if (( \${#calls[@]} <= failures )); then return 1; fi
  return 0
}
sleep() { delays+=("$1"); }
status=0
pull_ci_image "$image" || status=$?
printf 'STATUS=%s\\n' "$status"
for value in "\${calls[@]}"; do printf 'IMAGE=%s\\n' "$value"; done
for value in "\${delays[@]}"; do printf 'DELAY=%s\\n' "$value"; done
`;

function mockedPull(failures, image) {
  // Both docker and sleep are shell functions. No daemon, socket, download or real delay occurs.
  const result = spawnSync(resolveBashExecutable(), ['-c', script, 'mock-pull', bashHelper, String(failures), image], {
    encoding: 'utf8', timeout: 5000, windowsHide: true,
  });
  assert.equal(result.error, undefined);
  assert.equal(result.status, 0, result.stderr);
  return {
    status: Number(/^STATUS=(\d+)$/m.exec(result.stdout)?.[1]),
    images: [...result.stdout.matchAll(/^IMAGE=(.*)$/gm)].map(match => match[1]),
    delays: [...result.stdout.matchAll(/^DELAY=(\d+)$/gm)].map(match => Number(match[1])),
    stderr: result.stderr,
  };
}

const image = 'public.ecr.aws/docker/library/postgres:17-alpine@sha256:b0f9560a2de083e2cc7382e75f808c7381a32852a7ec49117deedb300e552b24';

test('first pull success performs exactly one request and no backoff', () => {
  assert.deepEqual(mockedPull(0, image), { status: 0, images: [image], delays: [], stderr: '' });
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
  assert.deepEqual(result.images, Array(6).fill(image));
  assert.deepEqual(result.delays, [5, 10, 20, 40, 80, 160]);
  assert.match(result.stderr, /still failing after 6 attempts/);
});

test('configured image is a single literal argument, not shell code', () => {
  const literal = 'repository:tag $(exit 90); exit 91';
  const result = mockedPull(0, literal);
  assert.equal(result.status, 0);
  assert.deepEqual(result.images, [literal]);
});
