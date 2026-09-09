import '@testing-library/jest-dom/vitest';
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const dashboardSidebarSpy = vi.hoisted(() => vi.fn());

vi.mock('./dashboard-sidebar', () => ({
  DashboardSidebar: (props: unknown) => {
    dashboardSidebarSpy(props);
    return <nav aria-label="Workspace navigation" />;
  },
  dashboardNavigationData: [],
  filterDashboardNavigation: () => [],
}));
vi.mock('./dashboard-header', () => ({ DashboardHeader: () => <header /> }));
vi.mock('./dashboard-command-palette', () => ({ DashboardCommandPalette: () => null }));
vi.mock('@/components/ui/sonner', () => ({ Toaster: () => null }));
vi.mock('@game-guild/ui/components/sidebar', () => ({
  SidebarProvider: ({ children }: { readonly children: React.ReactNode }) => <>{children}</>,
  SidebarInset: ({ children }: { readonly children: React.ReactNode }) => <>{children}</>,
}));

import { ConsoleShell } from './console-shell';

describe('dashboard keyboard navigation', () => {
  beforeEach(() => {
    dashboardSidebarSpy.mockClear();
  });

  it('offers a direct skip link to the focusable main content', () => {
    render(
      <ConsoleShell user={{ id: 'user-1', name: 'Member', initials: 'M' }}>
        <p>Workspace content</p>
      </ConsoleShell>,
    );

    expect(screen.getByRole('link', { name: 'Skip to main content' })).toHaveAttribute(
      'href',
      '#dashboard-main',
    );
    const skipTarget = document.getElementById('dashboard-main');
    expect(skipTarget).toHaveAttribute('tabindex', '-1');
    expect(skipTarget?.tagName).toBe('DIV');
  });

  it('does not expose workspace and operations as switchable tenant contexts', () => {
    render(
      <ConsoleShell
        user={{ id: 'user-1', name: 'Member', initials: 'M' }}
        contexts={[
          { type: 'Workspace', id: null, name: 'Workspace', route: '/workspace' },
          { type: 'Operations', id: null, name: 'Operations', route: '/dashboard' },
        ]}
      >
        <p>Workspace content</p>
      </ConsoleShell>,
    );

    expect(dashboardSidebarSpy).toHaveBeenCalledOnce();
    expect(dashboardSidebarSpy.mock.calls[0]?.[0]).not.toHaveProperty('contexts');
  });
});
