// @vitest-environment node
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';
import { expect, it } from 'vitest';

it('verifies all reconstruction adapter source cases with the bounded Node runner', () => {
  const directory = dirname(fileURLToPath(import.meta.url));
  const script = resolve(directory, '../../../scripts/audit-replay-reconstruct-client.test.mjs');
  const output = execFileSync(process.execPath, ['--max-old-space-size=128', '--test', '--test-reporter=tap', script], {
    encoding: 'utf8',
    timeout: 10_000,
    maxBuffer: 1024 * 1024,
    windowsHide: true,
    env: { ...process.env, AUDIT_REPLAY_RECONSTRUCT_SOURCE: resolve(directory, 'replay-reconstruct-client.ts') },
  });
  expect(output).toMatch(/^# tests 25$/m);
  expect(output).toMatch(/^# pass 25$/m);
  expect(output).toMatch(/^# fail 0$/m);
  expect(output).toMatch(/^# skipped 0$/m);
});
