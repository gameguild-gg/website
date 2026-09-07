import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  error: vi.fn(),
  push: vi.fn(),
  success: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/workspace/learning',
  useRouter: () => ({ push: mocks.push }),
}));

vi.mock('sonner', () => ({
  toast: mocks,
}));

import { TestingLabConfirmAction } from './testing-lab-confirm-action';

describe('TestingLabConfirmAction', () => {
  it('publishes successful mutation feedback outside the row that may unmount', async () => {
    const action = vi.fn().mockResolvedValue({
      success: true,
      message: 'Testing location archived.',
    });

    render(
      <TestingLabConfirmAction
        action={action}
        fields={{ locationId: 'location-1' }}
        label="Archive"
        title="Archive this location?"
        description="The location is hidden from scheduling."
        confirmLabel="Archive location"
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Archive' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Archive location' }));

    await waitFor(() => {
      expect(action).toHaveBeenCalledOnce();
      expect(mocks.success).toHaveBeenCalledWith('Testing location archived.');
    });
  });

  it('navigates to the configured destination after a successful mutation', async () => {
    const action = vi.fn().mockResolvedValue({
      success: true,
      message: 'Testing request archived.',
    });

    render(
      <TestingLabConfirmAction
        action={action}
        fields={{ requestId: 'request-1' }}
        label="Archive"
        title="Archive this testing request?"
        description="The request is hidden from active operations."
        confirmLabel="Archive request"
        successHref="/workspace/testing-lab/projects"
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Archive' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Archive request' }));

    await waitFor(() => {
      expect(mocks.push).toHaveBeenCalledWith('/workspace/testing-lab/projects');
    });
  });
});
