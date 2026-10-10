/**
 * Authentication Types
 *
 * Type definitions for the pluggable authentication system.
 * Inspired by next-auth's architecture but tailored for the GameGuild .NET backend.
 */

// ─── Token Types ─────────────────────────────────────────────────

/**
 * Token pair returned from authentication
 */
export interface TokenPair {
  /** JWT access token */
  accessToken: string;
  /** Refresh token (if using refresh flow) */
  refreshToken?: string;
  /** Token expiry in seconds from now */
  expiresIn?: number;
  /** Absolute expiry timestamp (ISO string) */
  accessTokenExpiresAt?: string;
  /** Refresh token expiry timestamp (ISO string) */
  refreshTokenExpiresAt?: string;
  /** Token type (always 'Bearer') */
  tokenType: 'Bearer';
  /** OAuth scopes granted */
  scope?: string;
}

/**
 * Token provider interface
 *
 * Implement this to provide tokens from any authentication source.
 */
export interface TokenProvider {
  /**
   * Get the current access token
   * Return null if not authenticated
   */
  getAccessToken(): Promise<string | null>;

  /**
   * Get the refresh token (optional)
   * Used for automatic token refresh
   */
  getRefreshToken?(): Promise<string | null>;

  /**
   * Called when tokens are refreshed
   * Store the new tokens in your auth system
   */
  onTokenRefresh?(tokens: TokenPair): Promise<void>;

  /**
   * Called when authentication is required
   * Typically redirect to login page
   */
  onAuthenticationRequired?(): Promise<void>;
}

/**
 * Authentication configuration for the low-level client
 */
export interface AuthConfig {
  /** Token provider implementation */
  tokenProvider: TokenProvider;

  /** Enable automatic token refresh */
  autoRefresh?: boolean;

  /** Refresh token before this many seconds until expiry */
  refreshThreshold?: number;

  /** Maximum number of refresh retries */
  maxRefreshRetries?: number;
}

/**
 * Auth mode for requests
 */
export type AuthMode = 'required' | 'optional' | 'none';

// ─── Session Types ───────────────────────────────────────────────

/**
 * User information exposed in the session.
 * Contains only safe-to-expose data (never tokens).
 */
export interface SessionUser {
  /** User ID from the .NET backend */
  id: string;
  /** User email */
  email?: string | null;
  /** Display name */
  name?: string | null;
  /** Avatar URL */
  image?: string | null;
  /** Assigned role names (e.g. ['admin', 'editor']) */
  roles?: string[];
  /** Granted permission strings (e.g. ['content:read', 'content:write']) */
  permissions?: string[];
}

/**
 * Session object exposed to the client.
 * Never contains tokens — only user info and metadata.
 */
export interface Session {
  /** Authenticated user */
  user: SessionUser;
  /** Session expiry (ISO string) */
  expires: string;
  /** Current tenant ID (if multi-tenant) */
  tenantId?: string | null;
  /** Available tenants for the user */
  availableTenants?: Array<{ id: string; name: string }> | null;
}

/**
 * Internal JWT payload stored in the encrypted cookie.
 * Contains tokens — never sent to the client directly.
 */
export interface JWTPayload {
  /** User info */
  user: SessionUser;
  /** .NET backend access token */
  accessToken: string;
  /** .NET backend refresh token */
  refreshToken: string;
  /** Access token expiry timestamp (ms since epoch) */
  accessTokenExpires: number;
  /** Refresh token expiry timestamp (ms since epoch) */
  refreshTokenExpires?: number;
  /** Session ID from the backend */
  sessionId?: string;
  /** Current tenant ID */
  tenantId?: string | null;
  /** Available tenants */
  availableTenants?: Array<{ id: string; name: string }> | null;
  /** JWT issued at (seconds since epoch) */
  iat?: number;
  /** JWT expiry (seconds since epoch) */
  exp?: number;
  /** JWT jti (unique identifier) */
  jti?: string;
  /**
   * Whether the session opted into persistent ("remember me") storage.
   * When explicitly false, the session cookie is written without a Max-Age
   * so the browser drops it when the session ends.
   */
  rememberMe?: boolean;
}

// ─── Provider Types ──────────────────────────────────────────────

/**
 * Supported authentication provider types
 */
export type ProviderType = 'credentials' | 'oauth' | 'oidc';

/**
 * Base provider configuration
 */
export interface ProviderConfig {
  /** Unique provider ID */
  id: string;
  /** Human-readable name */
  name: string;
  /** Provider type */
  type: ProviderType;
}

/**
 * Result of a provider's authorize/authenticate call
 */
export interface ProviderResult {
  /** The tokens from the .NET backend */
  tokens: TokenPair;
  /** User info extracted from the response */
  user: SessionUser;
  /** Session ID from the backend */
  sessionId?: string;
  /** Tenant information */
  tenantId?: string | null;
  availableTenants?: Array<{ id: string; name: string }> | null;
  /**
   * Whether the sign-in requested a persistent ("remember me") session.
   * Drives the cookie storage policy: true keeps the durable Max-Age cookie,
   * false writes a browser-session cookie, undefined preserves the configured default.
   */
  rememberMe?: boolean;
}

/**
 * Credentials provider configuration
 */
export interface CredentialsProviderConfig extends ProviderConfig {
  type: 'credentials';
  /**
   * Authenticate with credentials.
   * Called with the form data from sign-in.
   */
  authorize: (credentials: Record<string, unknown>, request?: Request) => Promise<ProviderResult | null>;
}

/**
 * OAuth provider configuration
 */
export interface OAuthProviderConfig extends ProviderConfig {
  type: 'oauth' | 'oidc';
  /** Client ID */
  clientId: string;
  /** Client secret */
  clientSecret: string;
  /** Authorization URL */
  authorization?: string | { url: string; params?: Record<string, string> };
  /** Token exchange URL */
  token?: string | { url: string };
  /** User info URL */
  userinfo?: string | { url: string };
}

/**
 * Union of all provider configs
 */
export type Provider = CredentialsProviderConfig | OAuthProviderConfig;

// ─── Callback Types ──────────────────────────────────────────────

/**
 * Callbacks to customize auth behavior.
 * Inspired by next-auth's callback system.
 */
export interface AuthCallbacks {
  /**
   * Called when the JWT is created or updated.
   * Use to persist extra data in the token.
   *
   * @param params.token - The current JWT payload
   * @param params.user - The user from provider (only on sign-in)
   * @param params.trigger - What caused this callback ('signIn', 'signUp', 'update')
   * @param params.session - The session data passed to update() (only on 'update' trigger)
   * @returns The modified token
   */
  jwt?: (params: {
    token: JWTPayload;
    user?: SessionUser;
    trigger?: 'signIn' | 'signUp' | 'update';
    session?: Partial<Session>;
  }) => Promise<JWTPayload> | JWTPayload;

  /**
   * Called when session is checked.
   * Use to control what data is exposed to the client.
   *
   * @param params.session - The session to be returned
   * @param params.token - The JWT payload (contains tokens)
   * @returns The session to expose to the client
   */
  session?: (params: { session: Session; token: JWTPayload }) => Promise<Session> | Session;

  /**
   * Called when sign-in is attempted.
   * Return true to allow, false to deny, or a URL to redirect.
   */
  signIn?: (params: { user: SessionUser; provider: string }) => Promise<boolean | string> | boolean | string;

  /**
   * Called on redirect.
   * Use to validate and modify redirect URLs.
   */
  redirect?: (params: { url: string; baseUrl: string }) => Promise<string> | string;

  /**
   * Called when the proxy's `authorized` callback runs.
   * Return true to allow access, false to deny.
   */
  authorized?: (params: { auth: Session | null; request: Request }) => Promise<boolean> | boolean;
}

// ─── Auth Configuration ──────────────────────────────────────────

/**
 * Cookie configuration
 */
export interface CookieConfig {
  /** Cookie name prefix (default: '__me') */
  name?: string;
  /** Use secure cookies (default: auto-detect from NEXTAUTH_URL) */
  secure?: boolean;
  /** SameSite attribute */
  sameSite?: 'lax' | 'strict' | 'none';
  /** Cookie path */
  path?: string;
  /** Cookie domain */
  domain?: string;
  /** Max age in seconds (default: 30 days) */
  maxAge?: number;
  /** HttpOnly flag (default: true) */
  httpOnly?: boolean;
}

/**
 * Cookie configuration after defaults are applied.
 * Domain remains optional because host-only cookies are valid and commonly used.
 */
export interface ResolvedCookieConfig {
  name: string;
  secure: boolean;
  sameSite: 'lax' | 'strict' | 'none';
  path: string;
  domain?: string;
  maxAge: number;
  httpOnly: boolean;
}

/**
 * Pages configuration for custom auth pages
 */
export interface PagesConfig {
  /** Sign-in page path */
  signIn?: string;
  /** Sign-up page path */
  signUp?: string;
  /** Sign-out page path */
  signOut?: string;
  /** Error page path */
  error?: string;
  /** New user page (redirect after first sign-up) */
  newUser?: string;
}

/**
 * Main auth configuration — the single config object for GameGuildAuth()
 */
export interface GameGuildAuthConfig {
  /**
   * Authentication providers
   * At minimum, include the credentials provider for email/password.
   */
  providers: Provider[];

  /**
   * Callbacks to customize behavior
   */
  callbacks?: AuthCallbacks;

  /**
   * Secret key for encrypting the session JWT.
   * If not provided, reads from AUTH_SECRET environment variable.
   */
  secret?: string;

  /**
   * .NET backend API base URL.
   * If not provided, reads from API_URL environment variable.
   */
  apiUrl?: string;

  /**
   * Custom page paths
   */
  pages?: PagesConfig;

  /**
   * Cookie configuration
   */
  cookies?: CookieConfig;

  /**
   * Session max age in seconds (default: 30 days = 2592000)
   */
  maxAge?: number;

  /**
   * How often the React SessionProvider polls the session endpoint, in seconds.
   * 0 = polling disabled (session only fetched on mount / focus / event).
   * This does NOT control server-side session revalidation — sessions are always
   * revalidated on every server request.
   *
   * @default 0
   */
  updateAge?: number;

  /**
   * Base path for auth API routes (default: '/api/auth')
   */
  basePath?: string;

  /**
   * Enable debug logging
   */
  debug?: boolean;

  /**
   * Trust the host header (for proxied environments)
   */
  trustHost?: boolean;

  /**
   * Tenant header name for multi-tenant requests
   * @default 'X-Tenant-Id'
   */
  tenantHeader?: string;
}

// ─── Auth Result Types ───────────────────────────────────────────

/**
 * Returned by GameGuildAuth() factory
 */
export interface AuthInstance {
  /**
   * Route handlers for /api/auth/[...auth]
   */
  handlers: {
    GET: (request: Request) => Promise<Response>;
    POST: (request: Request) => Promise<Response>;
  };

  /**
   * Get the current session.
   * Works in Server Components, Server Actions, Route Handlers, and Proxy.
   */
  auth: {
    (): Promise<Session | null>;
    (handler: (request: Request & { auth: Session | null }) => Promise<Response> | Response): (request: Request) => Promise<Response>;
  };

  /**
   * Sign in (Server Action)
   *
   * @param provider - Provider ID (e.g. 'credentials', 'google')
   * @param options - Credentials or OAuth options
   */
  signIn: (
    provider?: string,
    options?: Record<string, unknown> & {
      redirectTo?: string;
      redirect?: boolean;
    },
  ) => Promise<Response | ProviderResult | void>;

  /**
   * Sign up (Server Action) — GameGuild-specific, not in next-auth
   */
  signUp: (
    credentials: {
      username: string;
      email: string;
      password: string;
      firstName?: string;
      lastName?: string;
      tenantId?: string;
    },
    options?: { redirectTo?: string; redirect?: boolean },
  ) => Promise<Response | ProviderResult | void>;

  /**
   * Sign out (Server Action)
   */
  signOut: (options?: { redirectTo?: string; redirect?: boolean }) => Promise<Response | void>;

  /**
   * Update the session (Server Action).
   * Useful for changing tenant, refreshing tokens, etc.
   */
  update: (data?: Partial<Session>) => Promise<Session | null>;

  /**
   * The resolved configuration (read-only)
   */
  config: Readonly<ResolvedAuthConfig>;
}

/**
 * Resolved configuration with defaults applied
 */
export interface ResolvedAuthConfig {
  providers: Provider[];
  callbacks: Required<AuthCallbacks>;
  secret: string;
  apiUrl: string;
  pages: PagesConfig;
  cookies: ResolvedCookieConfig;
  maxAge: number;
  updateAge: number;
  basePath: string;
  debug: boolean;
  trustHost: boolean;
  tenantHeader: string;
}

// ─── React Hook Types ────────────────────────────────────────────

/**
 * Session status for the useSession hook
 */
export type SessionStatus = 'loading' | 'authenticated' | 'unauthenticated';

/**
 * Return type of useSession()
 */
export type UseSessionReturn =
  | {
      data: Session;
      status: 'authenticated';
      update: (data?: Partial<Session>) => Promise<Session | null>;
    }
  | {
      data: null;
      status: 'loading';
      update: (data?: Partial<Session>) => Promise<Session | null>;
    }
  | {
      data: null;
      status: 'unauthenticated';
      update: (data?: Partial<Session>) => Promise<Session | null>;
    };

/**
 * Props for SessionProvider
 */
export interface SessionProviderProps {
  children: unknown;
  /** Pre-fetched session (for SSR hydration) */
  session?: Session | null;
  /** Base path for auth API (default: '/api/auth') */
  basePath?: string;
  /** Refetch session every N seconds (0 = disabled) */
  refetchInterval?: number;
  /** Refetch session when window regains focus */
  refetchOnWindowFocus?: boolean;
  /** Refetch when user comes back online */
  refetchWhenOffline?: boolean;
}
