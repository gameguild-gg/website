import { render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getTestingLabAnalytics: vi.fn(),
  getTestingLabDashboard: vi.fn(),
  getTestingEventsDirectory: vi.fn(),
  getTestingApplicationsDirectory: vi.fn(),
  normalizeTestingRequestStatus: vi.fn(),
  normalizeTestingSessionStatus: vi.fn(),
}));

vi.mock('@/lib/testing-lab', () => ({
  getTestingLabAnalytics: mocks.getTestingLabAnalytics,
  getTestingLabDashboard: mocks.getTestingLabDashboard,
  normalizeTestingRequestStatus: mocks.normalizeTestingRequestStatus,
  normalizeTestingSessionStatus: mocks.normalizeTestingSessionStatus,
}));

vi.mock('@/lib/testing-lab/events-queries', () => ({
  getTestingEventsDirectory: mocks.getTestingEventsDirectory,
  getTestingApplicationsDirectory: mocks.getTestingApplicationsDirectory,
}));

vi.mock('@/components/testing-lab/testing-event-management', () => ({
  CreateTestingEventDialog: () => <button type="button">New event</button>,
}));

vi.mock('@/components/testing-lab/testing-lab-calendar', () => ({
  TestingLabCalendar: ({
    events,
    eventAnalytics,
  }: {
    events: Array<{ name?: string }>;
    eventAnalytics: Array<{ eventId: string; capacity: number }>;
  }) => (
    <section aria-label="Testing Lab calendar">
      {events.map((event) => (
        <span key={event.name}>{event.name}</span>
      ))}
      <span>Calendar capacity {eventAnalytics[0]?.capacity}</span>
    </section>
  ),
}));

vi.mock('@/i18n/navigation', () => ({
  Link: ({ children, href, ...rest }: { children: ReactNode; href: string }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

import TestingLabPage from './page';

describe('testing lab dashboard page', () => {
  it('renders live Testing Lab metrics and the Testing Lab calendar', async () => {
    mocks.normalizeTestingRequestStatus.mockReturnValue('Open');
    mocks.normalizeTestingSessionStatus.mockReturnValue('Scheduled');
    mocks.getTestingLabDashboard.mockResolvedValue({
      accessIssues: [],
      requests: [
        {
          id: 'request-1',
          title: 'Combat prototype playtest',
          description: 'Validate onboarding and first combat loop.',
          status: 'Open',
        },
      ],
      sessions: [
        {
          id: 'session-1',
          sessionName: 'Friday feedback lab',
          location: { id: 'location-1', name: 'Remote lab', status: 'Active' },
          status: 'Scheduled',
        },
      ],
      locations: [],
      publicSessions: [],
    });
    mocks.getTestingLabAnalytics.mockResolvedValue({
      accessIssues: [],
      current: {
        events: 1,
        completedEvents: 0,
        applications: 1,
        approvedProjects: 1,
        registeredTesters: 2,
        attendedTesters: 0,
        feedback: 0,
        averageRating: null,
        recommendationRate: null,
        capacity: 10,
        fillRate: 20,
      },
      previous: null,
      locations: { total: 1, active: 1 },
      trend: [],
      events: [
        {
          eventId: 'event-1',
          registeredTesters: 2,
          capacity: 10,
          fillRate: 20,
        },
      ],
    });
    mocks.getTestingEventsDirectory.mockResolvedValue({
      accessIssues: [],
      events: [
        {
          id: 'event-1',
          name: 'Campus playtest',
          startsAt: '2026-08-10T18:00:00.000Z',
        },
      ],
    });
    mocks.getTestingApplicationsDirectory.mockResolvedValue({
      accessIssues: [],
      entries: [
        {
          event: { id: 'event-1', name: 'Campus playtest' },
          application: { id: 'application-1', status: 'Pending' },
        },
      ],
    });

    render(await TestingLabPage());

    expect(screen.getByRole('heading', { name: 'Testing Lab' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /public lab/i })).toHaveAttribute('href', '/testing-lab');
    expect(screen.queryByRole('navigation', { name: 'Testing Lab operations' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'New event' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /manage events/i })).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Needs attention' })).toBeInTheDocument();
    expect(screen.getByText('1 pending application')).toBeInTheDocument();
    expect(screen.getByText('Recent testing projects')).toBeInTheDocument();
    expect(screen.queryByText('Recent requests')).not.toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Testing Lab calendar' })).toBeInTheDocument();
    expect(screen.getByText('Campus playtest')).toBeInTheDocument();
    expect(screen.getByText('Calendar capacity 10')).toBeInTheDocument();
    expect(screen.getByText('20%')).toBeInTheDocument();
    expect(screen.getByText('Combat prototype playtest')).toBeInTheDocument();
    expect(screen.getByText('Friday feedback lab')).toBeInTheDocument();
    expect(screen.getByText('Remote lab')).toBeInTheDocument();
  });

  it('describes registrations against unlimited capacity without rendering an impossible denominator', async () => {
    mocks.getTestingLabDashboard.mockResolvedValue({
      accessIssues: [],
      requests: [],
      sessions: [],
      locations: [],
      publicSessions: [],
    });
    mocks.getTestingLabAnalytics.mockResolvedValue({
      accessIssues: [],
      current: {
        events: 0,
        completedEvents: 0,
        applications: 0,
        approvedProjects: 0,
        registeredTesters: 2,
        attendedTesters: 0,
        feedback: 0,
        averageRating: null,
        recommendationRate: null,
        capacity: 0,
        fillRate: 0,
      },
      previous: null,
      locations: { total: 0, active: 0 },
      trend: [],
      events: [],
    });
    mocks.getTestingEventsDirectory.mockResolvedValue({
      accessIssues: [],
      events: [],
    });
    mocks.getTestingApplicationsDirectory.mockResolvedValue({
      accessIssues: [],
      entries: [],
    });

    render(await TestingLabPage());

    expect(screen.getByText('Unlimited')).toBeInTheDocument();
    expect(screen.getByText('2 registered')).toBeInTheDocument();
    expect(screen.queryByText('2/0 seats')).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Launch your first testing cycle' })).toBeInTheDocument();
    expect(screen.getByText('Create an event')).toBeInTheDocument();
    expect(screen.getByText('Add rules and instructions')).toBeInTheDocument();
    expect(screen.getByText('Configure slots and capacity')).toBeInTheDocument();
    expect(screen.getByText('Open applications')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'New event' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Testing Lab calendar' })).toBeInTheDocument();
  });
});
