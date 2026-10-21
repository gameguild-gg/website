import type React from 'react';

const tracks = [
  {
    name: 'Learning platform',
    status: 'Active delivery',
    details: 'Course catalog, attendance, assessments, certificates, cohorts, discovery, recommendations, and instructor management.',
  },
  {
    name: 'Testing Lab',
    status: 'Active delivery',
    details: 'Project testing requests, lab sessions, participant registration, feedback capture, and role templates.',
  },
  {
    name: 'Launch Pad',
    status: 'Active delivery',
    details: 'Launch readiness, project promotion, public release checklist, validation milestones, and post-launch follow-up.',
  },
  {
    name: 'Community and platform management',
    status: 'Ongoing',
    details: 'Profiles, groups, feeds, moderation, permissions, subscriptions, analytics, and operational health.',
  },
];

export default async function Page(): Promise<React.JSX.Element> {
  return (
    <main className="min-h-screen bg-background">
      <section className="mx-auto flex max-w-6xl flex-col gap-8 px-4 py-10">
        <div className="space-y-3">
          <p className="text-sm font-medium uppercase tracking-[0.18em] text-muted-foreground">Project plan</p>
          <h1 className="text-5xl font-bold tracking-tight">Development Roadmap</h1>
          <p className="max-w-3xl text-muted-foreground">
            The roadmap focuses on the day-zero product set first, then broadens into community, commerce, and platform operations.
          </p>
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          {tracks.map((track) => (
            <section key={track.name} className="rounded-lg border bg-card p-5">
              <div className="flex items-start justify-between gap-4">
                <h2 className="text-xl font-semibold">{track.name}</h2>
                <span className="rounded-full border px-2.5 py-1 text-xs font-medium text-muted-foreground">{track.status}</span>
              </div>
              <p className="mt-4 text-sm text-muted-foreground">{track.details}</p>
            </section>
          ))}
        </div>
      </section>
    </main>
  );
}
