import { Link } from '@/i18n/navigation';
import { ArrowRight, BookOpen, Code2, GitBranch, HeartHandshake, KanbanSquare, MessagesSquare, ShieldCheck } from 'lucide-react';
import React from 'react';
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'About GameGuild',
  description:
    'Who builds GameGuild, why the platform exists, the technology behind it, and how to reach the team behind the game-creator community.',
  robots: { index: true, follow: true },
};

const pillars = [
  {
    icon: BookOpen,
    title: 'Learn by building',
    description:
      'Courses, tracks, and programs are built around real projects. Enrollments, progress, and completion certificates live next to the work they certify.',
  },
  {
    icon: Code2,
    title: 'Create in the browser',
    description:
      'An in-browser code editor and testing lab let creators prototype, run, and share work without leaving the platform.',
  },
  {
    icon: KanbanSquare,
    title: 'Manage the whole project',
    description:
      'Teams, project management, playtests, and community feedback connect course exercises to shipped outcomes.',
  },
];

const stack = [
  {
    icon: GitBranch,
    title: 'One monorepo, two runtimes',
    entries: [
      'Next.js App Router web app with next-intl localization',
      '.NET modular monolith API with build-time architecture rules',
      'Terraform-managed cloud infrastructure',
    ],
  },
  {
    icon: ShieldCheck,
    title: 'Security and auditability first',
    entries: [
      'Fail-closed authorization with tenant isolation',
      'Tamper-evident, hash-chained audit trails',
      'Compliance evidence packaging for regulated teams',
    ],
  },
];

export default async function Page(): Promise<React.JSX.Element> {
  return (
    <main className="bg-slate-950 text-white">
      <section className="border-b border-white/10 bg-[radial-gradient(circle_at_12%_0%,rgba(14,165,233,0.16),transparent_34%),radial-gradient(circle_at_88%_12%,rgba(168,85,247,0.14),transparent_30%),linear-gradient(180deg,#111827,#020617)]">
        <div className="mx-auto w-full max-w-7xl px-4 py-20 sm:px-6 lg:px-8">
          <div className="max-w-3xl space-y-6">
            <p className="text-sm font-semibold uppercase tracking-[0.2em] text-sky-300">About GameGuild</p>
            <h1 className="text-5xl font-semibold tracking-tight sm:text-6xl">The home of game creators</h1>
            <p className="text-lg leading-8 text-slate-300">
              GameGuild is a community platform where game creators learn, build, and ship together. We connect
              structured learning to real project work — courses and certifications, an in-browser code editor,
              project management, and playtesting — so creators go from first lesson to published project with
              feedback from people who play and review games.
            </p>
            <div className="flex flex-wrap gap-3">
              <Link
                href="/community"
                className="inline-flex items-center rounded-full bg-sky-300 px-5 py-3 text-sm font-semibold text-slate-950 transition hover:bg-sky-200"
              >
                Meet the community
                <ArrowRight className="ml-2 size-4" aria-hidden="true" />
              </Link>
              <Link
                href="/courses"
                className="inline-flex items-center rounded-full border border-white/15 px-5 py-3 text-sm font-semibold text-white transition hover:bg-white/10"
              >
                Explore courses
              </Link>
            </div>
          </div>
        </div>
      </section>

      <section className="border-b border-white/10">
        <div className="mx-auto grid w-full max-w-7xl gap-6 px-4 py-16 sm:px-6 md:grid-cols-3 lg:px-8">
          {pillars.map((pillar) => (
            <article key={pillar.title} className="rounded-3xl border border-white/10 bg-white/[0.04] p-6">
              <pillar.icon className="mb-5 size-6 text-sky-200" aria-hidden="true" />
              <h2 className="text-lg font-semibold">{pillar.title}</h2>
              <p className="mt-2 text-sm leading-6 text-slate-400">{pillar.description}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="border-b border-white/10">
        <div className="mx-auto w-full max-w-7xl space-y-10 px-4 py-16 sm:px-6 lg:px-8">
          <div className="max-w-3xl space-y-4">
            <h2 className="text-3xl font-semibold tracking-tight">How GameGuild is built</h2>
            <p className="text-base leading-7 text-slate-400">
              GameGuild is engineered as an open platform: a pnpm monorepo with a Next.js web app and a modular
              .NET API, where architecture boundaries and security invariants are enforced by the build itself.
            </p>
          </div>
          <div className="grid gap-6 md:grid-cols-2">
            {stack.map((group) => (
              <article key={group.title} className="rounded-3xl border border-white/10 bg-white/[0.04] p-6">
                <group.icon className="mb-5 size-6 text-violet-200" aria-hidden="true" />
                <h3 className="text-lg font-semibold">{group.title}</h3>
                <ul className="mt-3 space-y-2 text-sm leading-6 text-slate-400">
                  {group.entries.map((entry) => (
                    <li key={entry} className="flex gap-2">
                      <span aria-hidden="true" className="mt-2 size-1.5 shrink-0 rounded-full bg-sky-300" />
                      {entry}
                    </li>
                  ))}
                </ul>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="border-b border-white/10">
        <div className="mx-auto grid w-full max-w-7xl gap-10 px-4 py-16 sm:px-6 lg:grid-cols-[1.1fr_0.9fr] lg:px-8">
          <div className="space-y-4">
            <h2 className="text-3xl font-semibold tracking-tight">Our commitment to open development</h2>
            <p className="text-base leading-7 text-slate-400">
              GameGuild is developed in the open: issues, discussions, roadmap decisions, and the changelog are
              public, and contributions follow the community guidelines. We publish our engineering invariants so
              contributors can hold us to the same standards we hold ourselves — fail-closed security, auditable
              mutations, and reviewable architecture.
            </p>
            <div className="flex flex-wrap gap-3">
              <Link
                href="/about/roadmap"
                className="inline-flex items-center rounded-full border border-white/15 px-5 py-3 text-sm font-semibold text-white transition hover:bg-white/10"
              >
                Public roadmap
              </Link>
              <Link
                href="/about/contributors"
                className="inline-flex items-center rounded-full border border-white/15 px-5 py-3 text-sm font-semibold text-white transition hover:bg-white/10"
              >
                Contributors
              </Link>
            </div>
          </div>
          <article className="rounded-3xl border border-white/10 bg-white/[0.04] p-6">
            <HeartHandshake className="mb-5 size-6 text-emerald-200" aria-hidden="true" />
            <h3 className="text-lg font-semibold">The team</h3>
            <p className="mt-2 text-sm leading-6 text-slate-400">
              GameGuild is built by an independent team together with the creators who use it. The project is
              maintained under the{' '}
              <a
                href="https://github.com/gameguild-gg"
                className="font-semibold text-sky-300 underline-offset-4 hover:underline"
                target="_blank"
                rel="noopener noreferrer"
              >
                gameguild-gg GitHub organization
              </a>
              .
            </p>
          </article>
        </div>
      </section>

      <section>
        <div className="mx-auto w-full max-w-7xl px-4 py-16 sm:px-6 lg:px-8">
          <div className="rounded-3xl border border-white/10 bg-white/[0.04] p-8">
            <MessagesSquare className="mb-5 size-6 text-sky-200" aria-hidden="true" />
            <h2 className="text-2xl font-semibold tracking-tight">Talk to us</h2>
            <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-400">
              Questions, partnership ideas, press inquiries, or bug reports — every channel reaches a real person.
            </p>
            <div className="mt-6 flex flex-wrap gap-3 text-sm font-semibold">
              <Link
                href="/contact"
                className="inline-flex items-center rounded-full bg-sky-300 px-5 py-3 text-slate-950 transition hover:bg-sky-200"
              >
                Contact form
                <ArrowRight className="ml-2 size-4" aria-hidden="true" />
              </Link>
              <a
                href="https://github.com/gameguild-gg/gameguild"
                className="inline-flex items-center rounded-full border border-white/15 px-5 py-3 text-white transition hover:bg-white/10"
                target="_blank"
                rel="noopener noreferrer"
              >
                GitHub repository
              </a>
              <a
                href="https://github.com/gameguild-gg/gameguild/issues"
                className="inline-flex items-center rounded-full border border-white/15 px-5 py-3 text-white transition hover:bg-white/10"
                target="_blank"
                rel="noopener noreferrer"
              >
                Report an issue
              </a>
            </div>
          </div>
        </div>
      </section>
    </main>
  );
}
