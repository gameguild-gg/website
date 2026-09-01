import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  redirect: vi.fn((args: unknown) => {
    throw new Error(`redirect:${JSON.stringify(args)}`);
  }),
}));

vi.mock('@/i18n/navigation', () => ({ redirect: mocks.redirect }));

import LegacyFeedRedirectPage from './page';

describe('legacy /feed redirect', () => {
  it('forwards to the authenticated social shell', async () => {
    await expect(
      LegacyFeedRedirectPage({ params: Promise.resolve({ locale: 'pt-BR' }) } as never),
    ).rejects.toThrow('redirect:{"href":"/social","locale":"pt-BR"}');
  });
});
