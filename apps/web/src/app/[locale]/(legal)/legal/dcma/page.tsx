import React from 'react';

const noticeSteps = [
  'Send a written notice to copyright@gameguild.gg identifying the copyrighted work you believe is infringed.',
  'Include the exact URL of the material on GameGuild that you report.',
  'State your contact information and a good-faith statement that the use is unauthorized.',
  'Sign the notice (physical or electronic signature of the copyright owner or agent).',
];

export default async function Page(): Promise<React.JSX.Element> {
  return (
    <article className="flex flex-col gap-8">
      <div className="space-y-3">
        <p className="text-sm font-medium uppercase tracking-[0.18em] text-muted-foreground">Copyright</p>
        <h1 className="text-4xl font-bold tracking-tight">DMCA Notice</h1>
        <p className="text-muted-foreground">
          GameGuild responds to copyright takedown notices under the Digital Millennium Copyright Act. This page explains how rights holders can report infringing material hosted on GameGuild projects, courses, and community posts.
        </p>
      </div>

      <section className="space-y-4 rounded-lg border bg-card p-5">
        <h2 className="text-xl font-semibold">Filing a takedown notice</h2>
        <ul className="space-y-2 text-sm text-muted-foreground">
          {noticeSteps.map((step) => (
            <li key={step}>{step}</li>
          ))}
        </ul>
      </section>

      <section className="space-y-3 rounded-lg border bg-card p-5">
        <h2 className="text-xl font-semibold">Counter-notices and repeat infringers</h2>
        <p className="text-sm text-muted-foreground">
          Submitters may file a counter-notice if they believe material was removed in error. Accounts with repeated upheld notices lose upload access. Misrepresentations in either direction may create liability under the DMCA.
        </p>
      </section>
    </article>
  );
}
