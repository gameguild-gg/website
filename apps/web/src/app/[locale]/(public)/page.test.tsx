import '@testing-library/jest-dom/vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  auth: vi.fn(),
  redirect: vi.fn((args: unknown) => {
    throw new Error(`redirect:${JSON.stringify(args)}`);
  }),
}));

vi.mock('@/auth', () => ({ auth: mocks.auth, getToken: vi.fn().mockResolvedValue(null) }));
vi.mock('@/i18n/navigation', () => ({
  Link: ({ href, children }: { href: string; children: React.ReactNode }) => (
    <a href={href}>{children}</a>
  ),
  redirect: mocks.redirect,
}));
vi.mock('@/lib/community/public-community-queries', () => ({
  getPublicActivities: vi.fn().mockResolvedValue([]),
  getPublicMemberSpotlights: vi.fn().mockResolvedValue([]),
  getPublicPlaytests: vi.fn().mockResolvedValue([]),
}));
vi.mock('@/lib/projects/public-projects', () => ({
  getPublishedProjects: vi.fn().mockResolvedValue([]),
}));

import RootPage from './page';

const props = { params: Promise.resolve({ locale: 'en-US' }) } as never;

describe('contextual root page', () => {
  beforeEach(() => vi.clearAllMocks());
  afterEach(cleanup);

  it('sends signed-in members to the social shell', async () => {
    mocks.auth.mockResolvedValue({ user: { id: 'user-1' } });

    await expect(RootPage(props)).rejects.toThrow(
      'redirect:{"href":"/social","locale":"en-US"}',
    );
  });

  it('preserves a supported social tab during the redirect', async () => {
    mocks.auth.mockResolvedValue({ user: { id: 'user-1' } });

    await expect(
      RootPage({
        params: Promise.resolve({ locale: 'en-US' }),
        searchParams: Promise.resolve({ tab: 'following' }),
      } as never),
    ).rejects.toThrow(
      'redirect:{"href":"/social?tab=following","locale":"en-US"}',
    );
  });

  it('renders the marketing landing for anonymous visitors', async () => {
    mocks.auth.mockResolvedValue(null);

    render(await RootPage(props));

    expect(screen.getByRole('heading', { name: 'Learn, Build & Connect' })).toBeInTheDocument();
  });
});
