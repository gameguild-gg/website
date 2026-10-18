import { render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('@/i18n/navigation', () => ({
  Link: ({ children, href, ...props }: { readonly children: ReactNode; readonly href: string }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock('next-intl', () => ({
  useTranslations: () => (key: string) =>
    ({
      'nudge.title': 'Protect your account — enable two-factor authentication',
      'nudge.action': 'Set up 2FA',
      'nudge.dismiss': 'Dismiss two-factor reminder',
    })[key] ?? key,
}));

import {
  TWO_FACTOR_NUDGE_STORAGE_KEY,
  TwoFactorNudgeBanner,
} from './two-factor-nudge-banner';

const THIRTY_ONE_DAYS_MS = 31 * 24 * 60 * 60 * 1000;

describe('TwoFactorNudgeBanner', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  afterEach(() => {
    window.localStorage.clear();
    vi.restoreAllMocks();
  });

  it('shows the nudge when 2FA is disabled and no dismissal is stored', async () => {
    render(<TwoFactorNudgeBanner twoFactorEnabled={false} />);

    expect(
      await screen.findByTestId('two-factor-nudge-banner'),
    ).toBeInTheDocument();
    expect(
      screen.getByText('Protect your account — enable two-factor authentication'),
    ).toBeInTheDocument();
    expect(screen.getByTestId('two-factor-nudge-action')).toHaveAttribute(
      'href',
      '/workspace/settings/security',
    );
  });

  it('renders nothing when 2FA is already enabled (and clears stale dismissal)', () => {
    window.localStorage.setItem(
      TWO_FACTOR_NUDGE_STORAGE_KEY,
      JSON.stringify({ dismissedAt: new Date().toISOString() }),
    );

    render(<TwoFactorNudgeBanner twoFactorEnabled={true} />);

    expect(screen.queryByTestId('two-factor-nudge-banner')).not.toBeInTheDocument();
    // Re-arm: the stale dismissal is cleared so a later disable re-nudges.
    expect(window.localStorage.getItem(TWO_FACTOR_NUDGE_STORAGE_KEY)).toBeNull();
  });

  it('is hidden while a fresh dismissal exists (<30 days)', () => {
    window.localStorage.setItem(
      TWO_FACTOR_NUDGE_STORAGE_KEY,
      JSON.stringify({ dismissedAt: new Date().toISOString() }),
    );

    render(<TwoFactorNudgeBanner twoFactorEnabled={false} />);

    expect(screen.queryByTestId('two-factor-nudge-banner')).not.toBeInTheDocument();
  });

  it('re-shows when the dismissal is older than 30 days', async () => {
    window.localStorage.setItem(
      TWO_FACTOR_NUDGE_STORAGE_KEY,
      JSON.stringify({
        dismissedAt: new Date(Date.now() - THIRTY_ONE_DAYS_MS).toISOString(),
      }),
    );

    render(<TwoFactorNudgeBanner twoFactorEnabled={false} />);

    expect(
      await screen.findByTestId('two-factor-nudge-banner'),
    ).toBeInTheDocument();
  });
});
