import '@testing-library/jest-dom/vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { NextIntlClientProvider } from 'next-intl';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import enMessages from '@/i18n/messages/en-US.json';
import { SecurityForm } from './security-form';

const actionMocks = vi.hoisted(() => ({
  startTotpSetup: vi.fn(),
  completeTotpSetup: vi.fn(),
  regenerateBackupCodes: vi.fn(),
  disableMfa: vi.fn(),
}));

vi.mock('@/lib/user-settings/mfa-actions', () => actionMocks);

function renderForm(props?: { initialEnabled?: boolean; initialBackupCodesRemaining?: number | null }) {
  return render(
    <NextIntlClientProvider locale="en-US" messages={enMessages}>
      <SecurityForm
        initialEnabled={props?.initialEnabled ?? false}
        initialBackupCodesRemaining={props?.initialBackupCodesRemaining ?? null}
      />
    </NextIntlClientProvider>,
  );
}

describe('SecurityForm', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    actionMocks.startTotpSetup.mockResolvedValue({
      ok: true,
      data: {
        secretKey: 'JBSW Y3DP EHPK 3PXP',
        qrCodeUri: 'otpauth://totp/GameGuild:test%40example.com?secret=JBSWY3DPEHPK3PXP',
        expiresAt: '2030-01-01T00:00:00.000Z',
      },
    });
    actionMocks.completeTotpSetup.mockResolvedValue({
      ok: true,
      data: { backupCodes: ['AAAA-1111', 'BBBB-2222', 'CCCC-3333'] },
    });
    actionMocks.regenerateBackupCodes.mockResolvedValue({
      ok: true,
      data: { codes: ['DDDD-4444', 'EEEE-5555'], generatedAt: '2030-01-01T00:00:00.000Z' },
    });
    actionMocks.disableMfa.mockResolvedValue({ ok: true, data: undefined });
  });

  it('shows the setup button when not enrolled, then the QR code and secret after starting setup', async () => {
    const user = userEvent.setup();
    renderForm({ initialEnabled: false });

    expect(screen.getByTestId('mfa-status')).toHaveTextContent(
      'Two-factor authentication is disabled.',
    );
    const setupButton = screen.getByRole('button', { name: 'Set up two-factor authentication' });
    expect(setupButton).toBeInTheDocument();

    await user.click(setupButton);

    await waitFor(() => {
      expect(screen.getByTestId('totp-setup-panel')).toBeInTheDocument();
    });
    expect(screen.getByText('Scan this QR code with your authenticator app.')).toBeInTheDocument();
    expect(document.querySelector('svg')).toBeInTheDocument();
    expect(screen.getByTestId('totp-secret')).toHaveTextContent('JBSW Y3DP EHPK 3PXP');
  });

  it('hides the setup path and shows the disable panel when enrolled', () => {
    renderForm({ initialEnabled: true, initialBackupCodesRemaining: 2 });

    expect(screen.getByTestId('mfa-status')).toHaveTextContent(
      'Two-factor authentication is enabled.',
    );
    expect(screen.getByText('2 backup codes remaining')).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Set up two-factor authentication' }),
    ).not.toBeInTheDocument();
    expect(screen.getByTestId('disable-panel')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Disable' })).toBeInTheDocument();
  });

  it('shows backup codes exactly once after completing setup and discards them on confirm', async () => {
    const user = userEvent.setup();
    renderForm({ initialEnabled: false });

    await user.click(
      screen.getByRole('button', { name: 'Set up two-factor authentication' }),
    );
    const codeInput = await screen.findByRole('textbox', { name: 'Verification code' });
    await user.type(codeInput, '123456');
    await user.click(screen.getByRole('button', { name: 'Verify and enable' }));

    const list = await screen.findByTestId('backup-codes-list');
    expect(list).toHaveTextContent('AAAA-1111');
    expect(list).toHaveTextContent('BBBB-2222');
    expect(list).toHaveTextContent('CCCC-3333');

    await user.click(screen.getByRole('button', { name: 'I saved my codes' }));

    await waitFor(() => {
      expect(screen.queryByTestId('backup-codes-list')).not.toBeInTheDocument();
    });
    expect(screen.getByTestId('mfa-status')).toHaveTextContent(
      'Two-factor authentication is enabled.',
    );
  });

  it('calls disableMfa with the entered code from the disable panel', async () => {
    const user = userEvent.setup();
    renderForm({ initialEnabled: true });

    const disableInput = screen.getByRole('textbox', { name: 'Verification code' });
    await user.type(disableInput, '987654');
    fireEvent.click(screen.getByRole('button', { name: 'Disable' }));

    await waitFor(() => {
      expect(actionMocks.disableMfa).toHaveBeenCalledWith({ code: '987654' });
    });
    await waitFor(() => {
      expect(screen.getByTestId('mfa-status')).toHaveTextContent(
        'Two-factor authentication is disabled.',
      );
    });
  });
});
