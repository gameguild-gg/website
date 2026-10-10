/**
 * Extended Auth Operations
 *
 * Client-side wrappers for backend auth endpoints that go beyond
 * basic sign-in/sign-up/sign-out. Includes:
 *
 * - MFA verification (TOTP, SMS, backup codes)
 * - Password reset (request + confirm)
 * - Password change (authenticated)
 * - Email verification (send + confirm)
 * - Session management (list, terminate)
 *
 * These are plain async functions that call the .NET backend API directly.
 * They can be used from:
 * - Server Actions (Next.js)
 * - Route handlers
 * - Client-side via the auth API proxy
 *
 * @example
 * ```ts
 * import { verifyMfa, requestPasswordReset, listSessions } from '@game-guild/client';
 *
 * // MFA verification after sign-in returns MfaRequiredError
 * const result = await verifyMfa(apiUrl, { mfaSessionId, code: '123456', method: 'totp' });
 *
 * // Password reset flow
 * await requestPasswordReset(apiUrl, { email: 'user@example.com' });
 * await confirmPasswordReset(apiUrl, { token: '...', newPassword: '...' });
 * ```
 */

import type { ProviderResult } from './types.js';
import { parseBackendAuthResponse } from '../../integrations/next/handlers.js';
import { MfaVerificationError, PasswordResetError, EmailVerificationError, SessionTerminationError, parseErrorBody, extractErrorMessage } from './errors.js';
import { assertSafeServiceUrl } from '../security/safe-remote-url.js';

// Re-export error classes so existing consumers don't break
export { MfaVerificationError, PasswordResetError, EmailVerificationError, SessionTerminationError } from './errors.js';

// ─── Types ───────────────────────────────────────────────────────

export interface MfaVerifyInput {
  /** MFA session ID returned by the sign-in attempt */
  mfaSessionId: string;
  /** The TOTP/SMS code or backup code */
  code: string;
  /** Which MFA method to use */
  method: 'totp' | 'sms' | 'backup_code';
}

export interface MfaSetupResult {
  /** The TOTP secret (for QR code generation) */
  secret?: string;
  /** QR code URI for authenticator apps */
  qrCodeUri?: string;
  /** Backup codes (only returned once during setup) */
  backupCodes?: string[];
  /** Whether setup is complete or needs verification */
  requiresVerification: boolean;
}

export interface PasswordResetRequestInput {
  /** Email address to send reset link to */
  email: string;
}

export interface PasswordResetConfirmInput {
  /** The reset token from the email link */
  token: string;
  /** The new password */
  newPassword: string;
}

export interface PasswordChangeInput {
  /** Current password (for verification) */
  currentPassword: string;
  /** New password */
  newPassword: string;
}

export interface EmailVerificationInput {
  /** The verification token from the email link */
  token: string;
}

export interface SessionInfo {
  /** Session ID */
  id: string;
  /** Device/browser info */
  userAgent?: string;
  /** IP address */
  ipAddress?: string;
  /** When the session was created */
  createdAt: string;
  /** When the session was last active */
  lastActiveAt: string;
  /** Whether this is the current session */
  isCurrent: boolean;
}

// ─── Shared Fetch Helpers (DRY) ──────────────────────────────────

/**
 * Build headers for an authenticated JSON request.
 */
function authHeaders(accessToken: string): Record<string, string> {
  return {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${accessToken}`,
  };
}

const JSON_HEADERS: Record<string, string> = {
  'Content-Type': 'application/json',
};

/**
 * POST to a backend endpoint and throw a typed error on failure.
 * Eliminates the repeated fetch + parse-error-body + throw pattern.
 */
async function postOrThrow(
  apiUrl: string,
  url: string,
  options: {
    body?: unknown;
    headers?: Record<string, string>;
    errorClass: new (message: string) => Error;
    fallbackMessage: string;
  },
): Promise<Response> {
  const response = await fetch(assertSafeServiceUrl(url, apiUrl), {
    redirect: 'error',
    method: 'POST',
    headers: options.headers ?? JSON_HEADERS,
    body: options.body ? JSON.stringify(options.body) : undefined,
  });

  if (!response.ok) {
    const errorData = await parseErrorBody(response);
    throw new options.errorClass(extractErrorMessage(errorData, options.fallbackMessage));
  }

  return response;
}

// ─── MFA Operations ─────────────────────────────────────────────

/**
 * Complete MFA verification after a sign-in attempt returned MfaRequiredError.
 *
 * @param apiUrl - Backend API base URL
 * @param input - MFA session ID + code + method
 * @param accessToken - Optional access token (if partial auth was returned)
 * @returns ProviderResult with full tokens on success
 */
export async function verifyMfa(apiUrl: string, input: MfaVerifyInput, accessToken?: string): Promise<ProviderResult> {
  const headers: Record<string, string> = { ...JSON_HEADERS };
  if (accessToken) {
    headers['Authorization'] = `Bearer ${accessToken}`;
  }

  const response = await fetch(assertSafeServiceUrl(`${apiUrl}/v1/auth/mfa/verify`, apiUrl), {
    redirect: 'error',
    method: 'POST',
    headers,
    body: JSON.stringify({
      mfaSessionId: input.mfaSessionId,
      code: input.code,
      method: input.method,
    }),
  });

  if (!response.ok) {
    const errorData = await parseErrorBody(response);
    throw new MfaVerificationError(extractErrorMessage(errorData, 'MFA verification failed'), {
      attemptsRemaining: errorData.attemptsRemaining as number | undefined,
    });
  }

  const data = (await response.json()) as Record<string, unknown>;
  return parseBackendAuthResponse(data);
}

/**
 * Set up TOTP-based MFA for the authenticated user.
 */
export async function setupTotpMfa(apiUrl: string, accessToken: string): Promise<MfaSetupResult> {
  const response = await postOrThrow(apiUrl, `${apiUrl}/v1/auth/mfa/totp/setup`, {
    headers: authHeaders(accessToken),
    errorClass: MfaVerificationError,
    fallbackMessage: 'TOTP setup failed',
  });

  return (await response.json()) as MfaSetupResult;
}

/**
 * Get available MFA methods for the authenticated user.
 */
export async function getMfaMethods(apiUrl: string, accessToken: string): Promise<string[]> {
  const response = await fetch(assertSafeServiceUrl(`${apiUrl}/v1/auth/mfa/methods`, apiUrl), {
    redirect: 'error',
    headers: { Authorization: `Bearer ${accessToken}` },
  });

  if (!response.ok) return [];

  const data = (await response.json()) as { methods?: string[] };
  return data.methods ?? [];
}

// ─── Password Operations ─────────────────────────────────────────

/**
 * Request a password reset email.
 *
 * Always returns void (never reveals whether the email exists).
 */
export async function requestPasswordReset(apiUrl: string, input: PasswordResetRequestInput): Promise<void> {
  await fetch(assertSafeServiceUrl(`${apiUrl}/v1/auth/password:reset-request`, apiUrl), {
    redirect: 'error',
    method: 'POST',
    headers: JSON_HEADERS,
    body: JSON.stringify({ email: input.email }),
  });
  // Always succeed from the client's perspective (prevent email enumeration)
}

/**
 * Confirm a password reset with the token from the email.
 */
export async function confirmPasswordReset(apiUrl: string, input: PasswordResetConfirmInput): Promise<void> {
  await postOrThrow(apiUrl, `${apiUrl}/v1/auth/password:reset`, {
    body: { token: input.token, newPassword: input.newPassword },
    errorClass: PasswordResetError,
    fallbackMessage: 'Password reset failed',
  });
}

/**
 * Change password for the authenticated user.
 */
export async function changePassword(apiUrl: string, input: PasswordChangeInput, accessToken: string): Promise<void> {
  await postOrThrow(apiUrl, `${apiUrl}/v1/auth/password:change`, {
    headers: authHeaders(accessToken),
    body: {
      currentPassword: input.currentPassword,
      newPassword: input.newPassword,
    },
    errorClass: PasswordResetError,
    fallbackMessage: 'Password change failed',
  });
}

// ─── Email Verification ──────────────────────────────────────────

/**
 * Send a verification email to the authenticated user.
 */
export async function sendVerificationEmail(apiUrl: string, accessToken: string): Promise<void> {
  await postOrThrow(apiUrl, `${apiUrl}/v1/auth/email:send-verification`, {
    headers: authHeaders(accessToken),
    errorClass: EmailVerificationError,
    fallbackMessage: 'Failed to send verification email',
  });
}

/**
 * Resend a verification email by email address (unauthenticated).
 *
 * The backend endpoint is AllowAnonymous and accepts { email } in the body.
 * This variant is used when the user is not yet authenticated (e.g. post-signup).
 */
export async function resendVerificationEmail(apiUrl: string, input: { email: string }): Promise<void> {
  await postOrThrow(apiUrl, `${apiUrl}/v1/auth/email:send-verification`, {
    body: { email: input.email },
    errorClass: EmailVerificationError,
    fallbackMessage: 'Failed to resend verification email',
  });
}

/**
 * Verify email with the token from the verification email.
 */
export async function verifyEmail(apiUrl: string, input: EmailVerificationInput): Promise<void> {
  await postOrThrow(apiUrl, `${apiUrl}/v1/auth/email:verify`, {
    body: { token: input.token },
    errorClass: EmailVerificationError,
    fallbackMessage: 'Email verification failed',
  });
}

// ─── Session Management ──────────────────────────────────────────

/**
 * List all active sessions for the authenticated user.
 */
export async function listSessions(apiUrl: string, accessToken: string): Promise<SessionInfo[]> {
  const response = await fetch(assertSafeServiceUrl(`${apiUrl}/v1/auth/sessions`, apiUrl), {
    redirect: 'error',
    headers: { Authorization: `Bearer ${accessToken}` },
  });

  if (!response.ok) return [];

  const data = (await response.json()) as { sessions?: SessionInfo[] } | SessionInfo[];
  return Array.isArray(data) ? data : (data.sessions ?? []);
}

/**
 * Terminate a specific session by ID.
 */
export async function terminateSession(apiUrl: string, sessionId: string, accessToken: string): Promise<void> {
  const response = await fetch(assertSafeServiceUrl(`${apiUrl}/v1/auth/sessions/${sessionId}`, apiUrl), {
    redirect: 'error',
    method: 'DELETE',
    headers: { Authorization: `Bearer ${accessToken}` },
  });

  if (!response.ok) {
    throw new SessionTerminationError('Failed to terminate session');
  }
}

/**
 * Terminate all sessions except the current one.
 */
export async function terminateOtherSessions(apiUrl: string, accessToken: string): Promise<void> {
  await postOrThrow(apiUrl, `${apiUrl}/v1/auth/sessions:terminate-others`, {
    headers: authHeaders(accessToken),
    errorClass: SessionTerminationError,
    fallbackMessage: 'Failed to terminate other sessions',
  });
}

/**
 * Terminate all sessions (including current — forces re-login).
 */
export async function terminateAllSessions(apiUrl: string, accessToken: string): Promise<void> {
  await postOrThrow(apiUrl, `${apiUrl}/v1/auth/sessions:terminate-all`, {
    headers: authHeaders(accessToken),
    errorClass: SessionTerminationError,
    fallbackMessage: 'Failed to terminate all sessions',
  });
}
