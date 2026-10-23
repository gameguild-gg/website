/** @vitest-environment happy-dom */
import { afterEach, describe, expect, it, vi } from 'vitest';
import React from 'react';
import { act, render, renderHook, waitFor } from '@testing-library/react';
import { useAuth, type UseAuthReturn } from '../../src/integrations/react/use-auth.js';
import { MfaRequiredError } from '../../src/runtime/auth/errors.js';
import { SessionProvider } from '../../src/integrations/react/session-provider.js';

vi.mock('../../src/integrations/react/broadcast.js', () => ({
  createAuthBroadcast: () => ({ send: vi.fn(), close: vi.fn() }),
}));

afterEach(() => vi.unstubAllGlobals());
const token = 'x'.repeat(43);
const json = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status });

describe('browser MFA state', () => {
  it('retains only the limited challenge and never stores it in web storage', async () => {
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
    expect(sessionStorage.getItem('mfaToken')).toBeNull();
    expect(localStorage.getItem('mfaToken')).toBeNull();
    act(() => result.current.clearMfa());
    expect(result.current.mfaChallenge).toBeNull();
    unmount();
    const next = renderHook(() => useAuth());
    expect(next.result.current.mfaChallenge).toBeNull();
    next.unmount();
  });

  it('keeps the challenge when the useAuth consumer changes inside SessionProvider', async () => {
    const backend = vi
      .fn()
      .mockResolvedValueOnce(json({ csrfToken: 'first-csrf' }))
      .mockResolvedValueOnce(json({ error: 'MfaRequired', mfaToken: token, availableMethods: ['TOTP'] }, 403));
    vi.stubGlobal('fetch', backend);

    let auth: UseAuthReturn | undefined;
    const capture = (next: UseAuthReturn) => {
      auth = next;
    };

    function Consumer({ onAuth }: { onAuth: (value: UseAuthReturn) => void }) {
      const value = useAuth();
      React.useEffect(() => onAuth(value), [onAuth, value]);
      return null;
    }

    const session = {
      user: { id: 'user-1', email: 'a@example.com', name: 'A' },
      expires: new Date(Date.now() + 60_000).toISOString(),
    };
    const view = render(
      <SessionProvider session={session}>
        <Consumer key="sign-in" onAuth={capture} />
      </SessionProvider>,
    );

    await waitFor(() => expect(auth).toBeDefined());
    await act(async () => {
      await expect(auth!.signIn('credentials', { email: 'a@example.com', password: 'pw', redirect: false })).rejects.toBeInstanceOf(MfaRequiredError);
    });
    expect(auth!.mfaChallenge).toEqual({ mfaToken: token, availableMethods: ['TOTP'] });

    auth = undefined;
    view.rerender(
      <SessionProvider session={session}>
        <Consumer key="mfa-route" onAuth={capture} />
      </SessionProvider>,
    );

    await waitFor(() => expect(auth?.mfaChallenge).toEqual({ mfaToken: token, availableMethods: ['TOTP'] }));
    expect(sessionStorage.getItem('mfaToken')).toBeNull();
    expect(localStorage.getItem('mfaToken')).toBeNull();
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
