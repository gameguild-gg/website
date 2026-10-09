/**
 * Next.js Proxy Helper
 *
 * Provides proxy integration for route protection.
 * In Next.js 16+, middleware.ts was renamed to proxy.ts.
 *
 * @example
 * ```typescript
 * // proxy.ts
 * import { auth } from '@/auth';
 *
 * export default auth((req) => {
 *   if (!req.auth && req.nextUrl.pathname !== '/sign-in') {
 *     const signInUrl = new URL('/sign-in', req.nextUrl.origin);
 *     signInUrl.searchParams.set('callbackUrl', req.nextUrl.pathname);
 *     return Response.redirect(signInUrl);
 *   }
 * });
 *
 * export const config = {
 *   matcher: ['/((?!api|_next/static|_next/image|favicon.ico).*)'],
 * };
 * ```
 */

import type { Session, ResolvedAuthConfig } from '../../runtime/auth/types.js';
import { encodeSession, processSession } from '../../runtime/auth/session.js';
import { SessionStore, resolveCookieOptions, type CookieSerializeOptions } from '../../runtime/auth/cookies.js';
import { applyResponseCookies, parseCookieHeader } from './handlers.js';

/**
 * Create a proxy-compatible auth checker.
 *
 * This returns a function that can be used as Next.js proxy (formerly middleware).
 * It reads the session from the request cookies and attaches it
 * to `request.auth`.
 *
 * @param config - Resolved auth configuration
 * @returns A function that wraps a proxy handler
 */
export function createProxy(config: ResolvedAuthConfig) {
  const cookieOptions = resolveCookieOptions(config.cookies, config.cookies.secure);
  const sessionStore = new SessionStore(cookieOptions);

  return function withAuth(
    handler?: (request: Request & { auth: Session | null }) => Promise<Response | void> | Response | void,
  ): (request: Request) => Promise<Response> {
    return async (request: Request): Promise<Response> => {
      // Parse cookies using the shared helper
      const cookieHeader = request.headers.get('cookie') || '';
      const cookieMap = parseCookieHeader(cookieHeader);

      const encryptedToken = sessionStore.read((name) => cookieMap.get(name));
      const responseCookies: Array<{ name: string; value: string; options: CookieSerializeOptions }> = [];
      const setCookie = (name: string, value: string, options: CookieSerializeOptions) => {
        responseCookies.push({ name, value, options });
      };

      let session: Session | null = null;

      if (encryptedToken) {
        const result = await processSession(encryptedToken, config);
        session = result.session;
        if (result.updated && result.token) {
          sessionStore.write(await encodeSession(result.token, config), setCookie);
        }
        if (!session) sessionStore.delete(setCookie);
      }

      // Check the authorized callback
      const isAuthorized = await config.callbacks.authorized({
        auth: session,
        request,
      });

      if (!isAuthorized) {
        const url = new URL(request.url);
        const signInPage = config.pages.signIn || '/sign-in';
        const signInUrl = new URL(signInPage, url.origin);
        signInUrl.searchParams.set('callbackUrl', url.pathname);
        return applyResponseCookies(Response.redirect(signInUrl.toString()), responseCookies);
      }

      // If the user provided a handler, call it with the augmented request
      if (handler) {
        const augmentedRequest = request as Request & { auth: Session | null };
        augmentedRequest.auth = session;

        const result = await handler(augmentedRequest);
        if (result) return applyResponseCookies(result, responseCookies);
      }

      // Continue to the next handler
      return applyResponseCookies(new Response(null, { status: 200 }), responseCookies);
    };
  };
}

/**
 * @deprecated Use `createProxy` instead. In Next.js 16+, middleware was renamed to proxy.
 */
export const createMiddleware = createProxy;
