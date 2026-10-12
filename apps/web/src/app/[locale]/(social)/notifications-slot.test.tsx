import '@testing-library/jest-dom/vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { DashboardNotificationSummary } from '@/lib/dashboard-notifications';

const mocks = vi.hoisted(() => ({ getSummary: vi.fn() }));

vi.mock('@/lib/dashboard-notifications', () => ({
  getDashboardNotificationSummary: mocks.getSummary,
}));
vi.mock('@/components/app/app-shell-header-menu', () => ({
  AppShellHeaderMenu: ({
    notifications,
    user,
  }: {
    notifications?: DashboardNotificationSummary;
    user?: { name?: string };
  }) => (
    <div
      data-testid="header-menu"
      data-unread={notifications ? String(notifications.unreadCount) : 'undefined'}
    >
      {user?.name}
    </div>
  ),
}));

import { NotificationsSlot } from './notifications-slot';

const user = { id: 'user-1', name: 'Ada', email: 'ada@example.com', image: null };

describe('NotificationsSlot', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });
  afterEach(cleanup);

  it('passes the fetched summary to the header menu', async () => {
    mocks.getSummary.mockResolvedValue({ items: [], unreadCount: 2 } satisfies DashboardNotificationSummary);

    const ui = await NotificationsSlot({ userId: 'user-1', user });
    render(ui);

    expect(screen.getByTestId('header-menu')).toHaveAttribute('data-unread', '2');
  });

  it('degrades to an empty summary instead of throwing when the fetch rejects', async () => {
    mocks.getSummary.mockRejectedValue(new Error('notifications api down'));

    const ui = await NotificationsSlot({ userId: 'user-1', user });
    render(ui);

    expect(screen.getByTestId('header-menu')).toHaveAttribute('data-unread', '0');
    expect(screen.getByText('Ada')).toBeInTheDocument();
  });
});
