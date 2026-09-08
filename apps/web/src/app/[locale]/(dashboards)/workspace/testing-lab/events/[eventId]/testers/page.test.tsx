import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getTestingEventWorkspaceData: vi.fn(),
  getTestingApplicationTesterEligibility: vi.fn(),
  getMembers: vi.fn(),
  getTestingProjectOptions: vi.fn(),
}));

vi.mock('next/navigation', async (importOriginal) => ({
  usePathname: () => '/workspace/learning',
  ...(await importOriginal<typeof import('next/navigation')>()),
  useRouter: () => ({ refresh: vi.fn() }),
}));

vi.mock('@/lib/testing-lab/events-queries', () => ({
  getTestingEventWorkspaceData: mocks.getTestingEventWorkspaceData,
  getTestingApplicationTesterEligibility: mocks.getTestingApplicationTesterEligibility,
}));

vi.mock('@/lib/community/queries/members', () => ({
  getMembers: mocks.getMembers,
}));

vi.mock('@/lib/testing-lab/queries', () => ({
  getTestingProjectOptions: mocks.getTestingProjectOptions,
}));

vi.mock('@/lib/testing-lab/events-actions', () => ({
  updateTestingEventAttendance: vi.fn(),
  assignTestedProjectToRegistration: vi.fn(),
}));

import TestingEventTestersPage from './page';

describe('Testing Event testers', () => {
  it('shows tester names and project assignment only after check-in', async () => {
    mocks.getTestingEventWorkspaceData.mockResolvedValue({
      event: { id: 'event-1', status: 'Active' },
      slots: [
        { id: 'slot-1', startsAt: '2026-08-12T18:00:00.000Z', mode: 'Online' },
        { id: 'slot-2', startsAt: '2026-08-13T18:00:00.000Z', mode: 'Online' },
      ],
      applications: [
        {
          id: 'application-1',
          projectId: 'project-1',
          assignedSlotId: 'slot-1',
          status: 'Approved',
        },
      ],
      registrationsBySlot: {
        'slot-1': [
          {
            id: 'registration-cancelled',
            slotId: 'slot-1',
            userId: 'tester-1',
            status: 'Cancelled',
            pendingFeedbackCount: 1,
          },
          {
            id: 'registration-1',
            slotId: 'slot-1',
            userId: 'tester-1',
            status: 'CheckedIn',
            pendingFeedbackCount: 1,
          },
        ],
        'slot-2': [
          {
            id: 'registration-2',
            slotId: 'slot-2',
            userId: 'tester-2',
            status: 'Registered',
          },
        ],
      },
      committee: [],
      accessIssues: [],
    });
    mocks.getMembers.mockResolvedValue({
      members: [
        { id: 'tester-1', displayName: 'Ana Tester', email: 'ana@example.com' },
        {
          id: 'tester-2',
          displayName: 'Bruno Tester',
          email: 'bruno@example.com',
        },
      ],
      total: 2,
    });
    mocks.getTestingProjectOptions.mockResolvedValue([{ id: 'project-1', title: 'Asterion' }]);
    mocks.getTestingApplicationTesterEligibility.mockResolvedValue({
      eligibility: [
        { testerUserId: 'tester-1', eligibleApplicationIds: ['application-1'] },
        { testerUserId: 'tester-2', eligibleApplicationIds: [] },
      ],
      accessIssues: [],
    });

    render(
      await TestingEventTestersPage({
        params: Promise.resolve({ eventId: 'event-1' }),
      }),
    );

    expect(screen.getAllByText('Ana Tester')).toHaveLength(2);
    expect(screen.getByText('Bruno Tester')).toBeInTheDocument();
    expect(screen.queryByText('tester-1')).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Assign tested project' })).toHaveLength(1);
    expect(screen.getByText('2 testers registered across this event.')).toBeInTheDocument();
    expect(screen.getAllByText('1 registered')).toHaveLength(2);
    expect(screen.getByText('0 pending feedback / Cancelled')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Update' })).toHaveLength(2);
    expect(mocks.getTestingApplicationTesterEligibility).toHaveBeenCalledWith(
      'event-1',
      ['tester-1', 'tester-2'],
    );
  });
});
