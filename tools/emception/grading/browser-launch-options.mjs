/** Deployment configuration selects a supported browser; sandbox policy is fixed. */
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
