/**
 * Server-side security policy configuration (issue #838, display-only v1).
 *
 * Workspace-level security posture is defined by environment configuration on
 * the server — never by client state. The Security settings section renders
 * these values read-only; mutation of the underlying knobs stays an
 * administrator/ops concern so a compromised browser session cannot relax
 * policy.
 */

export type TwoFactorEnforcement = 'optional' | 'required';

export interface SecurityPolicies {
  /** Whether accounts must enroll a second factor before full access. */
  twoFactorEnforcement: TwoFactorEnforcement;
  /** Idle session lifetime before re-authentication, in minutes. */
  sessionTimeoutMinutes: number;
  /** Recommended maximum age of API keys before rotation, in days. */
  apiKeyRotationDays: number;
}

const TWO_FACTOR_ENFORCEMENT_VALUES: readonly TwoFactorEnforcement[] = ['optional', 'required'];

/** Default posture: 2FA stays opt-in (issue #838 default policy). */
export const DEFAULT_SECURITY_POLICIES: SecurityPolicies = {
  twoFactorEnforcement: 'optional',
  sessionTimeoutMinutes: 60,
  apiKeyRotationDays: 90,
};

function parsePositiveInteger(value: string | undefined, fallback: number): number {
  if (!value) return fallback;
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

/**
 * Reads the effective security policies from server environment variables.
 * Invalid or missing values fall back to the documented defaults rather than
 * throwing, so the settings page can always render.
 *
 * Supported variables:
 * - `SECURITY_2FA_ENFORCEMENT` — `optional` (default) or `required`.
 * - `SECURITY_SESSION_TIMEOUT_MINUTES` — positive integer, default 60.
 * - `SECURITY_API_KEY_ROTATION_DAYS` — positive integer, default 90.
 */
export function getSecurityPolicies(): SecurityPolicies {
  const enforcement = process.env.SECURITY_2FA_ENFORCEMENT?.trim().toLowerCase();

  return {
    twoFactorEnforcement:
      enforcement && TWO_FACTOR_ENFORCEMENT_VALUES.includes(enforcement as TwoFactorEnforcement)
        ? (enforcement as TwoFactorEnforcement)
        : DEFAULT_SECURITY_POLICIES.twoFactorEnforcement,
    sessionTimeoutMinutes: parsePositiveInteger(
      process.env.SECURITY_SESSION_TIMEOUT_MINUTES,
      DEFAULT_SECURITY_POLICIES.sessionTimeoutMinutes,
    ),
    apiKeyRotationDays: parsePositiveInteger(
      process.env.SECURITY_API_KEY_ROTATION_DAYS,
      DEFAULT_SECURITY_POLICIES.apiKeyRotationDays,
    ),
  };
}
