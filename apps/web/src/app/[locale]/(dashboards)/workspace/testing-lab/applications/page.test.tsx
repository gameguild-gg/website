import { render, screen } from '@testing-library/react';
import type { ComponentProps } from 'react';
import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getTestingApplicationsDirectory: vi.fn(),
  getTestingProjectOptions: vi.fn(),
  getMembers: vi.fn(),
}));

vi.mock('@/lib/testing-lab/events-queries', () => ({
  getTestingApplicationsDirectory: mocks.getTestingApplicationsDirectory,
}));
vi.mock('@/lib/testing-lab/queries', () => ({
  getTestingProjectOptions: mocks.getTestingProjectOptions,
}));
vi.mock('@/lib/community/queries/members', () => ({
  getMembers: mocks.getMembers,
}));
vi.mock('@/i18n/navigation', () => ({
  Link: ({ href, ...props }: ComponentProps<'a'>) => <a href={String(href)} {...props} />,
}));

import TestingLabApplicationsPage from './page';

describe('Testing Lab applications directory', () => {
  it('renders tenant applications with event, Project, and submitter context', async () => {
    mocks.getTestingApplicationsDirectory.mockResolvedValue({
      entries: [
        {
          event: { id: 'event-1', name: 'Friday campus lab' },
          application: {
            id: 'application-1',
            eventId: 'event-1',
            projectId: 'project-1',
            submittedByUserId: 'user-1',
            preferredAvailability: 'After 18:00',
            status: 'Pending',
            votes: [],
          },
        },
      ],
      accessIssues: [],
    });
    mocks.getTestingProjectOptions.mockResolvedValue([{ id: 'project-1', title: 'Orbit Tactics' }]);
    mocks.getMembers.mockResolvedValue({
      members: [{ id: 'user-1', displayName: 'Ana Applicant', email: 'ana@example.test' }],
      total: 1,
    });

    render(
      await TestingLabApplicationsPage({
        searchParams: Promise.resolve({ status: 'Pending' }),
      }),
    );

    expect(screen.getByRole('heading', { name: 'Project applications' })).toBeInTheDocument();
    expect(screen.getByText('Friday campus lab')).toBeInTheDocument();
    expect(screen.getByText('Orbit Tactics')).toBeInTheDocument();
    expect(screen.getByText('Ana Applicant / ana@example.test')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /review in friday campus lab/i })).toHaveAttribute(
      'href',
      '/workspace/testing-lab/events/event-1/applications?applicationStatus=Pending',
    );
    expect(mocks.getTestingApplicationsDirectory).toHaveBeenCalledWith({ status: 'Pending' });
  });

  it('renders a useful empty state', async () => {
    mocks.getTestingApplicationsDirectory.mockResolvedValue({ entries: [], accessIssues: [] });
    mocks.getTestingProjectOptions.mockResolvedValue([]);
    mocks.getMembers.mockResolvedValue({ members: [], total: 0 });

    render(await TestingLabApplicationsPage({ searchParams: Promise.resolve({}) }));

    expect(screen.getByText('No project applications')).toBeInTheDocument();
    expect(screen.getByText(/applications submitted to managed events will appear here/i)).toBeInTheDocument();
  });
});
