'use client';

import { AppShellHeader } from '@/components/app/app-shell-header';
import { AppShellHeaderMenu } from '@/components/app/app-shell-header-menu';
import { AppShellSidebar } from '@/components/app/app-shell-sidebar';
import { AppShellContent, AppShellInset, AppShell } from '@/components/app/app-shell-layout';
import { socialDesktopNav, socialNavigationData } from '@/components/app/social-navigation';
import { WorkspaceCommandPalette } from '@/components/console/workspace-command-palette';
import { PublicDesktopNav } from '@/components/app/public-website-nav';
import type { WorkspaceUser } from '@/components/console/workspace-user-menu';
import { Toaster } from '@game-guild/ui/components/sonner';

interface SocialAppShellProps {
  children: React.ReactNode;
  /** Streamed Suspense slot from the layout (bell + user menu). */
  notificationsMenu?: React.ReactNode;
  user: WorkspaceUser;
}

export function SocialAppShell({ children, notificationsMenu, user }: SocialAppShellProps): React.JSX.Element {
  return (
    <div className="flex h-svh min-w-0 flex-1 overflow-hidden">
      <a
        href="#dashboard-main"
        className="sr-only fixed left-4 top-4 z-50 rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground focus:not-sr-only"
      >
        Skip to main content
      </a>
      <AppShell>
        <AppShellSidebar navigation={socialNavigationData} />
        <AppShellInset>
          <WorkspaceCommandPalette navigation={socialNavigationData} />
          <AppShellHeader>
            <div className="flex min-w-0 items-center gap-2">
              <PublicDesktopNav items={socialDesktopNav} variant="app" />
            </div>
            {notificationsMenu ?? <AppShellHeaderMenu user={user} />}
          </AppShellHeader>
          <AppShellContent>{children}</AppShellContent>
        </AppShellInset>
      </AppShell>
      <Toaster closeButton richColors position="top-right" />
    </div>
  );
}
