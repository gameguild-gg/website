import '@testing-library/jest-dom/vitest';
import { render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('@/i18n/navigation', () => ({
  Link: ({ href, children }: { href: string; children: ReactNode }) => <a href={href}>{children}</a>,
  usePathname: () => '/workspace',
}));

vi.mock('@game-guild/ui/components/sidebar', () => ({
  SidebarMenu: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  SidebarMenuItem: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  SidebarMenuButton: ({ asChild, children }: { asChild?: boolean; children: ReactNode }) =>
    asChild ? <>{children}</> : <button type="button">{children}</button>,
  useSidebar: () => ({ isMobile: false }),
}));

import { ContextSwitcher } from './team-switcher';
import { TenantSwitcher } from './tenant-switcher';

function TenantLogo() {
  return <span aria-hidden="true" />;
}

describe('workspace shell switchers', () => {
  it('does not render a tenant switcher when only one tenant is available', () => {
    const { container } = render(
      <TenantSwitcher
        tenants={[{ id: 'gameguild', name: 'GameGuild', logo: TenantLogo, plan: 'Platform' }]}
      />,
    );

    expect(container).toBeEmptyDOMElement();
  });

  it('renders a single workspace context as a link instead of a dropdown', () => {
    render(
      <ContextSwitcher
        contexts={[{ type: 'Workspace', id: null, name: 'Workspace', route: '/workspace' }]}
      />,
    );

    expect(screen.getByRole('link', { name: 'Workspace' })).toHaveAttribute('href', '/workspace');
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('renders one context dropdown when Workspace and Operations are available', () => {
    render(
      <ContextSwitcher
        contexts={[
          { type: 'Workspace', id: null, name: 'Workspace', route: '/workspace' },
          { type: 'Operations', id: null, name: 'Operations', route: '/dashboard' },
        ]}
      />,
    );

    expect(screen.getByRole('button', { name: 'Workspace' })).toBeInTheDocument();
  });
});
