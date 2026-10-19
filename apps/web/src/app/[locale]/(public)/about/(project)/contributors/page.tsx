import type React from 'react';

const sections = [
  {
    title: 'Maintainers',
    body: 'Maintainers own repository quality, release readiness, architecture direction, module boundaries, and production review standards.',
  },
  {
    title: 'Contributors',
    body: 'Contributors may improve learning content, platform UX, tests, documentation, translations, and integrations through reviewed changes.',
  },
  {
    title: 'Review standards',
    body: 'Every product change should include clear scope, tests where practical, accessibility checks for UI, and a migration or rollout note when data changes.',
  },
];

export default async function Page(): Promise<React.JSX.Element> {
  return (
    <main className="min-h-screen bg-background">
      <section className="mx-auto flex max-w-5xl flex-col gap-8 px-4 py-10">
        <div className="space-y-3">
          <p className="text-sm font-medium uppercase tracking-[0.18em] text-muted-foreground">Project governance</p>
          <h1 className="text-4xl font-bold tracking-tight">Contributors</h1>
          <p className="max-w-3xl text-muted-foreground">
            GameGuild contribution work is organized around maintainable product modules, reviewed learning content, and reliable delivery standards.
          </p>
        </div>

        <div className="grid gap-4 md:grid-cols-3">
          {sections.map((section) => (
            <section key={section.title} className="rounded-lg border bg-card p-5">
              <h2 className="text-xl font-semibold">{section.title}</h2>
              <p className="mt-4 text-sm text-muted-foreground">{section.body}</p>
            </section>
          ))}
        </div>
      </section>
    </main>
  );
}
