'use server';

import { getToken } from '@/auth';
import {
  createServerClient,
  GeneratedApi,
  type IdentityAuthenticationBackupCodesStatusOutput,
  type IdentityAuthenticationMfaConfigurationOutput,
  type IdentityAuthenticationMfaMethodsOutput,
  type IdentityAuthenticationMfaSetupOutput,
} from '@game-guild/client';
import { revalidatePath } from 'next/cache';

/**
 * Server actions for the MFA method-management surface (settings/security, issue #244).
 *
 * Mirrors sessions-actions.ts: bearer via createServerClient + getToken; every mutation
 * revalidates the security page so the method list, recovery-code status and profile state
 * reflect server state after each action.
 */

const SETTINGS_SECURITY_PATH = '/workspace/settings/security';

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

export interface MfaPageData {
  configuration: IdentityAuthenticationMfaConfigurationOutput | null;
  methods: IdentityAuthenticationMfaMethodsOutput | null;
  backupCodes: IdentityAuthenticationBackupCodesStatusOutput | null;
}

export type MfaActionStatus = 'success' | 'notFound' | 'unauthorized' | 'error';

export interface MfaActionResult {
  success: boolean;
  status: MfaActionStatus;
  message?: string;
}

/** Result of starting an enrollment: provisioning data (TOTP) or delivery metadata (SMS). */
export interface MfaEnrollmentStartResult extends MfaActionResult {
  setup?: IdentityAuthenticationMfaSetupOutput;
  smsSetup?: {
    message?: string | null;
    phoneNumberMasked?: string | null;
    expiresInSeconds?: number;
  };
}

/** Result of regenerating recovery codes: the fresh codes, shown exactly once. */
export interface MfaBackupCodesResult extends MfaActionResult {
  codes?: string[];
}

function statusFromError(error: { status?: number } | undefined): MfaActionStatus {
  switch (error?.status) {
    case 401:
      return 'unauthorized';
    case 404:
      return 'notFound';
    default:
      return 'error';
  }
}

function resultFrom(success: boolean, error?: { status?: number }, message?: string): MfaActionResult {
  return success
    ? { success: true, status: 'success', message }
    : { success: false, status: statusFromError(error) };
}

function errorMessage(error?: { message?: string; detail?: string }): string | undefined {
  const message = error?.detail ?? error?.message;
  return message && message.length > 0 ? message : undefined;
}

/**
 * Defensive read for page loaders (issue #864): the generated client's
 * safeParse THROWS on zod contract mismatch, and a throw inside a server
 * component render crashes the page with React #441. Any failure — thrown
 * validation/network error or a non-ok result — must degrade to `fallback`
 * so the security page renders with empty data instead of erroring.
 */
async function safeLoad<T>(call: () => Promise<{ ok: boolean; data?: T }>, fallback: T | null): Promise<T | null> {
  try {
    const result = await call();
    return result.ok ? (result.data ?? fallback) : fallback;
  } catch {
    // Contract drift between the deployed API and the generated client (issue #864):
    // render the page with degraded data rather than crashing the RSC render.
    return fallback;
  }
}

/** Loads the MFA configuration, method catalog and recovery-code status in one pass. */
export async function loadMfaData(): Promise<MfaPageData> {
  const mfaModule = new GeneratedApi.AuthMultiFactorModule(getApiClient());

  const [configuration, methods, backupCodes] = await Promise.all([
    safeLoad(() => mfaModule.getAuthMfa(), null),
    safeLoad(() => mfaModule.getAuthMfaMethods(), null),
    safeLoad(() => mfaModule.getAuthMfaBackupCodes(), null),
  ]);

  return { configuration, methods, backupCodes };
}

/** Starts authenticator-app (TOTP) enrollment, returning the secret and QR provisioning URI. */
export async function startTotpEnrollmentAction(): Promise<MfaEnrollmentStartResult> {
  const mfaModule = new GeneratedApi.AuthMultiFactorModule(getApiClient());
  const result = await mfaModule.postAuthMfaTotpSetup();

  if (!result.ok) {
    return { ...resultFrom(false, result.error, errorMessage(result.error)), setup: undefined };
  }

  return { success: true, status: 'success', setup: result.data ?? undefined };
}

/** Completes authenticator-app (TOTP) enrollment by verifying a code from the app. */
export async function completeTotpEnrollmentAction(code: string, secretKey: string): Promise<MfaActionResult> {
  const mfaModule = new GeneratedApi.AuthMultiFactorModule(getApiClient());
  // The generated contract carries the provisioning secret alongside the code; the server
  // verifies against the stored pending enrollment and ignores the echo for SMS.
  const result = await mfaModule.postAuthMfaTotpComplete({ code, secretKey });
  revalidatePath(SETTINGS_SECURITY_PATH);

  if (!result.ok) {
    return resultFrom(false, result.error, errorMessage(result.error));
  }

  return resultFrom(true);
}

/** Starts SMS enrollment by sending a verification code to the given phone number. */
export async function startSmsEnrollmentAction(phoneNumber: string): Promise<MfaEnrollmentStartResult> {
  const mfaModule = new GeneratedApi.AuthMultiFactorModule(getApiClient());
  const result = await mfaModule.postAuthMfaSmsSetup({ phoneNumber });

  if (!result.ok) {
    return { ...resultFrom(false, result.error, errorMessage(result.error)), smsSetup: undefined };
  }

  return { success: true, status: 'success', smsSetup: result.data ?? undefined };
}

/** Completes SMS enrollment by verifying the code sent to the pending phone number. */
export async function completeSmsEnrollmentAction(code: string): Promise<MfaActionResult> {
  const mfaModule = new GeneratedApi.AuthMultiFactorModule(getApiClient());
  // SMS enrollment has no provisioning secret; the contract field stays empty.
  const result = await mfaModule.postAuthMfaSmsComplete({ code, secretKey: '' });
  revalidatePath(SETTINGS_SECURITY_PATH);

  if (!result.ok) {
    return resultFrom(false, result.error, errorMessage(result.error));
  }

  return resultFrom(true);
}

/** Regenerates the recovery (backup) codes; the fresh set is shown exactly once. */
export async function regenerateBackupCodesAction(): Promise<MfaBackupCodesResult> {
  const mfaModule = new GeneratedApi.AuthMultiFactorModule(getApiClient());
  const result = await mfaModule.postAuthMfaBackupCodesRegenerate();
  revalidatePath(SETTINGS_SECURITY_PATH);

  if (!result.ok) {
    return { ...resultFrom(false, result.error, errorMessage(result.error)), codes: undefined };
  }

  return { success: true, status: 'success', codes: result.data?.codes ?? [] };
}

/** Disables MFA after re-verifying the account password (server enforces password proof). */
export async function disableMfaAction(password: string): Promise<MfaActionResult> {
  const mfaModule = new GeneratedApi.AuthMultiFactorModule(getApiClient());
  const result = await mfaModule.postAuthMfaDisable({ password });
  revalidatePath(SETTINGS_SECURITY_PATH);

  if (!result.ok) {
    return resultFrom(false, result.error, errorMessage(result.error));
  }

  return resultFrom(true);
}
