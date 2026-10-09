'use client';

/**
 * useAuth Hook
 *
 * Provides authentication actions (signIn, signOut, signUp)
 * for use in React client components.
 *
 * These call the auth API routes directly from the browser,
 * handling CSRF tokens automatically.
 *
 * @example
 * ```tsx
 * import { useAuth } from '@game-guild/client/react';
 *
 * function LoginForm() {
 *   const { signIn, signUp, signOut, isLoading } = useAuth();
 *
 *   async function handleSubmit(e: FormEvent) {
 *     e.preventDefault();
 *     const formData = new FormData(e.target as HTMLFormElement);
 *     await signIn('credentials', {
 *       email: formData.get('email') as string,
 *       password: formData.get('password') as string,
 *       redirectTo: '/dashboard',
 *     });
 *   }
 *
 *   return <form onSubmit={handleSubmit}>...</form>;
 * }
 * ```
 */

import { useCallback, useState, useRef, useContext, useEffect } from 'react';
import { assertSafeServiceUrl } from '../../runtime/security/safe-remote-url.js';
import { SessionContext } from './session-provider.js';

/**
 * Options for client-side authentication actions
 */
export interface AuthActionOptions {
  /** Base path for auth API (default: '/api/auth') */
  basePath?: string;
}

/**
 * Return type of useAuth
 */
export interface UseAuthReturn {
  /** Sign in with a provider */
  signIn: (
    provider?: string,
    options?: Record<string, unknown> & {
      redirectTo?: string;
      redirect?: boolean;
    },
  ) => Promise<void>;

  /** Sign up with credentials */
  signUp: (credentials: {
    username: string;
    email: string;
    password: string;
    firstName?: string;
    lastName?: string;
    tenantId?: string;
    redirectTo?: string;
    redirect?: boolean;
  }) => Promise<void>;

  /** Sign out */
  signOut: (options?: { redirectTo?: string; redirect?: boolean }) => Promise<void>;

  /** Whether an auth action is in progress */
  isLoading: boolean;

  /** Last error from an auth action */
  error: Error | null;

  /** Clear the error state */
  clearError: () => void;
}

/**
 * Hook for client-side authentication actions.
 */
export function useAuth(options?: AuthActionOptions): UseAuthReturn {
  const { basePath = '/api/auth' } = options ?? {};

  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const csrfTokenRef = useRef<string | null>(null);
  const basePathRef = useRef(basePath);

  useEffect(() => {
    basePathRef.current = basePath;
  }, [basePath]);

  // Access session context for updating state after auth actions
  const sessionContext = useContext(SessionContext);

  /**
   * Fetch a CSRF token
   */
  const getCSRFToken = useCallback(async (): Promise<string> => {
    /* v8 ignore start */
    if (csrfTokenRef.current) return csrfTokenRef.current;
    /* v8 ignore stop */

    const response = await fetch(`${basePathRef.current}/csrf`, {
      credentials: 'include',
    });

    if (!response.ok) {
      throw new Error('Failed to fetch CSRF token');
    }

    const data = await response.json();
    csrfTokenRef.current = data.csrfToken;
    return data.csrfToken;
  }, []);

  /**
   * Sign in
   */
  const signIn = useCallback(
    async (
      provider: string = 'credentials',
      actionOptions?: Record<string, unknown> & {
        redirectTo?: string;
        redirect?: boolean;
      },
    ): Promise<void> => {
      setIsLoading(true);
      setError(null);

      try {
        const csrfToken = await getCSRFToken();
        const { redirectTo, redirect = true, ...credentials } = actionOptions ?? {};

        // SSRF (Codacy rule-node-ssrf) — false positive: the target origin is
        // pinned to the hook's configured auth base path by assertSafeServiceUrl;
        // the provider segment cannot select another origin.
        const response = await fetch(assertSafeServiceUrl(`${basePathRef.current}/signin/${provider}`, basePathRef.current), {
          redirect: 'error',
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            ...credentials,
            csrfToken,
            redirectTo,
            redirect: false, // Always handle redirect client-side
          }),
        });

        // Clear cached CSRF token after use
        csrfTokenRef.current = null;

        if (!response.ok) {
          const errorData = await response.json().catch(() => ({}));
          throw new Error((errorData as Record<string, string>).message || (errorData as Record<string, string>).detail || 'Sign-in failed');
        }

        const data = await response.json();

        // If the response has a URL (OAuth redirect)
        if (data.url) {
          window.location.href = data.url;
          return;
        }

        // Update session context
        if (sessionContext) {
          await sessionContext.update();
        }

        // Handle redirect
        if (redirect && redirectTo) {
          window.location.href = redirectTo;
        }
      } catch (err) {
        const authError = err instanceof Error ? err : new Error('Sign-in failed');
        setError(authError);
        throw authError;
      } finally {
        setIsLoading(false);
      }
    },
    [getCSRFToken, sessionContext],
  );

  /**
   * Sign up
   */
  const signUp = useCallback(
    async (credentials: {
      username: string;
      email: string;
      password: string;
      firstName?: string;
      lastName?: string;
      tenantId?: string;
      redirectTo?: string;
      redirect?: boolean;
    }): Promise<void> => {
      setIsLoading(true);
      setError(null);

      try {
        const csrfToken = await getCSRFToken();
        const { redirectTo, redirect = true, ...signUpData } = credentials;

        const response = await fetch(`${basePathRef.current}/signup`, {
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            ...signUpData,
            csrfToken,
          }),
        });

        csrfTokenRef.current = null;

        if (!response.ok) {
          const errorData = await response.json().catch(() => ({}));
          throw new Error((errorData as Record<string, string>).message || (errorData as Record<string, string>).detail || 'Sign-up failed');
        }

        // Update session context
        if (sessionContext) {
          await sessionContext.update();
        }

        if (redirect && redirectTo) {
          window.location.href = redirectTo;
        }
      } catch (err) {
        const authError = err instanceof Error ? err : new Error('Sign-up failed');
        setError(authError);
        throw authError;
      } finally {
        setIsLoading(false);
      }
    },
    [getCSRFToken, sessionContext],
  );

  /**
   * Sign out
   */
  const signOut = useCallback(
    async (signOutOptions?: { redirectTo?: string; redirect?: boolean }): Promise<void> => {
      setIsLoading(true);
      setError(null);

      try {
        const csrfToken = await getCSRFToken();
        const { redirectTo, redirect = true } = signOutOptions ?? {};

        const response = await fetch(`${basePathRef.current}/signout`, {
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ csrfToken }),
        });

        csrfTokenRef.current = null;

        if (!response.ok) {
          throw new Error('Sign-out failed');
        }

        // Broadcast sign-out to other tabs
        // (SessionProvider will handle this via the BroadcastChannel)

        // Update session context
        if (sessionContext) {
          await sessionContext.update();
        }

        if (redirect) {
          window.location.href = redirectTo ?? '/';
        }
      } catch (err) {
        const authError = err instanceof Error ? err : new Error('Sign-out failed');
        setError(authError);
        throw authError;
      } finally {
        setIsLoading(false);
      }
    },
    [getCSRFToken, sessionContext],
  );

  const clearError = useCallback(() => setError(null), []);

  return {
    signIn,
    signUp,
    signOut,
    isLoading,
    error,
    clearError,
  };
}
