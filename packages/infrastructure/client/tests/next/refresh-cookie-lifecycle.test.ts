// @vitest-environment node
import { afterEach, describe, expect, it, vi } from 'vitest';
import { NextResponse } from 'next/server';
import { createHandlers, parseCookieHeader } from '../../src/integrations/next/handlers.js';
import { createProxy } from '../../src/integrations/next/proxy.js';
import { createAuthFunction } from '../../src/integrations/next/actions.js';
import { encodeSession } from '../../src/runtime/auth/session.js';
import { decodeJWT } from '../../src/runtime/auth/jwt.js';
import { resolveCookieOptions, SessionStore } from '../../src/runtime/auth/cookies.js';
import type { JWTPayload, ResolvedAuthConfig } from '../../src/runtime/auth/types.js';

function config(): ResolvedAuthConfig {
  return {
    providers: [],
    callbacks: {
      jwt: async ({ token }) => token,
      session: async ({ session }) => session,
      signIn: async () => true,
      redirect: async ({ url }) => url,
      authorized: async ({ auth }) => !!auth,
    },
    secret: 'issue-263-synthetic-cookie-secret-at-least-32-characters',
    apiUrl: 'http://localhost:8080', pages: {},
    cookies: { name: 'issue263', secure: false, httpOnly: true, sameSite: 'lax', path: '/', maxAge: 86400 },
    maxAge: 86400, updateAge: 0, basePath: '/api/auth', debug: false, trustHost: false, tenantHeader: 'X-Tenant-Id',
  };
}

async function request(settings: ResolvedAuthConfig, path = '/dashboard', extra?: Record<string, unknown>) {
  const token: JWTPayload = {
    user: { id: 'synthetic-owner', email: 'owner@example.test', name: 'Synthetic owner', image: null },
    accessToken: 'synthetic-access', refreshToken: 'synthetic-refresh-' + crypto.randomUUID(),
    accessTokenExpires: Date.now() + 15_000, ...extra,
  };
  const encrypted = await encodeSession(token, settings);
  const cookies: string[] = [];
  new SessionStore(resolveCookieOptions(settings.cookies)).write(encrypted, (name, value, options) => {
    if (options.maxAge !== 0) cookies.push(`${name}=${encodeURIComponent(value)}`);
  });
  return new Request('http://localhost:3000' + path, { headers: { cookie: cookies.join('; ') } });
}

function expectDeleted(response: Response) {
  const cookies = response.headers.getSetCookie();
  for (const name of ['issue263.session-token', 'issue263.session-token.1']) {
    const cookie = cookies.find((value) => value.startsWith(`${name}=;`));
    expect(cookie).toBeDefined();
    expect(cookie).toContain('Max-Age=0');
    expect(cookie).toContain('Path=/');
    expect(cookie).toContain('HttpOnly');
  }
  expect(response.headers.get('set-cookie')).not.toContain('synthetic-refresh');
}

afterEach(() => vi.restoreAllMocks());

describe('real encrypted refresh-cookie lifecycle', () => {
  it.each([401, 403])('deletes denied %i session cookies on the session endpoint', async (status) => {
    const settings = config();
    const fetch = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('{}', { status }));
    const response = await createHandlers(settings).GET(await request(settings, '/api/auth/session'));
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({});
    expectDeleted(response);
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it.each([401, 403])('deletes denied %i cookies on the proxy redirect', async (status) => {
    const settings = config();
    const fetch = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('{}', { status }));
    const response = await createProxy(settings)()(await request(settings));
    expect(response.status).toBe(302);
    expect(response.headers.get('location')).toContain('/sign-in');
    expectDeleted(response);
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it('persists the real encrypted replacement pair on a successful proxy refresh', async () => {
    const settings = config();
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(Response.json({
      accessToken: 'replacement-access', refreshToken: 'replacement-refresh', expiresIn: 3600,
    }));
    const response = await createProxy(settings)((incoming) => {
      expect(incoming.auth?.user.id).toBe('synthetic-owner');
      return new Response('accepted', { headers: { 'x-preserved': 'yes' } });
    })(await request(settings));
    expect(response.headers.get('x-preserved')).toBe('yes');
    expect(await response.text()).toBe('accepted');
    const cookie = response.headers.getSetCookie().find((value) => value.startsWith('issue263.session-token='));
    expect(cookie).toBeDefined();
    const encrypted = parseCookieHeader(cookie!.split(';')[0]).get('issue263.session-token')!;
    const replacement = await decodeJWT({ token: encrypted, secret: settings.secret });
    expect(replacement?.accessToken).toBe('replacement-access');
    expect(replacement?.refreshToken).toBe('replacement-refresh');
  });

  it('preserves a still-valid session and cookie during temporary 503 failure', async () => {
    const settings = config();
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('{}', { status: 503 }));
    const response = await createProxy(settings)((incoming) => {
      expect(incoming.auth?.user.id).toBe('synthetic-owner');
      return new Response('temporary');
    })(await request(settings));
    expect(response.status).toBe(200);
    expect(response.headers.getSetCookie()).toEqual([]);
  });

  it('deletes chunked cookies while retaining an immutable handler redirect', async () => {
    const settings = config();
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('{}', { status: 401 }));
    const incoming = await request(settings, '/dashboard', { extra: 'x'.repeat(5000) });
    expect(incoming.headers.get('cookie')).toContain('issue263.session-token.1=');
    const response = await createAuthFunction(settings)(() => Response.redirect('http://localhost:3000/sign-in'))(incoming);
    expect(response.status).toBe(302);
    expect(response.headers.get('location')).toBe('http://localhost:3000/sign-in');
    expectDeleted(response);
  });

  it('persists a replacement using actual NextResponse middleware cookie metadata', async () => {
    const settings = config();
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(Response.json({
      accessToken: 'replacement-access', refreshToken: 'replacement-refresh', expiresIn: 3600,
    }));
    const downstream = NextResponse.rewrite(new URL('http://localhost:3000/en-US/dashboard'));
    const response = await createAuthFunction(settings)(() => downstream)(await request(settings));
    expect(response).toBe(downstream);
    expect(response.headers.get('x-middleware-rewrite')).toBe('http://localhost:3000/en-US/dashboard');
    const encrypted = downstream.cookies.get('issue263.session-token')?.value;
    expect(encrypted).toBeDefined();
    expect(response.headers.get('x-middleware-set-cookie')).toContain(`issue263.session-token=${encrypted}`);
    const replacement = await decodeJWT({ token: encrypted!, secret: settings.secret });
    expect(replacement?.accessToken).toBe('replacement-access');
    expect(replacement?.refreshToken).toBe('replacement-refresh');
  });

  it('propagates denial deletion through actual NextResponse middleware cookie metadata', async () => {
    const settings = config();
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('{}', { status: 403 }));
    const downstream = NextResponse.rewrite(new URL('http://localhost:3000/en-US/dashboard'));
    const response = await createAuthFunction(settings)(() => downstream)(await request(settings));
    expect(response).toBe(downstream);
    expect(downstream.cookies.get('issue263.session-token')?.value).toBe('');
    expect(response.headers.get('x-middleware-set-cookie')).toContain('issue263.session-token=;');
    expectDeleted(response);
  });
});
