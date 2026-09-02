import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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
    mocks.push.mockReset();
    mocks.signOut.mockReset();
    mocks.signOut.mockResolvedValue(undefined);
  });

  it('updates the accessible breadcrumb when Testing Lab routes change', () => {
    mocks.pathname = '/console/community/testing-lab/reports';
    const { rerender } = render(
      <DashboardHeader
        user={{ id: 'user-123', name: 'Ada Lovelace', email: 'ada@gameguild.gg', image: null }}
        notifications={{ items: [], unreadCount: 0 }}
      />,
    );

    expect(screen.getByRole('navigation', { name: 'Dashboard breadcrumb' })).toHaveTextContent('Reports');

    mocks.pathname = '/console/community/testing-lab/settings/access';
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
  it('renders the signed-in user menu and routes sign-out through auth', async () => {
    const user = userEvent.setup();

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

    const menuTrigger = screen.getByRole('button', { name: 'Open Ada Lovelace account menu' });
    expect(menuTrigger).toBeInTheDocument();

    await user.click(menuTrigger);

    expect(await screen.findByText('ada@gameguild.gg')).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: /my workspace/i })).toHaveAttribute(
      'href',
      '/workspace',
    );
    expect(screen.getByRole('menuitem', { name: /account settings/i })).toHaveAttribute(
      'href',
      '/workspace/settings/account',
    );

    await user.click(screen.getByRole('menuitem', { name: /sign out/i }));

    await waitFor(() => {
      expect(mocks.signOut).toHaveBeenCalledWith({ redirect: false });
    });
    expect(mocks.push).toHaveBeenCalledWith('/sign-in');
  });

  it('keeps dashboard search responsive across desktop and mobile header layouts', () => {
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

    const searchButtons = screen.getAllByRole('button', { name: 'Search dashboard' });
    expect(searchButtons).toHaveLength(2);
    expect(searchButtons[0]).toHaveClass('max-w-sm');
    expect(searchButtons[0]).toHaveClass('lg:max-w-md');
    expect(searchButtons[0].parentElement).toHaveClass('hidden', 'xl:flex', 'justify-center');
    expect(searchButtons[1]).toHaveClass('xl:hidden');
  });

  it('returns workspace members to Community without a desktop sidebar toggle in the header', () => {
    mocks.pathname = '/workspace/projects';

    render(
      <DashboardHeader
        user={{ id: 'user-123', name: 'Ada Lovelace', email: 'ada@gameguild.gg', image: null }}
        notifications={{ items: [], unreadCount: 0 }}
      />,
    );

    expect(screen.getByRole('link', { name: 'Back to Community' })).toHaveAttribute('href', '/');
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

    expect(screen.queryByRole('link', { name: 'Back to Community' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Toggle sidebar' })).not.toHaveClass('md:hidden');
  });
});
