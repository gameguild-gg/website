import '@testing-library/jest-dom/vitest';
import { render, screen } from '@testing-library/react';
import type { ButtonHTMLAttributes, ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  pathname: '/workspace',
  toggleSidebar: vi.fn(),
}));

vi.mock('@/i18n/navigation', () => ({
  Link: ({ href, children }: { href: string; children: ReactNode }) => <a href={href}>{children}</a>,
  usePathname: () => mocks.pathname,
}));

vi.mock('@game-guild/ui/components/sidebar', () => ({
  SidebarMenu: ({ children }: { children: ReactNode }) => <div data-sidebar="menu">{children}</div>,
  SidebarMenuItem: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  SidebarMenuButton: ({
    asChild,
    children,
    ...props
  }: { asChild?: boolean; children: ReactNode } & ButtonHTMLAttributes<HTMLButtonElement>) =>
    asChild ? <>{children}</> : <button type="button" data-sidebar="menu-button" {...props}>{children}</button>,
  SidebarFooter: ({ children }: { children?: ReactNode }) => <footer>{children}</footer>,
  useSidebar: () => ({
    isMobile: false,
    openMobile: false,
    state: 'expanded',
    toggleSidebar: mocks.toggleSidebar,
  }),
}));

import { DashboardSidebarFooter } from './dashboard-sidebar';
import { TenantSwitcher } from './tenant-switcher';

function TenantLogo() {
  return <span aria-hidden="true" />;
}

describe('workspace shell switchers', () => {
  beforeEach(() => {
    mocks.pathname = '/workspace';
    mocks.toggleSidebar.mockReset();
  });

  it('renders the only tenant as static identity instead of a switch control', () => {
    render(
      <TenantSwitcher
        tenants={[{ id: 'gameguild', name: 'GameGuild', logo: TenantLogo, plan: 'Platform' }]}
      />,
    );

    expect(screen.getByText('GameGuild')).toBeInTheDocument();
    expect(screen.getByText('Platform')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.queryByText('Tenants')).not.toBeInTheDocument();
  });

  it('places the desktop collapse control in the workspace sidebar footer', () => {
    render(<DashboardSidebarFooter />);

    const toggle = screen.getByRole('button', { name: 'Collapse sidebar' });
    expect(toggle).toHaveAttribute('data-sidebar', 'menu-button');
    expect(toggle.closest('[data-sidebar="menu"]')).toBeInTheDocument();
    toggle.click();

    expect(mocks.toggleSidebar).toHaveBeenCalledOnce();
  });

  it('does not add a footer toggle to the console sidebar', () => {
    mocks.pathname = '/console/community';

    render(<DashboardSidebarFooter />);

    expect(screen.queryByRole('button', { name: /sidebar/i })).not.toBeInTheDocument();
  });
});
