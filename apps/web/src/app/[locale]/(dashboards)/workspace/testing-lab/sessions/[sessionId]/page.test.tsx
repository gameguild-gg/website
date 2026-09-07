import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getTestingSessionDetail: vi.fn(),
  getTestingProjectOptions: vi.fn(),
  getTestingLabDashboard: vi.fn(),
  getMembers: vi.fn(),
  push: vi.fn(),
}));

vi.mock('@/lib/testing-lab', () => ({
  getTestingSessionDetail: mocks.getTestingSessionDetail,
  getTestingProjectOptions: mocks.getTestingProjectOptions,
  getTestingLabDashboard: mocks.getTestingLabDashboard,
  normalizeTestingSessionStatus: (status: string) => status,
}));
vi.mock('@/lib/community/queries/members', () => ({
  getMembers: mocks.getMembers,
}));
vi.mock('next/navigation', async (importOriginal) => ({
  usePathname: () => '/workspace/learning',
  ...(await importOriginal<typeof import('next/navigation')>()),
  useRouter: () => ({ push: mocks.push }),
}));
vi.mock('@/lib/testing-lab/actions', () => ({
  deleteTestingSession: vi.fn(),
  linkTestingSessionProject: vi.fn(),
  restoreTestingSession: vi.fn(),
  unlinkTestingSessionProject: vi.fn(),
  updateTestingAttendance: vi.fn(),
  updateTestingSession: vi.fn(),
}));

import TestingSessionDetailPage from './page';

describe('Testing Session detail', () => {
  it('uses human tester labels and dialog-based project linking without native selects', async () => {
    mocks.getTestingSessionDetail.mockResolvedValue({
      session: {
        id: 'session-1',
        sessionName: 'Evening playtest',
        sessionDate: '2026-08-12',
        status: 'Scheduled',
        maxTesters: 10,
        isDeleted: false,
        location: { name: 'Room 2' },
      },
      registrations: [
        {
          id: 'registration-1',
          userId: 'user-1',
          status: 'Registered',
          attendanceStatus: 'Registered',
        },
      ],
      waitlist: [],
      projects: [],
      accessIssues: [],
    });
    mocks.getTestingProjectOptions.mockResolvedValue([{ id: 'project-1', title: 'Asterion' }]);
    mocks.getTestingLabDashboard.mockResolvedValue({ locations: [] });
    mocks.getMembers.mockResolvedValue({
      members: [
        {
          id: 'user-1',
          displayName: 'Bruno Tester',
          email: 'bruno@example.com',
        },
      ],
      total: 1,
    });

    const user = userEvent.setup();
    const view = render(
      await TestingSessionDetailPage({
        params: Promise.resolve({ sessionId: 'session-1' }),
      }),
    );

    expect(screen.getByText('Bruno Tester')).toBeInTheDocument();
    expect(screen.queryByText('user-1')).not.toBeInTheDocument();
    expect(view.container.querySelector('select:not([aria-hidden="true"])')).toBeNull();

    await user.click(screen.getByRole('button', { name: 'Link project' }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Link a project' })).toBeInTheDocument();
  });
});
