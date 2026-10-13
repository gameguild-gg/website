'use server';

import { getToken } from '@/auth';
import {
  createServerClient,
  GeneratedApi,
  type IdentityAuthenticationSessionOutput,
  type IdentityAuthenticationSessionSecurityAnalysis,
  type IdentityAuthenticationTrustedDeviceOutput,
} from '@game-guild/client';
import { revalidatePath } from 'next/cache';

/**
 * Server actions for the session management surface (settings/security, issue #278).
 *
 * Mirrors password-change-action.ts: bearer via createServerClient + getToken. All
 * mutations revalidate the security page so the session and trusted-device lists
 * reflect server state after every action.
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

export interface SessionsPageData {
  sessions: IdentityAuthenticationSessionOutput[];
  analysis: IdentityAuthenticationSessionSecurityAnalysis | null;
  trustedDevices: IdentityAuthenticationTrustedDeviceOutput[];
}

export type SessionActionStatus = 'success' | 'notFound' | 'unauthorized' | 'error';

export interface SessionActionResult {
  success: boolean;
  status: SessionActionStatus;
}

function statusFromError(error: { status?: number } | undefined): SessionActionStatus {
  switch (error?.status) {
    case 401:
      return 'unauthorized';
    case 404:
      return 'notFound';
    default:
      return 'error';
  }
}

function resultFrom(success: boolean, error?: { status?: number }): SessionActionResult {
  return success
    ? { success: true, status: 'success' }
    : { success: false, status: statusFromError(error) };
}

/**
 * Loads the session list, the current-session security analysis, and the trusted
 * device list in one pass for the settings/security page.
 */
export async function loadSessionsData(): Promise<SessionsPageData> {
  const client = getApiClient();
  const sessionsModule = new GeneratedApi.AuthSessionsModule(client);
  const trustedDevicesModule = new GeneratedApi.AuthTrustedDevicesModule(client);

  const [sessionsResult, analysisResult, trustedResult] = await Promise.all([
    sessionsModule.getAuthSessions(),
    sessionsModule.getAuthSessionsAnalyzeSecurity(),
    trustedDevicesModule.getAuthTrustedDevices(),
  ]);

  return {
    sessions: sessionsResult.ok ? (sessionsResult.data ?? []) : [],
    analysis: analysisResult.ok ? (analysisResult.data ?? null) : null,
    trustedDevices: trustedResult.ok ? (trustedResult.data ?? []) : [],
  };
}

/** Names or renames one of the current user's own sessions (1–64 characters). */
export async function renameSessionAction(sessionId: string, name: string): Promise<SessionActionResult> {
  const sessionsModule = new GeneratedApi.AuthSessionsModule(getApiClient());
  const result = await sessionsModule.patchAuthSessionsName(sessionId, { name });
  revalidatePath(SETTINGS_SECURITY_PATH);
  return resultFrom(result.ok, result.ok ? undefined : result.error);
}

/** Terminates one specific session owned by the current user. */
export async function terminateSessionAction(sessionId: string): Promise<SessionActionResult> {
  const sessionsModule = new GeneratedApi.AuthSessionsModule(getApiClient());
  const result = await sessionsModule.deleteAuthSessions(sessionId);
  revalidatePath(SETTINGS_SECURITY_PATH);
  return resultFrom(result.ok, result.ok ? undefined : result.error);
}

/** Terminates every session except the current one. */
export async function terminateOtherSessionsAction(): Promise<SessionActionResult> {
  const sessionsModule = new GeneratedApi.AuthSessionsModule(getApiClient());
  const result = await sessionsModule.postAuthSessionsTerminateOthers();
  revalidatePath(SETTINGS_SECURITY_PATH);
  return resultFrom(result.ok, result.ok ? undefined : result.error);
}

/** Terminates every session including the current one (signs the user out everywhere). */
export async function terminateAllSessionsAction(): Promise<SessionActionResult> {
  const sessionsModule = new GeneratedApi.AuthSessionsModule(getApiClient());
  const result = await sessionsModule.postAuthSessionsTerminateAll();
  revalidatePath(SETTINGS_SECURITY_PATH);
  return resultFrom(result.ok, result.ok ? undefined : result.error);
}

/** Marks the device behind the current session as trusted. */
export async function trustCurrentDeviceAction(deviceName?: string): Promise<SessionActionResult> {
  const trustedDevicesModule = new GeneratedApi.AuthTrustedDevicesModule(getApiClient());
  const result = await trustedDevicesModule.postAuthTrustedDevices({
    deviceName: deviceName ?? null,
  });
  revalidatePath(SETTINGS_SECURITY_PATH);
  return resultFrom(result.ok, result.ok ? undefined : result.error);
}

/** Removes one device from the trusted device list. */
export async function revokeTrustedDeviceAction(deviceId: string): Promise<SessionActionResult> {
  const trustedDevicesModule = new GeneratedApi.AuthTrustedDevicesModule(getApiClient());
  const result = await trustedDevicesModule.deleteAuthTrustedDevices(deviceId);
  revalidatePath(SETTINGS_SECURITY_PATH);
  return resultFrom(result.ok, result.ok ? undefined : result.error);
}
