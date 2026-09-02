'use client';

import { Link } from '@/i18n/navigation';
import { BarChart3, CalendarDays, ClipboardCheck, FolderKanban, LayoutDashboard, Settings, Users } from 'lucide-react';

const operations = [
  {
    href: '/console/community/testing-lab',
    label: 'Overview',
    Icon: LayoutDashboard,
  },
  {
    href: '/console/community/testing-lab/events',
    label: 'Events',
    Icon: CalendarDays,
  },
  {
    href: '/console/community/testing-lab/applications',
    label: 'Applications',
    Icon: ClipboardCheck,
  },
  {
    href: '/console/community/testing-lab/projects',
    label: 'Projects',
    Icon: FolderKanban,
  },
  {
    href: '/console/community/testing-lab/participants',
    label: 'Participants',
    Icon: Users,
  },
  {
    href: '/console/community/testing-lab/analytics',
    label: 'Analytics',
    Icon: BarChart3,
  },
  {
    href: '/console/community/testing-lab/settings/general',
    label: 'Settings',
    Icon: Settings,
  },
] as const;

export function TestingLabOperationsNavigation({ activeHref }: { activeHref?: string }) {
  return (
    <div className="-mx-1 overflow-x-auto px-1 pb-1">
      <nav aria-label="Testing Lab operations" className="flex min-w-max items-center gap-1">
        {operations.map(({ href, label, Icon }) => {
          const active = activeHref === href;
          return (
            <Link
              key={href}
              href={href}
              aria-current={active ? 'page' : undefined}
              className={`inline-flex min-h-11 shrink-0 items-center gap-2 rounded-md px-3 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${
                active ? 'bg-muted text-foreground' : 'text-muted-foreground hover:bg-muted/50 hover:text-foreground'
              }`}
            >
              <Icon className="size-4" aria-hidden="true" />
              <span>{label}</span>
            </Link>
          );
        })}
      </nav>
    </div>
  );
}
