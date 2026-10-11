/** @vitest-environment happy-dom */
import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useAuth } from '../../src/integrations/react/use-auth.js';
import { MfaRequiredError } from '../../src/runtime/auth/errors.js';

afterEach(() => vi.unstubAllGlobals());
const token = 'x'.repeat(43);
const json = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status });

describe('browser MFA state', () => {
  it('retains only the limited challenge and uses fresh CSRF for enrollment', async () => {
    const setup = {
      success: true,
      secretKey: 'SYNTHETIC',
      qrCodeUri: 'otpauth://totp/Example?secret=SYNTHETIC',
      expiresAt: new Date(Date.now() + 300000).toISOString(),
    };
    const backend = vi
      .fn()
      .mockResolvedValueOnce(json({ csrfToken: 'first-csrf' }))
      .mockResolvedValueOnce(json({ error: 'MfaRequired', mfaToken: token, availableMethods: ['TOTP'] }, 403))
      .mockResolvedValueOnce(json({ csrfToken: 'setup-csrf' }))
      .mockResolvedValueOnce(json(setup));
    vi.stubGlobal('fetch', backend);
    const { result, unmount } = renderHook(() => useAuth());
    await act(async () => {
      await expect(result.current.signIn('credentials', { email: 'a@example.com', password: 'pw', redirect: false })).rejects.toBeInstanceOf(MfaRequiredError);
    });
    expect(result.current.mfaChallenge).toEqual({ mfaToken: token, availableMethods: ['TOTP'] });
    await act(async () => {
      expect(await result.current.startMfaEnrollment()).toEqual(setup);
    });
    expect(JSON.parse(backend.mock.calls[3][1].body)).toEqual({ mfaToken: token, csrfToken: 'setup-csrf' });
    expect(backend.mock.calls[3][0]).toBe('/api/auth/mfa/enrollment');
    expect(sessionStorage.getItem('mfaToken')).toBeNull();
    expect(localStorage.getItem('mfaToken')).toBeNull();
    act(() => result.current.clearMfa());
    expect(result.current.mfaChallenge).toBeNull();
    unmount();
    const next = renderHook(() => useAuth());
    expect(next.result.current.mfaChallenge).toBeNull();
    next.unmount();
  });

  it('keeps recovery codes only until acknowledgment and suppresses immediate redirect', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(json({ csrfToken: 'csrf' }))
        .mockResolvedValueOnce(json({ user: { id: 'verified' }, mfaEnrollmentBackupCodes: ['one-time-code'] })),
    );
    const location = { href: '/before-confirmation' };
    vi.stubGlobal('window', { location });
    const { result, unmount } = renderHook(() => useAuth());
    await act(async () => {
      await result.current.signIn('credentials', { mfaToken: token, method: 'Totp', code: '123456', redirectTo: '/dashboard' });
    });
    expect(result.current.mfaEnrollmentBackupCodes).toEqual(['one-time-code']);
    expect(location.href).toBe('/before-confirmation');
    act(() => result.current.clearMfa());
    expect(result.current.mfaEnrollmentBackupCodes).toBeNull();
    unmount();
  });

  it('denies setup without a first-factor challenge', async () => {
    const backend = vi.fn();
    vi.stubGlobal('fetch', backend);
    const { result, unmount } = renderHook(() => useAuth());
    await expect(result.current.startMfaEnrollment()).rejects.toThrow('Sign in again');
    expect(backend).not.toHaveBeenCalled();
    unmount();
  });
});
