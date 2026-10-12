import { describe, expect, it } from 'vitest';
import { IdentityAuthenticationBackupCodesStatusOutputSchema, IdentityAuthenticationMfaConfigurationOutputSchema } from '../../src/generated/types.gen';

describe('MFA recovery status compatibility', () => {
  it('preserves numeric v1 fields and marks unknown legacy history', () => {
    const parsed = IdentityAuthenticationBackupCodesStatusOutputSchema.parse({
      totalCount: 4,
      remainingCount: 4,
      usedCount: 0,
      hasBackupCodes: true,
      areUsageCountsKnown: false,
    });
    expect(parsed.areUsageCountsKnown).toBe(false);
    expect(parsed.totalCount).toBe(4);
    expect(parsed.usedCount).toBe(0);
  });

  it('retains compatibility with older responses lacking the additive flag', () => {
    expect(
      IdentityAuthenticationBackupCodesStatusOutputSchema.safeParse({
        totalCount: 10,
        remainingCount: 8,
        usedCount: 2,
        hasBackupCodes: true,
      }).success,
    ).toBe(true);
    expect(
      IdentityAuthenticationBackupCodesStatusOutputSchema.safeParse({
        totalCount: null,
        remainingCount: 8,
        usedCount: null,
        hasBackupCodes: true,
      }).success,
    ).toBe(false);
  });

  it('supports explicit unknown issuance and newly recorded issuance metadata', () => {
    expect(
      IdentityAuthenticationMfaConfigurationOutputSchema.parse({
        backupCodesIssued: null,
        backupCodesRemaining: 4,
      }).backupCodesIssued,
    ).toBeNull();
    expect(
      IdentityAuthenticationMfaConfigurationOutputSchema.parse({
        backupCodesIssued: 12,
        backupCodesRemaining: 11,
      }).backupCodesIssued,
    ).toBe(12);
  });
});
