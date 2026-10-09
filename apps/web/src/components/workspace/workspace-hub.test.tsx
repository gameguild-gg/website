import '@testing-library/jest-dom/vitest';
import { cleanup, render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getWorkspaceTeams: vi.fn(),
  getWorkspaceProjects: vi.fn(),
  getWorkspaceMyTeamInvitations: vi.fn(),
  listMyBlogPosts: vi.fn(),
}));

vi.mock('@/i18n/navigation', () => ({
  Link: ({ children, href, ...props }: { children: ReactNode; href: string }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

vi.mock('@/lib/workspaces', () => ({
  getWorkspaceTeams: mocks.getWorkspaceTeams,
  getWorkspaceProjects: mocks.getWorkspaceProjects,
  getWorkspaceMyTeamInvitations: mocks.getWorkspaceMyTeamInvitations,
}));

vi.mock('@/lib/blogs/queries', () => ({
  listMyBlogPosts: mocks.listMyBlogPosts,
}));

import { WorkspaceHub } from './workspace-hub';

describe('WorkspaceHub', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getWorkspaceTeams.mockResolvedValue([]);
    mocks.getWorkspaceProjects.mockResolvedValue([]);
    mocks.getWorkspaceMyTeamInvitations.mockResolvedValue([]);
    mocks.listMyBlogPosts.mockResolvedValue([]);
  });

  afterEach(cleanup);

  it('renders its navigation actions as styled links without a Radix Slot boundary', async () => {
    render(await WorkspaceHub());

    expect(screen.getByRole('link', { name: 'Team' })).toHaveAttribute('href', '/workspace/teams/new');
    expect(screen.getByRole('link', { name: 'Project' })).toHaveAttribute('href', '/workspace/projects/new');
    expect(screen.getByRole('link', { name: 'All teams' })).toHaveAttribute('href', '/workspace/teams');
    expect(screen.getByRole('link', { name: 'All projects' })).toHaveAttribute('href', '/workspace/projects');
    expect(screen.getByRole('link', { name: 'Open assigned work' })).toHaveAttribute('href', '/workspace/work');
    expect(screen.getByText('Create a team to collaborate on projects.')).toBeInTheDocument();
    expect(screen.getByText('Create a project or join a team project.')).toBeInTheDocument();
  });

  it('renders recent teams, projects, and invitation totals', async () => {
    mocks.getWorkspaceTeams.mockResolvedValue([{ id: 'team-1', slug: 'alpha', name: 'Alpha' }]);
    mocks.getWorkspaceProjects.mockResolvedValue([{ id: 'project-1', slug: 'arcade', title: 'Arcade', status: 'Active' }]);
    mocks.getWorkspaceMyTeamInvitations.mockResolvedValue([{ id: 'invitation-1' }]);

    render(await WorkspaceHub());

    expect(screen.getByRole('link', { name: /Alpha/ })).toHaveAttribute('href', '/workspace/teams/alpha');
    expect(screen.getByRole('link', { name: /Arcade/ })).toHaveAttribute('href', '/workspace/projects/arcade');
    expect(screen.getByText('Invitations').closest('[data-slot="card"]')).toHaveTextContent('1');
  });

  it('links blog post rows to the authoring editor via the profile handle', async () => {
    mocks.listMyBlogPosts.mockResolvedValue([
      {
        id: 'post-1',
        slug: 'hello-world',
        title: 'Hello world',
        status: 'Published',
        format: 'Markdown',
        publishedAt: '2026-01-15T10:00:00Z',
        updatedAt: '2026-01-15T10:00:00Z',
        primaryAuthorHandle: 'f3manual_a',
      },
    ]);

    render(await WorkspaceHub());

    expect(screen.getByText('Blog posts').closest('[data-slot="card"]')).toHaveTextContent('1');
    expect(screen.getByRole('link', { name: /Hello world/ })).toHaveAttribute('href', '/blogs/f3manual_a/hello-world/edit');
    expect(screen.getByRole('link', { name: 'Post' })).toHaveAttribute('href', '/blog/new');
  });

  it('falls back to the blogs index when the handle cannot be resolved', async () => {
    mocks.listMyBlogPosts.mockResolvedValue([
      {
        id: 'post-2',
        slug: 'draft-note',
        title: 'Draft note',
        status: 'Draft',
        format: 'Lexical',
        publishedAt: null,
        updatedAt: '2026-02-01T08:00:00Z',
        primaryAuthorHandle: null,
      },
    ]);

    render(await WorkspaceHub());

    expect(screen.getByRole('link', { name: /Draft note/ })).toHaveAttribute('href', '/blogs');
  });

  it('offers a new-post action in the empty state', async () => {
    render(await WorkspaceHub());

    expect(screen.getByText('No posts yet.')).toBeInTheDocument();
    const card = screen.getByText('Recent blog posts').closest('[data-slot="card"]');
    const emptyState = screen.getByText('No posts yet.').parentElement?.parentElement;
    const newPostLinks = screen.getAllByRole('link', { name: /New post/ });
    expect(newPostLinks).toHaveLength(2);
    expect(newPostLinks[0]).toHaveAttribute('href', '/blog/new');
    expect(card).toContainElement(newPostLinks[0]);
    expect(emptyState).toContainElement(newPostLinks[1]);
  });

  it('keeps the workspace available when blog posts cannot be loaded', async () => {
    mocks.listMyBlogPosts.mockRejectedValue(new Error('Network error'));

    render(await WorkspaceHub());

    expect(screen.getByRole('heading', { name: 'Your teams and projects' })).toBeInTheDocument();
    expect(screen.getByText('Blog posts').closest('[data-slot="card"]')).toHaveTextContent('Unavailable');
    expect(screen.getByText('Blog posts are temporarily unavailable.')).toBeInTheDocument();
    expect(screen.queryByText('No posts yet.')).not.toBeInTheDocument();
  });
});
