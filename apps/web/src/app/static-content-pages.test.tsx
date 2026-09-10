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

vi.mock('@/lib/community/public-community-queries', () => ({
  getPublicActivities: vi.fn().mockResolvedValue([]),
  getPublicMemberSpotlights: vi.fn().mockResolvedValue([]),
  getPublicPlaytests: vi.fn().mockResolvedValue([]),
}));

vi.mock('@/lib/projects/public-projects', () => ({
  getPublishedProjects: vi.fn().mockResolvedValue([]),
}));

import LicensesPage from './[locale]/(legal)/legal/licenses/page';
import FerpaWaiverPage from './[locale]/(legal)/legal/ferpa-waiver/page';
import AcademicHonestyPage from './[locale]/(legal)/legal/academic-honesty/page';
import RoadmapPage from './[locale]/(public)/about/(project)/roadmap/page';
import ContributorsPage from './[locale]/(public)/about/(project)/contributors/page';
import HomeLayout from './[locale]/(public)/layout';
import HomePage from './[locale]/(public)/page';

describe('static legal and project pages', () => {
  it('renders the public home page with a website header and footer', async () => {
    const homeContent = await HomePage({ params: Promise.resolve({ locale: 'en-US' }) } as PageProps<'/[locale]'>);
    render(await HomeLayout({ children: homeContent }));

    const banner = screen.getByRole('banner');
    const mainNavigation = within(banner).getByRole('navigation', { name: /main navigation/i });
    expect(banner).toBeInTheDocument();
    expect(within(banner).getByRole('link', { name: /gameguild home/i })).toBeInTheDocument();
    expect(within(mainNavigation).getByRole('button', { name: /^learn$/i })).toBeInTheDocument();
    expect(within(mainNavigation).getByRole('link', { name: /^testing lab$/i })).toBeInTheDocument();
    expect(within(mainNavigation).getByRole('button', { name: /^more$/i })).toBeInTheDocument();
    expect(within(banner).getByRole('link', { name: /^sign in$/i })).toBeInTheDocument();

    expect(screen.getByRole('contentinfo')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /learn, build & connect/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /everything you need to succeed/i })).toBeInTheDocument();
    expect(screen.getByText(/thriving gaming community dedicated to education/i)).toBeInTheDocument();
    expect(screen.queryByText(/temporary public home/i)).not.toBeInTheDocument();
  });

  it('renders real license guidance instead of a reconstruction placeholder', async () => {
    render(await LicensesPage());

    expect(screen.getByRole('heading', { name: /licenses/i })).toBeInTheDocument();
    expect(screen.getAllByText(/third-party packages/i).length).toBeGreaterThan(0);
    expect(screen.queryByText(/under reconstruction/i)).not.toBeInTheDocument();
  });

  it('renders FERPA waiver content with consent and revocation guidance', async () => {
    render(await FerpaWaiverPage({} as PageProps<'/[locale]/legal/ferpa-waiver'>));

    expect(screen.getByRole('heading', { name: /ferpa waiver/i })).toBeInTheDocument();
    expect(screen.getAllByText(/education records/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/revoke/i).length).toBeGreaterThan(0);
    expect(screen.queryByText(/under reconstruction/i)).not.toBeInTheDocument();
  });

  it('renders academic honesty policy content', async () => {
    render(await AcademicHonestyPage({} as PageProps<'/[locale]/legal/academic-honesty'>));

    expect(screen.getByRole('heading', { name: /academic honesty/i })).toBeInTheDocument();
    expect(screen.getAllByText(/plagiarism/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/assessment/i).length).toBeGreaterThan(0);
    expect(screen.queryByText(/under reconstruction/i)).not.toBeInTheDocument();
  });

  it('renders the roadmap with day-zero product tracks', async () => {
    render(await RoadmapPage({} as PageProps<'/[locale]/about/roadmap'>));

    expect(screen.getByRole('heading', { name: /development roadmap/i })).toBeInTheDocument();
    expect(screen.getByText(/learning platform/i)).toBeInTheDocument();
    expect(screen.getByText(/testing lab/i)).toBeInTheDocument();
    expect(screen.getByText(/launch pad/i)).toBeInTheDocument();
    expect(screen.queryByText(/under reconstruction/i)).not.toBeInTheDocument();
  });

  it('renders contributor governance and project ownership content', async () => {
    render(await ContributorsPage({} as PageProps<'/[locale]/about/contributors'>));

    expect(screen.getByRole('heading', { level: 1, name: /contributors/i })).toBeInTheDocument();
    expect(screen.getAllByText(/maintainers/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/review standards/i).length).toBeGreaterThan(0);
    expect(screen.queryByText(/under reconstruction/i)).not.toBeInTheDocument();
  });
});
