import { fireEvent, render, screen, within } from '@testing-library/react';
import type { AnchorHTMLAttributes, ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { authMock, getTokenMock, requestMock, getPublishedProjectsMock, getVisibleProjectMock, publicQueriesMocks } = vi.hoisted(() => ({
  authMock: vi.fn(),
  getTokenMock: vi.fn(),
  requestMock: vi.fn(),
  getPublishedProjectsMock: vi.fn(),
  getVisibleProjectMock: vi.fn(),
  publicQueriesMocks: {
    getPublicPlaytests: vi.fn(),
    getPublicMemberSpotlights: vi.fn(),
    getPublicActivities: vi.fn(),
    getPublicCommunityGroups: vi.fn(),
  },
}));

vi.mock('@/auth', () => ({
  auth: authMock,
  getSession: authMock,
  getToken: getTokenMock,
}));

vi.mock('@game-guild/client', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@game-guild/client')>()),
  createServerClient: () => ({ request: requestMock }),
}));

vi.mock('@game-guild/client/react', () => ({
  useAuth: () => ({ signOut: vi.fn() }),
}));

vi.mock('@/i18n/navigation', () => ({
  Link: ({ href, children, ...props }: AnchorHTMLAttributes<HTMLAnchorElement> & { href: string; children: ReactNode }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
  usePathname: () => '/',
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/i18n', () => ({
  Link: ({ href, children, ...props }: AnchorHTMLAttributes<HTMLAnchorElement> & { href: string; children: ReactNode }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock('@/lib/projects/public-projects', () => ({
  getPublishedProjects: getPublishedProjectsMock,
  getVisibleProject: getVisibleProjectMock,
}));

vi.mock('@/lib/community/public-community-queries', () => publicQueriesMocks);

import { PublicWebsiteHeader } from '@/components/app/app-shell';
import CommunityPage from './[locale]/(public)/community/page';
import JobsPage from './[locale]/(public)/jobs/page';
import LaunchPadPage from './[locale]/(public)/launch-pad/page';
import ShowcasePage from './[locale]/(public)/projects/page';
import ProjectDetailPage from './[locale]/(public)/projects/[slug]/page';
import TestingLabPage from './[locale]/(public)/testing-lab/page';
import HomePage from './[locale]/(public)/page';

const publishedProject = {
  slug: 'real-api-project',
  title: 'Real API Project',
  creator: 'Ada Builder',
  creatorRole: 'Game creator',
  summary: 'A real published project returned by the Projects API.',
  description: 'This project is rendered from the tenant-scoped Projects API instead of static showcase data.',
  status: 'Published',
  tags: ['API', 'Testing Lab'],
  coursePath: 'Independent project',
  accent: 'from-sky-400/30 via-cyan-300/10 to-slate-950',
  previewImage: 'https://images.unsplash.com/photo-1511512578047-dfb367046420?w=1400&h=900&fit=crop',
  buildType: 'Game',
  feedbackGoal: 'Validate the public project route.',
  metrics: [
    { label: 'Releases', value: '1' },
    { label: 'Followers', value: '2' },
    { label: 'Feedback', value: '3' },
  ],
  media: [{ label: 'Latest release', detail: '1.0.0' }],
};

describe('public community website UX', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getPublishedProjectsMock.mockResolvedValue([publishedProject]);
    getVisibleProjectMock.mockResolvedValue(publishedProject);
    publicQueriesMocks.getPublicPlaytests.mockResolvedValue([
      {
        title: 'Real API Playtest',
        date: 'Tuesday, 7:00 PM UTC',
        format: 'Online session',
        seats: '8 open seats',
        href: '/testing-lab/events/event-1',
      },
    ]);
    publicQueriesMocks.getPublicMemberSpotlights.mockResolvedValue([
      {
        name: 'Ada Builder',
        handle: '@adabuilder',
        role: 'Game creator',
        focus: 'Independent project',
        contribution: 'A real published project returned by the Projects API.',
      },
    ]);
    publicQueriesMocks.getPublicActivities.mockResolvedValue([
      {
        actor: 'Ada Builder',
        action: 'recently updated the project',
        target: 'Real API Project',
        href: '/projects/real-api-project',
      },
    ]);
    publicQueriesMocks.getPublicCommunityGroups.mockResolvedValue([
      { name: 'Independent project', description: '1 published community project.', projectCount: 1 },
    ]);
    requestMock.mockImplementation(async ({ path }: { path: string }) =>
      path === '/v1/access/capabilities'
        ? { ok: true, data: { capabilities: [] } }
        : { ok: true, data: [] },
    );
  });

  it('exposes a compact community-first information architecture in the header', async () => {
    authMock.mockResolvedValueOnce(null);

    render(await PublicWebsiteHeader());

    const nav = screen.getByRole('navigation', { name: /main navigation/i });
    expect(within(nav).getAllByRole('link').map((link) => link.textContent)).toEqual([
      'Community',
      'Projects',
      'Testing Lab',
      'Launch Pad',
    ]);
    expect(within(nav).getAllByRole('button').map((button) => button.textContent)).toEqual(['Learn', 'More']);

    fireEvent.click(within(nav).getByRole('button', { name: /learn/i }));
    expect((await screen.findAllByRole('menuitem')).map((item) => item.textContent)).toEqual(['Courses', 'Programs']);
  });

  it('shows the authenticated member profile instead of sign-in calls to action', async () => {
    authMock.mockResolvedValueOnce({
      user: {
        id: 'user-1',
        name: 'Ada Lovelace',
        email: 'ada@gameguild.gg',
        image: null,
      },
      expires: '2026-12-31T00:00:00.000Z',
    });

    render(await PublicWebsiteHeader());

    expect(screen.queryByRole('link', { name: /^sign in$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /join community/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /open ada lovelace account menu/i })).toBeInTheDocument();
    expect(screen.getByText('AL')).toBeInTheDocument();
  });

  it('turns the home page into a community gateway', async () => {
    authMock.mockResolvedValueOnce(null);

    render(await HomePage({ params: Promise.resolve({ locale: 'en-US' }) } as PageProps<'/[locale]'>));

    expect(screen.getByRole('heading', { name: /learn, build & connect/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /latest projects/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Real API Project' })).toBeInTheDocument();
    expect(getPublishedProjectsMock).toHaveBeenCalledOnce();
    expect(screen.getByRole('link', { name: /real api playtest/i })).toBeInTheDocument();
    expect(screen.getAllByText('Ada Builder').length).toBeGreaterThan(0);
    expect(screen.getByText(/recently updated the project/i)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /active members/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /upcoming playtests/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /community activity/i })).toBeInTheDocument();
  });

  it('renders a public project showcase and detail path', async () => {
    render(await ShowcasePage());

    expect(screen.getByRole('heading', { name: /project showcase/i })).toBeInTheDocument();
    expect(screen.getByAltText(/real api project project preview/i)).toBeInTheDocument();
    expect(screen.getAllByRole('link', { name: /view project/i }).length).toBeGreaterThan(0);
    expect(screen.getByRole('link', { name: /submit to testing lab/i })).toBeInTheDocument();
    expect(getPublishedProjectsMock).toHaveBeenCalledOnce();
  });

  it('renders project detail with creator, media, playtest status, and community CTAs', async () => {
    render(await ProjectDetailPage({ params: Promise.resolve({ slug: 'real-api-project' }) }));

    expect(screen.getByRole('heading', { name: /real api project/i })).toBeInTheDocument();
    expect(screen.getByAltText(/real api project project preview/i)).toBeInTheDocument();
    expect(screen.getAllByText(/creator/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/playtest/i).length).toBeGreaterThan(0);
    expect(screen.getByRole('link', { name: /join this playtest/i })).toBeInTheDocument();
    expect(getVisibleProjectMock).toHaveBeenCalledWith('real-api-project');
  });

  it('returns not found when the Projects API hides an inaccessible slug', async () => {
    getVisibleProjectMock.mockResolvedValueOnce(null);

    await expect(
      ProjectDetailPage({ params: Promise.resolve({ project: 'private-project' }) }),
    ).rejects.toThrow('NEXT_HTTP_ERROR_FALLBACK;404');
  });

  it('renders the community hub and public testing lab entry', async () => {
    render(await CommunityPage());

    expect(screen.getByRole('heading', { name: /community hub/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /member spotlights/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /recent activity/i })).toBeInTheDocument();

    render(await TestingLabPage());
    expect(screen.getByRole('heading', { name: /game testing lab/i, level: 1 })).toBeInTheDocument();
    expect(screen.getByText(/community playtesting is live/i)).toBeInTheDocument();
    expect(screen.getByText(/help community creators improve their games/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /browse events/i })).toHaveAttribute('href', '/testing-lab/events');
  });

  it('renders the public launch pad entry for release-ready projects', async () => {
    render(await LaunchPadPage());

    expect(screen.getByRole('heading', { name: /launch pad/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /from project to public release/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /readiness signals/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /discover launch pad events/i })).toHaveAttribute('href', '/launch-pad/events');
  });

  it('replaces the jobs placeholder with a community opportunities page', async () => {
    render(await JobsPage());

    expect(screen.getByRole('heading', { name: /community opportunities/i })).toBeInTheDocument();
    expect(screen.getAllByText(/mentor/i).length).toBeGreaterThan(0);
    expect(screen.queryByText(/show modal immediately/i)).not.toBeInTheDocument();
  });
});
