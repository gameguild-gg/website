import { describe, expect, it } from 'vitest';
import { resolveConfig } from '../../src/integrations/next/config.js';
import { createHandlers, parseCookieHeader } from '../../src/integrations/next/handlers.js';

const config = resolveConfig({
  providers: [],
  secret: 'test-only-csrf-cookie-regression-secret',
  apiUrl: 'http://unused.invalid',
  cookies: { name: '__me_web', secure: false },
});

describe('malformed cookies do not break authentication', () => {
  it.each(['preference=100%', 'preference=%E0%A4%A', 'invalid%name=value'])('ignores %s while preserving valid cookies', (malformed) => {
    const cookies = parseCookieHeader(`before=hello%20world; ${malformed}; __me_web.csrf-token=valid%7Ctoken`);
    expect([...cookies]).toEqual([
      ['before', 'hello world'],
      ['__me_web.csrf-token', 'valid|token'],
    ]);
  });

  it('issues a usable CSRF token despite an unrelated malformed cookie', async () => {
    const { GET, POST } = createHandlers(config);
    const response = await GET(
      new Request('http://localhost/api/auth/csrf', {
        headers: { cookie: 'preference=100%' },
      }),
    );
    expect(response.status).toBe(200);
    expect(response.headers.get('cache-control')).toContain('no-store');
    const { csrfToken } = await response.json();
    const csrfCookie = response.headers.get('set-cookie')!.split(';')[0];

    // Exercise the real token generator and validator, without a backend call.
    const signOut = await POST(
      new Request('http://localhost/api/auth/signout', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', cookie: `preference=100%; ${csrfCookie}` },
        body: JSON.stringify({ csrfToken }),
      }),
    );
    expect(signOut.status).toBe(200);
    expect(await signOut.json()).toEqual({ ok: true });
  });

  it.each(['preference=100%', '__me_web.csrf-token=invalid%'])('still rejects mutations without valid CSRF when %s is present', async (cookie) => {
    const { POST } = createHandlers(config);
    const response = await POST(
      new Request('http://localhost/api/auth/signout', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', cookie },
        body: JSON.stringify({ csrfToken: 'forged-token' }),
      }),
    );
    expect(response.status).toBe(403);
    expect(response.headers.get('set-cookie')).toBeNull();
  });
});
