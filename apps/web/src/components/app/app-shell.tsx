import { getSession, getToken } from '@/auth';
import { createServerClient } from '@game-guild/client';
import { Link } from '@/i18n/navigation';
import { Bell, FlaskConical, Gamepad2, GraduationCap, Heart, MessageCircle, Rocket, Search, Users } from 'lucide-react';
import { Github, Twitter, Youtube } from '@/components/ui/brand-icons';
import type { ReactNode } from 'react';
import {
  PublicDesktopNav,
  PublicMobileNav,
  type PublicNavEntry,
  type PublicWebsiteUser,
} from './public-website-nav';
import { PublicAccountMenu } from './public-account-menu';
import { SocialHeaderCreateButton } from '@/components/feed/social-header-create-button';

const primaryNav = [
  { label: 'Community', href: '/community' },
  { label: 'Courses', href: '/courses' },
  { label: 'Programs', href: '/programs' },
  { label: 'Projects', href: '/projects' },
  { label: 'Testing Lab', href: '/testing-lab' },
  { label: 'Launch Pad', href: '/launch-pad' },
  { label: 'Jobs', href: '/jobs' },
  { label: 'About', href: '/about' },
] as const;

const desktopPrimaryNav = [
  { label: 'Community', href: '/community' },
  {
    label: 'Learn',
    items: [
      { label: 'Courses', href: '/courses' },
      { label: 'Programs', href: '/programs' },
    ],
  },
  { label: 'Projects', href: '/projects' },
  { label: 'Testing Lab', href: '/testing-lab' },
  { label: 'Launch Pad', href: '/launch-pad' },
  {
    label: 'More',
    items: [
      { label: 'Jobs', href: '/jobs' },
      { label: 'About', href: '/about' },
    ],
  },
] as const satisfies readonly PublicNavEntry[];

const socialDesktopNav = [
  { label: 'Community', href: '/community' },
  {
    label: 'Learn',
    items: [
      { label: 'Courses', href: '/courses' },
      { label: 'Programs', href: '/programs' },
    ],
  },
  {
    label: 'Build',
    items: [
      { label: 'Workspace', href: '/workspace' },
      { label: 'Projects', href: '/projects' },
    ],
  },
  {
    label: 'Test & Launch',
    items: [
      { label: 'Testing Lab', href: '/testing-lab' },
      { label: 'Launch Pad', href: '/launch-pad' },
    ],
  },
] as const satisfies readonly PublicNavEntry[];

const socialMobileNav = [
  {
    label: 'Community',
    items: [
      { label: 'Home', href: '/' },
      { label: 'Explore', href: '/projects' },
      { label: 'Testing Lab', href: '/testing-lab' },
      { label: 'Messages', href: '/workspace/invitations' },
      { label: 'Saved', href: '/workspace/projects' },
    ],
  },
  {
    label: 'GameGuild',
    items: [
      { label: 'Courses', href: '/courses' },
      { label: 'Programs', href: '/programs' },
      { label: 'Workspace', href: '/workspace' },
      { label: 'Projects', href: '/projects' },
      { label: 'Launch Pad', href: '/launch-pad' },
      { label: 'Jobs', href: '/jobs' },
      { label: 'About', href: '/about' },
    ],
  },
] as const satisfies readonly PublicNavEntry[];

const footerSections = [
  {
    title: 'Learn',
    accentClass: 'text-primary',
    hoverClass: 'hover:text-primary',
    links: [
      { label: 'Courses', href: '/courses' },
      { label: 'Programs', href: '/programs' },
    ],
  },
  {
    title: 'Build & test',
    accentClass: 'text-highlight',
    hoverClass: 'hover:text-highlight',
    links: [
      { label: 'Testing Lab', href: '/testing-lab' },
      { label: 'Launch Pad', href: '/launch-pad' },
      { label: 'Project showcase', href: '/projects' },
    ],
  },
  {
    title: 'Community',
    accentClass: 'text-success',
    hoverClass: 'hover:text-success',
    links: [
      { label: 'Join community', href: '/sign-up' },
      { label: 'Community hub', href: '/community' },
      { label: 'Feed', href: '/' },
      { label: 'Jobs', href: '/jobs' },
    ],
  },
  {
    title: 'Company',
    accentClass: 'text-primary',
    hoverClass: 'hover:text-primary',
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
    <span className="flex size-9 items-center justify-center rounded-xl border border-sidebar-border bg-sidebar-foreground text-sidebar shadow-sm">
      <GraduationCap className="size-5" aria-hidden="true" />
    </span>
  );
}

function FooterBrandMark() {
  return (
    <span className="flex size-10 items-center justify-center rounded-lg bg-gradient-to-br from-primary to-highlight text-primary-foreground shadow-lg">
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
  const publicAccountActions = (
    <div className="flex items-center gap-2">
      <a
        href="https://github.com/gameguild-gg/gameguild"
        aria-label="GameGuild on GitHub"
        title="GitHub"
        className="hidden size-9 items-center justify-center rounded-full text-muted-foreground transition-colors hover:bg-accent hover:text-foreground xl:inline-flex"
      >
        <Github className="size-4" aria-hidden="true" />
      </a>
      {user ? <PublicAccountMenu user={user} /> : (
        <>
          <Link
            href="/sign-in"
            className="hidden rounded-full border border-border px-4 py-2 text-sm font-semibold text-foreground transition hover:bg-accent sm:inline-flex"
          >
            Sign in
          </Link>
          <Link
            href="/sign-up"
            className="hidden items-center rounded-full bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground transition hover:bg-primary/85 sm:inline-flex"
          >
            Join community
          </Link>
        </>
      )}
      <PublicMobileNav items={primaryNav} user={user} />
    </div>
  );
  const socialAccountActions = (
    <div className="flex items-center gap-1">
      <Link
        href="/projects"
        aria-label="Explore community"
        title="Explore community"
        className="inline-flex size-9 items-center justify-center rounded-full text-foreground transition-colors hover:bg-accent"
      >
        <Search className="size-4" aria-hidden="true" />
      </Link>
      <SocialHeaderCreateButton />
      <Link
        href="/workspace/settings/notifications"
        aria-label="Notification settings"
        title="Notification settings"
        className="hidden size-9 items-center justify-center rounded-full text-foreground transition-colors hover:bg-accent sm:inline-flex"
      >
        <Bell className="size-4" aria-hidden="true" />
      </Link>
      {user ? <PublicAccountMenu user={user} /> : null}
      <PublicMobileNav
        items={socialMobileNav}
        user={user}
        triggerLabel="Open community navigation"
        title="Navigation"
        description="Community and GameGuild destinations."
      />
    </div>
  );

  return (
    <header
      className={
        embedded
          ? 'sticky top-0 z-40 bg-sidebar/95 text-sidebar-foreground backdrop-blur-xl'
          : 'sticky top-0 z-40 border-b border-border bg-sidebar/90 text-sidebar-foreground backdrop-blur-xl'
      }
    >
      <div
        className={
          embedded
            ? 'flex min-h-14 w-full items-center justify-between gap-3 px-3 py-1.5 sm:px-4 lg:px-6'
            : 'mx-auto flex min-h-16 w-full max-w-7xl items-center justify-between gap-4 px-4 py-3 sm:px-6 lg:px-8'
        }
      >
        {embedded ? (
          <div className="flex min-w-0 items-center gap-3">
            {leading}
            <Link href="/" aria-label="GameGuild home" className="flex min-w-0 items-center gap-2 md:hidden">
              <BrandMark />
              <span className="truncate text-sm font-semibold tracking-tight text-sidebar-foreground">GameGuild</span>
            </Link>
            <PublicDesktopNav items={socialDesktopNav} variant="app" />
          </div>
        ) : (
          <>
            <Link href="/" aria-label="GameGuild home" className="flex min-w-0 items-center gap-3">
              <BrandMark />
              <span className="truncate text-base font-semibold tracking-tight text-sidebar-foreground">GameGuild</span>
            </Link>
            <PublicDesktopNav items={desktopPrimaryNav} />
          </>
        )}

        {embedded ? socialAccountActions : publicAccountActions}
      </div>
    </header>
  );
}

export function PublicWebsiteFooter() {
  return (
    <footer className="border-t-2 border-border bg-gradient-to-b from-card via-secondary to-sidebar text-sidebar-foreground">
      <div className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 lg:px-8 lg:py-12">
        <div data-testid="footer-primary-grid" className="grid gap-10 lg:grid-cols-6 lg:gap-12">
          <div className="max-w-sm lg:col-span-2">
            <div className="flex items-center gap-3">
              <FooterBrandMark />
              <span className="bg-gradient-to-r from-primary to-highlight bg-clip-text text-xl font-bold text-transparent">Game Guild</span>
            </div>
            <p className="mt-5 text-sm leading-6 text-muted-foreground">
              A thriving gaming community dedicated to education, collaboration, and innovation. Join us as we grow together and shape the future of gaming.
            </p>
            <div className="mt-5 space-y-3 text-sm text-muted-foreground">
              <div className="flex items-center gap-3 transition-colors hover:text-primary">
                <Users className="size-4 shrink-0" aria-hidden="true" />
                <span>Community-driven learning and development</span>
              </div>
              <div className="flex items-center gap-3 transition-colors hover:text-highlight">
                <Heart className="size-4 shrink-0" aria-hidden="true" />
                <span>Open source and collaborative</span>
              </div>
            </div>
          </div>

          <nav aria-label="Footer" className="grid gap-x-8 gap-y-8 sm:grid-cols-2 lg:col-span-4 lg:grid-cols-4">
            {footerSections.map((section) => (
              <div key={section.title} className="min-w-0">
                <h2 className={`mb-4 text-sm font-semibold ${section.accentClass}`}>{section.title}</h2>
                <ul className="space-y-2 text-sm text-muted-foreground">
                  {section.links.map((link) => (
                    <li key={link.href} className="flex items-start gap-2">
                      <span className="mt-0.5 shrink-0 text-muted-foreground/50" aria-hidden="true">
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

        <div className="mt-10 border-t border-border pt-6 lg:mt-12 lg:pt-8">
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
                  className="group flex size-10 items-center justify-center rounded-lg border border-border bg-accent/50 text-muted-foreground transition hover:border-primary/50 hover:bg-accent hover:text-primary hover:shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  <Icon className="size-4" aria-hidden="true" />
                </a>
              ))}
            </div>
          </div>

          <div className="mt-6 flex flex-col items-center justify-between gap-4 border-t border-border pt-5 text-sm text-muted-foreground/70 sm:flex-row lg:mt-8 lg:pt-6">
            <p className="text-center sm:text-left">© 2026 Game Guild. All rights reserved.</p>
            <nav aria-label="Legal" className="flex flex-wrap justify-center gap-x-6 gap-y-3 sm:justify-end">
              <Link href="/legal/licenses" className="transition-colors hover:text-primary">
                Licenses
              </Link>
              <Link href="/terms-of-service" className="transition-colors hover:text-primary">
                Terms of Service
              </Link>
              <Link href="/polices/privacy" className="transition-colors hover:text-primary">
                Privacy
              </Link>
            </nav>
          </div>
        </div>
      </div>
      <div className="h-1 bg-gradient-to-r from-success via-primary to-highlight" aria-hidden="true" />
    </footer>
  );
}

export async function AppShell({ children }: { readonly children: ReactNode }) {
  const header = await PublicWebsiteHeader();

  return (
    <div className="min-h-svh bg-background text-foreground">
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
