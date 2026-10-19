import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  auth: vi.fn(),
  headers: vi.fn(),
  redirect: vi.fn(),
  getDashboardNotificationSummary: vi.fn(),
  getDashboardContexts: vi.fn(),
  getMfaStatus: vi.fn(),
}));

vi.mock('@/auth', () => ({ auth: mocks.auth }));
vi.mock('next/headers', () => ({ headers: mocks.headers }));
vi.mock('@/i18n/navigation', () => ({ redirect: mocks.redirect }));
vi.mock('@/lib/dashboard-notifications', () => ({
  getDashboardNotificationSummary: mocks.getDashboardNotificationSummary,
}));
vi.mock('@/lib/dashboard-contexts', () => ({
  getDashboardContexts: mocks.getDashboardContexts,
}));
vi.mock('@/lib/user-settings/mfa-actions', () => ({
  getMfaStatus: mocks.getMfaStatus,
}));
vi.mock('@/components/console/workspace-shell', () => ({
  WorkspaceShell: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));
vi.mock('@/components/settings/theme-sync-initializer', () => ({ ThemeSyncInitializer: () => null }));
vi.mock('@/components/settings/accessibility-sync-initializer', () => ({
  AccessibilitySyncInitializer: () => null,
}));
vi.mock('@/components/settings/editor-preferences-sync-initializer', () => ({
  EditorPreferencesSyncInitializer: () => null,
}));
vi.mock('@/components/settings/two-factor-nudge-banner', () => ({
  TwoFactorNudgeBanner: () => null,
}));
vi.mock('@/components/community/github-star-prompt', () => ({
  GitHubStarPrompt: () => null,
}));

import Layout from './layout';

describe('dashboard layout unauthenticated guard callbackUrl (issue #864)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.auth.mockResolvedValue(null);
    mocks.headers.mockResolvedValue(new Headers());
  });

  afterEach(cleanup);

  it('uses the proxy-forwarded public path as the sign-in callbackUrl', async () => {
    mocks.headers.mockResolvedValue(
      new Headers({ 'x-gameguild-path': '/workspace/settings/security' }),
    );

    await expect(
      Layout({
        children: <div />,
        params: Promise.resolve({ locale: 'en-US' }),
      } as never),
    ).rejects.toThrow('Unauthenticated dashboard access');

    expect(mocks.redirect).toHaveBeenCalledWith({
      href: { pathname: '/sign-in', query: { callbackUrl: '/workspace/settings/security' } },
      locale: 'en-US',
    });
  });

  it('ignores unsafe forwarded paths and falls back to /workspace', async () => {
    mocks.headers.mockResolvedValue(
      new Headers({ 'x-gameguild-path': '//evil.example/path' }),
    );

    await expect(
      Layout({
        children: <div />,
        params: Promise.resolve({ locale: 'en-US' }),
      } as never),
    ).rejects.toThrow('Unauthenticated dashboard access');

    expect(mocks.redirect).toHaveBeenCalledWith({
      href: { pathname: '/sign-in', query: { callbackUrl: '/workspace' } },
      locale: 'en-US',
    });
  });

  it('falls back to /workspace when no path was forwarded', async () => {
    await expect(
      Layout({
        children: <div />,
        params: Promise.resolve({ locale: 'en-US' }),
      } as never),
    ).rejects.toThrow('Unauthenticated dashboard access');

    expect(mocks.redirect).toHaveBeenCalledWith({
      href: { pathname: '/sign-in', query: { callbackUrl: '/workspace' } },
      locale: 'en-US',
    });
  });
});
