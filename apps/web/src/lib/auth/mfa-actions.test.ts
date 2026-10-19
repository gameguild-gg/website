import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getAuthMfa: vi.fn(),
  getAuthMfaMethods: vi.fn(),
  getAuthMfaBackupCodes: vi.fn(),
  revalidatePath: vi.fn(),
}));

vi.mock('@/auth', () => ({ getToken: vi.fn().mockResolvedValue('token') }));

vi.mock('next/cache', () => ({ revalidatePath: mocks.revalidatePath }));

vi.mock('@game-guild/client', () => ({
  createServerClient: vi.fn(() => ({})),
  GeneratedApi: {
    AuthMultiFactorModule: class {
      getAuthMfa = mocks.getAuthMfa;
      getAuthMfaMethods = mocks.getAuthMfaMethods;
      getAuthMfaBackupCodes = mocks.getAuthMfaBackupCodes;
    },
  },
}));

const { loadMfaData } = await import('./mfa-actions');

const CONFIGURATION = { isEnabled: true } as never;
const METHODS = { methods: [] } as never;
const BACKUP_CODES = { hasBackupCodes: false } as never;

describe('loadMfaData (issue #864 contract-drift defense)', () => {
  beforeEach(() => {
    mocks.getAuthMfa.mockReset();
    mocks.getAuthMfaMethods.mockReset();
    mocks.getAuthMfaBackupCodes.mockReset();
  });

  it('returns data when all calls succeed', async () => {
    mocks.getAuthMfa.mockResolvedValue({ ok: true, data: CONFIGURATION });
    mocks.getAuthMfaMethods.mockResolvedValue({ ok: true, data: METHODS });
    mocks.getAuthMfaBackupCodes.mockResolvedValue({ ok: true, data: BACKUP_CODES });

    await expect(loadMfaData()).resolves.toEqual({
      configuration: CONFIGURATION,
      methods: METHODS,
      backupCodes: BACKUP_CODES,
    });
  });

  it('degrades to null when safeParse throws on contract mismatch instead of crashing the RSC render', async () => {
    const contractDrift = new Error('[GET /v1/auth/mfa] Response validation failed');
    mocks.getAuthMfa.mockRejectedValue(contractDrift);
    mocks.getAuthMfaMethods.mockRejectedValue(contractDrift);
    mocks.getAuthMfaBackupCodes.mockRejectedValue(contractDrift);

    await expect(loadMfaData()).resolves.toEqual({
      configuration: null,
      methods: null,
      backupCodes: null,
    });
  });

  it('degrades individual failures to null while keeping successful sections', async () => {
    mocks.getAuthMfa.mockRejectedValue(new Error('drift'));
    mocks.getAuthMfaMethods.mockResolvedValue({ ok: false, error: { status: 500 } });
    mocks.getAuthMfaBackupCodes.mockResolvedValue({ ok: true, data: BACKUP_CODES });

    await expect(loadMfaData()).resolves.toEqual({
      configuration: null,
      methods: null,
      backupCodes: BACKUP_CODES,
    });
  });

  it('degrades non-ok results to null', async () => {
    mocks.getAuthMfa.mockResolvedValue({ ok: false, error: { status: 401 } });
    mocks.getAuthMfaMethods.mockResolvedValue({ ok: false, error: { status: 500 } });
    mocks.getAuthMfaBackupCodes.mockResolvedValue({ ok: false, error: { status: 404 } });

    await expect(loadMfaData()).resolves.toEqual({
      configuration: null,
      methods: null,
      backupCodes: null,
    });
  });
});
