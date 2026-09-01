'use client';

import { Link, usePathname } from '@/i18n/navigation';
import { cn } from '@game-guild/ui/lib/utils';
import {
  Bookmark,
  Compass,
  FlaskConical,
  Home,
  MessageCircle,
  Plus,
  UsersRound,
} from 'lucide-react';
import { useSearchParams } from 'next/navigation';

const socialNavigation = [
  { label: 'Home', href: '/social', icon: Home },
  { label: 'Explore', href: '/projects', icon: Compass },
  { label: 'Playtests', href: '/social?tab=playtests', icon: FlaskConical },
  { label: 'Messages', href: '/workspace/invitations', icon: MessageCircle, badge: '5' },
  { label: 'Saved', href: '/workspace/projects', icon: Bookmark },
] as const;

export function SocialSidebar(): React.JSX.Element {
  const pathname = usePathname() ?? '/social';
  const searchParams = useSearchParams();
  const activeTab = searchParams.get('tab');

  function openComposer(): void {
    window.dispatchEvent(new CustomEvent('social:compose'));
    window.requestAnimationFrame(() => {
      document.getElementById('social-composer')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    });
  }

  return (
    <aside className="sticky top-16 hidden h-[calc(100svh-4rem)] w-60 shrink-0 border-r border-white/10 bg-[#070c18] px-4 py-6 lg:flex lg:flex-col">
      <div className="px-3 pb-5">
        <p className="text-xs font-semibold uppercase tracking-[0.16em] text-sky-300">Community</p>
        <p className="mt-1 text-sm leading-5 text-slate-400">Discover builds, creators and live playtests.</p>
      </div>

      <nav aria-label="Social navigation" className="space-y-1.5">
        {socialNavigation.map(({ label, href, icon: Icon, ...item }) => {
          const active =
            (label === 'Home' && pathname === '/social' && !activeTab) ||
            (label === 'Playtests' && pathname === '/social' && activeTab === 'playtests') ||
            (label === 'Explore' && pathname.startsWith('/projects')) ||
            (label === 'Messages' && pathname.startsWith('/workspace/invitations')) ||
            (label === 'Saved' && pathname.startsWith('/workspace/projects'));
          return (
            <Link
              key={label}
              href={href}
              aria-current={active ? 'page' : undefined}
              className={cn(
                'group flex min-h-11 items-center gap-3 rounded-xl px-3 text-sm font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sky-300',
                active
                  ? 'bg-sky-400/10 text-sky-300'
                  : 'text-slate-300 hover:bg-white/[0.05] hover:text-white',
              )}
            >
              <Icon className="size-5" strokeWidth={1.8} aria-hidden="true" />
              <span>{label}</span>
              {'badge' in item ? (
                <span className="ml-auto flex min-w-5 items-center justify-center rounded-full bg-violet-500/25 px-1.5 py-0.5 text-[10px] font-bold text-violet-200">
                  {item.badge}
                </span>
              ) : null}
            </Link>
          );
        })}

        <Link
          href="/workspace"
          className="group flex min-h-11 items-center gap-3 rounded-xl px-3 text-sm font-semibold text-slate-300 transition-colors hover:bg-white/[0.05] hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sky-300"
        >
          <UsersRound className="size-5" strokeWidth={1.8} aria-hidden="true" />
          <span>Workspace</span>
        </Link>
      </nav>

      <div className="mt-6 border-t border-white/10 pt-6">
        <button
          type="button"
          onClick={openComposer}
          className="flex h-11 w-full items-center justify-center gap-2 rounded-xl bg-sky-400 text-sm font-bold text-slate-950 shadow-[0_12px_28px_rgba(56,189,248,0.18)] transition hover:bg-sky-300 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sky-200 focus-visible:ring-offset-2 focus-visible:ring-offset-[#070c18]"
        >
          <Plus className="size-5" aria-hidden="true" />
          Create
        </button>
      </div>

    </aside>
  );
}
