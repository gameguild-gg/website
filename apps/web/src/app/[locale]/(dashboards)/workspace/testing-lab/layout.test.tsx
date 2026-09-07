import type { ReactNode } from 'react';
import { describe, expect, it } from 'vitest';

import TestingLabLayout from './layout';

describe('TestingLabLayout', () => {
  it('defers event-specific authorization to the child route', async () => {
    const result = await TestingLabLayout({
      children: 'event applications' as unknown as ReactNode,
    });

    expect(result.props.children).toBe('event applications');
  });
});
