import { Link } from "@/i18n/navigation";
import type {
  SocialCreatorPreview,
  SocialPlaytestPreview,
} from "@/lib/posts/demo";
import { ArrowRight, CalendarDays, Users } from "lucide-react";

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
  return (
    <aside className="sticky top-0 hidden h-fit space-y-5 py-6 xl:block">
      <section className="rounded-2xl border border-white/10 bg-[#0e1422]/80 p-5">
        <div className="flex items-center justify-between gap-3">
          <h2 className="flex items-center gap-2 text-sm font-semibold text-white">
            <CalendarDays
              className="size-4 text-[#48c7ff]"
              aria-hidden="true"
            />
            Upcoming playtests
          </h2>
          <Link
            href="/testing-lab"
            className="text-xs font-medium text-[#7dd3fc] hover:text-white"
          >
            View all
          </Link>
        </div>
        <div className="mt-4 divide-y divide-white/10">
          {playtests.length === 0 ? (
            <p className="py-4 text-sm leading-6 text-slate-400">
              New sessions will appear here when registrations open.
            </p>
          ) : (
            playtests.map((playtest) => (
              <Link
                key={`${playtest.title}-${playtest.date}`}
                href={playtest.href}
                className="group block py-3 first:pt-0 last:pb-0"
              >
                <p className="text-sm font-semibold text-slate-100 transition-colors group-hover:text-[#7dd3fc]">
                  {playtest.title}
                </p>
                <p className="mt-1 text-xs text-slate-400">{playtest.detail}</p>
                <p className="mt-1 text-xs text-slate-500">{playtest.date}</p>
              </Link>
            ))
          )}
        </div>
      </section>

      <section className="rounded-2xl border border-white/10 bg-[#0e1422]/80 p-5">
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
          {creators.length === 0 ? (
            <p className="text-sm leading-6 text-slate-400">
              Creators with published projects will appear here.
            </p>
          ) : (
            creators.map((creator) => (
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

      <Link
        href="/workspace"
        className="flex items-center justify-between rounded-xl border border-white/10 px-4 py-3 text-sm font-medium text-slate-300 transition-colors hover:border-white/20 hover:bg-white/[0.04] hover:text-white"
      >
        Open your workspace
        <ArrowRight className="size-4" aria-hidden="true" />
      </Link>
    </aside>
  );
}
