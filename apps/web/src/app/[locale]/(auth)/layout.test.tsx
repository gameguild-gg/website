import '@testing-library/jest-dom/vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  auth: vi.fn(),
  headers: vi.fn(),
  redirect: vi.fn((href: string) => {
    throw new Error(`redirect:${href}`);
  }),
}));

vi.mock('@/auth', () => ({ auth: mocks.auth }));
vi.mock('next/headers', () => ({ headers: mocks.headers }));
vi.mock('next/navigation', () => ({ redirect: mocks.redirect }));
vi.mock('@/components/brand/game-guild-logo', () => ({
  GameGuildLogo: () => <div data-testid="game-guild-logo" />,
}));

import Layout from './layout';

function gameguildPath(value: string | null) {
  mocks.headers.mockResolvedValue(
    new Headers(value ? { 'x-gameguild-path': value } : {}),
  );
}

describe('(auth) layout authenticated redirect (issue #864)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.auth.mockResolvedValue(null);
    gameguildPath(null);
  });

  afterEach(cleanup);

  it('renders children for unauthenticated visitors', async () => {
    const ui = await Layout({
      children: <div data-testid="auth-page">form</div>,
      params: Promise.resolve({ locale: 'en-US' }),
    } as never);
    render(ui);

    expect(screen.getByTestId('auth-page')).toBeInTheDocument();
    expect(mocks.redirect).not.toHaveBeenCalled();
  });

  it('redirects authenticated users to the allowlisted original target', async () => {
    mocks.auth.mockResolvedValue({ user: { id: 'u1' } });
    gameguildPath('/workspace/settings/security');

    await expect(
      Layout({
        children: <div />,
        params: Promise.resolve({ locale: 'en-US' }),
      } as never),
    ).rejects.toThrow('redirect:/en-US/workspace/settings/security');
  });

  it('strips the query string before redirecting', async () => {
    mocks.auth.mockResolvedValue({ user: { id: 'u1' } });
    gameguildPath('/workspace?tab=overview&x=1');

    await expect(
      Layout({
        children: <div />,
        params: Promise.resolve({ locale: 'en-US' }),
      } as never),
    ).rejects.toThrow('redirect:/en-US/workspace');
  });

  it('falls back to the locale root when no path was forwarded', async () => {
    mocks.auth.mockResolvedValue({ user: { id: 'u1' } });
    gameguildPath(null);

    await expect(
      Layout({
        children: <div />,
        params: Promise.resolve({ locale: 'en-US' }),
      } as never),
    ).rejects.toThrow('redirect:/en-US/feed');
  });

  it('rejects non-relative forwarded paths and falls back safely', async () => {
    mocks.auth.mockResolvedValue({ user: { id: 'u1' } });
    gameguildPath('//evil.example/phishing');

    await expect(
      Layout({
        children: <div />,
        params: Promise.resolve({ locale: 'en-US' }),
      } as never),
    ).rejects.toThrow('redirect:/en-US/feed');
  });
});
