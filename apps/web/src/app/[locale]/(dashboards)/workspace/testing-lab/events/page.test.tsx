import { render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getArchivedTestingEventsDirectory: vi.fn(),
  getTestingEventsDirectory: vi.fn(),
  getTestingEventTemplates: vi.fn(),
  replace: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/workspace/learning',
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

vi.mock('@/lib/testing-lab/events-queries', () => ({
  getArchivedTestingEventsDirectory: mocks.getArchivedTestingEventsDirectory,
  getTestingEventsDirectory: mocks.getTestingEventsDirectory,
  getTestingEventTemplates: mocks.getTestingEventTemplates,
}));

vi.mock('@/i18n/navigation', () => ({
  Link: ({ children, href, ...rest }: { children: ReactNode; href: string }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
  usePathname: () => '/workspace/testing-lab/events',
  useRouter: () => ({ replace: mocks.replace }),
}));

import TestingEventsPage from './page';

describe('Testing Events page', () => {
  it('renders API-backed events and the creation workflow', async () => {
    mocks.getTestingEventsDirectory.mockResolvedValue({
      accessIssues: [],
      events: [
        {
          id: 'event-1',
          name: 'August campus playtest',
          description: 'Moderated testing for community projects.',
          mode: 'InPerson',
          status: 'ApplicationsOpen',
          approvalMode: 'Committee',
          startsAt: '2026-08-12T18:00:00.000Z',
          endsAt: '2026-08-12T22:00:00.000Z',
          slotCount: 2,
          applicationCount: 5,
        },
      ],
    });
    mocks.getTestingEventTemplates.mockResolvedValue({ templates: [], accessIssues: [] });

    render(await TestingEventsPage({ searchParams: Promise.resolve({ status: 'ApplicationsOpen' }) }));

    expect(screen.getByRole('heading', { name: 'Testing sessions' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /new event/i })).toBeInTheDocument();
    expect(
      screen.getByRole('combobox', { name: 'Filter testing events by status' }),
    ).toHaveTextContent('Applications open');
    expect(screen.getByRole('searchbox', { name: 'Search testing events' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Active' })).not.toBeInTheDocument();
    expect(screen.getByText('August campus playtest')).toBeInTheDocument();
    expect(screen.getByText('Applications Open')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /manage event/i })).toHaveAttribute(
      'href',
      '/workspace/testing-lab/events/event-1',
    );
    expect(mocks.getTestingEventsDirectory).toHaveBeenCalledWith({
      status: 'ApplicationsOpen',
      skip: 0,
      take: 100,
    });
  });

  it('renders archived events with a restore action', async () => {
    mocks.getArchivedTestingEventsDirectory.mockResolvedValue({
      accessIssues: [],
      events: [
        {
          id: 'event-archived',
          name: 'Archived campus playtest',
          mode: 'InPerson',
          status: 'Completed',
          startsAt: '2026-08-12T18:00:00.000Z',
        },
      ],
    });

    render(await TestingEventsPage({ searchParams: Promise.resolve({ archived: 'true' }) }));

    expect(screen.getByText('Archived campus playtest')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /restore event/i })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /manage event/i })).not.toBeInTheDocument();
    expect(mocks.getArchivedTestingEventsDirectory).toHaveBeenCalledWith({ skip: 0, take: 100 });
  });
});
