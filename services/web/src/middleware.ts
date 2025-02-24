import createIntlMiddleware from 'next-intl/middleware';
import { locales } from '@/data/locales';
import NextAuth from 'next-auth';
import { authConfig } from '@/config/auth.config';
import { NextRequest, NextResponse } from 'next/server';

// Create the i18n middleware
const i18nMiddleware = createIntlMiddleware({
  locales: locales,
  localeDetection: true,
  localePrefix: 'as-needed',
  defaultLocale: 'en',
});

// Create the NextAuth middleware
const authMiddleware = NextAuth(authConfig);

export async function middleware(request: NextRequest) {
  // on production check if the request is coming from the old domain and redirect to the new one
  const oldDomains = ['web.gameguild.gg', 'gamedevguild.org'];
  const newDomain = 'gameguild.gg';

  // Get the request hostname from headers (more reliable in middleware)
  const host =
    request.headers.get('X-Request-Domain') ||
    request.headers.get('x-forwarded-host') ||
    request.headers.get('host') ||
    request.nextUrl.hostname;

  console.log(host);
  // Redirect only if the request is GET (to avoid issues with CORS preflight and non-idempotent requests)
  if (host && oldDomains.includes(host) && request.method === 'GET') {
    const newUrl = new URL(request.url);
    newUrl.hostname = newDomain;
    newUrl.port = '';
    return NextResponse.redirect(newUrl, 308);
  }

  // 1. Handle internationalization first
  const i18nResponse = await i18nMiddleware(request);

  // 2. If i18nMiddleware redirected, return the response
  if (i18nResponse) {
    return i18nResponse;
  }

  // // 3. Handle authentication
  // const authResponse = await authMiddleware(request);
  //
  // // 4. If authMiddleware redirected, return the response
  // if (authResponse) {
  //   return authResponse;
  // }

  // 5. If no redirects, continue to your application
  return NextResponse.next();
}

export const config = {
  matcher: ['/((?!api|_next/static|_next/image|assets|favicon.ico).*)'],
};
