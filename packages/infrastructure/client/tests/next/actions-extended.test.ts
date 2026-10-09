/**
 * Extended Actions Tests — signIn success paths, signUp success, signOut, update, OAuth
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  createAuthFunction,
  createSignInAction,
  createSignUpAction,
  createSignOutAction,
  createUpdateAction,
  type CookieAdapter,
} from '../../src/integrations/next/actions.js';
import type { ResolvedAuthConfig } from '../../src/runtime/auth/types.js';

// Track what cookies() returns per-test
let mockCookieAdapter: CookieAdapter | null = null;

vi.mock('next/headers', () => ({
  cookies: vi.fn(async () => mockCookieAdapter),
}));

vi.mock('next/navigation', () => ({
  redirect: vi.fn((url: string) => {
    throw Object.assign(new Error(`REDIRECT:${url}`), { digest: 'NEXT_REDIRECT' });
  }),
}));

vi.mock('../../src/runtime/auth/jwt.js', () => ({
  decodeJWT: vi.fn(async ({ token }: any) => {
    if (token === 'encrypted-session') {
      return {
        user: { id: '1', email: 'test@example.com', name: 'Test', image: null },
        accessToken: 'access-token',
        refreshToken: 'refresh-token',
        tenantId: 'tenant-1',
        accessTokenExpires: Date.now() + 3600000,
        iat: Math.floor(Date.now() / 1000),
        exp: Math.floor(Date.now() / 1000) + 86400,
      };
    }
    return null;
  }),
  encodeJWT: vi.fn(async () => 'new-encrypted-token'),
}));

vi.mock('../../src/runtime/auth/session.js', () => ({
  createJWTPayload: vi.fn((result: any) => ({
    user: result.user,
    accessToken: result.tokens.accessToken,
    refreshToken: result.tokens.refreshToken,
    accessTokenExpires: Date.now() + 3600000,
    iat: Math.floor(Date.now() / 1000),
    exp: Math.floor(Date.now() / 1000) + 86400,
  })),
  processSession: vi.fn(async (token: string) => {
    if (token === 'encrypted-session') {
      return {
        session: {
          user: { id: '1', email: 'test@example.com', name: 'Test', image: null },
          expires: new Date(Date.now() + 86400000).toISOString(),
        },
        token: {
          user: { id: '1', email: 'test@example.com', name: 'Test', image: null },
          accessToken: 'access-token',
        },
        updated: false,
      };
    }
    if (token === 'updated-session') {
      return {
        session: {
          user: { id: '1', email: 'test@example.com', name: 'Test', image: null },
          expires: new Date(Date.now() + 86400000).toISOString(),
        },
        token: {
          user: { id: '1', email: 'test@example.com', name: 'Test', image: null },
          accessToken: 'refreshed-access-token',
        },
        updated: true,
      };
    }
    return { session: null, token: null, updated: false };
  }),
  encodeSession: vi.fn(async () => 'new-encrypted-token'),
  refreshAccessToken: vi.fn(async (token: any) => token),
  toSession: vi.fn((token: any) => ({
    user: token.user,
    expires: new Date(Date.now() + 86400000).toISOString(),
  })),
}));

vi.mock('../../src/integrations/next/handlers.js', async (importOriginal) => ({
  ...await importOriginal<typeof import('../../src/integrations/next/handlers.js')>(),
  parseCookieHeader: vi.fn((header: string) => {
    const map = new Map<string, string>();
    for (const pair of header.split(';')) {
      const [name, ...rest] = pair.trim().split('=');
      if (name) map.set(name.trim(), rest.join('=').trim());
    }
    return map;
  }),
  parseBackendAuthResponse: vi.fn((data: any, email?: string, name?: string) => ({
    tokens: { accessToken: data.accessToken || 'at', refreshToken: data.refreshToken || 'rt', tokenType: 'Bearer' },
    user: { id: data.userId || '1', email: email || '', name: name || null, image: null },
  })),
  serializeCookie: vi.fn(
    (name: string, value: string, options: Record<string, unknown>) => `${name}=${value}; Path=${options.path ?? '/'}; Max-Age=${options.maxAge ?? ''}`,
  ),
}));

function createMockAdapter(cookies: Record<string, string> = {}): CookieAdapter {
  const store = new Map(Object.entries(cookies));
  return {
    get: vi.fn((name: string) => {
      const val = store.get(name);
      return val !== undefined ? { value: val } : undefined;
    }),
    set: vi.fn((name: string, value: string) => {
      store.set(name, value);
    }),
    delete: vi.fn((name: string) => {
      store.delete(name);
    }),
  };
}

function makeConfig(overrides?: Partial<ResolvedAuthConfig>): ResolvedAuthConfig {
  return {
    providers: [
      {
        id: 'credentials',
        name: 'Credentials',
        type: 'credentials',
        authorize: vi.fn(async (creds: any) => ({
          tokens: { accessToken: 'at', refreshToken: 'rt', tokenType: 'Bearer' as const },
          user: { id: '1', email: creds.email || 'a@b.com', name: 'Test', image: null },
        })),
      } as any,
    ],
    callbacks: {
      jwt: async ({ token }) => token,
      session: async ({ session }) => session,
      signIn: async () => true,
      redirect: async ({ url, baseUrl }) => (url.startsWith('/') ? `${baseUrl}${url}` : url),
      authorized: async ({ auth }) => !!auth,
    },
    secret: 'test-secret-min-32-chars-long-ok',
    apiUrl: 'http://localhost:8080',
    pages: {},
    cookies: {
      name: '__me',
      secure: false,
      sameSite: 'lax',
      path: '/',
      maxAge: 2592000,
      httpOnly: true,
    },
    maxAge: 2592000,
    updateAge: 0,
    basePath: '/api/auth',
    debug: false,
    trustHost: false,
    tenantHeader: 'X-Tenant-Id',
    ...overrides,
  };
}

describe('createAuthFunction — extended', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockCookieAdapter = null;
  });

  it('should return session when adapter has valid token', async () => {
    const adapter = createMockAdapter({ '__me.session-token': 'encrypted-session' });
    mockCookieAdapter = adapter;

    const config = makeConfig();
    const auth = createAuthFunction(config);
    const session = await auth();

    expect(session).toBeDefined();
    expect(session?.user?.email).toBe('test@example.com');
  });

  it('should return null when no session cookie', async () => {
    mockCookieAdapter = createMockAdapter({});

    const config = makeConfig();
    const auth = createAuthFunction(config);
    const session = await auth();

    expect(session).toBeNull();
  });

  it('should update cookie when session is refreshed', async () => {
    const adapter = createMockAdapter({ '__me.session-token': 'updated-session' });
    mockCookieAdapter = adapter;

    const config = makeConfig();
    const auth = createAuthFunction(config);
    const session = await auth();

    expect(session).toBeDefined();
    expect(adapter.set).toHaveBeenCalled();
  });

  it('proxy wrapper should augment request with null auth when no cookie', async () => {
    const config = makeConfig();
    const auth = createAuthFunction(config);

    const handler = vi.fn(async (req: Request & { auth: any }) => {
      return new Response(JSON.stringify({ auth: req.auth }));
    });

    const wrapper = auth(handler);
    const request = new Request('http://localhost:3000/api/data');
    const response = await wrapper(request);

    expect(handler).toHaveBeenCalled();
    const call = handler.mock.calls[0][0];
    expect(call.auth).toBeNull();
  });

  it('proxy wrapper should persist a rotated session cookie on the response', async () => {
    const auth = createAuthFunction(makeConfig());
    const wrapper = auth(async () => new Response('ok'));
    const response = await wrapper(
      new Request('http://localhost:3000/dashboard', {
        headers: { cookie: '__me.session-token=updated-session' },
      }),
    );

    const setCookie = response.headers.get('set-cookie');
    expect(setCookie).toContain('__me.session-token=new-encrypted-token');
    expect(setCookie).toContain('Max-Age=2592000');
  });

  it('proxy wrapper should expire a session cookie that can no longer be authenticated', async () => {
    const auth = createAuthFunction(makeConfig());
    const wrapper = auth(async (request) => new Response(JSON.stringify({ authenticated: request.auth !== null })));
    const response = await wrapper(
      new Request('http://localhost:3000/dashboard', {
        headers: { cookie: '__me.session-token=invalid-session' },
      }),
    );

    expect(await response.json()).toEqual({ authenticated: false });
    const expiredCookie = response.headers.getSetCookie().find((cookie) => cookie.startsWith('__me.session-token=;'));
    expect(expiredCookie).toBeDefined();
    expect(expiredCookie).toContain('Path=/');
    expect(expiredCookie).toContain('Max-Age=0');
  });
});

describe('createSignInAction — extended', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockCookieAdapter = null;
  });

  it('should sign in with credentials and set cookie (no redirect)', async () => {
    const adapter = createMockAdapter();
    mockCookieAdapter = adapter;

    const config = makeConfig();
    const signIn = createSignInAction(config);

    await signIn('credentials', {
      email: 'user@example.com',
      password: 'pwd',
      redirect: false,
    });

    // Should have written session cookie
    expect(adapter.set).toHaveBeenCalled();
  });

  it('should redirect after sign-in when redirectTo is set', async () => {
    const adapter = createMockAdapter();
    mockCookieAdapter = adapter;

    const config = makeConfig();
    const signIn = createSignInAction(config);

    await expect(
      signIn('credentials', {
        email: 'user@example.com',
        password: 'pwd',
        redirectTo: '/dashboard',
        redirect: true,
      }),
    ).rejects.toThrow('REDIRECT');
  });

  it('should handle OAuth provider with exchangeToken', async () => {
    const adapter = createMockAdapter();
    mockCookieAdapter = adapter;

    const config = makeConfig({
      providers: [
        {
          id: 'google',
          name: 'Google',
          type: 'oauth',
          exchangeToken: vi.fn(async () => ({
            tokens: { accessToken: 'at', refreshToken: 'rt', tokenType: 'Bearer' as const },
            user: { id: '1', email: 'g@g.com', name: 'Google User', image: null },
          })),
        } as any,
      ],
    });
    const signIn = createSignInAction(config);

    await signIn('google', { idToken: 'google-id-token', redirect: false });

    expect(adapter.set).toHaveBeenCalled();
  });

  it('should return null result when OAuth has no exchangeToken', async () => {
    const adapter = createMockAdapter();
    mockCookieAdapter = adapter;

    const config = makeConfig({
      providers: [
        {
          id: 'github',
          name: 'GitHub',
          type: 'oauth',
        } as any,
      ],
    });
    const signIn = createSignInAction(config);

    await expect(signIn('github', { redirect: false })).rejects.toThrow();
  });
});

describe('createSignUpAction — extended', () => {
  let fetchSpy: any;

  beforeEach(() => {
    vi.clearAllMocks();
    mockCookieAdapter = createMockAdapter();
  });

  afterEach(() => {
    if (fetchSpy) fetchSpy.mockRestore();
  });

  it('should sign up successfully and set session cookie', async () => {
    fetchSpy = vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          accessToken: 'at',
          refreshToken: 'rt',
          userId: '1',
        }),
        { status: 200 },
      ),
    );

    const config = makeConfig();
    const signUp = createSignUpAction(config);

    await expect(signUp({ username: 'testuser', email: 'test@example.com', password: 'Password1!' }, { redirect: false })).resolves.not.toThrow();
  });

  it('should send optional fields in sign-up body', async () => {
    fetchSpy = vi.spyOn(global, 'fetch').mockResolvedValueOnce(new Response(JSON.stringify({ accessToken: 'at', refreshToken: 'rt' }), { status: 200 }));

    const config = makeConfig();
    const signUp = createSignUpAction(config);

    await signUp(
      {
        username: 'testuser',
        email: 'test@example.com',
        password: 'Password1!',
        firstName: 'Test',
        lastName: 'User',
        tenantId: 'tenant-1',
      },
      { redirect: false },
    );

    const fetchCall = (global.fetch as any).mock.calls[0];
    const sentBody = JSON.parse(fetchCall[1].body);
    expect(sentBody.firstName).toBe('Test');
    expect(sentBody.lastName).toBe('User');
    expect(sentBody.tenantId).toBe('tenant-1');
  });

  it('should redirect after successful signup by default', async () => {
    fetchSpy = vi.spyOn(global, 'fetch').mockResolvedValueOnce(new Response(JSON.stringify({ accessToken: 'at', refreshToken: 'rt' }), { status: 200 }));

    const config = makeConfig();
    const signUp = createSignUpAction(config);

    await expect(signUp({ username: 'testuser', email: 'test@example.com', password: 'Password1!' })).rejects.toThrow('REDIRECT');
  });

  it('should throw with field errors for sign-up API error', async () => {
    fetchSpy = vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          message: 'Validation failed',
          errors: { email: ['Already in use'] },
        }),
        { status: 400 },
      ),
    );

    const config = makeConfig();
    const signUp = createSignUpAction(config);

    await expect(signUp({ username: 'testuser', email: 'taken@example.com', password: 'pw' }, { redirect: false })).rejects.toThrow('Validation failed');
  });
});

describe('createSignOutAction — extended', () => {
  let fetchSpy: any;

  beforeEach(() => {
    vi.clearAllMocks();
    mockCookieAdapter = null;
  });

  afterEach(() => {
    if (fetchSpy) fetchSpy.mockRestore();
  });

  it('should sign out and delete cookies', async () => {
    const adapter = createMockAdapter({ '__me.session-token': 'encrypted-session' });
    mockCookieAdapter = adapter;

    fetchSpy = vi.spyOn(global, 'fetch').mockResolvedValueOnce(new Response('{}', { status: 200 }));

    const config = makeConfig();
    const signOut = createSignOutAction(config);

    await expect(signOut({ redirect: false })).resolves.not.toThrow();
    // Cookie should have been set to empty (deletion)
    expect(adapter.set).toHaveBeenCalled();
    expect(fetchSpy).toHaveBeenCalledWith(
      expect.stringContaining('tokens:revoke'),
      expect.objectContaining({
        body: JSON.stringify({ token: 'refresh-token' }),
        headers: { 'Content-Type': 'application/json' },
        method: 'POST',
      }),
    );
  });

  it('should redirect after signout by default', async () => {
    mockCookieAdapter = createMockAdapter({ '__me.session-token': 'encrypted-session' });
    fetchSpy = vi.spyOn(global, 'fetch').mockResolvedValueOnce(new Response('{}', { status: 200 }));

    const config = makeConfig();
    const signOut = createSignOutAction(config);

    await expect(signOut()).rejects.toThrow('REDIRECT');
  });

  it('should handle signout without session gracefully', async () => {
    mockCookieAdapter = createMockAdapter({});

    const config = makeConfig();
    const signOut = createSignOutAction(config);

    await expect(signOut({ redirect: false })).resolves.not.toThrow();
  });

  it('should continue even if token revocation fails', async () => {
    mockCookieAdapter = createMockAdapter({ '__me.session-token': 'encrypted-session' });
    fetchSpy = vi.spyOn(global, 'fetch').mockRejectedValueOnce(new Error('network'));

    const config = makeConfig();
    const signOut = createSignOutAction(config);

    await expect(signOut({ redirect: false })).resolves.not.toThrow();
  });

  it('should handle signout when getNextCookies returns null', async () => {
    mockCookieAdapter = null;

    const config = makeConfig();
    const signOut = createSignOutAction(config);

    // No redirect=false, but getNextCookies is null so redirect will try
    // but since adapter is null, it should redirect by default
    await expect(signOut({ redirect: false })).resolves.not.toThrow();
  });
});

describe('createUpdateAction — extended', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockCookieAdapter = null;
  });

  it('should return null when getNextCookies returns null', async () => {
    mockCookieAdapter = null;

    const config = makeConfig();
    const update = createUpdateAction(config);
    const result = await update({ user: { name: 'New Name' } } as any);

    expect(result).toBeNull();
  });

  it('should return null when no session cookie', async () => {
    mockCookieAdapter = createMockAdapter({});

    const config = makeConfig();
    const update = createUpdateAction(config);
    const result = await update();

    expect(result).toBeNull();
  });

  it('should return null when decodeJWT returns null', async () => {
    mockCookieAdapter = createMockAdapter({ '__me.session-token': 'invalid-token' });

    const config = makeConfig();
    const update = createUpdateAction(config);
    const result = await update();

    expect(result).toBeNull();
  });

  it('should update session and write new cookie', async () => {
    const adapter = createMockAdapter({ '__me.session-token': 'encrypted-session' });
    mockCookieAdapter = adapter;

    const config = makeConfig();
    const update = createUpdateAction(config);
    const result = await update({ user: { name: 'Updated' } } as any);

    expect(result).toBeDefined();
    expect(result?.user).toBeDefined();
    expect(adapter.set).toHaveBeenCalled();
  });

  it.each([
    ['tenant-2', 'tenant-2'],
    [null, null],
  ])('should refresh the access token when tenant changes to %s', async (tenantId, expectedTenantId) => {
    const adapter = createMockAdapter({ '__me.session-token': 'encrypted-session' });
    mockCookieAdapter = adapter;
    const config = makeConfig();
    const { refreshAccessToken } = await import('../../src/runtime/auth/session.js');

    await createUpdateAction(config)({ tenantId } as any);

    expect(refreshAccessToken).toHaveBeenCalledWith(expect.objectContaining({ tenantId: expectedTenantId }), config);
  });

  it('should not refresh the access token when tenant is unchanged', async () => {
    const adapter = createMockAdapter({ '__me.session-token': 'encrypted-session' });
    mockCookieAdapter = adapter;
    const config = makeConfig();
    const { refreshAccessToken } = await import('../../src/runtime/auth/session.js');

    await createUpdateAction(config)({ tenantId: 'tenant-1' } as any);

    expect(refreshAccessToken).not.toHaveBeenCalled();
  });
});
