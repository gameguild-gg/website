/**
 * Signed OAuth State Cookie
 *
 * CSRF protection for the OAuth redirect sign-in flow
 * (GET /api/auth/signin/:provider → provider → GET /api/auth/callback/:provider).
 *
 * Cookie spec (M6):
 *   - name: `__game-guild-oauth-state-<providerId>` (sign-in flow; the web link flow
 *     uses a distinct name but mirrors this spec)
 *   - value: base64url(JSON payload) + '.' + hex HMAC-SHA256(payload, secret)
 *   - attributes: HttpOnly, SameSite=Lax (never Strict — must survive the
 *     provider's top-level cross-site redirect), Path=/, MaxAge=600
 *
 * The secret is the same raw AUTH_SECRET used for the session JWT; HMAC uses
 * Web Crypto (like the jose-based session JWT) so the shared chunk stays
 * free of node: imports — the main entry is imported from client components.
 *
 * Internal to the client lib for now — not part of the public exports.
 */

import type { CookieSerializeOptions } from '../../runtime/auth/cookies.js';

/** State cookie lifetime in seconds (10 minutes). */
export const STATE_COOKIE_MAX_AGE = 600;

/**
 * Payload stashed in the state cookie during the sign-in redirect flow.
 */
export interface OAuthStatePayload {
  /** OAuth state value expected back on the callback query string. */
  state: string;
  /** Relative-only post-sign-in redirect target. */
  redirectTo: string;
  tenantId?: string;
  locale?: string;
  flow: 'signin';
  /** Expiry in epoch milliseconds. */
  exp: number;
}

/**
 * Cookie name for a provider's sign-in state cookie.
 */
export function stateCookieName(providerId: string): string {
  return `__game-guild-oauth-state-${providerId}`;
}

/**
 * Cookie attributes for set (maxAge=600) and delete (maxAge=0).
 */
export function stateCookieOptions(maxAge: number = STATE_COOKIE_MAX_AGE): CookieSerializeOptions {
  return { httpOnly: true, sameSite: 'lax', path: '/', maxAge };
}

/**
 * Validate a redirect target as relative-only:
 * must start with '/' and must not start with '//' (protocol-relative).
 * Anything else (absolute URLs, malformed values) falls back.
 */
export function resolveAllowedRedirect(value: string | null | undefined, fallback: string): string {
  return value && value.startsWith('/') && !value.startsWith('//') ? value : fallback;
}

/**
 * Sign a state payload: base64url(payload) + '.' + hex HMAC-SHA256.
 */
export async function signStatePayload(payload: OAuthStatePayload, secret: string): Promise<string> {
  const encoded = Buffer.from(JSON.stringify(payload), 'utf8').toString('base64url');
  return `${encoded}.${await hmacHex(encoded, secret)}`;
}

/**
 * Verify the cookie signature and expiry.
 * Returns the payload, or null on any failure (bad format, HMAC mismatch,
 * undecodable JSON, expired). Flow and state matching are the caller's
 * policy so the future link flow can reuse this.
 */
export async function verifyStateCookie(value: string | undefined, secret: string): Promise<OAuthStatePayload | null> {
  if (!value) return null;

  const dot = value.lastIndexOf('.');
  /* v8 ignore start -- malformed cookie from a tampering client */
  if (dot <= 0 || dot === value.length - 1) return null;
  /* v8 ignore stop */

  const encoded = value.slice(0, dot);
  const mac = value.slice(dot + 1);

  if (!constantTimeEqual(mac, await hmacHex(encoded, secret))) return null;

  try {
    const payload = JSON.parse(Buffer.from(encoded, 'base64url').toString('utf8')) as OAuthStatePayload;
    if (typeof payload.exp !== 'number' || payload.exp <= Date.now()) return null;
    return payload;
  } catch {
    return null;
  }
}

/**
 * Constant-time string comparison. Branch-free over content; the early
 * length return leaks only lengths (hex digests have fixed length anyway).
 */
export function constantTimeEqual(a: string, b: string): boolean {
  /* v8 ignore start -- length mismatch path */
  if (a.length !== b.length) return false;
  /* v8 ignore stop */
  let diff = 0;
  for (let i = 0; i < a.length; i++) {
    diff |= a.charCodeAt(i) ^ b.charCodeAt(i);
  }
  return diff === 0;
}

async function hmacHex(payload: string, secret: string): Promise<string> {
  const encoder = new TextEncoder();
  const key = await crypto.subtle.importKey('raw', encoder.encode(secret), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
  const signature = await crypto.subtle.sign('HMAC', key, encoder.encode(payload));
  return Buffer.from(signature).toString('hex');
}
