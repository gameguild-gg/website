/** @vitest-environment happy-dom */
import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useAuth } from '../../src/integrations/react/use-auth.js';
import { MfaRequiredError } from '../../src/runtime/auth/errors.js';

afterEach(() => vi.unstubAllGlobals());
const token = 'x'.repeat(43);
const json = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status });

describe('browser MFA state', () => {
  it('persists the challenge in sessionStorage and hydrates it in a fresh hook', async () => {
    const backend = vi
      .fn()
      .mockResolvedValueOnce(json({ csrfToken: 'first-csrf' }))
      .mockResolvedValueOnce(json({ error: 'MfaRequired', mfaToken: token, availableMethods: ['TOTP'] }, 403));
    vi.stubGlobal('fetch', backend);
    const { result, unmount } = renderHook(() => useAuth());
    await act(async () => {
      await expect(result.current.signIn('credentials', { email: 'a@example.com', password: 'pw', redirect: false })).rejects.toBeInstanceOf(MfaRequiredError);
    });
    expect(result.current.mfaChallenge).toEqual({ mfaToken: token, availableMethods: ['TOTP'] });
    expect(sessionStorage.getItem('gg.auth.mfaChallenge')).toBe(JSON.stringify({ mfaToken: token, availableMethods: ['TOTP'] }));
    expect(localStorage.getItem('gg.auth.mfaChallenge')).toBeNull();
    unmount();
    const next = renderHook(() => useAuth());
    expect(next.result.current.mfaChallenge).toEqual({ mfaToken: token, availableMethods: ['TOTP'] });
    act(() => next.result.current.clearMfa());
    expect(next.result.current.mfaChallenge).toBeNull();
    expect(sessionStorage.getItem('gg.auth.mfaChallenge')).toBeNull();
    next.unmount();
  });

  it('clears persisted challenge after a successful MFA sign-in', async () => {
    sessionStorage.setItem('gg.auth.mfaChallenge', JSON.stringify({ mfaToken: token, availableMethods: ['TOTP'] }));
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(json({ csrfToken: 'csrf' }))
        .mockResolvedValueOnce(json({ user: { id: 'verified' } })),
    );
    const location = { href: '/before-confirmation' };
    vi.stubGlobal('window', { location, sessionStorage: globalThis.sessionStorage });
    const { result, unmount } = renderHook(() => useAuth());
    expect(result.current.mfaChallenge).toEqual({ mfaToken: token, availableMethods: ['TOTP'] });
    await act(async () => {
      await result.current.signIn('credentials', { mfaToken: token, method: 'Totp', code: '123456', redirectTo: '/dashboard' });
    });
    expect(result.current.mfaChallenge).toBeNull();
    expect(sessionStorage.getItem('gg.auth.mfaChallenge')).toBeNull();
    expect(location.href).toBe('/dashboard');
    unmount();
  });

  it('ignores malformed persisted challenges', () => {
    sessionStorage.setItem('gg.auth.mfaChallenge', '{not json');
    const { result, unmount } = renderHook(() => useAuth());
    expect(result.current.mfaChallenge).toBeNull();
    unmount();
  });

  it('redirects immediately after a verified challenge', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(json({ csrfToken: 'csrf' }))
        .mockResolvedValueOnce(json({ user: { id: 'verified' } })),
    );
    const location = { href: '/before-confirmation' };
    vi.stubGlobal('window', { location });
    const { result, unmount } = renderHook(() => useAuth());
    await act(async () => {
      await result.current.signIn('credentials', { mfaToken: token, method: 'Totp', code: '123456', redirectTo: '/dashboard' });
    });
    expect(result.current.mfaChallenge).toBeNull();
    expect(location.href).toBe('/dashboard');
    unmount();
  });
});
