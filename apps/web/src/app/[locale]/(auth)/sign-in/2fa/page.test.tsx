import '@testing-library/jest-dom/vitest';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('@/components/auth/mfa-challenge-form', () => ({
  MfaChallengeForm: ({ redirectTo }: { redirectTo?: string }) => (
    <div data-testid="mfa-challenge-form" data-redirect-to={redirectTo} />
  ),
}));

vi.mock('@/lib/auth/cross-domain-auth', () => ({
  resolveAllowedAuthRedirect: (value: string | undefined) => value ?? '/',
}));

import TwoFaPage from './page';

describe('/sign-in/2fa page', () => {
  it('passes the allowlisted redirectTo through to the challenge form', async () => {
    const ui = await TwoFaPage({
      searchParams: Promise.resolve({ redirectTo: '/workspace?tab=files' }),
    } as never);
    render(ui);

    expect(screen.getByTestId('mfa-challenge-form')).toHaveAttribute(
      'data-redirect-to',
      '/workspace?tab=files',
    );
  });

  it('falls back to a safe default without query params', async () => {
    const ui = await TwoFaPage({
      searchParams: Promise.resolve({}),
    } as never);
    render(ui);

    expect(screen.getByTestId('mfa-challenge-form')).toHaveAttribute(
      'data-redirect-to',
      '/',
    );
  });
});
