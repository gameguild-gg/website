/**
 * Shared Playwright fixtures for the emception e2e suite.
 *
 * Every spec imports test/expect from here so captured browser console errors
 * and page errors are ASSERTED at test end — a broken boot/compile fails the
 * test instead of staying green while the toolchain throws.
 *
 * Intentional errors (e.g. commands expected to exit non-zero) belong in
 * ALLOWED_ERROR_PATTERNS below — keep this list short and justified.
 *
 * Reconciled with main's tool-runner change (7fbb4015): WASM printErr no
 * longer reaches console.error (it flows to onStderr only), so it is not
 * allowlisted. [ninja-bypass] FAILED still console.errors during a
 * deliberately triggered failure and remains allowlisted.
 */

import { test as base, expect } from '@playwright/test';

/** Substrings of console.error/pageerror text that are expected, not bugs. */
const ALLOWED_ERROR_PATTERNS: string[] = [
  // The missing-build.ninja failure ninja-subprocess.spec.ts deliberately triggers.
  '[ninja-bypass]',
];

/** Error text that must fail the test if it appears in the browser console. */
function isUnexpected(text: string): boolean {
  return !ALLOWED_ERROR_PATTERNS.some((pattern) => text.includes(pattern));
}

export const test = base.extend<{ consoleErrors: string[] }>({
  consoleErrors: [
    async ({ page }, use) => {
      const errors: string[] = [];

      page.on('console', (msg) => {
        if (msg.type() === 'error') {
          const text = msg.text();
          if (isUnexpected(text)) errors.push(`[console.error] ${text}`);
        }
      });
      page.on('pageerror', (err) => {
        if (isUnexpected(err.message)) errors.push(`[pageerror] ${err.message}`);
      });

      await use(errors);

      expect(
        errors,
        `Unexpected browser errors during test:\n${errors.join('\n')}`,
      ).toEqual([]);
    },
    { auto: true },
  ],
});

export { expect };
