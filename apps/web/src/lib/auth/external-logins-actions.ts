'use server';

import { getToken } from '@/auth';
import { createServerClient, GeneratedApi } from '@game-guild/client';
import { revalidatePath } from 'next/cache';

/**
 * Server actions for the Connected Accounts card.
 *
 * All backend calls carry the caller's bearer token via createServerClient
 * (pattern: src/lib/coding-assignment/client.ts). Google linking MUST NOT go
 * through signIn — that would re-issue/clobber the session (plan M1); the
 * link endpoints attach the external identity to the CURRENT user only.
 */

const SETTINGS_ACCOUNT_PATH = '/workspace/settings/account';

export type ExternalLoginActionStatus =
  | 'success'
  /** 409 — provider account already linked to another user. */
  | 'conflict'
  /** 400 — unlink refused: last sign-in method and no password set. */
  | 'lastSignInMethod'
  /** 404 — provider not linked (unlink only). */
  | 'notLinked'
  /** 401 — no session / token missing. */
  | 'unauthorized'
  | 'error';

export interface ExternalLoginActionResult {
  success: boolean;
  status: ExternalLoginActionStatus;
}

function getApiClient() {
  const apiUrl =
    process.env.API_URL ||
    process.env.NEXT_PUBLIC_API_URL ||
    'http://localhost:8080';
  return createServerClient({
    baseUrl: apiUrl,
    auth: { getAccessToken: () => getToken() },
  });
}

function statusFromHttpError(status: number | undefined): ExternalLoginActionStatus {
  switch (status) {
    case 409:
      return 'conflict';
    case 400:
      return 'lastSignInMethod';
    case 404:
      return 'notLinked';
    case 401:
      return 'unauthorized';
    default:
      return 'error';
  }
}

/**
 * Link the signed-in user's Google account from a GIS credential (ID token).
 * Idempotent when the Google identity is already linked to the same user.
 */
export async function linkGoogleAccount(idToken: string): Promise<ExternalLoginActionResult> {
  const authModule = new GeneratedApi.AuthModule(getApiClient());
  const result = await authModule.postAuthExternalLoginsGoogle({ idToken });
  if (!result.ok) {
    return { success: false, status: statusFromHttpError(result.error?.status) };
  }
  revalidatePath(SETTINGS_ACCOUNT_PATH);
  return { success: true, status: 'success' };
}

/**
 * Unlink an external login provider from the signed-in user.
 */
export async function unlinkProvider(provider: 'google' | 'discord'): Promise<ExternalLoginActionResult> {
  const authModule = new GeneratedApi.AuthModule(getApiClient());
  const result = await authModule.deleteAuthExternalLogins(provider);
  if (!result.ok) {
    return { success: false, status: statusFromHttpError(result.error?.status) };
  }
  revalidatePath(SETTINGS_ACCOUNT_PATH);
  return { success: true, status: 'success' };
}

/**
 * Revoke individual OAuth scope grants on a linked provider without
 * unlinking it (issue #250). Whole-provider revocation stays on
 * {@link unlinkProvider}. Idempotent for scopes that are not granted.
 */
export async function revokeProviderScopes(
  provider: 'google' | 'discord',
  scopes: string[],
): Promise<ExternalLoginActionResult> {
  const authModule = new GeneratedApi.AuthModule(getApiClient());
  const result = await authModule.postAuthExternalLoginsScopesRevoke(provider, { scopes });
  if (!result.ok) {
    return { success: false, status: statusFromHttpError(result.error?.status) };
  }
  revalidatePath(SETTINGS_ACCOUNT_PATH);
  return { success: true, status: 'success' };
}
