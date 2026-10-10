import { render, screen } from '@testing-library/react';
import fs from 'node:fs';
import path from 'node:path';
import type { AnchorHTMLAttributes, ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('@/i18n/navigation', () => ({
  Link: ({ href, children, ...props }: AnchorHTMLAttributes<HTMLAnchorElement> & { href: string; children: ReactNode }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
  usePathname: () => '/',
}));

import { PublicWebsiteFooter } from '@/components/app/app-shell';

const appRoot = __dirname;

const footerRoutes = [
  { href: '/sign-up', page: '[locale]/(auth)/sign-up/page.tsx' },
  { href: '/community', page: '[locale]/(public)/community/page.tsx' },
  { href: '/', page: '[locale]/(public)/page.tsx' },
  { href: '/jobs', page: '[locale]/(public)/jobs/page.tsx' },
  { href: '/courses', page: '[locale]/(public)/courses/page.tsx' },
  { href: '/programs', page: '[locale]/(public)/programs/page.tsx' },
  { href: '/projects', page: '[locale]/(social)/projects/page.tsx' },
  { href: '/testing-lab', page: '[locale]/(public)/testing-lab/page.tsx' },
  { href: '/launch-pad', page: '[locale]/(social)/launch-pad/page.tsx' },
  { href: '/about', page: '[locale]/(public)/about/page.tsx' },
  { href: '/about/roadmap', page: '[locale]/(public)/about/(project)/roadmap/page.tsx' },
  { href: '/about/contributors', page: '[locale]/(public)/about/(project)/contributors/page.tsx' },
  { href: '/contact', page: '[locale]/(public)/contact/page.tsx' },
  { href: '/legal/licenses', page: '[locale]/(legal)/legal/licenses/page.tsx' },
  { href: '/terms-of-service', page: '[locale]/(legal)/terms-of-service/page.tsx' },
  { href: '/polices/privacy', page: '[locale]/(legal)/polices/privacy/page.tsx' },
] as const;

describe('PublicWebsiteFooter routes', () => {
  it('links only to app routes that exist in the app router', () => {
    render(<PublicWebsiteFooter />);

    for (const route of footerRoutes) {
      const links = screen.getAllByRole('link').filter((link) => link.getAttribute('href') === route.href);
      expect(links.length, `Expected footer link for ${route.href}`).toBeGreaterThan(0);
      expect(fs.existsSync(path.join(appRoot, route.page)), `Expected route file for ${route.href}`).toBe(true);
    }

    expect(screen.queryByRole('heading', { name: 'Legal' })).toBeNull();
    expect(screen.queryByRole('link', { name: 'Cookies' })).toBeNull();
    expect(screen.queryByTestId('footer-cta')).toBeNull();
    expect(screen.getByText('GameGuild')).toBeInTheDocument();
    expect(screen.getByText(/A thriving gaming community dedicated to education/i)).toBeInTheDocument();
    expect(screen.getByText('Community-driven learning and development')).toBeInTheDocument();
    expect(screen.getByText('Open source and collaborative')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Discord' })).toHaveAttribute('href', 'https://discord.gg/9CdJeQ2XKB');
    expect(screen.getByRole('link', { name: 'Twitter' })).toHaveAttribute('href', 'https://twitter.com/gameguild_gg');
    expect(screen.getByRole('link', { name: 'GitHub' })).toHaveAttribute('href', 'https://github.com/gameguild-gg/gameguild');
    expect(screen.getByRole('link', { name: 'YouTube' })).toHaveAttribute('href', 'https://youtube.com/@gameguild');
    const copyright = screen.getByText('© 2026 GameGuild. All rights reserved.');
    const legalNavigation = screen.getByRole('navigation', { name: 'Legal' });

    expect(copyright).toBeInTheDocument();
    expect(copyright.parentElement).toBe(legalNavigation.parentElement);
  });
});
