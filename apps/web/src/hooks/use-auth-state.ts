'use client';

import { useCallback, useEffect, useState } from 'react';

/** The v1 session-state projection of GET /v1/auth/session-state (issue #221). */
export type AuthSessionState = Readonly<{
  userId: string;
  sessionId: string | null;
  sessionValid: boolean;
  sessionExpiresAtUtc: string | null;
  sessionLastUsedAtUtc: string | null;
  sessionTerminationReason: string | null;
  tokenVersion: number | null;
  tokenRevoked: boolean | null;
  activeSessionCount: number;
  mfaEnrolled: boolean;
  mfaVerifiedInCurrentToken: boolean;
}>;

export type UseAuthStateResult = Readonly<{
  state: AuthSessionState | null;
  error: string | null;
  loading: boolean;
  refresh: () => Promise<void>;
}>;

/**
 * useAuthState (issue #221): surfaces the authenticated caller's own session state —
 * validity, expiry, token version, active-session count and MFA enrollment — through the
 * same-origin /api/auth/session-state proxy. The request is never cached.
 */
export function useAuthState(): UseAuthStateResult {
  const [state, setState] = useState<AuthSessionState | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async (): Promise<void> => {
    setError(null);
    setLoading(true);
    try {
      const response = await fetch('/api/auth/session-state', {
        credentials: 'same-origin',
        cache: 'no-store',
      });
      const body: unknown = await response.json();
      if (!response.ok || typeof body !== 'object' || body === null) {
        const message = (body as { error?: unknown; detail?: unknown }).error
          ?? (body as { detail?: unknown }).detail;
        throw new Error(typeof message === 'string' ? message : `Session state unavailable (${response.status}).`);
      }
      setState(body as AuthSessionState);
    } catch (cause) {
      setState(null);
      setError(cause instanceof Error ? cause.message : 'Session state unavailable.');
    } finally {
      setLoading(false);
    }
  }, []);

  // Initial synchronization with the session-state endpoint. All state updates happen
  // after the first await, never synchronously during the effect body.
  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const response: Response = await fetch('/api/auth/session-state', {
          credentials: 'same-origin',
          cache: 'no-store',
        });
        const body: unknown = await response.json();
        if (cancelled) return;
        if (!response.ok || typeof body !== 'object' || body === null) {
          const message = (body as { error?: unknown; detail?: unknown }).error
            ?? (body as { detail?: unknown }).detail;
          throw new Error(typeof message === 'string' ? message : `Session state unavailable (${response.status}).`);
        }
        setState(body as AuthSessionState);
      } catch (cause) {
        if (cancelled) return;
        setState(null);
        setError(cause instanceof Error ? cause.message : 'Session state unavailable.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  return { state, error, loading, refresh: load };
}
