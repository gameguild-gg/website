/**
 * Tests for useAuth Hook
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// Mock React hooks
vi.mock('react', () => ({
  useState: vi.fn((initial: unknown) => {
    let state = initial;
    const setState = vi.fn((newState: unknown) => {
      if (typeof newState === 'function') {
        const update = newState as (current: unknown) => unknown;
        state = update(state);
      } else {
        state = newState;
      }
    });
    return [state, setState];
  }),
  useCallback: vi.fn((fn: unknown) => fn),
  useEffect: vi.fn((effect: () => void) => effect()),
  useRef: vi.fn((initial: unknown) => ({ current: initial })),
  useContext: vi.fn(() => null),
}));

// Mock session-provider to avoid circular deps
vi.mock('../../src/integrations/react/session-provider.js', () => ({
  SessionContext: {},
}));

import { useAuth } from '../../src/integrations/react/use-auth.js';

describe('useAuth', () => {
  let mockFetch = vi.fn<typeof globalThis.fetch>();

  beforeEach(() => {
    mockFetch = vi.fn<typeof globalThis.fetch>();
    vi.stubGlobal('fetch', mockFetch);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('should return auth functions and state', () => {
    const result = useAuth();

    expect(result.signIn).toBeDefined();
    expect(result.signUp).toBeDefined();
    expect(result.signOut).toBeDefined();
    expect(typeof result.signIn).toBe('function');
    expect(typeof result.signUp).toBe('function');
    expect(typeof result.signOut).toBe('function');
    expect(result.isLoading).toBe(false);
    expect(result.error).toBeNull();
    expect(typeof result.clearError).toBe('function');
  });

  it('should accept custom basePath', () => {
    const result = useAuth({ basePath: '/custom/auth' });
    expect(result).toBeDefined();
  });

  it('signIn should fetch CSRF and then sign in', async () => {
    mockFetch
      .mockResolvedValueOnce(new Response(JSON.stringify({ csrfToken: 'tok123' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ user: { id: '1' } }), { status: 200 }));

    const result = useAuth();

    await result.signIn('credentials', {
      email: 'test@example.com',
      password: 'pw',
      redirect: false,
    });

    // Should have fetched CSRF then signin
    expect(mockFetch).toHaveBeenCalledTimes(2);
    expect(mockFetch.mock.calls[0][0]).toContain('/csrf');
    expect(mockFetch.mock.calls[1][0]).toContain('/signin/credentials');
  });

  it('signIn should throw on failed response', async () => {
    mockFetch
      .mockResolvedValueOnce(new Response(JSON.stringify({ csrfToken: 'tok123' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ message: 'Bad creds' }), { status: 401 }));

    const result = useAuth();

    await expect(result.signIn('credentials', { redirect: false })).rejects.toThrow('Bad creds');
  });

  it('signIn should handle CSRF fetch failure', async () => {
    mockFetch.mockResolvedValueOnce(new Response('', { status: 500 }));

    const result = useAuth();

    await expect(result.signIn('credentials', { redirect: false })).rejects.toThrow('Failed to fetch CSRF token');
  });

  it('signUp should fetch CSRF and then sign up', async () => {
    mockFetch
      .mockResolvedValueOnce(new Response(JSON.stringify({ csrfToken: 'tok456' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ user: { id: '1' } }), { status: 200 }));

    const result = useAuth();

    await result.signUp({
      username: 'newuser',
      email: 'new@example.com',
      password: 'pw',
      redirect: false,
    });

    expect(mockFetch).toHaveBeenCalledTimes(2);
    expect(mockFetch.mock.calls[1][0]).toContain('/signup');
  });

  it('signUp should throw on error', async () => {
    mockFetch
      .mockResolvedValueOnce(new Response(JSON.stringify({ csrfToken: 'tok' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ message: 'Email taken' }), { status: 400 }));

    const result = useAuth();

    await expect(
      result.signUp({
        username: 'u',
        email: 'e@e.com',
        password: 'p',
        redirect: false,
      }),
    ).rejects.toThrow('Email taken');
  });

  it('signOut should fetch CSRF and then sign out', async () => {
    mockFetch
      .mockResolvedValueOnce(new Response(JSON.stringify({ csrfToken: 'tok789' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ok: true }), { status: 200 }));

    const result = useAuth();

    await result.signOut({ redirect: false });

    expect(mockFetch).toHaveBeenCalledTimes(2);
    expect(mockFetch.mock.calls[1][0]).toContain('/signout');
  });

  it('signOut should throw on failure', async () => {
    mockFetch
      .mockResolvedValueOnce(new Response(JSON.stringify({ csrfToken: 'tok' }), { status: 200 }))
      .mockResolvedValueOnce(new Response('', { status: 500 }));

    const result = useAuth();

    await expect(result.signOut({ redirect: false })).rejects.toThrow('Sign-out failed');
  });

  it('signIn should handle non-Error throws', async () => {
    mockFetch.mockRejectedValueOnce('string error');

    const result = useAuth();

    await expect(result.signIn('credentials', { redirect: false })).rejects.toThrow('Sign-in failed');
  });

  it('signUp should handle non-Error throws', async () => {
    mockFetch.mockRejectedValueOnce(42);

    const result = useAuth();

    await expect(
      result.signUp({
        username: 'u',
        email: 'e@e.com',
        password: 'p',
        redirect: false,
      }),
    ).rejects.toThrow('Sign-up failed');
  });

  it('signOut should handle non-Error throws', async () => {
    mockFetch.mockRejectedValueOnce(null);

    const result = useAuth();

    await expect(result.signOut({ redirect: false })).rejects.toThrow('Sign-out failed');
  });

  it('signIn should handle failed response body parse', async () => {
    mockFetch.mockResolvedValueOnce(new Response(JSON.stringify({ csrfToken: 'tok' }), { status: 200 })).mockResolvedValueOnce(
      new Response('not json', {
        status: 401,
        headers: { 'Content-Type': 'text/plain' },
      }),
    );

    const result = useAuth();

    await expect(result.signIn('credentials', { redirect: false })).rejects.toThrow('Sign-in failed');
  });

  it('signIn should redirect to OAuth URL when response has url', async () => {
    // Mock window.location
    const locationMock = { href: '' };
    vi.stubGlobal('window', { location: locationMock });

    mockFetch
      .mockResolvedValueOnce(new Response(JSON.stringify({ csrfToken: 'tok' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ url: 'https://github.com/login/oauth?client_id=abc' }), { status: 200 }));

    const result = useAuth();
    await result.signIn('github', { redirect: false });

    expect(locationMock.href).toBe('https://github.com/login/oauth?client_id=abc');

  });

  it('signIn should redirect to redirectTo on success', async () => {
    const locationMock = { href: '' };
    vi.stubGlobal('window', { location: locationMock });

    mockFetch
      .mockResolvedValueOnce(new Response(JSON.stringify({ csrfToken: 'tok' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ user: { id: '1' } }), { status: 200 }));

    const result = useAuth();
    await result.signIn('credentials', {
      email: 'test@test.com',
      password: 'pw',
      redirect: true,
      redirectTo: '/dashboard',
    });

    expect(locationMock.href).toBe('/dashboard');

  });

  it('signUp should redirect to redirectTo on success', async () => {
    const locationMock = { href: '' };
    vi.stubGlobal('window', { location: locationMock });

    mockFetch
      .mockResolvedValueOnce(new Response(JSON.stringify({ csrfToken: 'tok' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ user: { id: '1' } }), { status: 200 }));

    const result = useAuth();
    await result.signUp({
      username: 'u',
      email: 'e@e.com',
      password: 'p',
      redirect: true,
      redirectTo: '/welcome',
    });

    expect(locationMock.href).toBe('/welcome');

  });

  it('signOut should redirect to / by default', async () => {
    const locationMock = { href: '' };
    vi.stubGlobal('window', { location: locationMock });

    mockFetch
      .mockResolvedValueOnce(new Response(JSON.stringify({ csrfToken: 'tok' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ok: true }), { status: 200 }));

    const result = useAuth();
    await result.signOut(); // default redirect=true

    expect(locationMock.href).toBe('/');

  });

  it('signOut should redirect to custom URL', async () => {
    const locationMock = { href: '' };
    vi.stubGlobal('window', { location: locationMock });

    mockFetch
      .mockResolvedValueOnce(new Response(JSON.stringify({ csrfToken: 'tok' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ok: true }), { status: 200 }));

    const result = useAuth();
    await result.signOut({ redirectTo: '/login', redirect: true });

    expect(locationMock.href).toBe('/login');

  });

  it('signUp should handle failed response body parse', async () => {
    mockFetch.mockResolvedValueOnce(new Response(JSON.stringify({ csrfToken: 'tok' }), { status: 200 })).mockResolvedValueOnce(
      new Response('not json', {
        status: 400,
        headers: { 'Content-Type': 'text/plain' },
      }),
    );

    const result = useAuth();

    await expect(
      result.signUp({
        username: 'u',
        email: 'e@e.com',
        password: 'p',
        redirect: false,
      }),
    ).rejects.toThrow('Sign-up failed');
  });
});
