/** @file Resolve trusted Code worker browser options with a fixed sandbox policy. */

/**
 * Deployment configuration selects a supported browser; sandbox policy is fixed.
 * @param {string | undefined} channel - Use Chrome when set to "chrome", or the bundled browser when omitted.
 * @returns {{headless: boolean, chromiumSandbox: boolean, channel?: string, args: string[]}} Browser launch options with the sandbox enabled.
 * @throws {Error} When a supplied channel is not "chrome".
 * @example
 * const options = createCodeBrowserLaunchOptions('chrome');
 */
export const createCodeBrowserLaunchOptions = (channel) => {
  if (channel !== undefined && channel !== 'chrome') {
    throw new Error('Unsupported trusted Code worker browser channel.');
  }
  return {
    headless: true,
    chromiumSandbox: true,
    ...(channel === 'chrome' ? { channel: 'chrome' } : {}),
    args: ['--js-flags=--max-old-space-size=512'],
  };
};
