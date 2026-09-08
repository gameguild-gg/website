"use client";

import { Link, usePathname } from "@/i18n/navigation";
import { cn } from "@game-guild/ui/lib/utils";
import { BarChart3, Files, MapPin, Settings, ShieldCheck } from "lucide-react";

const sections = [
  {
    label: "General",
    href: "/workspace/testing-lab/settings/general",
    icon: Settings,
    requiredCapabilities: ["TestingLab.ManageSettings"],
  },
  {
    label: "Templates",
    href: "/workspace/testing-lab/settings/templates",
    icon: Files,
    requiredCapabilities: ["TestingLab.ManageSettings"],
  },
  {
    label: "Locations",
    href: "/workspace/testing-lab/settings/locations",
    icon: MapPin,
    requiredCapabilities: ["TestingLab.ManageSettings"],
  },
  {
    label: "Analytics",
    href: "/workspace/testing-lab/settings/analytics",
    icon: BarChart3,
    requiredCapabilities: ["TestingLab.ViewAnalytics"],
  },
  {
    label: "Access",
    href: "/workspace/testing-lab/settings/access",
    icon: ShieldCheck,
    requiredCapabilities: ["TestingLab.ManageSettings"],
  },
] as const;

export function TestingLabSettingsNav({
  capabilities,
}: {
  capabilities?: readonly string[];
} = {}) {
  const pathname = usePathname() ?? "";
  const visibleSections = capabilities
    ? sections.filter((section) =>
        section.requiredCapabilities.some((capability) =>
          capabilities.includes(capability),
        ),
      )
    : sections;

  return (
    <nav
      aria-label="Testing Lab settings"
      className="grid grid-cols-2 gap-1 sm:grid-cols-5 lg:sticky lg:top-20 lg:grid-cols-1"
    >
      {visibleSections.map((section) => {
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
