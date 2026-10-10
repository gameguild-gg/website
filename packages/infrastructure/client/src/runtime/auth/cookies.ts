/**
 * Cookie Management
 *
 * Handles session cookie read/write with chunking for large payloads.
 * Inspired by next-auth's SessionStore pattern.
 *
 * Cookies have a ~4KB browser limit per cookie. Encrypted JWTs can exceed this
 * if the user has many tenants or extra session data. This module chunks the
 * encrypted token across multiple cookies when needed.
 */

import type { CookieConfig } from './types.js';

/** Maximum bytes per cookie (leave some room for name + attributes) */
const MAX_COOKIE_SIZE = 3800;

/** Default cookie name prefix */
const DEFAULT_COOKIE_NAME = '__me';

/**
 * Resolved cookie options with all defaults applied
 */
export interface ResolvedCookieOptions {
  name: string;
  secure: boolean;
  sameSite: 'lax' | 'strict' | 'none';
  path: string;
  domain?: string;
  maxAge: number;
  httpOnly: boolean;
}

/**
 * Resolve cookie configuration with defaults.
 *
 * @param config - Partial cookie config from the user
 * @param isSecure - Whether the deployment is HTTPS
 * @returns Fully resolved cookie options
 */
export function resolveCookieOptions(config?: CookieConfig, isSecure?: boolean): ResolvedCookieOptions {
  const secure = config?.secure ?? isSecure ?? false;

  return {
    name: config?.name ?? DEFAULT_COOKIE_NAME,
    secure,
    sameSite: config?.sameSite ?? 'lax',
    path: config?.path ?? '/',
    domain: config?.domain,
    maxAge: config?.maxAge ?? 30 * 24 * 60 * 60, // 30 days
    httpOnly: config?.httpOnly ?? true,
  };
}

/**
 * Get the full cookie name including secure prefix.
 *
 * In production (HTTPS), cookies use the `__Secure-` prefix which requires
 * the Secure flag. This prevents cookie injection from HTTP subdomains.
 */
export function getCookieName(baseName: string, secure: boolean): string {
  return secure ? `__Secure-${baseName}` : baseName;
}

/**
 * SessionStore — reads and writes session cookies with chunking support.
 *
 * Large JWTs (> 4KB) are split across numbered cookies:
 * - `__me.session-token` (or `__Secure-__me.session-token`)
 * - `__me.session-token.1`
 * - `__me.session-token.2`
 * - etc.
 */
export class SessionStore {
  private readonly cookieName: string;
  private readonly options: ResolvedCookieOptions;

  constructor(options: ResolvedCookieOptions) {
    this.options = options;
    this.cookieName = getCookieName(`${options.name}.session-token`, options.secure);
  }

  /**
   * Get the session cookie name (for external reference)
   */
  getCookieName(): string {
    return this.cookieName;
  }

  /**
   * Read the session token from cookies.
   * Handles reassembly of chunked cookies.
   *
   * @param getCookie - Function to read a cookie value by name
   * @returns The full session token string, or null if not found
   */
  read(getCookie: (name: string) => string | undefined): string | null {
    // Try the main cookie first
    const mainValue = getCookie(this.cookieName);

    if (mainValue === undefined) {
      return null;
    }

    // Check if there are chunks
    let fullValue = mainValue;
    let chunkIndex = 1;

    while (true) {
      const chunkName = `${this.cookieName}.${chunkIndex}`;
      const chunkValue = getCookie(chunkName);

      if (chunkValue === undefined) break;

      fullValue += chunkValue;
      chunkIndex++;
    }

    return fullValue || null;
  }

  /**
   * Write the session token to cookies.
   * Automatically chunks if the value exceeds the cookie size limit.
   *
   * @param value - The encrypted JWT to store
   * @param setCookie - Function to set a cookie (name, value, options)
   * @param policy - Storage policy: `session` omits Max-Age so the browser drops
   * the cookie when the session ends; `persistent` (default) keeps the durable cookie.
   */
  write(
    value: string,
    setCookie: (name: string, value: string, options: CookieSerializeOptions) => void,
    policy?: { storage?: 'session' | 'persistent' },
  ): void {
    const cookieOptions = this.serializeOptions();
    if (policy?.storage === 'session') {
      delete cookieOptions.maxAge;
    }

    if (value.length <= MAX_COOKIE_SIZE) {
      // Fits in a single cookie
      setCookie(this.cookieName, value, cookieOptions);

      // Clean up any leftover chunks from a previous larger session
      this.clearChunks(setCookie, 1);
    } else {
      // Split into chunks
      const chunks = this.chunk(value);

      chunks.forEach((chunk, index) => {
        const name = index === 0 ? this.cookieName : `${this.cookieName}.${index}`;
        setCookie(name, chunk, cookieOptions);
      });

      // Clean up any extra chunks from a previous even-larger session
      this.clearChunks(setCookie, chunks.length);
    }
  }

  /**
   * Delete the session cookie (and any chunks).
   *
   * @param setCookie - Function to set a cookie (for deletion via maxAge=0)
   */
  delete(setCookie: (name: string, value: string, options: CookieSerializeOptions) => void): void {
    const deleteOptions: CookieSerializeOptions = {
      ...this.serializeOptions(),
      maxAge: 0,
    };

    setCookie(this.cookieName, '', deleteOptions);
    this.clearChunks(setCookie, 1);
  }

  /**
   * Split a value into cookie-sized chunks.
   */
  private chunk(value: string): string[] {
    const chunks: string[] = [];
    for (let i = 0; i < value.length; i += MAX_COOKIE_SIZE) {
      chunks.push(value.slice(i, i + MAX_COOKIE_SIZE));
    }
    return chunks;
  }

  /**
   * Clear chunk cookies starting from a given index.
   */
  private clearChunks(setCookie: (name: string, value: string, options: CookieSerializeOptions) => void, startIndex: number): void {
    const deleteOptions: CookieSerializeOptions = {
      ...this.serializeOptions(),
      maxAge: 0,
    };

    // Clear up to 10 potential old chunks
    for (let i = startIndex; i < startIndex + 10; i++) {
      setCookie(`${this.cookieName}.${i}`, '', deleteOptions);
    }
  }

  /**
   * Convert options to standard cookie serialization format.
   */
  private serializeOptions(): CookieSerializeOptions {
    return toSerializeOptions(this.options);
  }
}

/**
 * Standard cookie serialize options (compatible with next/headers cookies API)
 */
export interface CookieSerializeOptions {
  httpOnly?: boolean;
  secure?: boolean;
  sameSite?: 'lax' | 'strict' | 'none';
  path?: string;
  domain?: string;
  maxAge?: number;
  expires?: Date;
}

/**
 * Convert resolved options to the standard serialization format.
 * Shared by all cookie stores to avoid duplication.
 */
function toSerializeOptions(options: ResolvedCookieOptions, overrides?: Partial<CookieSerializeOptions>): CookieSerializeOptions {
  return {
    httpOnly: options.httpOnly,
    secure: options.secure,
    sameSite: options.sameSite,
    path: options.path,
    domain: options.domain,
    maxAge: options.maxAge,
    ...overrides,
  };
}

/**
 * SimpleCookieStore — base class for single-value cookie stores.
 *
 * Both CsrfStore and CallbackStore are simple key-value stores
 * that differ only in cookie name suffix and httpOnly behavior.
 * This base class extracts the shared logic.
 */
class SimpleCookieStore {
  protected readonly cookieName: string;
  protected readonly options: ResolvedCookieOptions;
  private readonly httpOnlyOverride?: boolean;

  constructor(options: ResolvedCookieOptions, suffix: string, httpOnlyOverride?: boolean) {
    this.options = options;
    this.cookieName = getCookieName(`${options.name}.${suffix}`, options.secure);
    this.httpOnlyOverride = httpOnlyOverride;
  }

  getCookieName(): string {
    return this.cookieName;
  }

  read(getCookie: (name: string) => string | undefined): string | null {
    return getCookie(this.cookieName) ?? null;
  }

  write(value: string, setCookie: (name: string, value: string, options: CookieSerializeOptions) => void): void {
    setCookie(this.cookieName, value, this.getSerializeOptions());
  }

  delete(setCookie: (name: string, value: string, options: CookieSerializeOptions) => void): void {
    setCookie(this.cookieName, '', {
      ...this.getSerializeOptions(),
      maxAge: 0,
    });
  }

  private getSerializeOptions(): CookieSerializeOptions {
    return toSerializeOptions(this.options, this.httpOnlyOverride !== undefined ? { httpOnly: this.httpOnlyOverride } : undefined);
  }
}

/**
 * CSRF cookie store — non-HttpOnly cookie for client-side CSRF token reading.
 */
export class CsrfStore extends SimpleCookieStore {
  constructor(options: ResolvedCookieOptions) {
    super(options, 'csrf-token', false); // httpOnly = false so JS can read it
  }
}

/**
 * Callback URL cookie store — stores the redirect URL during OAuth flows.
 */
export class CallbackStore extends SimpleCookieStore {
  constructor(options: ResolvedCookieOptions) {
    super(options, 'callback-url');
  }
}
