import { beforeEach, describe, expect, it, vi } from 'vitest';

const { getPublicTestingEventMock } = vi.hoisted(() => ({
  getPublicTestingEventMock: vi.fn(),
}));

vi.mock('@/lib/testing-lab/events-public-queries', () => ({
  getPublicTestingEvent: getPublicTestingEventMock,
}));
vi.mock('@/lib/testing-lab/events-queries', () => ({
  getPublicTestingEventExperience: vi.fn(),
}));
vi.mock('@/lib/testing-lab/queries', () => ({
  getTestingProjectVersionOptions: vi.fn(),
}));
vi.mock('@/lib/user-settings/queries', () => ({
  getLocalizationPreference: vi.fn(),
}));
vi.mock('@/components/testing-lab/testing-feedback-submission', () => ({
  TestingFeedbackSubmission: () => null,
}));
vi.mock('@/components/testing-lab/testing-event-games', () => ({
  TestingEventGames: () => null,
}));
vi.mock('@/components/testing-lab/testing-event-join', () => ({
  TestingEventJoin: () => null,
}));
vi.mock('@/components/testing-lab/testing-event-date-range', () => ({
  TestingEventDateRange: () => null,
}));

import { generateMetadata } from './page';

describe('public testing event page metadata', () => {
  beforeEach(() => {
    getPublicTestingEventMock.mockReset();
  });

  it('derives title and description from the public event projection', async () => {
    getPublicTestingEventMock.mockResolvedValue({
      name: 'Autumn Playtest Night',
      description: 'One evening of community playtesting and feedback.',
    });

    const metadata = await generateMetadata({ params: Promise.resolve({ eventId: 'event-7' }) });

    expect(getPublicTestingEventMock).toHaveBeenCalledWith('event-7');
    expect(metadata).toMatchObject({
      title: 'Autumn Playtest Night',
      description: 'One evening of community playtesting and feedback.',
      openGraph: {
        title: 'Autumn Playtest Night',
        description: 'One evening of community playtesting and feedback.',
      },
    });
  });

  it('falls back to noindex metadata when the event is missing', async () => {
    getPublicTestingEventMock.mockResolvedValue(null);

    const metadata = await generateMetadata({ params: Promise.resolve({ eventId: 'missing' }) });

    expect(metadata).toMatchObject({
      title: 'Playtest Not Found',
      robots: { index: false, follow: false },
    });
  });
});
