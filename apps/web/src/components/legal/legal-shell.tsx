import { Link } from '@/i18n/navigation';
import { GraduationCap, Scale } from 'lucide-react';
import type React from 'react';
import type { ReactNode } from 'react';

const legalNavLinks = [
  { label: 'Legal', href: '/legal' },
  { label: 'Terms of Service', href: '/terms-of-service' },
  { label: 'Terms of Use', href: '/terms-of-use' },
  { label: 'Privacy', href: '/polices/privacy' },
  { label: 'Cookies', href: '/polices/cookies' },
  { label: 'Licenses', href: '/legal/licenses' },
  { label: 'FERPA Waiver', href: '/legal/ferpa-waiver' },
  { label: 'Academic Honesty', href: '/legal/academic-honesty' },
] as const;

/** Document-focused shell for /legal/* — minimal chrome, readable article container. */
export async function LegalShell({ children }: { readonly children: ReactNode }): Promise<React.JSX.Element> {
  return (
    <div className="flex min-h-svh flex-col bg-slate-950 text-white">
      <header className="border-b border-white/10">
        <div className="mx-auto flex w-full max-w-4xl items-center justify-between px-4 py-4 sm:px-6">
          <Link href="/" className="flex items-center gap-2 font-semibold">
            <span className="flex size-8 items-center justify-center rounded-lg bg-white text-slate-950">
              <GraduationCap className="size-4" aria-hidden="true" />
            </span>
            GameGuild
          </Link>
          <Link
            href="/"
            className="inline-flex items-center gap-2 rounded-full border border-white/15 px-4 py-1.5 text-sm font-semibold text-slate-200 transition hover:bg-white/10"
          >
            Back to GameGuild
          </Link>
        </div>
      </header>

      <main className="mx-auto w-full max-w-4xl flex-1 px-4 py-10 sm:px-6 lg:px-8">{children}</main>

      <footer className="border-t border-white/10">
        <div className="mx-auto flex w-full max-w-4xl flex-col gap-4 px-4 py-6 sm:px-6">
          <div className="flex items-center gap-2 text-sm font-semibold text-white">
            <Scale className="size-4 text-sky-200" aria-hidden="true" />
            Legal &amp; policies
          </div>
          <nav aria-label="Legal" className="flex flex-wrap gap-x-5 gap-y-2">
            {legalNavLinks.map((link) => (
              <Link key={link.href} href={link.href} className="text-sm text-slate-400 transition hover:text-white">
                {link.label}
              </Link>
            ))}
          </nav>
          <p className="text-xs text-slate-500">© 2026 GameGuild. All rights reserved.</p>
        </div>
      </footer>
    </div>
  );
}
