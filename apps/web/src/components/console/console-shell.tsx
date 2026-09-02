'use client';

import {
  DashboardSidebar,
  dashboardNavigationData,
  filterDashboardNavigation,
} from './dashboard-sidebar';
import { DashboardHeader } from './dashboard-header';
import { DashboardCommandPalette } from './dashboard-command-palette';
import { cn } from '@game-guild/ui/lib/utils';
import { SidebarInset, SidebarProvider } from '@game-guild/ui/components/sidebar';
import type { DashboardNotificationSummary } from '@/lib/dashboard-notifications';
import type { DashboardUser } from './dashboard-user-menu';
import { Toaster } from '@/components/ui/sonner';
import type { DashboardContextSummary } from '@/lib/dashboard-contexts';

interface DashboardShellProps {
  children: React.ReactNode;
  notifications?: DashboardNotificationSummary;
  user: DashboardUser;
  capabilities?: readonly string[];
  contexts?: readonly DashboardContextSummary[];
}

export function ConsoleShell({
  children,
  notifications,
  user,
  capabilities = [],
}: DashboardShellProps) {
  const navigation = filterDashboardNavigation(
    dashboardNavigationData,
    capabilities,
  );

  return (
    <div className="flex h-svh min-w-0 flex-1 overflow-hidden">
      <a
        href="#dashboard-main"
        className="sr-only fixed left-4 top-4 z-50 rounded-md bg-background px-4 py-2 text-sm font-medium shadow-lg focus:not-sr-only"
      >
        Skip to main content
      </a>
      <SidebarProvider>
        <DashboardSidebar navigation={navigation} />
        <SidebarInset className="min-w-0 overflow-hidden">
          <DashboardCommandPalette
            navigation={navigation}
            capabilities={capabilities}
          />
          {/* Main Content */}
          <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
            {/* Navbar */}
            <DashboardHeader notifications={notifications} user={user} />

            {/* Page Content */}
            <div
              id="dashboard-main"
              tabIndex={-1}
              className={cn('min-w-0 flex-1 overflow-y-auto overflow-x-hidden bg-muted/30 p-4 transition-all duration-300 sm:p-6')}
            >
              {children}
            </div>
          </div>
        </SidebarInset>
      </SidebarProvider>
      <Toaster closeButton richColors position="top-right" />
    </div>
  );
}
