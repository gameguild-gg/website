import '@testing-library/jest-dom/vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  startTotpEnrollment: vi.fn(),
  completeTotpEnrollment: vi.fn(),
  startSmsEnrollment: vi.fn(),
  completeSmsEnrollment: vi.fn(),
  regenerateBackupCodes: vi.fn(),
  disableMfa: vi.fn(),
  toastSuccess: vi.fn(),
  toastError: vi.fn(),
}));

vi.mock('@/lib/auth/mfa-actions', () => ({
  startTotpEnrollmentAction: mocks.startTotpEnrollment,
  completeTotpEnrollmentAction: mocks.completeTotpEnrollment,
  startSmsEnrollmentAction: mocks.startSmsEnrollment,
  completeSmsEnrollmentAction: mocks.completeSmsEnrollment,
  regenerateBackupCodesAction: mocks.regenerateBackupCodes,
  disableMfaAction: mocks.disableMfa,
}));

vi.mock('next-intl', () => ({
  useTranslations: (namespace: string) => (key: string, values?: Record<string, unknown>) => {
    let message = `${namespace}.${key}`;
    for (const [name, value] of Object.entries(values ?? {})) {
      message = message.replace(`{${name}}`, String(value));
    }
    return message;
  },
}));

vi.mock('sonner', () => ({
  toast: {
    success: mocks.toastSuccess,
    error: mocks.toastError,
  },
}));

const { MfaMethodsManager } = await import('./mfa-methods-manager');

function makeData(overrides?: {
  isEnabled?: boolean;
  totpEnabled?: boolean;
  smsEnabled?: boolean;
  smsAvailable?: boolean;
  remainingCount?: number;
}) {
  return {
    configuration: {
      isEnabled: overrides?.isEnabled ?? true,
      enabledAt: '2026-10-01T10:00:00Z',
      enabledMethods: [
        ...(overrides?.totpEnabled === false ? [] : ['Totp']),
        ...(overrides?.smsEnabled ? ['Sms'] : []),
      ],
      backupCodesRemaining: overrides?.remainingCount ?? 4,
    },
    methods: {
      defaultMethod: 'Totp' as const,
      methods: [
        {
          method: 'Totp' as const,
          name: 'Authenticator App (TOTP)',
          description: 'Use an authenticator app like Google Authenticator or Authy',
          isEnabled: overrides?.totpEnabled ?? true,
          isAvailable: true,
          priority: 1,
        },
        {
          method: 'Sms' as const,
          name: 'SMS',
          description: 'Receive verification codes via text message',
          isEnabled: overrides?.smsEnabled ?? false,
          isAvailable: overrides?.smsAvailable ?? false,
          priority: 2,
        },
        {
          method: 'BackupCode' as const,
          name: 'Backup Codes',
          description: 'One-time use codes for emergency access',
          isEnabled: (overrides?.remainingCount ?? 4) > 0,
          isAvailable: true,
          priority: 4,
        },
      ],
    },
    backupCodes: {
      hasBackupCodes: (overrides?.remainingCount ?? 4) > 0,
      remainingCount: overrides?.remainingCount ?? 4,
      totalCount: 10,
      usedCount: 6,
      areUsageCountsKnown: true,
    },
  };
}

describe('MfaMethodsManager (settings/security, issue #244)', () => {
  it('lists every method with enrollment badges and the MFA status', () => {
    render(<MfaMethodsManager data={makeData()} />);

    expect(screen.getByTestId('mfa-status-badge')).toHaveTextContent('settings.security.mfa.status.badgeEnabled');
    const list = screen.getByTestId('mfa-method-list');
    expect(list).toHaveTextContent('Authenticator App (TOTP)');
    expect(list).toHaveTextContent('SMS');
    expect(list).toHaveTextContent('Backup Codes');
    expect(screen.getByTestId('mfa-recovery-status')).toHaveTextContent('settings.security.mfa.recovery.remaining');
  });

  it('starts TOTP enrollment, shows the provisioning secret, and completes with the code', async () => {
    const user = userEvent.setup();
    mocks.startTotpEnrollment.mockResolvedValue({
      success: true,
      status: 'success',
      setup: { secretKey: 'JBSWY3DPEHPK3PXP', qrCodeUri: 'otpauth://totp/GameGuild:user%40example.com?secret=JBSWY3DPEHPK3PXP' },
    });
    mocks.completeTotpEnrollment.mockResolvedValue({ success: true, status: 'success' });
    render(<MfaMethodsManager data={makeData({ totpEnabled: false, isEnabled: false })} />);

    await user.click(screen.getByTestId('mfa-totp-start'));

    const secret = await screen.findByTestId('mfa-totp-secret');
    expect(secret).toHaveTextContent('JBSWY3DPEHPK3PXP');
    expect(mocks.startTotpEnrollment).toHaveBeenCalledTimes(1);

    await user.type(screen.getByLabelText('settings.security.mfa.totp.codeLabel'), '123456');
    await user.click(screen.getByRole('button', { name: 'settings.security.mfa.totp.complete' }));

    await waitFor(() => {
      expect(mocks.completeTotpEnrollment).toHaveBeenCalledWith('123456', 'JBSWY3DPEHPK3PXP');
    });
    expect(mocks.toastSuccess).toHaveBeenCalled();
  });

  it('runs the SMS enrollment flow when the method is available', async () => {
    const user = userEvent.setup();
    mocks.startSmsEnrollment.mockResolvedValue({
      success: true,
      status: 'success',
      smsSetup: { phoneNumberMasked: '***-***-4567', expiresInSeconds: 600 },
    });
    mocks.completeSmsEnrollment.mockResolvedValue({ success: true, status: 'success' });
    render(<MfaMethodsManager data={makeData({ smsAvailable: true })} />);

    await user.click(screen.getByTestId('mfa-sms-start'));
    await user.type(screen.getByLabelText('settings.security.mfa.sms.phoneLabel'), '+15551234567');
    await user.click(screen.getByRole('button', { name: 'settings.security.mfa.sms.send' }));

    await waitFor(() => {
      expect(mocks.startSmsEnrollment).toHaveBeenCalledWith('+15551234567');
    });

    await user.type(screen.getByLabelText('settings.security.mfa.sms.codeLabel'), '654321');
    await user.click(screen.getByRole('button', { name: 'settings.security.mfa.sms.complete' }));

    await waitFor(() => {
      expect(mocks.completeSmsEnrollment).toHaveBeenCalledWith('654321');
    });
  });

  it('does not offer SMS enrollment when the platform has no SMS provider', () => {
    render(<MfaMethodsManager data={makeData({ smsAvailable: false })} />);

    expect(screen.queryByTestId('mfa-sms-start')).not.toBeInTheDocument();
    expect(screen.getByText('settings.security.mfa.sms.unavailable')).toBeInTheDocument();
  });

  it('regenerates recovery codes and shows them exactly once', async () => {
    const user = userEvent.setup();
    mocks.regenerateBackupCodes.mockResolvedValue({
      success: true,
      status: 'success',
      codes: ['AAAA-BBBB', 'CCCC-DDDD', 'EEEE-FFFF', 'GGGG-HHHH'],
    });
    render(<MfaMethodsManager data={makeData()} />);

    await user.click(screen.getByTestId('mfa-recovery-regenerate'));

    const codes = await screen.findByTestId('mfa-recovery-codes');
    expect(codes).toHaveTextContent('AAAA-BBBB');
    expect(codes).toHaveTextContent('GGGG-HHHH');

    await user.click(screen.getByRole('button', { name: 'settings.security.mfa.recovery.hide' }));
    expect(screen.queryByTestId('mfa-recovery-codes')).not.toBeInTheDocument();
  });

  it('disables MFA only with the account password and surfaces failures', async () => {
    const user = userEvent.setup();
    mocks.disableMfa.mockResolvedValue({ success: true, status: 'success' });
    render(<MfaMethodsManager data={makeData()} />);

    await user.click(screen.getByTestId('mfa-disable'));
    const confirm = screen.getByTestId('mfa-disable-confirm');
    expect(confirm).toBeDisabled();

    await user.type(screen.getByLabelText('settings.security.mfa.disable.passwordLabel'), 'Tr0ub4dor&3');
    await user.click(confirm);

    await waitFor(() => {
      expect(mocks.disableMfa).toHaveBeenCalledWith('Tr0ub4dor&3');
    });

    mocks.disableMfa.mockResolvedValue({ success: false, status: 'error', message: 'Password verification failed' });
    await user.click(screen.getByTestId('mfa-disable'));
    await user.type(screen.getByLabelText('settings.security.mfa.disable.passwordLabel'), 'wrong');
    await user.click(screen.getByTestId('mfa-disable-confirm'));
    await waitFor(() => {
      expect(mocks.toastError).toHaveBeenCalledWith('Password verification failed');
    });
  });
});
