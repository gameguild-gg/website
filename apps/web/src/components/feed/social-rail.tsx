import { Link } from "@/i18n/navigation";
import type {
  SocialCreatorPreview,
  SocialPlaytestPreview,
} from "@/lib/posts/demo";
import { CalendarDays, Users } from "lucide-react";

function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join("")
    .toUpperCase();
}

export function SocialRail({
  playtests,
  creators,
}: {
  playtests: SocialPlaytestPreview[];
  creators: SocialCreatorPreview[];
}): React.JSX.Element {
  const featuredCreator = creators[0] ?? null;
  const suggestedCreators = creators.slice(1, 4);

  return (
    <aside className="sticky top-5 hidden h-fit space-y-3 xl:block">
      {featuredCreator ? (
        <section className="rounded-xl bg-card p-4 text-card-foreground">
          <div className="flex items-start gap-3">
            <span className="flex size-14 shrink-0 items-center justify-center rounded-full border border-highlight/50 bg-highlight/15 text-sm font-bold text-foreground">
              {initials(featuredCreator.name)}
            </span>
            <div className="min-w-0 flex-1 pt-0.5">
              <p className="truncate text-sm font-semibold text-foreground">{featuredCreator.name}</p>
              <p className="truncate text-xs text-muted-foreground">{featuredCreator.handle}</p>
              <p className="mt-1 line-clamp-2 text-xs leading-4 text-muted-foreground">{featuredCreator.focus}</p>
            </div>
          </div>
          <div className="mt-4 grid grid-cols-3 divide-x divide-border text-center">
            {[
              ["128", "Posts"],
              ["2.4K", "Followers"],
              ["312", "Following"],
            ].map(([value, label]) => (
              <div key={label}>
                <p className="text-sm font-semibold text-foreground">{value}</p>
                <p className="mt-0.5 text-[10px] text-muted-foreground/70">{label}</p>
              </div>
            ))}
          </div>
          <Link
            href="/workspace/settings/profile"
            className="mt-4 flex h-9 items-center justify-center rounded-lg bg-accent/50 text-xs font-semibold text-accent-foreground transition hover:bg-accent hover:text-foreground"
          >
            View profile
          </Link>
        </section>
      ) : null}

      <section className="rounded-xl bg-card p-4 text-card-foreground">
        <div className="flex items-center justify-between gap-3">
          <h2 className="flex items-center gap-2 text-sm font-semibold text-foreground">
            <CalendarDays
              className="size-4 text-primary"
              aria-hidden="true"
            />
            Upcoming community events
          </h2>
          <Link
            href="/testing-lab"
            className="text-xs font-medium text-primary hover:text-foreground"
          >
            View all
          </Link>
        </div>
        <div className="mt-3 divide-y divide-border">
          {playtests.length === 0 ? (
            <p className="py-4 text-sm leading-6 text-muted-foreground">
              New community events will appear here when registrations open.
            </p>
          ) : (
            playtests.map((playtest) => (
              <Link
                key={`${playtest.title}-${playtest.date}`}
                href={playtest.href}
                className="group flex items-center gap-3 py-3 first:pt-0 last:pb-0"
              >
                <span className="flex size-11 shrink-0 items-center justify-center rounded-lg border border-border bg-accent/50 text-[10px] font-bold text-primary">
                  {initials(playtest.title)}
                </span>
                <span className="min-w-0">
                  <span className="block truncate text-sm font-semibold text-foreground transition-colors group-hover:text-primary">
                    {playtest.title}
                  </span>
                  <span className="mt-0.5 block truncate text-xs text-muted-foreground">{playtest.detail}</span>
                  <span className="mt-1 block text-xs text-muted-foreground/70">{playtest.date}</span>
                </span>
              </Link>
            ))
          )}
        </div>
      </section>

      <section className="rounded-xl bg-card p-4 text-card-foreground">
        <div className="flex items-center justify-between gap-3">
          <h2 className="flex items-center gap-2 text-sm font-semibold text-foreground">
            <Users className="size-4 text-highlight" aria-hidden="true" />
            Suggested creators
          </h2>
          <Link
            href="/console/community/members/users"
            className="text-xs font-medium text-highlight hover:text-foreground"
          >
            View all
          </Link>
        </div>
        <div className="mt-4 space-y-4">
          {suggestedCreators.length === 0 ? (
            <p className="text-sm leading-6 text-muted-foreground">
              Creators with published projects will appear here.
            </p>
          ) : (
            suggestedCreators.map((creator) => (
              <div key={creator.handle} className="flex items-center gap-3">
                <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-primary/25 to-highlight/25 text-[11px] font-bold text-foreground">
                  {initials(creator.name)}
                </span>
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-semibold text-foreground">
                    {creator.name}
                  </p>
                  <p className="truncate text-xs text-muted-foreground/70">
                    {creator.handle} · {creator.focus}
                  </p>
                </div>
                <button
                  type="button"
                  className="rounded-lg border border-border px-2.5 py-1.5 text-xs font-semibold text-primary transition-colors hover:border-primary/40 hover:bg-primary/10"
                >
                  Follow
                </button>
              </div>
            ))
          )}
        </div>
      </section>

      <section className="rounded-xl bg-card p-4 text-card-foreground">
        <div className="flex items-center justify-between gap-3">
          <h2 className="text-sm font-semibold text-foreground">Trending tags</h2>
          <Link href="/projects" className="text-xs font-medium text-highlight hover:text-foreground">
            View all
          </Link>
        </div>
        <div className="mt-3 grid grid-cols-2 gap-2">
          {["#indiedev", "#playtesting", "#madewithunity", "#gamedev"].map((tag) => (
            <Link
              key={tag}
              href="/projects"
              className="rounded-lg bg-accent/50 px-3 py-2 text-xs font-medium text-muted-foreground transition hover:bg-accent hover:text-foreground"
            >
              {tag}
            </Link>
          ))}
        </div>
      </section>
    </aside>
  );
}
