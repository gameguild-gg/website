'use client';

import { Link, usePathname } from '@/i18n/navigation';
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from '@game-guild/ui/components/sidebar';
import {
  Bookmark,
  Compass,
  FlaskConical,
  GraduationCap,
  Home,
  PanelLeftClose,
  PanelLeftOpen,
} from 'lucide-react';
import { useSearchParams } from 'next/navigation';

export const SOCIAL_NAVIGATION = [
  { label: 'Home', href: '/', icon: Home },
  { label: 'Explore', href: '/projects', icon: Compass },
  { label: 'Testing Lab', href: '/workspace/testing-lab', icon: FlaskConical },
  { label: 'Saved', href: '/?tab=saved', icon: Bookmark },
] as const;

export function SocialSidebarToggle({ placement = 'header' }: { placement?: 'header' | 'footer' }): React.JSX.Element {
  const { isMobile, openMobile, state, toggleSidebar } = useSidebar();
  const expanded = isMobile ? openMobile : state === 'expanded';
  const label = isMobile
    ? expanded
      ? 'Close sidebar'
      : 'Open sidebar'
    : expanded
      ? 'Collapse sidebar'
      : 'Expand sidebar';
  const Icon = expanded ? PanelLeftClose : PanelLeftOpen;

  return (
    <button
      type="button"
      onClick={toggleSidebar}
      aria-label={label}
      title={label}
      className={
        placement === 'header'
          ? 'inline-flex size-9 items-center justify-center rounded-xl border border-sidebar-border bg-sidebar-accent/40 text-sidebar-foreground transition hover:bg-sidebar-accent hover:text-sidebar-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring md:hidden'
          : 'flex size-10 items-center justify-center rounded-xl text-muted-foreground transition hover:bg-sidebar-accent hover:text-sidebar-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring'
      }
    >
      <Icon className="size-4 shrink-0" aria-hidden="true" />
    </button>
  );
}

export function SocialSidebar(): React.JSX.Element {
  const pathname = usePathname() ?? '/';
  const searchParams = useSearchParams();
  const activeTab = searchParams.get('tab');

  return (
    <Sidebar
      collapsible="icon"
      style={{ borderRightWidth: 0 }}
      className="h-svh bg-sidebar text-sidebar-foreground [&_[data-slot=sidebar-inner]]:bg-sidebar"
    >
      <SidebarHeader className="p-3">
        <div className="flex min-h-10 items-center gap-2">
          <Link
            href="/"
            aria-label="GameGuild Social home"
            className="flex min-w-0 flex-1 items-center gap-3 overflow-hidden rounded-xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring group-data-[collapsible=icon]:flex-none"
          >
            <span className="flex size-9 shrink-0 items-center justify-center rounded-xl border border-sidebar-border bg-sidebar-foreground text-sidebar shadow-sm">
              <GraduationCap className="size-5" aria-hidden="true" />
            </span>
            <span className="min-w-0 group-data-[collapsible=icon]:hidden">
              <span className="block truncate text-sm font-semibold text-sidebar-foreground">GameGuild</span>
            </span>
          </Link>
        </div>
      </SidebarHeader>

      <SidebarContent className="px-2 py-3">
        <SidebarMenu aria-label="Social navigation" className="gap-1.5">
          {SOCIAL_NAVIGATION.map(({ label, href, icon: Icon }) => {
            const active =
              (label === 'Home' && pathname === '/' && !activeTab) ||
              (label === 'Testing Lab' && pathname.startsWith('/workspace/testing-lab')) ||
              (label === 'Explore' && pathname.startsWith('/projects')) ||
              (label === 'Saved' && pathname === '/' && activeTab === 'saved');
            return (
              <SidebarMenuItem key={label} className="group-data-[collapsible=icon]:mx-auto group-data-[collapsible=icon]:w-8">
                <SidebarMenuButton
                  asChild
                  size="lg"
                  isActive={active}
                  tooltip={label}
                  className="h-11 rounded-xl px-3 text-sm font-semibold text-muted-foreground transition-colors hover:bg-sidebar-accent hover:text-sidebar-accent-foreground data-active:bg-sidebar-primary/10 data-active:text-sidebar-primary [&_svg]:size-5"
                >
                  <Link href={href} aria-current={active ? 'page' : undefined}>
                    <Icon strokeWidth={1.8} aria-hidden="true" />
                    <span className="group-data-[collapsible=icon]:hidden">{label}</span>
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
            );
          })}

        </SidebarMenu>
      </SidebarContent>

      <SidebarFooter className="items-center p-3">
        <SocialSidebarToggle placement="footer" />
      </SidebarFooter>
    </Sidebar>
  );
}
