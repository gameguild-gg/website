"use client";

import { Link, usePathname } from "@/i18n/navigation";
import { cn } from "@game-guild/ui/lib/utils";
import {
  BarChart3,
  CalendarDays,
  ClipboardList,
  FolderKanban,
  LayoutDashboard,
  UsersRound,
} from "lucide-react";

const items = [
  { label: "Overview", segment: "overview", icon: LayoutDashboard },
  { label: "Schedule", segment: "schedule", icon: CalendarDays },
  { label: "Applications", segment: "applications", icon: ClipboardList },
  { label: "Projects", segment: "projects", icon: FolderKanban },
  { label: "Participants", segment: "participants", icon: UsersRound },
  { label: "Feedback", segment: "feedback", icon: BarChart3 },
] as const;

export function TestingEventWorkspaceNav({
  eventId,
  canManageWorkspace = true,
}: {
  eventId: string;
  canManageWorkspace?: boolean;
}) {
  const pathname = usePathname() ?? "";
  const base = `/workspace/testing-lab/events/${eventId}`;

  return (
    <nav
      aria-label="Testing event workspace"
      className="flex min-w-0 gap-1 overflow-x-auto border-b"
    >
      {items
        .filter((item) => canManageWorkspace || item.segment === "applications")
        .map((item) => {
          const href = `${base}/${item.segment}`;
          const active = pathname === href || pathname.startsWith(`${href}/`);
          const Icon = item.icon;

          return (
            <Link
              key={item.segment}
              href={href}
              aria-current={active ? "page" : undefined}
              className={cn(
                "relative flex min-h-11 shrink-0 items-center justify-center gap-2 px-3 py-2 text-sm font-medium text-muted-foreground transition-colors",
                "hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring",
                active &&
                  "text-foreground after:absolute after:inset-x-2 after:bottom-0 after:h-0.5 after:rounded-full after:bg-primary",
              )}
            >
              <Icon className="size-4 shrink-0" />
              <span>{item.label}</span>
            </Link>
          );
        })}
    </nav>
  );
}
