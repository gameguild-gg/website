import { render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getTestingEventWorkspaceData: vi.fn(),
  getMembers: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/workspace/learning',
  useRouter: () => ({ refresh: vi.fn() }),
}));

vi.mock('@/lib/testing-lab/events-queries', () => ({
  getTestingEventWorkspaceData: mocks.getTestingEventWorkspaceData,
}));


vi.mock('@/lib/community/queries/members', () => ({
  getMembers: mocks.getMembers,
}));


vi.mock('@/i18n/navigation', () => ({
  Link: ({ children, href, ...rest }: { children: ReactNode; href: string }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

import TestingEventOverviewPage from './page';

describe('Testing Event overview', () => {
  it('summarizes live event operations without mixing schedule and applications', async () => {
    mocks.getMembers.mockResolvedValue({
      members: [{ id: 'user-1', displayName: 'Ana Reviewer', email: 'ana@example.com' }],
    });
    mocks.getTestingEventWorkspaceData.mockResolvedValue({
      accessIssues: [],
      event: {
        id: 'event-1',
        name: 'August campus playtest',
        mode: 'InPerson',
        status: 'ApplicationsOpen',
        approvalMode: 'Committee',
        startsAt: '2026-08-12T18:00:00.000Z',
        endsAt: '2026-08-12T22:00:00.000Z',
        applicationsOpenAt: '2026-07-01T12:00:00.000Z',
        applicationsCloseAt: '2026-08-01T12:00:00.000Z',
      },
      slots: [{ id: 'slot-1', maxTesters: 10, maxProjects: 3 }],
      applications: [{ id: 'application-1', status: 'Pending' }],
      committee: [{ id: 'committee-1', userId: 'user-1', userName: 'Ana Reviewer' }],
      registrationsBySlot: {
        'slot-1': [{ id: 'registration-1', userId: 'tester-1', status: 'Registered' }],
      },
    });

    render(
      await TestingEventOverviewPage({
        params: Promise.resolve({ eventId: 'event-1' }),
      }),
    );

    expect(screen.getByRole('heading', { name: 'Event overview', level: 2 })).toBeInTheDocument();
    expect(screen.getByText('1 project application')).toBeInTheDocument();
    expect(screen.getByText('1 registered tester')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Review committee' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Schedule and capacity' })).not.toBeInTheDocument();
  });
});
