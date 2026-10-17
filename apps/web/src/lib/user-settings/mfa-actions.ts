'use server';

/**
 * Server actions for the workspace security settings page (2FA).
 *
 * Wraps the generated Identity.Authentication AuthMultiFactorModule. All
 * actions operate on the current authenticated user — identity comes from
 * the session token, never from the client payload. Results use the
 * settings-hub ActionResult shape ({ success, data } | { success, error }).
 */

import type {
  IdentityAuthenticationBackupCodesOutput,
  IdentityAuthenticationBackupCodesStatusOutput,
  IdentityAuthenticationMfaConfigurationOutput,
} from '@game-guild/client';
import { GeneratedApi } from '@game-guild/client';
import { revalidatePath } from 'next/cache';

import { getUserSettingsApiClient } from './api-client';

export type MfaActionResult<T> =
  | { readonly ok: true; readonly data: T }
  | { readonly ok: false; readonly error: string };

function getMfaModule(): GeneratedApi.AuthMultiFactorModule {
  // Matches mark-read-action.ts — the module is constructed per call from the
  // authenticated server client; getUserSettingsApiClient carries the same
  // auth + tenant pipeline via createServerClient.
  return new GeneratedApi.AuthMultiFactorModule(getUserSettingsApiClient());
}

function toError(message: string | null | undefined, fallback: string): string {
  return message || fallback;
}

/** Current user's MFA configuration (enabled state + backup-code counts). */
export async function getMfaStatus(): Promise<
  MfaActionResult<IdentityAuthenticationMfaConfigurationOutput>
> {
  const mfa = getMfaModule();
  const result = await mfa.getAuthMfa();

  if (!result.ok) {
    return { ok: false, error: toError(result.error.message, 'Unable to load MFA status.') };
  }

  return { ok: true, data: result.data };
}

/**
 * Starts a TOTP enrollment: returns the secret key, QR code URI, and the
 * pending setup session expiry. Safe to call repeatedly — each call
 * replaces any pending setup.
 */
export async function startTotpSetup(): Promise<
  MfaActionResult<{
    secretKey: string;
    qrCodeUri: string;
    expiresAt: string;
  }>
> {
  const mfa = getMfaModule();
  const result = await mfa.postAuthMfaTotpSetup();

  if (!result.ok) {
    return { ok: false, error: toError(result.error.message, 'Unable to start 2FA setup.') };
  }

  const { secretKey, qrCodeUri, expiresAt } = result.data;
  if (!secretKey || !qrCodeUri) {
    return { ok: false, error: toError(result.data.errorMessage, 'Unable to start 2FA setup.') };
  }

  return { ok: true, data: { secretKey, qrCodeUri, expiresAt: expiresAt ?? '' } };
}

/**
 * Completes TOTP enrollment by verifying a code from the user's
 * authenticator. Backup codes are returned exactly once, here.
 */
export async function completeTotpSetup(params: {
  code: string;
  secretKey: string;
}): Promise<MfaActionResult<{ backupCodes: string[] }>> {
  // The generated method validates the response as MfaSuccessOutput (message
  // only), but the deployed contract returns the backup-code set here. Same
  // mismatch workaround as lib/user-settings/queries.ts: raw request through
  // the shared ApiClient pipeline, validated against BackupCodesOutput.
  const client = getUserSettingsApiClient();
  const result = await client.request<IdentityAuthenticationBackupCodesOutput>({
    method: 'POST',
    path: '/v1/auth/mfa/totp:complete',
    body: { code: params.code, secretKey: params.secretKey },
    requiresAuth: true,
  });

  if (!result.ok) {
    return { ok: false, error: toError(result.error.message, 'Unable to verify the code.') };
  }

  return { ok: true, data: { backupCodes: result.data.codes ?? [] } };
}

/**
 * Regenerates the backup-code set, invalidating all previously issued
 * codes. New codes are returned exactly once, here.
 */
export async function regenerateBackupCodes(): Promise<
  MfaActionResult<{ codes: string[]; generatedAt: string | null }>
> {
  const mfa = getMfaModule();
  const result = await mfa.postAuthMfaBackupCodesRegenerate();

  if (!result.ok) {
    return { ok: false, error: toError(result.error.message, 'Unable to regenerate codes.') };
  }

  return {
    ok: true,
    data: { codes: result.data.codes ?? [], generatedAt: result.data.generatedAt ?? null },
  };
}

/**
 * Backup-code status (counts only — codes themselves are never re-served).
 */
export async function getMfaBackupCodesStatus(): Promise<
  MfaActionResult<IdentityAuthenticationBackupCodesStatusOutput>
> {
  const mfa = getMfaModule();
  const result = await mfa.getAuthMfaBackupCodes();

  if (!result.ok) {
    return { ok: false, error: toError(result.error.message, 'Unable to load backup codes.') };
  }

  return { ok: true, data: result.data };
}

/** Disables MFA for the current user after password re-verification (issue #244). */
export async function disableMfa(params: { password: string }): Promise<MfaActionResult<void>> {
  const mfa = getMfaModule();
  const result = await mfa.postAuthMfaDisable({ password: params.password });

  if (!result.ok) {
    return { ok: false, error: toError(result.error.message, 'Unable to disable 2FA.') };
  }

  revalidatePath('/', 'layout');
  return { ok: true, data: undefined };
}
