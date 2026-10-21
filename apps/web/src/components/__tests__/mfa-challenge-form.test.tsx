import '@testing-library/jest-dom/vitest';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  createMockUseAuth,
  renderWithUser,
  type MockUseAuthReturn,
} from '@/test/auth-test-helpers';

let mockAuth: MockUseAuthReturn;

vi.mock('@game-guild/client/react', () => ({
  useAuth: () => mockAuth,
}));

vi.mock('next-intl', () => ({
  useTranslations: (namespace: string) => (key: string) => `${namespace}.${key}`,
}));

vi.mock('@/i18n/navigation', () => ({
  Link: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

// Must import AFTER mocks
const { MfaChallengeForm } = await import('@/components/auth/mfa-challenge-form');

const challenge = {
  mfaToken: 'x'.repeat(43),
  availableMethods: ['Totp', 'BackupCode'],
};

describe('MfaChallengeForm', () => {
  beforeEach(() => {
    mockAuth = createMockUseAuth();
  });

  afterEach(cleanup);

  it('renders the challenge form with the code input when a challenge is active', () => {
    mockAuth.mfaChallenge = challenge;
    render(<MfaChallengeForm />);

    expect(screen.getByText('signIn.mfa.title')).toBeInTheDocument();
    expect(screen.getByLabelText('signIn.mfa.codeLabel')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'signIn.mfa.submit' })).toBeInTheDocument();
  });

  it('submits signIn with the held mfaToken and entered code', async () => {
    mockAuth.mfaChallenge = challenge;
    const { user } = renderWithUser(<MfaChallengeForm redirectTo="/workspace" />);

    await user.type(screen.getByLabelText('signIn.mfa.codeLabel'), '123456');
    await user.click(screen.getByRole('button', { name: 'signIn.mfa.submit' }));

    await waitFor(() => {
      expect(mockAuth.signIn).toHaveBeenCalledWith('credentials', {
        mfaToken: challenge.mfaToken,
        method: 'Totp',
        code: '123456',
        redirectTo: '/workspace',
      });
    });
  });

  it('offers the recovery-code method when the challenge allows BackupCode', () => {
    mockAuth.mfaChallenge = challenge;
    render(<MfaChallengeForm />);

    expect(
      screen.getByRole('option', { name: 'signIn.mfa.methodBackupCode' }),
    ).toBeInTheDocument();
  });

  it('hides the method selector when only TOTP is available', () => {
    mockAuth.mfaChallenge = { mfaToken: 'x'.repeat(43), availableMethods: ['Totp'] };
    render(<MfaChallengeForm />);

    expect(screen.queryByLabelText('signIn.mfa.methodLabel')).not.toBeInTheDocument();
  });

  it('renders the no-challenge notice and a link back to /sign-in without a challenge', () => {
    render(<MfaChallengeForm />);

    expect(screen.getByText('signIn.mfa.noChallenge')).toBeInTheDocument();
    expect(screen.getByText('signIn.mfa.backToSignIn')).toHaveAttribute(
      'href',
      '/sign-in',
    );
    expect(screen.queryByLabelText('signIn.mfa.codeLabel')).not.toBeInTheDocument();
  });

  it('keeps the challenge and surfaces the hook error on a failed verify', async () => {
    mockAuth.mfaChallenge = challenge;
    mockAuth.error = new Error('Invalid code');
    render(<MfaChallengeForm />);

    expect(screen.getByText('Invalid code')).toBeInTheDocument();
  });
});
