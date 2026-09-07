"use client";

import { Link, usePathname } from "@/i18n/navigation";
import { cn } from "@game-guild/ui/lib/utils";
import { Files, MapPin, Settings, ShieldCheck } from "lucide-react";

const sections = [
  {
    label: "General",
    href: "/workspace/testing-lab/settings/general",
    icon: Settings,
  },
  {
    label: "Templates",
    href: "/workspace/testing-lab/settings/templates",
    icon: Files,
  },
  {
    label: "Locations",
    href: "/workspace/testing-lab/settings/locations",
    icon: MapPin,
  },
  {
    label: "Access",
    href: "/workspace/testing-lab/settings/access",
    icon: ShieldCheck,
  },
] as const;

export function TestingLabSettingsNav() {
  const pathname = usePathname() ?? "";

  return (
    <nav
      aria-label="Testing Lab settings"
      className="grid grid-cols-2 gap-1 sm:grid-cols-4 lg:sticky lg:top-20 lg:grid-cols-1"
    >
      {sections.map((section) => {
        const Icon = section.icon;
        const current =
          pathname === section.href || pathname.startsWith(section.href + "/");

        return (
          <Link
            key={section.href}
            href={section.href}
            aria-current={current ? "page" : undefined}
            className={cn(
              "flex h-10 min-w-0 items-center gap-2 rounded-sm px-3 text-sm font-medium transition-colors",
              current
                ? "bg-muted text-foreground"
                : "text-muted-foreground hover:bg-muted/60 hover:text-foreground",
            )}
          >
            <Icon aria-hidden="true" className="size-4 shrink-0" />
            <span className="truncate">{section.label}</span>
          </Link>
        );
      })}
    </nav>
  );
}
