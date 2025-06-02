'use client';

import { usePathname } from 'next/navigation';
import Link from 'next/link';
import { Calendar, CastleIcon as ChessKnight, Home, List, Play, RotateCcw, Swords, Trophy, Upload } from 'lucide-react';
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarTrigger,
  useSidebar,
} from '@/components/chess/ui/sidebar';

const navigationItems = [
  {
    name: 'Summary',
    href: '/chess',
    icon: Home,
  },
  {
    name: 'Leaderboard',
    href: '/chess/leaderboard',
    icon: Trophy,
  },
  {
    name: 'Submit',
    href: '/chess/submit',
    icon: Upload,
  },
  {
    name: 'Matches',
    href: '/chess/matches',
    icon: List,
  },
  {
    name: 'Play',
    href: '/chess/play',
    icon: Play,
  },
  {
    name: 'Replay',
    href: '/chess/replay',
    icon: RotateCcw,
  },
  {
    name: 'Challenge',
    href: '/chess/challenge',
    icon: Swords,
  },
  {
    name: 'Tournament',
    href: '/chess/tournament',
    icon: Calendar,
  },
];

export function SideNavigation() {
  const pathname = usePathname();
  const { state, isMobile } = useSidebar();
  const isCollapsed = state === 'collapsed';

  return (
    <Sidebar>
      <SidebarHeader className="flex items-center justify-between p-4">
        {!isCollapsed ? (
          <h2 className="text-xl font-bold">Chess Bot</h2>
        ) : (
          <div className="flex justify-center w-full">
            <ChessKnight className="h-6 w-6" />
          </div>
        )}
        {/* Only show the trigger button on non-mobile devices */}
        {!isMobile && <SidebarTrigger className={isCollapsed ? 'ml-0' : 'ml-auto'} />}
      </SidebarHeader>
      <SidebarContent>
        <SidebarMenu>
          {navigationItems.map((item) => {
            const isActive = pathname === item.href;
            return (
              <SidebarMenuItem key={item.name}>
                <SidebarMenuButton asChild isActive={isActive} tooltip={isCollapsed ? item.name : undefined}>
                  <Link href={item.href} className={`flex items-center ${isCollapsed ? 'justify-center' : ''}`}>
                    <item.icon className="h-5 w-5" />
                    {!isCollapsed && <span>{item.name}</span>}
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
            );
          })}
        </SidebarMenu>
      </SidebarContent>
      <SidebarFooter className="p-4">
        {!isCollapsed && <div className="text-xs text-muted-foreground text-center">Chess Bot Competition Platform</div>}
      </SidebarFooter>
    </Sidebar>
  );
}
