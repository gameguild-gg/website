import { screen, waitFor } from '@testing-library/react';
import { render } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SessionStateCard } from './session-state-card';

function sessionStateBody(): Record<string, unknown> {
  return {
    userId: '40000000-0000-0000-0000-000000000390',
    sessionId: '40000000-0000-0000-0000-000000000391',
    sessionValid: true,
    sessionExpiresAtUtc: '2026-10-14T00:00:00Z',
    sessionLastUsedAtUtc: '2026-10-13T12:00:00Z',
    sessionTerminationReason: null,
    tokenVersion: 3,
    tokenRevoked: false,
    activeSessionCount: 2,
    mfaEnrolled: true,
    mfaVerifiedInCurrentToken: true,
  };
}

describe('SessionStateCard', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders the session state for the authenticated caller', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response(JSON.stringify(sessionStateBody()), { status: 200 })),
    );

    render(<SessionStateCard />);

    await waitFor(() => expect(screen.getByTestId('session-state-fields')).toBeInTheDocument());
    expect(screen.getByText('Valid')).toBeInTheDocument();
    expect(screen.getByText('Enrolled')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
  });

  it('renders the failure surface when the proxy rejects the caller', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response(JSON.stringify({ error: 'Authentication required.' }), { status: 401 })),
    );

    render(<SessionStateCard />);

    await waitFor(() => expect(screen.getByTestId('session-state-error')).toBeInTheDocument());
    expect(screen.getByTestId('session-state-error')).toHaveTextContent('Authentication required.');
  });
});
