import '@testing-library/jest-dom/vitest';
import { cleanup, render } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  auth: vi.fn(),
  readSessionUnCached: vi.fn(),
  headers: vi.fn(),
  cookies: vi.fn(),
  redirect: vi.fn(),
  getDashboardNotificationSummary: vi.fn(),
  getDashboardContexts: vi.fn(),
  getMfaStatus: vi.fn(),
}));

vi.mock('@/auth', () => ({
  auth: mocks.auth,
  readSessionUnCached: mocks.readSessionUnCached,
  authConfig: {
    cookies: { name: 'gameguild', secure: false, sameSite: 'lax', path: '/' },
  },
}));
vi.mock('next/headers', () => ({ headers: mocks.headers, cookies: mocks.cookies }));
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

// SessionStore(getCookieName): `gameguild.session-token` (dev) or
// `__Secure-gameguild.session-token` (secure). Tests run insecure.
const SESSION_COOKIE = 'gameguild.session-token';

function cookieStoreWith(cookieMap: Record<string, string>) {
  return {
    get: (name: string) => (name in cookieMap ? { name, value: cookieMap[name] } : undefined),
  };
}

const anonymousSession = { user: { id: 'u1', name: 'U', email: 'u@example.com' } };

describe('dashboard layout unauthenticated guard callbackUrl (issue #864)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    mocks.auth.mockResolvedValue(null);
    mocks.headers.mockResolvedValue(new Headers());
    mocks.cookies.mockResolvedValue(cookieStoreWith({}));
    mocks.getDashboardNotificationSummary.mockResolvedValue({});
    mocks.getDashboardContexts.mockResolvedValue({ capabilities: [], contexts: [] });
    mocks.getMfaStatus.mockResolvedValue({ ok: true, data: { isEnabled: false } });
  });

  afterEach(() => {
    vi.useRealTimers();
    cleanup();
  });

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

  it('redirects immediately without a session cookie (no retry)', async () => {
    await expect(
      Layout({
        children: <div />,
        params: Promise.resolve({ locale: 'en-US' }),
      } as never),
    ).rejects.toThrow('Unauthenticated dashboard access');

    expect(mocks.auth).toHaveBeenCalledTimes(1);
    expect(mocks.redirect).toHaveBeenCalledWith({
      href: { pathname: '/sign-in', query: { callbackUrl: '/workspace' } },
      locale: 'en-US',
    });
  });
});

describe('dashboard layout session-cookie retry (issue #864 post-MFA flash)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    mocks.headers.mockResolvedValue(new Headers());
    mocks.getDashboardNotificationSummary.mockResolvedValue({});
    mocks.getDashboardContexts.mockResolvedValue({ capabilities: [], contexts: [] });
    mocks.getMfaStatus.mockResolvedValue({ ok: true, data: { isEnabled: false } });
  });

  afterEach(() => {
    vi.useRealTimers();
    cleanup();
  });

  it('retries the uncached session read when a cookie is present and renders once it resolves', async () => {
    mocks.auth.mockResolvedValue(null);
    mocks.readSessionUnCached.mockResolvedValue(anonymousSession);
    mocks.cookies.mockResolvedValue(
      cookieStoreWith({ [SESSION_COOKIE]: 'encrypted-session' }),
    );

    const pending = Layout({
      children: <div data-testid="dashboard-page">content</div>,
      params: Promise.resolve({ locale: 'en-US' }),
    } as never);

    await vi.advanceTimersByTimeAsync(150);
    const ui = await pending;
    render(ui);

    expect(mocks.readSessionUnCached).toHaveBeenCalledTimes(1);
    expect(mocks.redirect).not.toHaveBeenCalled();
  });

  it('redirects after one retry when the cookie exists but the uncached read stays null', async () => {
    mocks.auth.mockResolvedValue(null);
    mocks.readSessionUnCached.mockResolvedValue(null);
    mocks.cookies.mockResolvedValue(
      cookieStoreWith({ [SESSION_COOKIE]: 'stale-or-expired' }),
    );

    const pending = Layout({
      children: <div />,
      params: Promise.resolve({ locale: 'en-US' }),
    } as never);
    // Attach the rejection handler before firing the timer so the
    // layout's throw is never an unhandled promise rejection.
    const assertion = expect(pending).rejects.toThrow(
      'Unauthenticated dashboard access',
    );

    await vi.advanceTimersByTimeAsync(150);
    await assertion;

    expect(mocks.readSessionUnCached).toHaveBeenCalledTimes(1);
    expect(mocks.redirect).toHaveBeenCalledWith({
      href: { pathname: '/sign-in', query: { callbackUrl: '/workspace' } },
      locale: 'en-US',
    });
  });
});
