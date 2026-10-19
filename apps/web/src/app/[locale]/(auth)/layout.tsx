import { auth } from '@/auth';
import { GameGuildLogo } from '@/components/brand/game-guild-logo';
import { resolveAllowedAuthRedirect } from '@/lib/auth/cross-domain-auth';
import { headers } from 'next/headers';
import Link from 'next/link';
import { redirect } from 'next/navigation';
import React from 'react';

export default async function Layout({ children, params }: LayoutProps<'/[locale]'>): Promise<React.JSX.Element> {
  const { locale } = await params;

  // Hard guard: redirect authenticated users away from auth pages, honoring
  // an allowlisted callbackUrl (issue #864) so a transient post-2FA bounce
  // through /sign-in returns the user to their original target.
  const session = await auth();
  if (session) {
    const headerList = await headers();
    const callbackUrl = resolveAllowedAuthRedirect(
      headerList.get('x-gameguild-path')?.split('?')[0],
    );
    redirect(`/${locale}${callbackUrl}`);
  }

  return (
    <div className="relative flex min-h-svh flex-col overflow-hidden bg-slate-950 text-white">
      <div className="absolute inset-0 bg-[radial-gradient(circle_at_18%_8%,rgba(56,189,248,0.18),transparent_32%),radial-gradient(circle_at_84%_18%,rgba(168,85,247,0.14),transparent_34%),linear-gradient(180deg,#020617,#0f172a)]" />
      <div className="relative z-10 mx-auto grid w-full max-w-7xl flex-1 place-items-center px-6 py-10 lg:px-8">
        <div className="flex w-full flex-col items-center gap-6">
          <GameGuildLogo locale={locale} />
          <section className="w-full max-w-md">
            {children}
          </section>
        </div>
      </div>

      <footer className="relative z-10 mx-auto w-full max-w-7xl px-6 pb-6 text-center">
        <p className="text-xs leading-relaxed text-slate-400 sm:whitespace-nowrap sm:text-sm">
          By clicking continue, you agree to our{' '}
          <Link href="/legal/terms-of-service" className="text-sky-200 underline-offset-4 hover:underline">Terms of Service</Link> and{' '}
          <Link href="/legal/privacy" className="text-sky-200 underline-offset-4 hover:underline">Privacy Policy</Link>.
        </p>
      </footer>
    </div>
  );
}
