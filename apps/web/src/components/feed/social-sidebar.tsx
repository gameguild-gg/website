'use client';

import { Link, usePathname } from '@/i18n/navigation';
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuBadge,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
  SidebarTrigger,
} from '@game-guild/ui/components/sidebar';
import { cn } from '@game-guild/ui/lib/utils';
import {
  Bookmark,
  Compass,
  FlaskConical,
  GraduationCap,
  Home,
  MessageCircle,
  Plus,
  UsersRound,
} from 'lucide-react';
import { useSearchParams } from 'next/navigation';

const socialNavigation = [
  { label: 'Home', href: '/social', icon: Home },
  { label: 'Explore', href: '/projects', icon: Compass },
  { label: 'Playtests', href: '/social?tab=playtests', icon: FlaskConical },
  { label: 'Messages', href: '/workspace/invitations', icon: MessageCircle, badge: '5' },
  { label: 'Saved', href: '/workspace/projects', icon: Bookmark },
] as const;

export function SocialSidebar(): React.JSX.Element {
  const pathname = usePathname() ?? '/social';
  const searchParams = useSearchParams();
  const activeTab = searchParams.get('tab');

  function openComposer(): void {
    window.dispatchEvent(new CustomEvent('social:compose'));
    window.requestAnimationFrame(() => {
      document.getElementById('social-composer')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    });
  }

  return (
    <Sidebar
      collapsible="icon"
      className="h-svh border-white/10 bg-[#070c18] text-slate-200 [&_[data-slot=sidebar-inner]]:bg-[#070c18]"
    >
      <SidebarHeader className="border-b border-white/10 p-3">
        <div className="flex min-h-10 items-center gap-2 group-data-[collapsible=icon]:flex-col group-data-[collapsible=icon]:gap-1">
          <Link
            href="/social"
            aria-label="GameGuild Social home"
            className="flex min-w-0 flex-1 items-center gap-3 overflow-hidden rounded-xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sky-300 group-data-[collapsible=icon]:flex-none"
          >
            <span className="flex size-9 shrink-0 items-center justify-center rounded-xl border border-white/15 bg-white text-slate-950 shadow-sm">
              <GraduationCap className="size-5" aria-hidden="true" />
            </span>
            <span className="grid min-w-0 leading-tight group-data-[collapsible=icon]:hidden">
              <span className="truncate text-sm font-semibold text-white">GameGuild</span>
              <span className="truncate text-xs text-slate-400">Social</span>
            </span>
          </Link>
          <SidebarTrigger className="ml-auto size-8 shrink-0 rounded-lg text-slate-400 hover:bg-white/[0.06] hover:text-white group-data-[collapsible=icon]:ml-0" />
        </div>
      </SidebarHeader>

      <SidebarContent className="px-2 py-3">
        <SidebarMenu aria-label="Social navigation" className="gap-1.5">
          {socialNavigation.map(({ label, href, icon: Icon, ...item }) => {
            const active =
              (label === 'Home' && pathname === '/social' && !activeTab) ||
              (label === 'Playtests' && pathname === '/social' && activeTab === 'playtests') ||
              (label === 'Explore' && pathname.startsWith('/projects')) ||
              (label === 'Messages' && pathname.startsWith('/workspace/invitations')) ||
              (label === 'Saved' && pathname.startsWith('/workspace/projects'));
            return (
              <SidebarMenuItem key={label}>
                <SidebarMenuButton
                  asChild
                  size="lg"
                  isActive={active}
                  tooltip={label}
                  className={cn(
                    'h-11 rounded-xl px-3 text-sm font-semibold text-slate-300 transition-colors hover:bg-white/[0.05] hover:text-white data-active:bg-sky-400/10 data-active:text-sky-300 [&_svg]:size-5',
                  )}
                >
                  <Link href={href} aria-current={active ? 'page' : undefined}>
                    <Icon strokeWidth={1.8} aria-hidden="true" />
                    <span className="group-data-[collapsible=icon]:hidden">{label}</span>
                  </Link>
                </SidebarMenuButton>
                {'badge' in item ? (
                  <SidebarMenuBadge className="bg-violet-500/25 text-[10px] font-bold text-violet-200">
                    {item.badge}
                  </SidebarMenuBadge>
                ) : null}
              </SidebarMenuItem>
            );
          })}

          <SidebarMenuItem>
            <SidebarMenuButton
              asChild
              size="lg"
              tooltip="Workspace"
              className="h-11 rounded-xl px-3 text-sm font-semibold text-slate-300 hover:bg-white/[0.05] hover:text-white [&_svg]:size-5"
            >
              <Link href="/workspace">
                <UsersRound strokeWidth={1.8} aria-hidden="true" />
                <span className="group-data-[collapsible=icon]:hidden">Workspace</span>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarContent>

      <SidebarFooter className="border-t border-white/10 p-3">
        <button
          type="button"
          onClick={openComposer}
          className="flex h-11 w-full items-center justify-center gap-2 overflow-hidden rounded-xl bg-sky-400 px-3 text-sm font-bold text-slate-950 shadow-[0_12px_28px_rgba(56,189,248,0.18)] transition hover:bg-sky-300 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sky-200 focus-visible:ring-offset-2 focus-visible:ring-offset-[#070c18] group-data-[collapsible=icon]:size-10 group-data-[collapsible=icon]:px-0"
        >
          <Plus className="size-5 shrink-0" aria-hidden="true" />
          <span className="group-data-[collapsible=icon]:hidden">Create</span>
        </button>
      </SidebarFooter>
      <SidebarRail />
    </Sidebar>
  );
}
