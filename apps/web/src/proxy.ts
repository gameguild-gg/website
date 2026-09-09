import { auth } from "@/auth";
import { routing } from "@/i18n/routing";
import createMiddleware from "next-intl/middleware";
import { NextResponse, type NextRequest } from "next/server";

const intlMiddleware = createMiddleware(routing);
const INTERNAL_LOCALE_HEADER = "x-gameguild-internal-locale-rewrite";
const DEFAULT_LOCALE_PREFIX = `/${routing.defaultLocale}`;
const NON_DEFAULT_LOCALE_PREFIXES = routing.locales
  .filter((locale) => locale !== routing.defaultLocale)
  .map((locale) => `/${locale}`);

function matchesPrefix(pathname: string, prefix: string): boolean {
  return pathname === prefix || pathname.startsWith(`${prefix}/`);
}

function rewriteWithLocale(request: NextRequest, pathname: string, locale: string): NextResponse {
  const url = request.nextUrl.clone();
  url.pathname = pathname;

  const requestHeaders = new Headers(request.headers);
  requestHeaders.set(INTERNAL_LOCALE_HEADER, "1");
  requestHeaders.set("x-next-intl-locale", locale);

  return NextResponse.rewrite(url, { request: { headers: requestHeaders } });
}

function redirectToPath(request: NextRequest, pathname: string): NextResponse {
  const url = request.nextUrl.clone();
  url.pathname = pathname;
  return NextResponse.redirect(url);
}

/**
 * Keeps the default locale internal while preserving explicit non-default
 * locales. The marker header prevents Next 16 from canonicalizing the
 * internal `/en-US` rewrite back into a redirect loop.
 */
export function routeRequest(request: NextRequest, authenticated = false): NextResponse {
  const pathname = request.nextUrl.pathname;

  if (request.headers.get(INTERNAL_LOCALE_HEADER) === "1") {
    return NextResponse.next();
  }

  if (matchesPrefix(pathname, DEFAULT_LOCALE_PREFIX)) {
    const unprefixedPath = pathname.slice(DEFAULT_LOCALE_PREFIX.length) || "/";
    return redirectToPath(request, authenticated && unprefixedPath === "/social" ? "/" : unprefixedPath);
  }

  const nonDefaultPrefix = NON_DEFAULT_LOCALE_PREFIXES.find((prefix) => matchesPrefix(pathname, prefix));
  if (nonDefaultPrefix) {
    if (authenticated && pathname === `${nonDefaultPrefix}/social`) {
      return redirectToPath(request, nonDefaultPrefix);
    }
    if (authenticated && pathname === nonDefaultPrefix) {
      return rewriteWithLocale(request, `${nonDefaultPrefix}/social`, nonDefaultPrefix.slice(1));
    }
    return intlMiddleware(request);
  }

  if (authenticated && pathname === "/social") {
    return redirectToPath(request, "/");
  }

  if (authenticated && pathname === "/") {
    return rewriteWithLocale(request, `${DEFAULT_LOCALE_PREFIX}/social`, routing.defaultLocale);
  }

  const localizedPath = pathname === "/" ? DEFAULT_LOCALE_PREFIX : `${DEFAULT_LOCALE_PREFIX}${pathname}`;
  return rewriteWithLocale(request, localizedPath, routing.defaultLocale);
}

export default auth((request) => {
  const nextRequest = request as unknown as NextRequest;
  return routeRequest(nextRequest, Boolean(request.auth));
});

export const config = {
  matcher: "/((?!api|trpc|_next|_vercel|.*\\..*).*)",
};
