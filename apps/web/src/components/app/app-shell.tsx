import { getSession, getToken } from '@/auth';
import { createServerClient } from '@game-guild/client';
import { Link } from '@/i18n/navigation';
import { FlaskConical, Gamepad2, GraduationCap, Heart, MessageCircle, Rocket, Users } from 'lucide-react';
import { Github, Twitter, Youtube } from '@/components/ui/brand-icons';
import type { ReactNode } from 'react';
import { PublicDesktopNav, PublicMobileNav, type PublicWebsiteUser } from './public-website-nav';
import { PublicAccountMenu } from './public-account-menu';

const primaryNav = [
  { label: 'Courses', href: '/courses' },
  { label: 'Programs', href: '/programs' },
  { label: 'Testing Lab', href: '/testing-lab' },
  { label: 'Launch Pad', href: '/launch-pad' },
  { label: 'Projects', href: '/projects' },
  { label: 'Community', href: '/community' },
  { label: 'Jobs', href: '/jobs' },
  { label: 'About', href: '/about' },
] as const;

const footerSections = [
  {
    title: 'Learn',
    accentClass: 'text-blue-400',
    hoverClass: 'hover:text-blue-300',
    links: [
      { label: 'Courses', href: '/courses' },
      { label: 'Programs', href: '/programs' },
    ],
  },
  {
    title: 'Build & test',
    accentClass: 'text-purple-400',
    hoverClass: 'hover:text-purple-300',
    links: [
      { label: 'Testing Lab', href: '/testing-lab' },
      { label: 'Launch Pad', href: '/launch-pad' },
      { label: 'Project showcase', href: '/projects' },
    ],
  },
  {
    title: 'Community',
    accentClass: 'text-emerald-400',
    hoverClass: 'hover:text-emerald-300',
    links: [
      { label: 'Join community', href: '/sign-up' },
      { label: 'Community hub', href: '/community' },
      { label: 'Feed', href: '/' },
      { label: 'Jobs', href: '/jobs' },
    ],
  },
  {
    title: 'Company',
    accentClass: 'text-sky-400',
    hoverClass: 'hover:text-sky-300',
    links: [
      { label: 'About GameGuild', href: '/about' },
      { label: 'Roadmap', href: '/about/roadmap' },
      { label: 'Contributors', href: '/about/contributors' },
      { label: 'Contact', href: '/contact' },
    ],
  },
] as const;

const footerSocialLinks = [
  { label: 'Discord', href: 'https://discord.gg/9CdJeQ2XKB', icon: MessageCircle },
  { label: 'Twitter', href: 'https://twitter.com/gameguild_gg', icon: Twitter },
  { label: 'GitHub', href: 'https://github.com/gameguild-gg/gameguild', icon: Github },
  { label: 'YouTube', href: 'https://youtube.com/@gameguild', icon: Youtube },
] as const;

function BrandMark() {
  return (
    <span className="flex size-9 items-center justify-center rounded-xl border border-white/15 bg-white text-slate-950 shadow-sm">
      <GraduationCap className="size-5" aria-hidden="true" />
    </span>
  );
}

function FooterBrandMark() {
  return (
    <span className="flex size-10 items-center justify-center rounded-lg bg-gradient-to-br from-blue-500 to-purple-600 text-white shadow-lg shadow-purple-950/30">
      <Gamepad2 className="size-5" aria-hidden="true" />
    </span>
  );
}

function getInitials(value: string) {
  const parts = value
    .split(/\s+/)
    .map((part) => part.trim())
    .filter(Boolean);

  if (parts.length === 0) return 'GG';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
}

async function getHeaderUser(): Promise<PublicWebsiteUser | null> {
  try {
    const session = await getSession();
    const user = session?.user;
    if (!user?.email && !user?.name) return null;

    const displayName = user.name?.trim() || user.email?.trim() || 'GameGuild member';

    const client = createServerClient({
      baseUrl: process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080',
      auth: { getAccessToken: () => getToken() },
    });
    const access = await client.request<{ capabilities?: unknown }>({ method: 'GET', path: '/v1/access/capabilities', requiresAuth: true });
    const canManage = access.ok && Array.isArray(access.data?.capabilities) && access.data.capabilities.length > 0;

    return {
      name: displayName,
      email: user.email?.trim() || null,
      image: user.image?.trim() || null,
      initials: getInitials(displayName),
      canManage,
    };
  } catch {
    return null;
  }
}

export async function PublicWebsiteHeader({
  embedded = false,
  leading,
}: {
  embedded?: boolean;
  leading?: ReactNode;
} = {}) {
  const user = await getHeaderUser();

  return (
    <header className="sticky top-0 z-40 border-b border-white/10 bg-slate-950/90 text-white backdrop-blur-xl">
      <div
        className={
          embedded
            ? 'grid min-h-16 w-full grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-3 px-3 py-2 sm:px-4 lg:px-6'
            : 'mx-auto flex min-h-16 w-full max-w-7xl items-center justify-between gap-4 px-4 py-3 sm:px-6 lg:px-8'
        }
      >
        {embedded ? (
          <div className="flex min-w-0 items-center gap-2">
            {leading}
            <Link href="/" aria-label="GameGuild home" className="flex min-w-0 items-center gap-2 md:hidden">
              <BrandMark />
              <span className="truncate text-sm font-semibold tracking-tight text-white">GameGuild</span>
            </Link>
          </div>
        ) : (
          <Link href="/" aria-label="GameGuild home" className="flex min-w-0 items-center gap-3">
            <BrandMark />
            <span className="truncate text-base font-semibold tracking-tight text-white">GameGuild</span>
          </Link>
        )}

        <PublicDesktopNav items={primaryNav} />

        <div className={embedded ? 'flex items-center justify-self-end gap-2' : 'flex items-center gap-2'}>
          <a
            href="https://github.com/gameguild-gg/gameguild"
            className="hidden rounded-full border border-white/10 px-3 py-2 text-sm font-medium text-slate-300 transition hover:border-white/20 hover:bg-white/10 hover:text-white xl:inline-flex xl:items-center xl:gap-2"
          >
            <Github className="size-4" aria-hidden="true" />
            GitHub
          </a>
          {user ? <PublicAccountMenu user={user} /> : (
            <>
              <Link
                href="/sign-in"
                className="hidden rounded-full border border-white/10 px-4 py-2 text-sm font-semibold text-slate-200 transition hover:bg-white/10 hover:text-white sm:inline-flex"
              >
                Sign in
              </Link>
              <Link
                href="/sign-up"
                className="hidden items-center rounded-full bg-sky-300 px-4 py-2 text-sm font-semibold text-slate-950 transition hover:bg-sky-200 sm:inline-flex"
              >
                Join community
              </Link>
            </>
          )}
          <PublicMobileNav items={primaryNav} user={user} />
        </div>
      </div>
    </header>
  );
}

export function PublicWebsiteFooter() {
  return (
    <footer className="border-t-2 border-slate-700/40 bg-gradient-to-b from-[#101a30] via-[#142039] to-[#0d172b] text-white">
      <div className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 lg:px-8 lg:py-12">
        <div data-testid="footer-primary-grid" className="grid gap-10 lg:grid-cols-6 lg:gap-12">
          <div className="max-w-sm lg:col-span-2">
            <div className="flex items-center gap-3">
              <FooterBrandMark />
              <span className="bg-gradient-to-r from-blue-400 to-purple-400 bg-clip-text text-xl font-bold text-transparent">Game Guild</span>
            </div>
            <p className="mt-5 text-sm leading-6 text-slate-400">
              A thriving gaming community dedicated to education, collaboration, and innovation. Join us as we grow together and shape the future of gaming.
            </p>
            <div className="mt-5 space-y-3 text-sm text-slate-400">
              <div className="flex items-center gap-3 transition-colors hover:text-blue-300">
                <Users className="size-4 shrink-0" aria-hidden="true" />
                <span>Community-driven learning and development</span>
              </div>
              <div className="flex items-center gap-3 transition-colors hover:text-purple-300">
                <Heart className="size-4 shrink-0" aria-hidden="true" />
                <span>Open source and collaborative</span>
              </div>
            </div>
          </div>

          <nav aria-label="Footer" className="grid gap-x-8 gap-y-8 sm:grid-cols-2 lg:col-span-4 lg:grid-cols-4">
            {footerSections.map((section) => (
              <div key={section.title} className="min-w-0">
                <h2 className={`mb-4 text-sm font-semibold ${section.accentClass}`}>{section.title}</h2>
                <ul className="space-y-2 text-sm text-slate-400">
                  {section.links.map((link) => (
                    <li key={link.href} className="flex items-start gap-2">
                      <span className="mt-0.5 shrink-0 text-slate-600" aria-hidden="true">
                        •
                      </span>
                      <Link href={link.href} className={`leading-5 transition-colors ${section.hoverClass}`}>
                        {link.label}
                      </Link>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </nav>
        </div>

        <div className="mt-10 border-t border-slate-700/50 pt-6 lg:mt-12 lg:pt-8">
          <div className="flex justify-center sm:justify-start">
            <div className="flex gap-3">
              {footerSocialLinks.map(({ label, href, icon: Icon }) => (
                <a
                  key={label}
                  href={href}
                  target="_blank"
                  rel="noopener noreferrer"
                  aria-label={label}
                  title={label}
                  className="group flex size-10 items-center justify-center rounded-lg border border-slate-600/50 bg-slate-800/60 text-slate-400 transition hover:border-blue-400/50 hover:bg-slate-800 hover:text-blue-300 hover:shadow-lg hover:shadow-blue-950/30 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-400"
                >
                  <Icon className="size-4" aria-hidden="true" />
                </a>
              ))}
            </div>
          </div>

          <div className="mt-6 flex flex-col items-center justify-between gap-4 border-t border-slate-700/50 pt-5 text-sm text-slate-500 sm:flex-row lg:mt-8 lg:pt-6">
            <p className="text-center sm:text-left">© 2026 Game Guild. All rights reserved.</p>
            <nav aria-label="Legal" className="flex flex-wrap justify-center gap-x-6 gap-y-3 sm:justify-end">
              <Link href="/legal/licenses" className="transition-colors hover:text-blue-300">
                Licenses
              </Link>
              <Link href="/terms-of-service" className="transition-colors hover:text-blue-300">
                Terms of Service
              </Link>
              <Link href="/polices/privacy" className="transition-colors hover:text-blue-300">
                Privacy
              </Link>
            </nav>
          </div>
        </div>
      </div>
      <div className="h-1 bg-gradient-to-r from-emerald-500 via-blue-500 to-purple-500" aria-hidden="true" />
    </footer>
  );
}

export async function AppShell({ children }: { readonly children: ReactNode }) {
  const header = await PublicWebsiteHeader();

  return (
    <div className="min-h-svh bg-slate-950">
      {header}
      {children}
      <PublicWebsiteFooter />
    </div>
  );
}

export const publicWebsiteHighlights = [
  {
    title: 'Interactive Courses',
    description: 'Structured game development paths with practical projects, assessments, and production-ready outcomes.',
    icon: GraduationCap,
  },
  {
    title: 'Testing Lab',
    description: 'A focused review space where creators can validate builds, gather feedback, and improve playable work.',
    icon: FlaskConical,
  },
  {
    title: 'Launch Pad',
    description: 'Release planning, store-page critique, and launch-readiness checklists for student projects.',
    icon: Rocket,
  },
  {
    title: 'Community Studio',
    description: 'Connect with peers, instructors, and project teams around critique, collaboration, and shipped work.',
    icon: Users,
  },
] as const;
