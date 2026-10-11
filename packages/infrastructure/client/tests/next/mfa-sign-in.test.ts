import { afterEach, describe, expect, it, vi } from 'vitest';
import { createHandlers } from '../../src/integrations/next/handlers.js';
import { CredentialsProvider } from '../../src/runtime/auth/providers/credentials.js';
import { decodeJWT } from '../../src/runtime/auth/jwt.js';
import type { ResolvedAuthConfig } from '../../src/runtime/auth/types.js';

vi.mock('../../src/runtime/auth/csrf.js', () => ({
  createCSRFToken: vi.fn(),
  validateCSRFToken: vi.fn(async (cookie, token) => cookie === 'csrf-cookie' && token === 'csrf-token'),
}));

const mfaToken = 'x'.repeat(43);
const config: ResolvedAuthConfig = {
  providers: [CredentialsProvider()],
  apiUrl: 'http://api.example',
  callbacks: {
    jwt: async ({ token }) => token,
    session: async ({ session }) => session,
    signIn: async () => true,
    authorized: async ({ auth }) => !!auth,
    redirect: async ({ url, baseUrl }) => new URL(url, baseUrl).href,
  },
  secret: 'a-synthetic-local-test-secret-with-32-characters',
  pages: {},
  cookies: { name: '__me', secure: false, sameSite: 'lax', path: '/', maxAge: 2592000, httpOnly: true },
  maxAge: 2592000,
  updateAge: 0,
  basePath: '/api/auth',
  debug: true,
  trustHost: false,
  tenantHeader: 'X-Tenant-Id',
};
const originalFetch = globalThis.fetch;
afterEach(() => {
  globalThis.fetch = originalFetch;
  vi.restoreAllMocks();
});
function request(path: string, body: Record<string, unknown>, csrf = true) {
  return new Request(`http://localhost/api/auth/${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...(csrf ? { Cookie: '__me.csrf-token=csrf-cookie' } : {}) },
    body: JSON.stringify({ ...body, ...(csrf ? { csrfToken: 'csrf-token' } : {}) }),
  });
}
const setup = {
  success: true,
  secretKey: 'SYNTHETIC-SETUP',
  qrCodeUri: 'otpauth://totp/Example?secret=SYNTHETIC',
  expiresAt: new Date(Date.now() + 300000).toISOString(),
};

describe('native limited MFA through Next handlers', () => {
  it('rejects enrollment without valid CSRF before calling the API', async () => {
    globalThis.fetch = vi.fn();
    const response = await createHandlers(config).POST(request('mfa/enrollment', { mfaToken }, false));
    expect(response.status).toBe(403);
    expect(globalThis.fetch).not.toHaveBeenCalled();
    expect(response.headers.get('set-cookie')).toBeNull();
  });

  it('returns only provisioning data, uses only the limited bearer, and sets no session', async () => {
    const backend = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ...setup, accessToken: 'must-not-forward', userId: 'must-not-forward' })));
    globalThis.fetch = backend;
    const response = await createHandlers(config).POST(request('mfa/enrollment', { mfaToken, tenantId: 'untrusted', password: 'must-not-forward' }));
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual(setup);
    expect(backend.mock.calls[0][0]).toBe('http://api.example/v1/auth/mfa/sign-in/enrollment');
    expect(JSON.parse(backend.mock.calls[0][1].body)).toEqual({ mfaToken });
    expect(response.headers.get('set-cookie')).toBeNull();
    expect(response.headers.get('cache-control')).toContain('no-store');
  });

  it('rejects a malformed enrollment bearer locally', async () => {
    globalThis.fetch = vi.fn();
    const response = await createHandlers(config).POST(request('mfa/enrollment', { mfaToken: 'bad' }));
    expect(response.status).toBe(400);
    expect(globalThis.fetch).not.toHaveBeenCalled();
  });

  it('returns the first-factor challenge without a session cookie or logging the bearer', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ requiresMfa: true, mfaToken, availableMethods: ['TOTP'] })));
    const log = vi.spyOn(console, 'error').mockImplementation(() => {});
    const response = await createHandlers(config).POST(request('signin/credentials', { email: 'a@example.com', password: 'pw', redirect: false }));
    expect(response.status).toBe(403);
    expect(await response.json()).toMatchObject({ error: 'MfaRequired', mfaToken, availableMethods: ['TOTP'] });
    expect(response.headers.get('set-cookie')).toBeNull();
    expect(JSON.stringify(log.mock.calls)).not.toContain(mfaToken);
  });

  it('persists the verified session but never its one-time recovery codes or limited bearer', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          accessToken: 'verified-access',
          refreshToken: 'verified-refresh',
          expiresIn: 3600,
          userId: 'verified-user',
          email: 'server@example.com',
          mfaEnrollmentBackupCodes: ['one-time-code'],
        }),
      ),
    );
    const response = await createHandlers(config).POST(request('signin/credentials', { mfaToken, method: 'Totp', code: '123456', redirect: false }));
    expect(response.status).toBe(200);
    const body = await response.json();
    expect(body.mfaEnrollmentBackupCodes).toEqual(['one-time-code']);
    expect(body).not.toHaveProperty('mfaToken');
    const cookie = response.headers.get('set-cookie')!;
    const sessionToken = decodeURIComponent(cookie.match(/__me\.session-token=([^;]+)/)![1]);
    const jwt = await decodeJWT({ token: sessionToken, secret: config.secret });
    expect(jwt?.accessToken).toBe('verified-access');
    expect(jwt).not.toHaveProperty('mfaEnrollmentBackupCodes');
    expect(JSON.stringify(jwt)).not.toContain('one-time-code');
    expect(JSON.stringify(jwt)).not.toContain(mfaToken);
  });

  it('sets no session on denied verification', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ message: 'Invalid authentication code' }), { status: 401 }));
    const response = await createHandlers(config).POST(request('signin/credentials', { mfaToken, method: 'Totp', code: '123456', redirect: false }));
    expect(response.status).toBe(401);
    expect(response.headers.get('set-cookie')).toBeNull();
  });
});
