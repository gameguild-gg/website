import { Link } from "@/i18n/navigation";
import { cn } from "@game-guild/ui/lib/utils";

export const SOCIAL_FEED_TABS = [
  { id: "foryou", label: "For you" },
  { id: "following", label: "Following" },
  { id: "playtests", label: "Playtests" },
] as const;

export type SocialFeedTab = (typeof SOCIAL_FEED_TABS)[number]["id"];

export function isSocialFeedTab(
  value: string | undefined,
): value is SocialFeedTab {
  return SOCIAL_FEED_TABS.some((tab) => tab.id === value);
}

export function SocialFeedTabs({
  active,
}: {
  active: SocialFeedTab;
}): React.JSX.Element {
  return (
    <nav
      aria-label="Social feed"
      className="sticky top-16 z-20 flex h-14 items-end gap-8 border-b border-white/10 bg-[#070a12]/95 px-4 backdrop-blur-xl sm:px-6"
    >
      {SOCIAL_FEED_TABS.map((tab) => {
        const isActive = tab.id === active;
        return (
          <Link
            key={tab.id}
            href={tab.id === "foryou" ? "/social" : `/social?tab=${tab.id}`}
            aria-current={isActive ? "page" : undefined}
            className={cn(
              "relative flex h-full items-center text-sm font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#48c7ff]",
              isActive ? "text-white" : "text-slate-400 hover:text-slate-100",
            )}
          >
            {tab.label}
            {isActive ? (
              <span className="absolute inset-x-0 bottom-0 h-0.5 rounded-full bg-[#48c7ff]" />
            ) : null}
          </Link>
        );
      })}
    </nav>
  );
}
