import type { ReactNode } from 'react';
import { describe, expect, it } from 'vitest';

import TestingEventsLayout from './layout';

describe('TestingEventsLayout', () => {
  it('defers event-specific authorization to the child route', async () => {
    const result = await TestingEventsLayout({
      children: 'event applications' as unknown as ReactNode,
    });

    expect(result).toBe('event applications');
  });
});
