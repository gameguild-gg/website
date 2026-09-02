'use client';

import * as React from 'react';
import { ChevronsUpDown, Settings2 } from 'lucide-react';
import { Link, usePathname } from '@/i18n/navigation';
import type { DashboardContextSummary, DashboardContextType } from '@/lib/dashboard-contexts';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from '@game-guild/ui/components/dropdown-menu';
import { SidebarMenu, SidebarMenuButton, SidebarMenuItem, useSidebar } from '@game-guild/ui/components/sidebar';

const contextMeta: Record<DashboardContextType, { label: string; icon: React.ElementType }> = {
  Workspace: { label: 'Workspace', icon: Settings2 },
  Team: { label: 'Team', icon: Settings2 },
  Project: { label: 'Project', icon: Settings2 },
  Operations: { label: 'Operations', icon: Settings2 },
};

function isOperationsPath(pathname: string | null): boolean {
  return Boolean(pathname?.startsWith('/console/community/testing-lab') || pathname?.startsWith('/console/community/launch-pad'));
}

export function ContextSwitcher({ contexts }: { contexts: readonly DashboardContextSummary[] }) {
  const { isMobile } = useSidebar();
  const pathname = usePathname();
  const available = contexts.length > 0
    ? contexts
    : [{ type: 'Workspace' as const, id: null, name: 'Workspace', route: '/workspace' }];
  const active = available.find((context) =>
    context.type === 'Operations'
      ? isOperationsPath(pathname)
      : context.route !== '/dashboard' && (pathname === context.route || pathname?.startsWith(`${context.route}/`)),
  ) ?? available.find((context) => context.type === 'Operations') ?? available[0]!;
  const ActiveIcon = contextMeta[active.type].icon;
  const activeTypeLabel = contextMeta[active.type].label;
  const showActiveType = active.name.trim().toLowerCase() !== activeTypeLabel.toLowerCase();

  if (available.length === 1) {
    return (
      <SidebarMenu>
        <SidebarMenuItem>
          <SidebarMenuButton size="lg" asChild>
            <Link href={active.route}>
              <div className="flex aspect-square size-8 items-center justify-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground">
                <ActiveIcon className="size-4" />
              </div>
              <div className="grid flex-1 text-left text-sm leading-tight">
                <span className="truncate font-medium">{active.name}</span>
                {showActiveType && (
                  <span className="truncate text-xs text-sidebar-foreground/70">{activeTypeLabel}</span>
                )}
              </div>
            </Link>
          </SidebarMenuButton>
        </SidebarMenuItem>
      </SidebarMenu>
    );
  }

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <SidebarMenuButton size="lg" className="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground">
              <div className="flex aspect-square size-8 items-center justify-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground">
                <ActiveIcon className="size-4" />
              </div>
              <div className="grid flex-1 text-left text-sm leading-tight">
                <span className="truncate font-medium">{active.name}</span>
                {showActiveType && (
                  <span className="truncate text-xs text-sidebar-foreground/70">{activeTypeLabel}</span>
                )}
              </div>
              <ChevronsUpDown className="ml-auto size-4" />
            </SidebarMenuButton>
          </DropdownMenuTrigger>
          <DropdownMenuContent
            className="w-[--radix-dropdown-menu-trigger-width] min-w-64 rounded-lg"
            align="start"
            side={isMobile ? 'bottom' : 'right'}
            sideOffset={4}
          >
            <DropdownMenuLabel className="text-xs text-muted-foreground">Switch context</DropdownMenuLabel>
            {available.map((context) => {
              const Icon = contextMeta[context.type].icon;
              return (
                <DropdownMenuItem key={`${context.type}:${context.id ?? 'root'}`} asChild>
                  <Link href={context.route} className="gap-2 p-2">
                    <div className="flex size-7 items-center justify-center rounded-md border">
                      <Icon className="size-3.5" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium">{context.name}</p>
                      <p className="text-xs text-muted-foreground">{contextMeta[context.type].label}</p>
                    </div>
                  </Link>
                </DropdownMenuItem>
              );
            })}
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    </SidebarMenu>
  );
}

export const TeamSwitcher = ContextSwitcher;
