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
  const suggestedCreators = creators.slice(0, 3);

  return (
    <aside className="sticky top-0 hidden h-fit space-y-3 xl:block">
      <section className="rounded-xl border border-white/10 bg-[#0b1220] p-4">
        <div className="flex items-center justify-between gap-3">
          <h2 className="flex items-center gap-2 text-sm font-semibold text-white">
            <CalendarDays
              className="size-4 text-[#48c7ff]"
              aria-hidden="true"
            />
            Upcoming community events
          </h2>
          <Link
            href="/testing-lab"
            className="text-xs font-medium text-[#7dd3fc] hover:text-white"
          >
            View all
          </Link>
        </div>
        <div className="mt-3 divide-y divide-white/10">
          {playtests.length === 0 ? (
            <p className="py-4 text-sm leading-6 text-slate-400">
              New community events will appear here when registrations open.
            </p>
          ) : (
            playtests.map((playtest) => (
              <Link
                key={`${playtest.title}-${playtest.date}`}
                href={playtest.href}
                className="group flex items-center gap-3 py-3 first:pt-0 last:pb-0"
              >
                <span className="flex size-11 shrink-0 items-center justify-center rounded-lg border border-white/10 bg-white/[0.04] text-[10px] font-bold text-sky-200">
                  {initials(playtest.title)}
                </span>
                <span className="min-w-0">
                  <span className="block truncate text-sm font-semibold text-slate-100 transition-colors group-hover:text-[#7dd3fc]">
                    {playtest.title}
                  </span>
                  <span className="mt-0.5 block truncate text-xs text-slate-400">{playtest.detail}</span>
                  <span className="mt-1 block text-xs text-slate-500">{playtest.date}</span>
                </span>
              </Link>
            ))
          )}
        </div>
      </section>

      <section className="rounded-xl border border-white/10 bg-[#0b1220] p-4">
        <div className="flex items-center justify-between gap-3">
          <h2 className="flex items-center gap-2 text-sm font-semibold text-white">
            <Users className="size-4 text-[#a78bfa]" aria-hidden="true" />
            Suggested creators
          </h2>
          <Link
            href="/console/community/members/users"
            className="text-xs font-medium text-[#c4b5fd] hover:text-white"
          >
            View all
          </Link>
        </div>
        <div className="mt-4 space-y-4">
          {suggestedCreators.length === 0 ? (
            <p className="text-sm leading-6 text-slate-400">
              Creators with published projects will appear here.
            </p>
          ) : (
            suggestedCreators.map((creator) => (
              <div key={creator.handle} className="flex items-center gap-3">
                <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-[#48c7ff]/25 to-[#8b5cf6]/25 text-[11px] font-bold text-white">
                  {initials(creator.name)}
                </span>
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-semibold text-slate-100">
                    {creator.name}
                  </p>
                  <p className="truncate text-xs text-slate-500">
                    {creator.handle} · {creator.focus}
                  </p>
                </div>
                <button
                  type="button"
                  className="rounded-lg border border-white/10 px-2.5 py-1.5 text-xs font-semibold text-[#7dd3fc] transition-colors hover:border-[#48c7ff]/40 hover:bg-[#48c7ff]/10"
                >
                  Follow
                </button>
              </div>
            ))
          )}
        </div>
      </section>

      <section className="rounded-xl border border-white/10 bg-[#0b1220] p-4">
        <div className="flex items-center justify-between gap-3">
          <h2 className="text-sm font-semibold text-white">Trending tags</h2>
          <Link href="/projects" className="text-xs font-medium text-violet-300 hover:text-white">
            View all
          </Link>
        </div>
        <div className="mt-3 grid grid-cols-2 gap-2">
          {["#indiedev", "#playtesting", "#madewithunity", "#gamedev"].map((tag) => (
            <Link
              key={tag}
              href="/projects"
              className="rounded-lg border border-white/10 px-3 py-2 text-xs font-medium text-slate-300 transition hover:border-violet-400/30 hover:bg-violet-500/10 hover:text-white"
            >
              {tag}
            </Link>
          ))}
        </div>
      </section>
    </aside>
  );
}
