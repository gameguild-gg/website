import { render, screen, within } from '@testing-library/react';
import type { ButtonHTMLAttributes, ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DashboardHeader } from './dashboard-header';

const mocks = vi.hoisted(() => ({
  pathname: '/workspace/learning/courses',
  push: vi.fn(),
  signOut: vi.fn(),
}));

vi.mock('@/components/ui/theme-toggle', () => ({
  ThemeToggle: () => <button type="button">Toggle theme</button>,
}));

vi.mock('@/i18n/navigation', () => ({
  Link: ({ children, href, ...props }: { children: ReactNode; href: string }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
  usePathname: () => mocks.pathname,
  useRouter: () => ({ push: mocks.push }),
}));

vi.mock('@game-guild/client/react', () => ({
  useAuth: () => ({
    signOut: mocks.signOut,
    isLoading: false,
  }),
}));

vi.mock('@game-guild/ui/components/sidebar', () => ({
  SidebarTrigger: (props: ButtonHTMLAttributes<HTMLButtonElement>) => (
    <button type="button" {...props}>Toggle sidebar</button>
  ),
}));

describe('DashboardHeader', () => {
  beforeEach(() => {
    mocks.pathname = '/workspace/learning/courses';
  });

  it('updates the accessible breadcrumb when Testing Lab routes change', () => {
    mocks.pathname = '/workspace/testing-lab/reports';
    const { rerender } = render(
      <DashboardHeader
        user={{ id: 'user-123', name: 'Ada Lovelace', email: 'ada@gameguild.gg', image: null }}
        notifications={{ items: [], unreadCount: 0 }}
      />,
    );

    expect(screen.getByRole('navigation', { name: 'Dashboard breadcrumb' })).toHaveTextContent('Reports');

    mocks.pathname = '/workspace/testing-lab/settings/access';
    rerender(
      <DashboardHeader
        user={{ id: 'user-123', name: 'Ada Lovelace', email: 'ada@gameguild.gg', image: null }}
        notifications={{ items: [], unreadCount: 0 }}
      />,
    );

    const breadcrumb = screen.getByRole('navigation', { name: 'Dashboard breadcrumb' });
    expect(breadcrumb).toHaveTextContent('Access');
    expect(breadcrumb).not.toHaveTextContent('Reports');
  });
  it('keeps Feed, Notifications, and the user profile in workspace header actions', () => {
    render(
      <DashboardHeader
        user={{ id: 'user-123', name: 'Ada Lovelace', email: 'ada@gameguild.gg', image: null }}
        notifications={{ items: [], unreadCount: 0 }}
      />,
    );

    const actions = screen.getByRole('group', { name: 'Dashboard actions' });
    expect(within(actions).getByRole('link', { name: 'Open Community feed' })).toBeInTheDocument();
    expect(within(actions).getByRole('button', { name: 'Notifications' })).toBeInTheDocument();
    expect(within(actions).getByRole('button', { name: 'Open Ada Lovelace account menu' })).toBeInTheDocument();
    expect(within(actions).queryByRole('button', { name: 'Search dashboard' })).not.toBeInTheDocument();
    expect(within(actions).queryByRole('button', { name: 'Toggle theme' })).not.toBeInTheDocument();
  });

  it('temporarily omits dashboard search from desktop and mobile layouts', () => {
    render(
      <DashboardHeader
        user={{
          id: 'user-123',
          name: 'Ada Lovelace',
          email: 'ada@gameguild.gg',
          image: null,
        }}
        notifications={{ items: [], unreadCount: 0 }}
      />,
    );

    expect(screen.queryByRole('button', { name: 'Search dashboard' })).not.toBeInTheDocument();
  });

  it('places the Community feed link with the global header actions', () => {
    mocks.pathname = '/workspace/projects';

    render(
      <DashboardHeader
        user={{ id: 'user-123', name: 'Ada Lovelace', email: 'ada@gameguild.gg', image: null }}
        notifications={{ items: [], unreadCount: 0 }}
      />,
    );

    const actions = screen.getByRole('group', { name: 'Dashboard actions' });
    const feedLink = within(actions).getByRole('link', { name: 'Open Community feed' });
    expect(feedLink).toHaveAttribute('href', '/');
    expect(feedLink.textContent).toBe('');
    expect(feedLink.querySelector('svg.lucide-rss')).toBeInTheDocument();
    expect(feedLink).toHaveAttribute('data-slot', 'button');
    expect(feedLink).not.toHaveClass('text-muted-foreground');
    expect(screen.getByRole('button', { name: 'Toggle sidebar' })).toHaveClass('md:hidden');
  });

  it('keeps the existing sidebar toggle and omits the Community return in the console', () => {
    mocks.pathname = '/console/community';

    render(
      <DashboardHeader
        user={{ id: 'user-123', name: 'Ada Lovelace', email: 'ada@gameguild.gg', image: null }}
        notifications={{ items: [], unreadCount: 0 }}
      />,
    );

    expect(screen.queryByRole('link', { name: 'Open Community feed' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Toggle sidebar' })).not.toHaveClass('md:hidden');
  });
});
