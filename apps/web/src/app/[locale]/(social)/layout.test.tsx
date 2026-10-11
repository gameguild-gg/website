import '@testing-library/jest-dom/vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { DashboardNotificationSummary } from '@/lib/dashboard-notifications';

const mocks = vi.hoisted(() => ({
  auth: vi.fn(),
  getSummary: vi.fn(),
  redirect: vi.fn(),
}));

vi.mock('@/auth', () => ({ auth: mocks.auth }));
vi.mock('@/i18n/navigation', () => ({ redirect: mocks.redirect }));
vi.mock('@/lib/dashboard-notifications', () => ({
  getDashboardNotificationSummary: mocks.getSummary,
}));
vi.mock('@/components/settings/theme-sync-initializer', () => ({ ThemeSyncInitializer: () => null }));
vi.mock('@/components/settings/accessibility-sync-initializer', () => ({
  AccessibilitySyncInitializer: () => null,
}));
vi.mock('@/components/settings/editor-preferences-sync-initializer', () => ({
  EditorPreferencesSyncInitializer: () => null,
}));
vi.mock('@/components/feed/social-app-shell', () => ({
  SocialAppShell: ({
    children,
    notificationsMenu,
  }: {
    children: React.ReactNode;
    notificationsMenu?: React.ReactNode;
  }) => (
    <div data-testid="social-app-shell">
      {notificationsMenu}
      <main>{children}</main>
    </div>
  ),
}));
vi.mock('@/components/app/app-shell-header-menu', () => ({
  AppShellHeaderMenu: ({ notifications }: { notifications?: DashboardNotificationSummary }) => (
    <div
      data-testid="header-menu"
      data-unread={notifications ? String(notifications.unreadCount) : 'undefined'}
    />
  ),
}));

import Layout from './layout';

describe('social layout notifications streaming', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.auth.mockResolvedValue({
      user: { id: 'user-1', name: 'Ada', email: 'ada@example.com', image: null },
    });
  });
  afterEach(cleanup);

  it('renders the shell and children while a slow (300ms) summary is still pending', async () => {
    mocks.getSummary.mockImplementation(
      async () =>
        await new Promise<DashboardNotificationSummary>((resolve) =>
          setTimeout(() => resolve({ items: [], unreadCount: 1 }), 300),
        ),
    );

    const ui = await Layout({
      children: <div data-testid="feed-page">feed</div>,
      params: Promise.resolve({ locale: 'en' }),
    } as never);
    render(ui);

    expect(mocks.getSummary).toHaveBeenCalledWith('user-1');
    expect(screen.getByTestId('social-app-shell')).toBeInTheDocument();
    expect(screen.getByTestId('feed-page')).toBeInTheDocument();
    expect(screen.getAllByTestId('header-menu').length).toBeGreaterThan(0);
  });

  it('renders the shell and children when the summary rejects, with no layout throw', async () => {
    mocks.getSummary.mockRejectedValue(new Error('notifications api down'));

    const ui = await Layout({
      children: <div data-testid="feed-page">feed</div>,
      params: Promise.resolve({ locale: 'en' }),
    } as never);
    render(ui);

    expect(screen.getByTestId('social-app-shell')).toBeInTheDocument();
    expect(screen.getByTestId('feed-page')).toBeInTheDocument();
    expect(screen.getAllByTestId('header-menu').length).toBeGreaterThan(0);
  });

  it('still redirects unauthenticated sessions before any notifications work', async () => {
    mocks.auth.mockResolvedValue(null);

    await expect(
      Layout({
        children: <div />,
        params: Promise.resolve({ locale: 'en' }),
      } as never),
    ).rejects.toThrow('Unauthenticated social access');

    expect(mocks.redirect).toHaveBeenCalledWith({ href: { pathname: '/sign-in' }, locale: 'en' });
    expect(mocks.getSummary).not.toHaveBeenCalled();
  });
});
