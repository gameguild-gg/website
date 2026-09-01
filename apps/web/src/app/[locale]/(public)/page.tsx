import { auth } from '@/auth';
import { publicWebsiteHighlights } from '@/components/app/app-shell';
import { isSocialFeedTab } from '@/components/feed/social-feed-tabs';
import { Link, redirect } from '@/i18n/navigation';
import {
  getPublicActivities,
  getPublicMemberSpotlights,
  getPublicPlaytests,
} from '@/lib/community/public-community-queries';
import { getPublishedProjects } from '@/lib/projects/public-projects';
import { ArrowRight, CalendarDays, MessageSquare, Sparkles, Users } from 'lucide-react';
import React from 'react';

/** `/` is contextual: the community feed when signed in, the marketing landing otherwise. */
export default async function Page({ params, searchParams }: PageProps<'/[locale]'>): Promise<React.JSX.Element> {
  const [{ locale }, query, session] = await Promise.all([params, searchParams, auth()]);
  if (session && typeof session !== 'function') {
    const rawTab = typeof query?.tab === 'string' && isSocialFeedTab(query.tab) ? query.tab : undefined;
    redirect({ href: rawTab ? `/social?tab=${rawTab}` : '/social', locale });
    throw new Error('Authenticated home redirect');
  }

  const [latestProjects, memberSpotlights, playtests, activities] = await Promise.all([
    getPublishedProjects().then((projects) => projects.slice(0, 3)),
    getPublicMemberSpotlights(),
    getPublicPlaytests(),
    getPublicActivities(),
  ]);

  return (
    <main className="bg-slate-950 text-white">
      <section className="relative overflow-hidden">
        <div className="absolute inset-x-0 top-[-20%] h-96 bg-[radial-gradient(circle_at_center,rgba(56,189,248,0.18),transparent_58%)]" />
        <div className="mx-auto grid w-full max-w-7xl items-center gap-12 px-4 py-20 sm:px-6 sm:py-24 lg:grid-cols-[1.05fr_0.95fr] lg:px-8 lg:py-28">
          <div className="relative z-10 max-w-3xl space-y-8">
            <div className="space-y-5">
              <h1 className="max-w-4xl text-balance text-5xl font-semibold tracking-tight text-white sm:text-6xl lg:text-7xl">
                Learn, Build & Connect
              </h1>
              <p className="max-w-2xl text-lg leading-8 text-slate-300 sm:text-xl">
                Master game development through practical courses, community critique, testing workflows, and launch
                support designed for builders who want to ship.
              </p>
            </div>

            <div className="flex flex-col gap-3 sm:flex-row">
              <Link
                href="/courses"
                className="inline-flex items-center justify-center rounded-full bg-sky-300 px-5 py-3 text-sm font-semibold text-slate-950 transition hover:bg-sky-200"
              >
                Start Learning
                <ArrowRight className="ml-2 size-4" aria-hidden="true" />
              </Link>
              <Link
                href="/courses"
                className="inline-flex items-center justify-center rounded-full border border-white/15 px-5 py-3 text-sm font-semibold text-white transition hover:border-white/30 hover:bg-white/10"
              >
                Explore Programs
              </Link>
            </div>
          </div>

          <div className="relative z-10">
            <div className="rounded-[2rem] border border-white/10 bg-white/[0.04] p-4 shadow-2xl shadow-sky-950/40 backdrop-blur">
              <div className="rounded-[1.5rem] border border-white/10 bg-slate-900/90 p-5">
                <div className="mb-6 flex items-center justify-between">
                  <div>
                    <p className="text-sm font-medium text-slate-400">Learning path</p>
                    <h2 className="mt-1 text-2xl font-semibold text-white">From course to shipped project</h2>
                  </div>
                  <Sparkles className="size-6 text-sky-300" aria-hidden="true" />
                </div>

                <div className="space-y-3">
                  {['Study core systems', 'Build a playable prototype', 'Test with peers', 'Prepare launch assets'].map(
                    (step, index) => (
                      <div
                        key={step}
                        className="flex items-center gap-3 rounded-2xl border border-white/10 bg-white/[0.03] px-4 py-3"
                      >
                        <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-sky-300/15 text-sm font-semibold text-sky-200">
                          {index + 1}
                        </span>
                        <span className="text-sm font-medium text-slate-200">{step}</span>
                      </div>
                    ),
                  )}
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      <section className="border-y border-white/10 bg-white/[0.03]">
        <div className="mx-auto w-full max-w-7xl px-4 py-16 sm:px-6 lg:px-8">
          <div className="max-w-2xl space-y-3">
            <h2 className="text-3xl font-semibold tracking-tight text-white sm:text-4xl">
              Everything You Need to Succeed
            </h2>
            <p className="text-base leading-7 text-slate-400">
              A compact ecosystem for building game skills, validating work, and moving from learning into public launch
              with less friction.
            </p>
          </div>

          <div className="mt-10 grid gap-4 md:grid-cols-3">
            {publicWebsiteHighlights.map((feature) => {
              const Icon = feature.icon;

              return (
                <article key={feature.title} className="rounded-3xl border border-white/10 bg-slate-900/70 p-6">
                  <div className="mb-6 flex size-11 items-center justify-center rounded-2xl bg-sky-300/10 text-sky-200">
                    <Icon className="size-5" aria-hidden="true" />
                  </div>
                  <h3 className="text-lg font-semibold text-white">{feature.title}</h3>
                  <p className="mt-3 text-sm leading-6 text-slate-400">{feature.description}</p>
                </article>
              );
            })}
          </div>
        </div>
      </section>

      <section className="bg-slate-950">
        <div className="mx-auto grid w-full max-w-7xl gap-10 px-4 py-16 sm:px-6 lg:grid-cols-[0.8fr_1.2fr] lg:px-8">
          <div className="max-w-xl space-y-4">
            <h2 className="text-3xl font-semibold tracking-tight text-white sm:text-4xl">Latest projects</h2>
            <p className="text-base leading-7 text-slate-400">
              GameGuild is built around visible work. Browse recently updated student projects, join playtests, and see
              how course outcomes become public portfolio evidence.
            </p>
            <Link
              href="/projects"
              className="inline-flex items-center rounded-full border border-white/15 px-4 py-2 text-sm font-semibold text-white transition hover:bg-white/10"
            >
              View project showcase
              <ArrowRight className="ml-2 size-4" aria-hidden="true" />
            </Link>
          </div>

          <div className="grid gap-4 md:grid-cols-3">
            {latestProjects.length === 0 ? (
              <p className="text-sm text-slate-400">Community projects will appear here soon.</p>
            ) : (
              latestProjects.map((project) => (
              <Link
                key={project.slug}
                href={`/projects/${project.slug}`}
                className="group overflow-hidden rounded-3xl border border-white/10 bg-slate-900/70 transition hover:-translate-y-1 hover:border-white/20"
              >
                <div className={`h-32 bg-gradient-to-br ${project.accent}`} />
                <div className="space-y-4 p-5">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.18em] text-sky-200">{project.status}</p>
                    <h3 className="mt-2 text-lg font-semibold text-white">{project.title}</h3>
                  </div>
                  <p className="line-clamp-3 text-sm leading-6 text-slate-400">{project.summary}</p>
                  <span className="inline-flex items-center text-sm font-semibold text-sky-200">
                    View project
                    <ArrowRight className="ml-2 size-4 transition group-hover:translate-x-1" aria-hidden="true" />
                  </span>
                </div>
              </Link>
              ))
            )}
          </div>
        </div>
      </section>

      <section className="border-y border-white/10 bg-white/[0.03]">
        <div className="mx-auto grid w-full max-w-7xl gap-6 px-4 py-16 sm:px-6 lg:grid-cols-3 lg:px-8">
          <div className="rounded-3xl border border-white/10 bg-slate-900/70 p-6">
            <div className="mb-5 flex items-center gap-3 text-sky-200">
              <Users className="size-5" aria-hidden="true" />
              <h2 className="text-xl font-semibold text-white">Active members</h2>
            </div>
            <div className="space-y-4">
              {memberSpotlights.length === 0 ? (
                <p className="text-sm leading-6 text-slate-400">Member spotlights will appear here as projects are published.</p>
              ) : (
                memberSpotlights.map((member) => (
                <div key={member.handle} className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                  <p className="font-semibold text-white">{member.name}</p>
                  <p className="text-sm text-slate-400">{member.role} - {member.focus}</p>
                </div>
                ))
              )}
            </div>
          </div>

          <div className="rounded-3xl border border-white/10 bg-slate-900/70 p-6">
            <div className="mb-5 flex items-center gap-3 text-sky-200">
              <CalendarDays className="size-5" aria-hidden="true" />
              <h2 className="text-xl font-semibold text-white">Upcoming playtests</h2>
            </div>
            <div className="space-y-4">
              {playtests.length === 0 ? (
                <p className="text-sm leading-6 text-slate-400">Playtest sessions will appear here once scheduled.</p>
              ) : (
                playtests.map((playtest) => (
                  <Link key={playtest.href} href={playtest.href} className="block rounded-2xl border border-white/10 bg-white/[0.03] p-4 transition hover:border-white/20">
                    <p className="font-semibold text-white">{playtest.title}</p>
                    <p className="text-sm text-slate-400">{playtest.date} - {playtest.seats}</p>
                  </Link>
                ))
              )}
            </div>
          </div>

          <div className="rounded-3xl border border-white/10 bg-slate-900/70 p-6">
            <div className="mb-5 flex items-center gap-3 text-sky-200">
              <MessageSquare className="size-5" aria-hidden="true" />
              <h2 className="text-xl font-semibold text-white">Community activity</h2>
            </div>
            <div className="space-y-4">
              {activities.length === 0 ? (
                <p className="text-sm leading-6 text-slate-400">Community activity will appear here as projects are updated.</p>
              ) : (
                activities.map((activity) => (
                  <Link key={`${activity.actor}-${activity.target}`} href={activity.href} className="block rounded-2xl border border-white/10 bg-white/[0.03] p-4 transition hover:border-white/20">
                    <p className="text-sm leading-6 text-slate-300">
                      <span className="font-semibold text-white">{activity.actor}</span> {activity.action}{' '}
                      <span className="font-semibold text-sky-200">{activity.target}</span>
                    </p>
                  </Link>
                ))
              )}
            </div>
          </div>
        </div>
      </section>
    </main>
  );
}
