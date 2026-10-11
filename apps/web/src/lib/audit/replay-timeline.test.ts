import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { expect, it } from 'vitest';

// The normal Web suite owns this check; the bounded child starts no app or browser.
it('verifies all replay source cases with the isolated Node runner', () => {
  const script = fileURLToPath(new URL('../../../scripts/audit-replay-timeline.test.mjs', import.meta.url));
  const output = execFileSync(process.execPath, ['--max-old-space-size=128', '--test', '--test-reporter=tap', script], {
    encoding: 'utf8',
    timeout: 10_000,
    maxBuffer: 1024 * 1024,
    windowsHide: true,
  });
  expect(output).toMatch(/^# tests 84$/m);
  expect(output).toMatch(/^# pass 84$/m);
  expect(output).toMatch(/^# fail 0$/m);
  expect(output).toMatch(/^# skipped 0$/m);
});
