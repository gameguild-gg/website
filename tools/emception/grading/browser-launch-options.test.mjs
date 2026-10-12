import assert from 'node:assert/strict';
import { test } from 'node:test';
import { createCodeBrowserLaunchOptions } from './browser-launch-options.mjs';

test('bundled browser configuration retains the mandatory sandbox', () => {
  assert.deepEqual(createCodeBrowserLaunchOptions(undefined), {
    headless: true, chromiumSandbox: true,
    args: ['--js-flags=--max-old-space-size=512'],
  });
});

test('installed Chrome configuration retains the mandatory sandbox', () => {
  assert.deepEqual(createCodeBrowserLaunchOptions('chrome'), {
    headless: true, chromiumSandbox: true, channel: 'chrome',
    args: ['--js-flags=--max-old-space-size=512'],
  });
});

for (const channel of [null, '', 'chromium', 'chrome-beta', 'chrome --no-sandbox', '--no-sandbox', {}, ['chrome']]) {
  test(`rejects unsupported browser configuration ${JSON.stringify(channel)}`, () => {
    assert.throws(() => createCodeBrowserLaunchOptions(channel), /Unsupported trusted Code worker browser channel/);
  });
}
