import { Link } from '@/i18n/navigation';
import type React from 'react';

const legalPages = [
  {
    href: '/terms-of-service',
    title: 'Terms of Service',
    description: 'The rules for using the GameGuild platform and API.',
  },
  {
    href: '/terms-of-use',
    title: 'Terms of Use',
    description: 'Acceptable-use expectations for courses, content, and community features.',
  },
  {
    href: '/polices/privacy',
    title: 'Privacy Policy',
    description: 'What data GameGuild collects, why, and how it is handled.',
  },
  {
    href: '/polices/cookies',
    title: 'Cookie Policy',
    description: 'How cookies and similar technologies are used on the site.',
  },
  {
    href: '/legal/licenses',
    title: 'Licenses',
    description: 'Licenses for GameGuild content and bundled open-source components.',
  },
  {
    href: '/legal/ferpa-waiver',
    title: 'FERPA Waiver',
    description: 'How education records are handled and how students consent to sharing coursework publicly.',
  },
  {
    href: '/legal/academic-honesty',
    title: 'Academic Honesty',
    description: 'Integrity expectations for assessments and coursework.',
  },
] as const;

export default async function LegalIndexPage(): Promise<React.JSX.Element> {
  return (
    <article className="flex flex-col gap-8">
      <div className="space-y-3">
        <p className="text-sm font-medium uppercase tracking-[0.18em] text-muted-foreground">Legal &amp; policies</p>
        <h1 className="text-4xl font-bold tracking-tight">Legal</h1>
        <p className="text-muted-foreground">Policies, terms, and licenses that govern GameGuild.</p>
      </div>

      <nav aria-label="Legal documents" className="flex flex-col divide-y divide-border rounded-lg border bg-card">
        {legalPages.map((page) => (
          <Link
            key={page.href}
            href={page.href}
            className="flex flex-col gap-1 p-5 transition hover:bg-accent"
          >
            <span className="text-lg font-semibold">{page.title}</span>
            <span className="text-sm text-muted-foreground">{page.description}</span>
          </Link>
        ))}
      </nav>
    </article>
  );
}
