import '@testing-library/jest-dom/vitest';
import type { ReactNode } from 'react';
import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  auth: vi.fn(),
  redirect: vi.fn((href: string) => {
    throw new Error(`redirect:${href}`);
  }),
}));

vi.mock('next-intl/server', () => ({
  getTranslations: async (namespace: string) => (key: string) => `${namespace}.${key}`,
}));

vi.mock('@/auth', () => ({ auth: mocks.auth }));
vi.mock('@/lib/auth/cross-domain-auth', () => ({
  resolveAllowedAuthRedirect: (value: string | undefined) => value ?? '/',
}));
vi.mock('@/components/sign-in-form', () => ({
  // Renders the providers slot so the composition assertions below can see
  // the provider buttons the page passes into the form.
  SignInForm: ({ providers }: { providers?: ReactNode }) => (
    <div data-testid="sign-in-form">{providers}</div>
  ),
}));
vi.mock('@/components/google-one-tap', () => ({
  GoogleOneTap: () => <div data-testid="google-one-tap" />,
}));
vi.mock('@/components/google-sign-in-button', () => ({
  GoogleSignInButton: () => <div data-testid="google-sign-in-button" />,
}));
vi.mock('@/components/wallet-sign-in-buttons', () => ({
  WalletSignInButtons: () => <div data-testid="wallet-sign-in-buttons" />,
}));
vi.mock('@/components/auth/auth-error-notice', () => ({
  AuthErrorNotice: ({ errorCode }: { errorCode?: string }) =>
    errorCode ? <div data-testid="auth-error-notice">{errorCode}</div> : null,
}));

import SignInPage from './page';

async function renderPage(query: Record<string, string | undefined>) {
  const page = await SignInPage({
    params: Promise.resolve({ locale: 'en-US' }),
    searchParams: Promise.resolve(query),
  } as never);
  return render(page);
}

describe('sign-in page inline auth errors', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.auth.mockResolvedValue(null);
  });

  afterEach(cleanup);

  it('renders the inline error notice when ?error= is present', async () => {
    await renderPage({ error: 'access_denied' });

    expect(screen.getByTestId('auth-error-notice')).toHaveTextContent('access_denied');
    expect(screen.getByTestId('sign-in-form')).toBeInTheDocument();
  });

  it('renders no notice without an error', async () => {
    await renderPage({});

    expect(screen.queryByTestId('auth-error-notice')).not.toBeInTheDocument();
    expect(screen.getByTestId('sign-in-form')).toBeInTheDocument();
  });

  it('mounts the SIWE wallet sign-in buttons alongside the other providers', async () => {
    await renderPage({});

    expect(screen.getByTestId('wallet-sign-in-buttons')).toBeInTheDocument();
    expect(screen.getByTestId('google-sign-in-button')).toBeInTheDocument();
    expect(screen.getByTestId('discord-sign-in-button')).toBeInTheDocument();
  });
});
