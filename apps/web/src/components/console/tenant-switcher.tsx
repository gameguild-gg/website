'use client';

import * as React from 'react';
import { ChevronsUpDown, Plus } from 'lucide-react';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuShortcut,
  DropdownMenuTrigger,
} from '@game-guild/ui/components/dropdown-menu';
import {
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from '@game-guild/ui/components/sidebar';

export interface Tenant {
  id: string;
  name: string;
  logo: React.ElementType;
  plan: string;
}

interface TenantSwitcherProps {
  tenants: Tenant[];
  activeTenant?: Tenant;
  onTenantChange?: (tenant: Tenant) => void;
  onAddTenant?: () => void;
}

export function TenantSwitcher({
  tenants,
  activeTenant: controlledActiveTenant,
  onTenantChange,
  onAddTenant,
}: TenantSwitcherProps) {
  const { isMobile } = useSidebar();
  const [internalActiveTenant, setInternalActiveTenant] = React.useState(tenants[0]);

  const activeTenant = controlledActiveTenant ?? internalActiveTenant;

  const handleTenantChange = (tenant: Tenant) => {
    if (onTenantChange) {
      onTenantChange(tenant);
    } else {
      setInternalActiveTenant(tenant);
    }
  };

  if (!activeTenant || tenants.length < 2) {
    return null;
  }

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <SidebarMenuButton
              size="lg"
              className="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground"
            >
              <div className="bg-sidebar-primary text-sidebar-primary-foreground flex aspect-square size-8 items-center justify-center rounded-lg">
                <activeTenant.logo className="size-4" />
              </div>
              <div className="grid flex-1 text-left text-sm leading-tight">
                <span className="truncate font-medium">{activeTenant.name}</span>
                <span className="truncate text-xs">{activeTenant.plan}</span>
              </div>
              <ChevronsUpDown className="ml-auto" />
            </SidebarMenuButton>
          </DropdownMenuTrigger>
          <DropdownMenuContent
            className="w-[--radix-dropdown-menu-trigger-width] min-w-56 rounded-lg"
            align="start"
            side={isMobile ? 'bottom' : 'right'}
            sideOffset={4}
          >
            <DropdownMenuLabel className="text-muted-foreground text-xs">
              Tenants
            </DropdownMenuLabel>
            {tenants.map((tenant, index) => (
              <DropdownMenuItem
                key={tenant.id}
                onClick={() => handleTenantChange(tenant)}
                className="gap-2 p-2"
              >
                <div className="flex size-6 items-center justify-center rounded-md border">
                  <tenant.logo className="size-3.5 shrink-0" />
                </div>
                {tenant.name}
                <DropdownMenuShortcut>⌘{index + 1}</DropdownMenuShortcut>
              </DropdownMenuItem>
            ))}
            {onAddTenant && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem className="gap-2 p-2" onClick={onAddTenant}>
                  <div className="flex size-6 items-center justify-center rounded-md border bg-transparent">
                    <Plus className="size-4" />
                  </div>
                  <div className="text-muted-foreground font-medium">Add tenant</div>
                </DropdownMenuItem>
              </>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    </SidebarMenu>
  );
}
