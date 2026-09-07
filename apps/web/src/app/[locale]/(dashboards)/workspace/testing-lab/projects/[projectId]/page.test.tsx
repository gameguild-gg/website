import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getTestingLabProjectDetail: vi.fn(),
  getMembers: vi.fn(),
  push: vi.fn(),
}));

vi.mock('@/lib/testing-lab', () => ({
  getTestingLabProjectDetail: mocks.getTestingLabProjectDetail,
  normalizeTestingRequestStatus: (status: string) => status,
}));
vi.mock('@/lib/community/queries/members', () => ({
  getMembers: mocks.getMembers,
}));
vi.mock('@/i18n/navigation', () => ({
  Link: ({ children }: { children: ReactNode }) => children,
}));
vi.mock('next/navigation', () => ({
  usePathname: () => '/workspace/learning',
  notFound: vi.fn(),
  useRouter: () => ({ push: mocks.push }),
}));
vi.mock('@/lib/testing-lab/actions', () => ({
  addTestingParticipant: vi.fn(),
  deleteTestingRequest: vi.fn(),
  removeTestingParticipant: vi.fn(),
  restoreTestingRequest: vi.fn(),
  updateTestingRequest: vi.fn(),
}));

import TestingProjectDetailPage from './page';

describe('Testing Project detail', () => {
  it('uses human participant labels and a dialog for participant management', async () => {
    mocks.getTestingLabProjectDetail.mockResolvedValue({
      project: { id: 'project-1', title: 'Asterion' },
      request: {
        id: 'request-1',
        title: 'Asterion usability pass',
        description: 'Validate the onboarding loop.',
        status: 'InProgress',
        isDeleted: false,
      },
      participants: [
        {
          id: 'participant-1',
          userId: 'user-1',
          status: 'Registered',
          timeSpentMinutes: 20,
        },
      ],
      sessions: [],
      feedback: [],
      accessIssues: [],
    });
    mocks.getMembers.mockResolvedValue({
      members: [{ id: 'user-1', displayName: 'Ana Member', email: 'ana@example.com' }],
      total: 1,
    });

    const user = userEvent.setup();
    const view = render(
      await TestingProjectDetailPage({
        params: Promise.resolve({ projectId: 'request-1' }),
      }),
    );

    expect(screen.getByText('Ana Member')).toBeInTheDocument();
    expect(screen.queryByText('user-1')).not.toBeInTheDocument();
    expect(view.container.querySelector('select')).toBeNull();

    await user.click(screen.getByRole('button', { name: 'Add participant' }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Add a participant' })).toBeInTheDocument();
  });

  it('renders a shared project without surfacing a request-not-found error', async () => {
    mocks.getTestingLabProjectDetail.mockResolvedValue({
      project: {
        id: 'project-1',
        title: 'Asterion',
        description: 'A shared platform project.',
        status: 'Published',
        developmentStatus: 'InDevelopment',
      },
      request: null,
      sessions: [],
      participants: [],
      feedback: [],
      accessIssues: [],
    });
    mocks.getMembers.mockResolvedValue({ members: [], total: 0 });

    render(
      await TestingProjectDetailPage({
        params: Promise.resolve({ projectId: 'project-1' }),
      }),
    );

    expect(screen.getByText('Not submitted to Testing Lab yet')).toBeInTheDocument();
    expect(screen.getByText('A shared platform project.')).toBeInTheDocument();
    expect(screen.queryByText(/Testing request returned 404/i)).not.toBeInTheDocument();
  });

  it('opens an archived request as read-only and keeps restore available', async () => {
    mocks.getTestingLabProjectDetail.mockResolvedValue({
      project: { id: 'project-1', title: 'Asterion' },
      request: {
        id: 'request-1',
        title: 'Archived usability pass',
        status: 'Completed',
        isDeleted: true,
      },
      participants: [{ id: 'participant-1', userId: 'user-1', status: 'Completed' }],
      sessions: [],
      feedback: [],
      accessIssues: [],
    });
    mocks.getMembers.mockResolvedValue({
      members: [{ id: 'user-1', displayName: 'Ana Member', email: 'ana@example.com' }],
      total: 1,
    });

    render(
      await TestingProjectDetailPage({
        params: Promise.resolve({ projectId: 'request-1' }),
      }),
    );

    expect(screen.getByText('Archived request')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Restore' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add participant' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Edit request' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Remove' })).not.toBeInTheDocument();
  });
});
