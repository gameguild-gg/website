import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getTestingEventWorkspaceData: vi.fn(),
  getTestingEventFeedbackReview: vi.fn(),
  getMembers: vi.fn(),
  getTestingProjectOptions: vi.fn(),
}));

vi.mock('@/lib/testing-lab/events-queries', () => ({
  getTestingEventWorkspaceData: mocks.getTestingEventWorkspaceData,
  getTestingEventFeedbackReview: mocks.getTestingEventFeedbackReview,
}));

vi.mock('@/lib/community/queries/members', () => ({
  getMembers: mocks.getMembers,
}));
vi.mock('@/lib/testing-lab/queries', () => ({
  getTestingProjectOptions: mocks.getTestingProjectOptions,
}));

import TestingEventFeedbackPage from './page';

describe('Testing Event feedback review', () => {
  it('shows real submitted feedback and pending obligations with human labels', async () => {
    mocks.getTestingEventWorkspaceData.mockResolvedValue({
      event: { id: 'event-1', name: 'Friday lab' },
      slots: [{ id: 'slot-1', startsAt: '2026-08-12T18:00:00.000Z' }],
      applications: [
        { id: 'application-1', projectId: 'project-1' },
        { id: 'application-2', projectId: 'project-2' },
      ],
      committee: [],
      registrationsBySlot: {},
      accessIssues: [],
    });
    mocks.getTestingEventFeedbackReview.mockResolvedValue({
      feedback: [
        {
          obligationId: 'obligation-1',
          slotId: 'slot-1',
          applicationId: 'application-1',
          testerUserId: 'tester-1',
          status: 'Fulfilled',
          feedback: {
            overallRating: 9,
            wouldRecommend: true,
            feedbackData: '{"playability":"Clear controls"}',
            additionalNotes: 'Ready for another round',
          },
        },
        {
          obligationId: 'obligation-2',
          slotId: 'slot-1',
          applicationId: 'application-2',
          status: 'Pending',
        },
      ],
      accessIssues: [],
    });
    mocks.getMembers.mockResolvedValue({
      members: [
        { id: 'tester-1', displayName: 'Ana Tester', email: 'ana@example.com' },
      ],
      total: 1,
    });
    mocks.getTestingProjectOptions.mockResolvedValue([
      { id: 'project-1', title: 'Asterion' },
      { id: 'project-2', title: 'Neon Harbor' },
    ]);

    render(
      await TestingEventFeedbackPage({
        params: Promise.resolve({ eventId: 'event-1' }),
      }),
    );

    expect(screen.getByText('1 pending')).toBeInTheDocument();
    expect(screen.getByText('1 submitted')).toBeInTheDocument();
    expect(screen.getByText('Asterion')).toBeInTheDocument();
    expect(screen.getByText('Ana Tester')).toBeInTheDocument();
    expect(screen.getByText('9/10')).toBeInTheDocument();
    expect(screen.getByText('Clear controls')).toBeInTheDocument();
    expect(screen.getByText('Neon Harbor')).toBeInTheDocument();
    expect(screen.getByText('Unknown tester')).toBeInTheDocument();
    expect(screen.queryByText('tester-1')).not.toBeInTheDocument();
  });
});
