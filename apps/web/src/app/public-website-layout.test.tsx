import { render, screen, within } from '@testing-library/react';
import type { AnchorHTMLAttributes, ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('@/i18n', () => ({
  Link: ({ href, children, ...props }: AnchorHTMLAttributes<HTMLAnchorElement> & { href: string; children: ReactNode }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock('@/i18n/navigation', () => ({
  Link: ({ href, children, ...props }: AnchorHTMLAttributes<HTMLAnchorElement> & { href: string; children: ReactNode }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
  usePathname: () => '/',
}));

import InstitutionalLayout from './[locale]/(public)/layout';

describe('public website layouts', () => {
  it('wraps public routes with the website header and footer', async () => {
    render(await InstitutionalLayout({ children: <main><h1>About GameGuild</h1></main> } as LayoutProps<'/[locale]'>));

    const banner = screen.getByRole('banner');
    const mainNavigation = within(banner).getByRole('navigation', { name: /main navigation/i });

    expect(within(banner).getByRole('link', { name: /gameguild home/i })).toBeInTheDocument();
    expect(within(mainNavigation).getByRole('button', { name: /^learn$/i })).toBeInTheDocument();
    expect(within(mainNavigation).getByRole('link', { name: /^testing lab$/i })).toBeInTheDocument();
    expect(screen.getByRole('contentinfo')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /about gameguild/i })).toBeInTheDocument();
  });
});
