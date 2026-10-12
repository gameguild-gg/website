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
  // NextURL normalizes numeric loopback hosts to localhost. Keep the actual
  // request origin so redirects and rewrites retain host-only session cookies.
  const url = new URL(request.url);
  url.pathname = pathname;

  const requestHeaders = new Headers(request.headers);
  requestHeaders.set(INTERNAL_LOCALE_HEADER, "1");
  requestHeaders.set("x-next-intl-locale", locale);

  return NextResponse.rewrite(url, { request: { headers: requestHeaders } });
}

function redirectToPath(request: NextRequest, pathname: string): NextResponse {
  const url = new URL(request.url);
  url.pathname = pathname;
  return NextResponse.redirect(url);
}

/**
 * Keeps the default locale internal while preserving explicit non-default
 * locales. The marker header prevents Next 16 from canonicalizing the
 * internal `/en-US` rewrite back into a redirect loop.
 */
export function routeRequest(request: NextRequest): NextResponse {
  const pathname = request.nextUrl.pathname;

  // Server Action redirects inherit the rewritten request's headers. Only
  // bypass routing when the destination itself already includes a locale;
  // an unprefixed destination still needs the internal locale rewrite.
  if (
    request.headers.get(INTERNAL_LOCALE_HEADER) === "1" &&
    routing.locales.some((locale) => matchesPrefix(pathname, `/${locale}`))
  ) {
    return NextResponse.next();
  }

  if (matchesPrefix(pathname, DEFAULT_LOCALE_PREFIX)) {
    const unprefixedPath = pathname.slice(DEFAULT_LOCALE_PREFIX.length) || "/";
    if (unprefixedPath === "/social") {
      return redirectToPath(request, "/feed");
    }
    // RSC navigation targets the internal locale tree. A canonical browser
    // redirect makes Next request that internal tree again and creates a loop.
    if (request.headers.get("rsc") === "1") {
      return rewriteWithLocale(request, pathname, routing.defaultLocale);
    }
    return redirectToPath(request, unprefixedPath);
  }

  const nonDefaultPrefix = NON_DEFAULT_LOCALE_PREFIXES.find((prefix) => matchesPrefix(pathname, prefix));
  if (nonDefaultPrefix) {
    if (pathname === `${nonDefaultPrefix}/social`) {
      return redirectToPath(request, `${nonDefaultPrefix}/feed`);
    }
    return intlMiddleware(request);
  }

  if (pathname === "/social") {
    return redirectToPath(request, "/feed");
  }

  const localizedPath = pathname === "/" ? DEFAULT_LOCALE_PREFIX : `${DEFAULT_LOCALE_PREFIX}${pathname}`;
  return rewriteWithLocale(request, localizedPath, routing.defaultLocale);
}

export default auth((request) => {
  const nextRequest = request as unknown as NextRequest;
  return routeRequest(nextRequest);
});

export const config = {
  matcher: "/((?!api|trpc|_next|_vercel|.*\\..*).*)",
};
