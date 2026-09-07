"use client";

import { Link, usePathname } from "@/i18n/navigation";
import { cn } from "@game-guild/ui/lib/utils";
import {
  BarChart3,
  BookOpenCheck,
  CalendarDays,
  ClipboardList,
  LayoutDashboard,
  UsersRound,
} from "lucide-react";

const items = [
  { label: "Overview", segment: "overview", icon: LayoutDashboard },
  { label: "Applications", segment: "applications", icon: ClipboardList },
  { label: "Schedule", segment: "schedule", icon: CalendarDays },
  { label: "Testers", segment: "testers", icon: UsersRound },
  { label: "Feedback", segment: "feedback", icon: BarChart3 },
  { label: "Learning", segment: "learning", icon: BookOpenCheck },
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
      className="grid grid-cols-2 gap-1 rounded-md border bg-muted/20 p-1 sm:grid-cols-3 xl:grid-cols-6"
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
                "flex min-h-10 items-center justify-center gap-2 rounded-sm px-3 py-2 text-sm font-medium text-muted-foreground transition-colors",
                "hover:bg-background hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                active && "bg-background text-foreground shadow-sm",
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
