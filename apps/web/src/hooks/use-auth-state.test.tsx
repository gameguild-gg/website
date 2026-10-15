import { act, renderHook, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useAuthState } from './use-auth-state';

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

describe('useAuthState', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('loads the caller session state through the same-origin proxy', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(sessionStateBody()), { status: 200 }),
    );
    vi.stubGlobal('fetch', fetchMock);

    const { result } = renderHook(() => useAuthState());

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/session-state', {
      credentials: 'same-origin',
      cache: 'no-store',
    });
    expect(result.current.error).toBeNull();
    expect(result.current.state?.sessionValid).toBe(true);
    expect(result.current.state?.activeSessionCount).toBe(2);
    expect(result.current.state?.mfaEnrolled).toBe(true);
  });

  it('surfaces proxy failures without state', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response(JSON.stringify({ error: 'Authentication required.' }), { status: 401 })),
    );

    const { result } = renderHook(() => useAuthState());

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.state).toBeNull();
    expect(result.current.error).toBe('Authentication required.');
  });

  it('reloads on refresh', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(sessionStateBody()), { status: 200 }),
    );
    vi.stubGlobal('fetch', fetchMock);
    const { result } = renderHook(() => useAuthState());
    await waitFor(() => expect(result.current.loading).toBe(false));

    await act(async () => {
      await result.current.refresh();
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);
  });
});
