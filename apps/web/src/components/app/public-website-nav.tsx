'use client';

import { Link, usePathname } from '@/i18n/navigation';
import { Button } from '@game-guild/ui/components/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@game-guild/ui/components/dropdown-menu';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@game-guild/ui/components/sheet';
import { cn } from '@game-guild/ui/lib/utils';
import { ChevronDown, Menu } from 'lucide-react';
import { Github } from '@/components/ui/brand-icons';
import { useState } from 'react';

export type PublicNavItem = {
  readonly label: string;
  readonly href: string;
};

export type PublicNavGroup = {
  readonly label: string;
  readonly items: readonly PublicNavItem[];
};

export type PublicNavEntry = PublicNavItem | PublicNavGroup;

export type PublicWebsiteUser = {
  readonly name: string;
  readonly email: string | null;
  readonly image: string | null;
  readonly initials: string;
  readonly canManage?: boolean;
};

function isActivePath(pathname: string, href: string, variant: 'public' | 'app' = 'public') {
  if (href === '/') return pathname === '/';
  if (variant === 'app' && href === '/community' && (pathname === '/' || pathname === '/social')) return true;
  return pathname === href || pathname.startsWith(`${href}/`);
}

function isNavGroup(entry: PublicNavEntry): entry is PublicNavGroup {
  return 'items' in entry;
}

export function PublicDesktopNav({
  items,
  variant = 'public',
}: {
  readonly items: readonly PublicNavEntry[];
  readonly variant?: 'public' | 'app';
}) {
  const pathname = usePathname() ?? '/';

  return (
    <nav
      aria-label="Main navigation"
      className={
        variant === 'app'
          ? 'hidden items-center gap-1 lg:flex'
          : 'hidden items-center rounded-full border border-border bg-accent/30 p-1 lg:flex'
      }
    >
      {items.map((item) => {
        if (isNavGroup(item)) {
          const active = item.items.some((child) => isActivePath(pathname, child.href, variant));

          return (
            <DropdownMenu key={item.label}>
              <DropdownMenuTrigger asChild>
                <button
                  type="button"
                  aria-current={active ? 'page' : undefined}
                  className={cn(
                    variant === 'app'
                      ? 'inline-flex h-9 items-center gap-1 rounded-lg px-2.5 text-[13px] font-medium transition-colors'
                      : 'inline-flex items-center gap-1 rounded-full px-3 py-1.5 text-sm font-medium transition-colors',
                    active
                      ? variant === 'app'
                        ? 'bg-accent text-accent-foreground'
                        : 'bg-primary text-primary-foreground'
                      : 'text-muted-foreground hover:bg-accent hover:text-foreground',
                  )}
                >
                  {item.label}
                  <ChevronDown className="size-3.5 opacity-60" aria-hidden="true" />
                </button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="start" className="min-w-40">
                {item.items.map((child) => {
                  const childActive = isActivePath(pathname, child.href, variant);

                  return (
                    <DropdownMenuItem key={child.href} asChild>
                      <a href={child.href} aria-current={childActive ? 'page' : undefined}>
                        {child.label}
                      </a>
                    </DropdownMenuItem>
                  );
                })}
              </DropdownMenuContent>
            </DropdownMenu>
          );
        }

        const active = isActivePath(pathname, item.href, variant);

        return (
          <a
            key={item.href}
            href={item.href}
            aria-current={active ? 'page' : undefined}
            className={cn(
              variant === 'app'
                ? 'inline-flex h-9 items-center rounded-lg px-2.5 text-[13px] font-medium transition-colors'
                : 'rounded-full px-3 py-1.5 text-sm font-medium transition',
              active
                ? variant === 'app'
                  ? 'bg-accent text-accent-foreground'
                  : 'bg-primary text-primary-foreground'
                : 'text-muted-foreground hover:bg-accent hover:text-foreground',
            )}
          >
            {item.label}
          </a>
        );
      })}
    </nav>
  );
}

export function PublicMobileNav({
  items,
  user = null,
  triggerLabel = 'Open public navigation',
  title = 'GameGuild',
  description = 'Move from learning to testing, projects, and community.',
}: {
  readonly items: readonly PublicNavEntry[];
  readonly user?: PublicWebsiteUser | null;
  readonly triggerLabel?: string;
  readonly title?: string;
  readonly description?: string;
}) {
  const pathname = usePathname() ?? '/';
  const [open, setOpen] = useState(false);

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger asChild>
        <Button
          type="button"
          variant="outline"
          size="icon"
          className="border-border bg-accent/30 text-foreground hover:bg-accent hover:text-foreground lg:hidden"
          aria-label={triggerLabel}
        >
          <Menu className="size-4" aria-hidden="true" />
        </Button>
      </SheetTrigger>
      <SheetContent side="right" className="border-border bg-popover text-popover-foreground">
        <SheetHeader>
          <SheetTitle className="text-foreground">{title}</SheetTitle>
          <SheetDescription className="text-muted-foreground">{description}</SheetDescription>
        </SheetHeader>

        <nav aria-label="Mobile navigation" className="mt-8 grid gap-6">
          {items.map((item) => {
            if (isNavGroup(item)) {
              return (
                <section key={item.label} aria-labelledby={`mobile-nav-${item.label.toLowerCase().replaceAll(/[^a-z0-9]+/g, '-')}`}>
                  <h2
                    id={`mobile-nav-${item.label.toLowerCase().replaceAll(/[^a-z0-9]+/g, '-')}`}
                    className="mb-2 px-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
                  >
                    {item.label}
                  </h2>
                  <div className="grid gap-1">
                    {item.items.map((child) => {
                      const active = isActivePath(pathname, child.href, 'app');

                      return (
                        <a
                          key={child.href}
                          href={child.href}
                          aria-current={active ? 'page' : undefined}
                          onClick={() => setOpen(false)}
                          className={cn(
                            'rounded-lg px-3 py-2.5 text-sm font-medium transition-colors',
                            active
                              ? 'bg-accent text-accent-foreground'
                              : 'text-foreground hover:bg-accent hover:text-accent-foreground',
                          )}
                        >
                          {child.label}
                        </a>
                      );
                    })}
                  </div>
                </section>
              );
            }

            const active = isActivePath(pathname, item.href);

            return (
              <a
                key={item.href}
                href={item.href}
                aria-current={active ? 'page' : undefined}
                onClick={() => setOpen(false)}
                className={cn(
                  'rounded-2xl border px-4 py-3 text-sm font-semibold transition',
                  active
                    ? 'border-primary bg-primary text-primary-foreground'
                    : 'border-border bg-accent/30 text-foreground hover:bg-accent hover:text-accent-foreground',
                )}
              >
                {item.label}
              </a>
            );
          })}
        </nav>

        <div className="mt-8 grid gap-3">
          {user ? (
            <Link
              href="/workspace"
              aria-label={`${user.name} profile`}
              onClick={() => setOpen(false)}
              className="inline-flex min-w-0 items-center gap-3 rounded-2xl border border-border bg-accent/40 px-4 py-3 text-left text-sm font-semibold text-foreground transition hover:bg-accent"
            >
              <span className="flex size-10 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-sm font-bold text-primary-foreground">
                {user.image ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img src={user.image} alt="" className="size-full object-cover" />
                ) : (
                  user.initials
                )}
              </span>
              <span className="min-w-0">
                <span className="block truncate">{user.name}</span>
                {user.email && <span className="block truncate text-xs font-medium text-muted-foreground">{user.email}</span>}
              </span>
            </Link>
          ) : (
            <>
              <Link
                href="/sign-up"
                onClick={() => setOpen(false)}
                className="inline-flex items-center justify-center rounded-full bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground transition hover:bg-primary/85"
              >
                Join community
              </Link>
              <Link
                href="/sign-in"
                onClick={() => setOpen(false)}
                className="inline-flex items-center justify-center rounded-full border border-border px-4 py-2 text-sm font-semibold text-foreground transition hover:bg-accent"
              >
                Sign in
              </Link>
            </>
          )}
          {user?.canManage ? <Link href="/dashboard" onClick={() => setOpen(false)} className="inline-flex items-center justify-center rounded-full border border-border px-4 py-2 text-sm font-semibold text-foreground transition hover:bg-accent">Dashboard</Link> : null}
          <a
            href="https://github.com/gameguild-gg/gameguild"
            onClick={() => setOpen(false)}
            className="inline-flex items-center justify-center gap-2 rounded-full border border-border px-4 py-2 text-sm font-semibold text-foreground transition hover:bg-accent"
          >
            <Github className="size-4" aria-hidden="true" />
            GitHub
          </a>
        </div>
      </SheetContent>
    </Sheet>
  );
}
