import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  DEFAULT_SECURITY_POLICIES,
  getSecurityPolicies,
} from '@/lib/security/policies';

describe('getSecurityPolicies (issue #838, display-only v1)', () => {
  beforeEach(() => {
    vi.unstubAllEnvs();
  });

  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('returns the documented defaults when no environment variables are set', () => {
    expect(getSecurityPolicies()).toEqual(DEFAULT_SECURITY_POLICIES);
    expect(DEFAULT_SECURITY_POLICIES.twoFactorEnforcement).toBe('optional');
  });

  it('reads valid policy values from the server environment', () => {
    vi.stubEnv('SECURITY_2FA_ENFORCEMENT', 'Required');
    vi.stubEnv('SECURITY_SESSION_TIMEOUT_MINUTES', '30');
    vi.stubEnv('SECURITY_API_KEY_ROTATION_DAYS', '45');

    expect(getSecurityPolicies()).toEqual({
      twoFactorEnforcement: 'required',
      sessionTimeoutMinutes: 30,
      apiKeyRotationDays: 45,
    });
  });

  it('falls back to defaults for invalid values instead of throwing', () => {
    vi.stubEnv('SECURITY_2FA_ENFORCEMENT', 'sometimes');
    vi.stubEnv('SECURITY_SESSION_TIMEOUT_MINUTES', 'not-a-number');
    vi.stubEnv('SECURITY_API_KEY_ROTATION_DAYS', '-5');

    expect(getSecurityPolicies()).toEqual(DEFAULT_SECURITY_POLICIES);
  });
});
