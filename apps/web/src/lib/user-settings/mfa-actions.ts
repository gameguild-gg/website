'use server';

/** Server-side MFA status query used by the authenticated workspace shell. */

import type { IdentityAuthenticationMfaConfigurationOutput } from '@game-guild/client';
import { GeneratedApi } from '@game-guild/client';

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
