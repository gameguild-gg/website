'use client';

/**
 * SessionProvider — React Context for Authentication
 *
 * Provides session state to the entire React tree via context.
 * Handles:
 * - Initial session fetch from /api/auth/session
 * - Periodic session revalidation
 * - Refetch on window focus
 * - Cross-tab synchronization via BroadcastChannel
 * - SSR hydration with pre-fetched session
 *
 * @example
 * ```tsx
 * // app/layout.tsx
 * import { SessionProvider } from '@game-guild/client/react';
 *
 * export default function RootLayout({ children }) {
 *   return (
 *     <html>
 *       <body>
 *         <SessionProvider>{children}</SessionProvider>
 *       </body>
 *     </html>
 *   );
 * }
 * ```
 */

import { createContext, useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import type { Session, SessionStatus, SessionProviderProps } from '../../runtime/auth/types.js';
import { createAuthBroadcast, type AuthBroadcastMessage } from './broadcast.js';

// ─── Context Types ───────────────────────────────────────────────

export interface SessionContextValue {
  data: Session | null;
  status: SessionStatus;
  update: (data?: Partial<Session>) => Promise<Session | null>;
}

export interface PendingMfaChallenge {
  mfaToken: string;
  availableMethods: string[];
}

interface AuthFlowContextValue {
  mfaChallenge: PendingMfaChallenge | null;
  setMfaChallenge: (challenge: PendingMfaChallenge | null) => void;
}

// ─── Context ─────────────────────────────────────────────────────

export const SessionContext = createContext<SessionContextValue | undefined>(undefined);

/**
 * Ephemeral state for authentication steps that span client-side routes.
 * This context is intentionally not exported from the public React barrel:
 * consumers access it through useAuth and the challenge never reaches storage.
 */
export const AuthFlowContext = createContext<AuthFlowContextValue | undefined>(undefined);

// ─── Provider Component ──────────────────────────────────────────

/**
 * SessionProvider wraps your app to provide authentication state.
 *
 * @example
 * ```tsx
 * <SessionProvider
 *   refetchInterval={5 * 60}
 *   refetchOnWindowFocus={true}
 * >
 *   <App />
 * </SessionProvider>
 * ```
 */
export function SessionProvider({
  children,
  session: initialSession,
  basePath = '/api/auth',
  refetchInterval = 0,
  refetchOnWindowFocus = true,
  refetchWhenOffline = false,
}: Omit<SessionProviderProps, 'children'> & {
  children: ReactNode;
}): ReactNode {
  const [session, setSession] = useState<Session | null>(initialSession ?? null);
  const [status, setStatus] = useState<SessionStatus>(initialSession ? 'authenticated' : 'loading');
  const [mfaChallenge, setMfaChallenge] = useState<PendingMfaChallenge | null>(null);
  const [isOnline, setIsOnline] = useState(
    /* v8 ignore start -- navigator always defined in happy-dom */
    typeof navigator !== 'undefined' ? navigator.onLine : true,
    /* v8 ignore stop */
  );

  const broadcastRef = useRef<ReturnType<typeof createAuthBroadcast> | null>(null);
  const basePathRef = useRef(basePath);

  useEffect(() => {
    basePathRef.current = basePath;
  }, [basePath]);

  // ─── Fetch Session ───────────────────────────────────────────

  const fetchSession = useCallback(async (notify = true): Promise<Session | null> => {
    try {
      const response = await fetch(`${basePathRef.current}/session`, {
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        setSession(null);
        setStatus('unauthenticated');
        return null;
      }

      const data = await response.json();

      // Empty object = no session
      if (!data || !data.user) {
        setSession(null);
        setStatus('unauthenticated');
        return null;
      }

      setSession(data as Session);
      setStatus('authenticated');

      // Notify other tabs
      /* v8 ignore start -- fetchSession always called with notify=false */
      if (notify) {
        broadcastRef.current?.send({
          type: 'session-update',
          timestamp: Date.now(),
        });
      }
      /* v8 ignore stop */

      return data as Session;
    } catch {
      setSession(null);
      setStatus('unauthenticated');
      return null;
    }
  }, []);

  // ─── Update Session ──────────────────────────────────────────

  const updateSession = useCallback(async (data?: Partial<Session>): Promise<Session | null> => {
    try {
      const response = await fetch(`${basePathRef.current}/session`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data ?? {}),
      });

      if (!response.ok) return null;

      const updated = await response.json();

      if (!updated || !updated.user) {
        setSession(null);
        setStatus('unauthenticated');
        return null;
      }

      setSession(updated as Session);
      setStatus('authenticated');

      broadcastRef.current?.send({
        type: 'session-update',
        timestamp: Date.now(),
      });

      return updated as Session;
    } catch {
      return null;
    }
  }, []);

  // ─── Initial Fetch ───────────────────────────────────────────

  useEffect(() => {
    // If we have an initial session from SSR, don't re-fetch
    if (initialSession !== undefined && initialSession !== null) {
      return;
    }

    const timeout = window.setTimeout(() => void fetchSession(false), 0);
    return () => window.clearTimeout(timeout);
  }, [fetchSession, initialSession]);

  // ─── Periodic Refetch ────────────────────────────────────────

  useEffect(() => {
    if (!refetchInterval || refetchInterval <= 0) return;

    const interval = setInterval(() => {
      if (!refetchWhenOffline && !isOnline) return;
      fetchSession(false);
    }, refetchInterval * 1000);

    return () => clearInterval(interval);
  }, [fetchSession, refetchInterval, refetchWhenOffline, isOnline]);

  // ─── Window Focus Refetch ────────────────────────────────────

  useEffect(() => {
    if (!refetchOnWindowFocus) return;
    /* v8 ignore start -- window always defined in happy-dom */
    if (typeof window === 'undefined') return;
    /* v8 ignore stop */

    const handleVisibilityChange = () => {
      /* v8 ignore start */
      if (document.visibilityState === 'visible') {
        /* v8 ignore stop */
        fetchSession(false);
      }
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);

    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
    };
  }, [fetchSession, refetchOnWindowFocus]);

  // ─── Online/Offline Tracking ─────────────────────────────────

  useEffect(() => {
    /* v8 ignore start -- window always defined in happy-dom */
    if (typeof window === 'undefined') return;
    /* v8 ignore stop */

    const handleOnline = () => setIsOnline(true);
    const handleOffline = () => setIsOnline(false);

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, []);

  // ─── Cross-Tab Sync ──────────────────────────────────────────

  useEffect(() => {
    const broadcast = createAuthBroadcast((message: AuthBroadcastMessage) => {
      /* v8 ignore start */
      if (message.type === 'sign-out') {
        setSession(null);
        setStatus('unauthenticated');
      } else if (message.type === 'session-update') {
        // Re-fetch session from server to get the latest state
        fetchSession(false);
      }
      /* v8 ignore stop */
    });

    broadcastRef.current = broadcast;

    return () => {
      broadcast.close();
      broadcastRef.current = null;
    };
  }, [fetchSession]);

  // ─── Context Value ───────────────────────────────────────────

  const value = useMemo<SessionContextValue>(
    () => ({
      data: session,
      status,
      update: updateSession,
    }),
    [session, status, updateSession],
  );

  const authFlowValue = useMemo<AuthFlowContextValue>(
    () => ({
      mfaChallenge,
      setMfaChallenge,
    }),
    [mfaChallenge],
  );

  return (
    <SessionContext.Provider value={value}>
      <AuthFlowContext.Provider value={authFlowValue}>{children}</AuthFlowContext.Provider>
    </SessionContext.Provider>
  );
}
