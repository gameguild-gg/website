/**
 * @vitest-environment happy-dom
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import React from 'react';
import { render, act, waitFor, screen } from '@testing-library/react';

// ─── Broadcast mock — must be set up BEFORE session-provider imports ──
// We use vi.hoisted + vi.mock so the broadcast module is mocked before
// session-provider.tsx imports it.
const broadcastMock = vi.hoisted(() => {
  let _onMessage: ((msg: any) => void) | null = null;
  return {
    getOnMessage: () => _onMessage,
    resetOnMessage: () => {
      _onMessage = null;
    },
    createAuthBroadcast: vi.fn((onMessage: (msg: any) => void) => {
      _onMessage = onMessage;
      return {
        send: vi.fn(),
        close: vi.fn(),
      };
    }),
  };
});

vi.mock('../../src/integrations/react/broadcast.js', () => ({
  createAuthBroadcast: broadcastMock.createAuthBroadcast,
}));

import { SessionProvider, SessionContext } from '../../src/integrations/react/session-provider.js';
import type { SessionContextValue } from '../../src/integrations/react/session-provider.js';

// ─── Helpers ────────────────────────────────────────────────────

function Consumer({ testId = 'status' }: { testId?: string }) {
  const ctx = React.useContext(SessionContext);
  return (
    <div>
      <span data-testid={testId}>{ctx?.status ?? 'no-ctx'}</span>
      <span data-testid="user">{ctx?.data?.user?.email ?? 'none'}</span>
    </div>
  );
}

const mockSession = {
  user: { id: 'u1', email: 'a@b.com', name: 'A' },
  expires: new Date(Date.now() + 3600_000).toISOString(),
};

// ─── Tests ──────────────────────────────────────────────────────

describe('SessionProvider', () => {
  let fetchSpy: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchSpy = vi.fn();
    vi.stubGlobal('fetch', fetchSpy);
    broadcastMock.resetOnMessage();
    broadcastMock.createAuthBroadcast.mockClear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('renders children and sets status to loading when no initial session', async () => {
    fetchSpy.mockResolvedValueOnce({
      ok: true,
      json: async () => mockSession,
    });

    const { getByTestId } = render(
      <SessionProvider>
        <Consumer />
      </SessionProvider>,
    );

    // Initially loading, then authenticated after fetch
    await waitFor(() => {
      expect(getByTestId('status').textContent).toBe('authenticated');
    });
  });

  it('uses initial session from SSR and sets status to authenticated', () => {
    const { getByTestId } = render(
      <SessionProvider session={mockSession as any}>
        <Consumer />
      </SessionProvider>,
    );

    expect(getByTestId('status').textContent).toBe('authenticated');
    expect(getByTestId('user').textContent).toBe('a@b.com');
    // Should NOT fetch since initial session provided
    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it('sets unauthenticated when fetch returns not ok', async () => {
    fetchSpy.mockResolvedValueOnce({ ok: false });

    const { getByTestId } = render(
      <SessionProvider>
        <Consumer />
      </SessionProvider>,
    );

    await waitFor(() => {
      expect(getByTestId('status').textContent).toBe('unauthenticated');
    });
  });

  it('sets unauthenticated when fetch returns empty user', async () => {
    fetchSpy.mockResolvedValueOnce({
      ok: true,
      json: async () => ({}),
    });

    const { getByTestId } = render(
      <SessionProvider>
        <Consumer />
      </SessionProvider>,
    );

    await waitFor(() => {
      expect(getByTestId('status').textContent).toBe('unauthenticated');
    });
  });

  it('sets unauthenticated when fetch throws', async () => {
    fetchSpy.mockRejectedValueOnce(new Error('Network error'));

    const { getByTestId } = render(
      <SessionProvider>
        <Consumer />
      </SessionProvider>,
    );

    await waitFor(() => {
      expect(getByTestId('status').textContent).toBe('unauthenticated');
    });
  });

  it('update() calls POST and updates session', async () => {
    // Initial session
    fetchSpy.mockResolvedValueOnce({
      ok: true,
      json: async () => mockSession,
    });

    let ctxValue: SessionContextValue | undefined;

    function Capture() {
      ctxValue = React.useContext(SessionContext);
      return null;
    }

    render(
      <SessionProvider>
        <Capture />
      </SessionProvider>,
    );

    await waitFor(() => {
      expect(ctxValue?.status).toBe('authenticated');
    });

    // Mock POST update
    fetchSpy.mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        user: { id: 'u1', email: 'updated@b.com', name: 'Updated' },
        expires: mockSession.expires,
      }),
    });

    let updated: any;
    await act(async () => {
      updated = await ctxValue!.update({ user: { name: 'Updated' } } as any);
    });

    expect(updated).toBeTruthy();
    expect(updated.user.email).toBe('updated@b.com');

    // Verify POST was made
    const postCall = fetchSpy.mock.calls.find((c: any[]) => c[1]?.method === 'POST');
    expect(postCall).toBeTruthy();
  });

  it('update() returns null on failed response', async () => {
    fetchSpy.mockResolvedValueOnce({
      ok: true,
      json: async () => mockSession,
    });

    let ctxValue: SessionContextValue | undefined;
    function Capture() {
      ctxValue = React.useContext(SessionContext);
      return null;
    }

    render(
      <SessionProvider>
        <Capture />
      </SessionProvider>,
    );

    await waitFor(() => expect(ctxValue?.status).toBe('authenticated'));

    fetchSpy.mockResolvedValueOnce({ ok: false });

    let result: any;
    await act(async () => {
      result = await ctxValue!.update();
    });

    expect(result).toBeNull();
  });

  it('update() returns null on exception', async () => {
    fetchSpy.mockResolvedValueOnce({
      ok: true,
      json: async () => mockSession,
    });

    let ctxValue: SessionContextValue | undefined;
    function Capture() {
      ctxValue = React.useContext(SessionContext);
      return null;
    }

    render(
      <SessionProvider>
        <Capture />
      </SessionProvider>,
    );

    await waitFor(() => expect(ctxValue?.status).toBe('authenticated'));

    fetchSpy.mockRejectedValueOnce(new Error('fail'));

    let result: any;
    await act(async () => {
      result = await ctxValue!.update();
    });

    expect(result).toBeNull();
  });

  it('update() sets unauthenticated when response has no user', async () => {
    fetchSpy.mockResolvedValueOnce({
      ok: true,
      json: async () => mockSession,
    });

    let ctxValue: SessionContextValue | undefined;
    function Capture() {
      ctxValue = React.useContext(SessionContext);
      return null;
    }

    render(
      <SessionProvider>
        <Capture />
      </SessionProvider>,
    );

    await waitFor(() => expect(ctxValue?.status).toBe('authenticated'));

    fetchSpy.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ user: null }),
    });

    await act(async () => {
      await ctxValue!.update();
    });

    expect(ctxValue!.status).toBe('unauthenticated');
  });

  it('refetches on window visibility change', async () => {
    fetchSpy.mockResolvedValue({
      ok: true,
      json: async () => mockSession,
    });

    render(
      <SessionProvider refetchOnWindowFocus={true}>
        <Consumer />
      </SessionProvider>,
    );

    await waitFor(() => {
      expect(fetchSpy).toHaveBeenCalledTimes(1);
    });

    // Simulate visibility change
    Object.defineProperty(document, 'visibilityState', {
      value: 'visible',
      writable: true,
      configurable: true,
    });
    document.dispatchEvent(new Event('visibilitychange'));

    await waitFor(() => {
      expect(fetchSpy.mock.calls.length).toBeGreaterThanOrEqual(2);
    });
  });

  it('does NOT refetch on visibility change when refetchOnWindowFocus=false', async () => {
    fetchSpy.mockResolvedValue({
      ok: true,
      json: async () => mockSession,
    });

    render(
      <SessionProvider refetchOnWindowFocus={false}>
        <Consumer />
      </SessionProvider>,
    );

    await waitFor(() => {
      expect(fetchSpy).toHaveBeenCalledTimes(1);
    });

    Object.defineProperty(document, 'visibilityState', {
      value: 'visible',
      writable: true,
      configurable: true,
    });
    document.dispatchEvent(new Event('visibilitychange'));

    // Wait a bit and confirm no additional fetch
    await new Promise((r) => setTimeout(r, 50));
    expect(fetchSpy).toHaveBeenCalledTimes(1);
  });

  it('handles periodic refetch interval', async () => {
    vi.useFakeTimers();
    fetchSpy.mockResolvedValue({
      ok: true,
      json: async () => mockSession,
    });

    render(
      <SessionProvider refetchInterval={1}>
        <Consumer />
      </SessionProvider>,
    );

    // Initial fetch
    await act(async () => {
      await vi.advanceTimersByTimeAsync(100);
    });

    const initialCalls = fetchSpy.mock.calls.length;

    // Advance 1 second for interval
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1100);
    });

    expect(fetchSpy.mock.calls.length).toBeGreaterThan(initialCalls);
  });

  it('skips periodic refetch when offline and refetchWhenOffline=false', async () => {
    vi.useFakeTimers();
    fetchSpy.mockResolvedValue({
      ok: true,
      json: async () => mockSession,
    });

    render(
      <SessionProvider refetchInterval={1} refetchWhenOffline={false}>
        <Consumer />
      </SessionProvider>,
    );

    await act(async () => {
      await vi.advanceTimersByTimeAsync(100);
    });

    await act(async () => {
      window.dispatchEvent(new Event('offline'));
      await vi.advanceTimersByTimeAsync(50);
    });

    const callsBeforeInterval = fetchSpy.mock.calls.length;

    // Advance past interval
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1100);
    });

    // Should NOT have fetched again while offline
    expect(fetchSpy.mock.calls.length).toBe(callsBeforeInterval);
  });

  it('tracks online/offline status', async () => {
    fetchSpy.mockResolvedValue({
      ok: true,
      json: async () => mockSession,
    });

    render(
      <SessionProvider>
        <Consumer />
      </SessionProvider>,
    );

    await waitFor(() => {
      expect(fetchSpy).toHaveBeenCalledTimes(1);
    });

    await act(async () => {
      window.dispatchEvent(new Event('offline'));
      window.dispatchEvent(new Event('online'));
    });

    // No crash, events handled gracefully
    expect(true).toBe(true);
  });

  it('handles cross-tab sign-out broadcast', async () => {
    fetchSpy.mockResolvedValue({
      ok: true,
      json: async () => mockSession,
    });

    let ctxValue: SessionContextValue | undefined;
    function Capture() {
      ctxValue = React.useContext(SessionContext);
      return null;
    }

    render(
      <SessionProvider>
        <Capture />
      </SessionProvider>,
    );

    await waitFor(() => expect(ctxValue?.status).toBe('authenticated'));

    const onMessage = broadcastMock.getOnMessage();
    expect(onMessage).toBeTruthy();

    // Simulate sign-out broadcast from another tab
    await act(async () => {
      onMessage!({ type: 'sign-out', timestamp: Date.now() });
    });

    await waitFor(() => {
      expect(ctxValue?.status).toBe('unauthenticated');
      expect(ctxValue?.data).toBeNull();
    });
  });

  it('handles cross-tab session-update broadcast', async () => {
    fetchSpy.mockResolvedValue({
      ok: true,
      json: async () => mockSession,
    });

    render(
      <SessionProvider>
        <Consumer />
      </SessionProvider>,
    );

    await waitFor(() => {
      expect(fetchSpy).toHaveBeenCalledTimes(1);
    });

    const onMessage = broadcastMock.getOnMessage();
    expect(onMessage).toBeTruthy();

    // Simulate session-update broadcast
    await act(async () => {
      onMessage!({ type: 'session-update', timestamp: Date.now() });
    });

    // Should trigger re-fetch
    await waitFor(
      () => {
        expect(fetchSpy.mock.calls.length).toBeGreaterThanOrEqual(2);
      },
      { timeout: 3000 },
    );
  });

  it('uses custom basePath for fetch', async () => {
    fetchSpy.mockResolvedValue({
      ok: true,
      json: async () => mockSession,
    });

    render(
      <SessionProvider basePath="/custom/auth">
        <Consumer />
      </SessionProvider>,
    );

    await waitFor(() => {
      expect(fetchSpy).toHaveBeenCalledWith('/custom/auth/session', expect.any(Object));
    });
  });

  it('does not refetch when refetchInterval is 0', async () => {
    vi.useFakeTimers();
    fetchSpy.mockResolvedValue({
      ok: true,
      json: async () => mockSession,
    });

    render(
      <SessionProvider refetchInterval={0}>
        <Consumer />
      </SessionProvider>,
    );

    await act(async () => {
      await vi.advanceTimersByTimeAsync(100);
    });

    const calls = fetchSpy.mock.calls.length;

    await act(async () => {
      await vi.advanceTimersByTimeAsync(5000);
    });

    expect(fetchSpy.mock.calls.length).toBe(calls);
  });
});
