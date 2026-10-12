// @vitest-environment node

import { beforeEach, describe, expect, it, vi } from "vitest";

const state = vi.hoisted(() => ({
  cookie: "encrypted-session" as string | undefined,
  cookieSet: vi.fn(),
  cookieRead: vi.fn(),
  processSession: vi.fn(),
  encodeSession: vi.fn(),
  sharedAuth: vi.fn(),
  proxy: vi.fn(),
}));

vi.mock("react", async (importOriginal) => ({
  ...await importOriginal<typeof import("react")>(),
  // Model the request-bound React cache so parallel server readers share one
  // session evaluation. Each test imports a fresh module/request scope.
  cache: (reader: () => unknown) => {
    let result: unknown;
    let started = false;
    return () => {
      if (!started) {
        started = true;
        result = reader();
      }
      return result;
    };
  },
}));

vi.mock("next/headers", () => ({
  cookies: state.cookieRead,
}));

vi.mock("@game-guild/client", () => ({
  GameGuildAuth: (config: Record<string, unknown>) => ({
    config: { ...config, cookies: { name: "gameguild", secure: false } },
    handlers: { GET: vi.fn(), POST: vi.fn() },
    auth: state.sharedAuth,
    signIn: vi.fn(),
    signOut: vi.fn(),
    signUp: vi.fn(),
    update: vi.fn(),
  }),
  CredentialsProvider: () => ({ authorize: vi.fn() }),
  GoogleProvider: vi.fn(),
  DiscordProvider: vi.fn(),
  processSession: state.processSession,
  encodeSession: state.encodeSession,
  resolveCookieOptions: (options: unknown) => options,
  SessionStore: class {
    read(get: (name: string) => string | undefined) {
      return get("gameguild.session-token") ?? null;
    }
    write(value: string, set: (name: string, value: string, options: object) => void) {
      set("gameguild.session-token", value, { httpOnly: true, path: "/" });
    }
    delete(set: (name: string, value: string, options: object) => void) {
      set("gameguild.session-token", "", { httpOnly: true, path: "/", maxAge: 0 });
    }
  },
}));

describe("request-bound authentication readers", () => {
  const session = { user: { id: "member-id" }, tenantId: "owned-tenant" };
  const token = { accessToken: "opaque-access-token" };

  beforeEach(() => {
    vi.resetModules();
    vi.clearAllMocks();
    state.cookie = "encrypted-session";
    state.cookieSet.mockReset();
    state.cookieRead.mockResolvedValue({
      get: () => state.cookie === undefined ? undefined : { value: state.cookie },
      set: state.cookieSet,
    });
    state.processSession.mockResolvedValue({ session, token, updated: false });
    state.encodeSession.mockResolvedValue("rotated-encrypted-session");
    // The shared library's dynamic next/headers import is unavailable in the
    // standalone RSC runtime; its explicit-request proxy path still works.
    state.sharedAuth.mockImplementation((handler: unknown) => handler ? state.proxy : Promise.resolve(null));
    state.proxy.mockResolvedValue(new Response("proxy-response"));
  });

  it("auth() reads the same request-bound session as getSession()", async () => {
    const authModule = await import("./auth");
    expect(await authModule.auth()).toBe(session);
    expect(await authModule.getSession()).toBe(session);
    expect(state.sharedAuth).not.toHaveBeenCalled();
  });

  it("parallel session, token, and tenant reads process one encrypted cookie", async () => {
    const authModule = await import("./auth");
    const [currentSession, currentToken, context] = await Promise.all([
      authModule.auth(), authModule.getToken(), authModule.getRequestAuthContext(),
    ]);
    expect(currentSession).toBe(session);
    expect(currentToken).toBe(token.accessToken);
    expect(context).toEqual({ session, token: token.accessToken, tenantId: session.tenantId });
    expect(state.cookieRead).toHaveBeenCalledTimes(1);
    expect(state.processSession).toHaveBeenCalledTimes(1);
  });

  it("auth(handler) keeps the shared explicit-request proxy wrapper", async () => {
    const authModule = await import("./auth");
    const handler = vi.fn(() => new Response("handler-response"));
    expect(authModule.auth(handler)).toBe(state.proxy);
    expect(state.sharedAuth).toHaveBeenCalledWith(handler);
    expect(state.cookieRead).not.toHaveBeenCalled();
    expect(state.processSession).not.toHaveBeenCalled();
  });

  it("a request without a cookie remains anonymous", async () => {
    state.cookie = undefined;
    const authModule = await import("./auth");
    expect(await authModule.auth()).toBeNull();
    expect(await authModule.getToken()).toBeNull();
    expect(state.processSession).not.toHaveBeenCalled();
  });

  it("an invalid encrypted cookie remains anonymous", async () => {
    state.processSession.mockResolvedValue({ session: null, token: null, updated: false });
    const authModule = await import("./auth");
    expect(await authModule.auth()).toBeNull();
    expect(await authModule.getRequestAuthContext()).toEqual({ session: null, token: null, tenantId: null });
    expect(state.processSession).toHaveBeenCalledTimes(1);
  });

  it("a readable refreshed session survives a read-only RSC cookie store", async () => {
    state.processSession.mockResolvedValue({ session, token, updated: true });
    state.cookieSet.mockImplementation(() => { throw new Error("RSC cookies are read-only"); });
    const authModule = await import("./auth");
    expect(await authModule.auth()).toBe(session);
    expect(await authModule.getToken()).toBe(token.accessToken);
    expect(state.encodeSession).toHaveBeenCalledTimes(1);
    expect(state.processSession).toHaveBeenCalledTimes(1);
  });

  it("a writable request persists the refreshed cookie once", async () => {
    state.processSession.mockResolvedValue({ session, token, updated: true });
    const authModule = await import("./auth");
    expect(await authModule.auth()).toBe(session);
    expect(await authModule.getSession()).toBe(session);
    expect(state.cookieSet).toHaveBeenCalledExactlyOnceWith(
      "gameguild.session-token", "rotated-encrypted-session", { httpOnly: true, path: "/" },
    );
  });

  it("a writable request removes an invalidated session cookie once", async () => {
    state.processSession.mockResolvedValue({ session: null, token: null, updated: false });
    const authModule = await import("./auth");
    expect(await authModule.auth()).toBeNull();
    expect(await authModule.getToken()).toBeNull();
    expect(state.cookieSet).toHaveBeenCalledExactlyOnceWith(
      "gameguild.session-token", "", { httpOnly: true, path: "/", maxAge: 0 },
    );
  });

  it("an invalidated session stays anonymous in a read-only RSC store", async () => {
    state.processSession.mockResolvedValue({ session: null, token: null, updated: false });
    state.cookieSet.mockImplementation(() => { throw new Error("RSC cookies are read-only"); });
    const authModule = await import("./auth");
    expect(await authModule.auth()).toBeNull();
    expect(await authModule.getRequestAuthContext()).toEqual({ session: null, token: null, tenantId: null });
    expect(state.cookieSet).toHaveBeenCalledTimes(1);
    expect(state.processSession).toHaveBeenCalledTimes(1);
  });
});
