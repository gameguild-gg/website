import { beforeEach, describe, expect, it, vi } from 'vitest';

const redirect = vi.hoisted(() => vi.fn());

vi.mock('next/navigation', () => ({
  usePathname: () => '/workspace/learning', redirect }));

import TestingEventPage from './page';

describe('Testing Event route', () => {
  beforeEach(() => redirect.mockReset());

  it('redirects the event root to its overview workspace', async () => {
    await TestingEventPage({
      params: Promise.resolve({ eventId: 'event-1' }),
    });

    expect(redirect).toHaveBeenCalledWith('/workspace/testing-lab/events/event-1/overview');
  });
});
