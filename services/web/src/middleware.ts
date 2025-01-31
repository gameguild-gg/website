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
  const oldDomains = ['web.gameguild.gg', 'gamedevguild.org'];
  const newDomain = 'gameguild.gg';

  if (oldDomains.includes(request.nextUrl.hostname)) {
    // redirect to the new domain keeping the path, search and query string
    const url = new URL(
      request.nextUrl.href.replace(request.nextUrl.hostname, newDomain),
    );
    return NextResponse.redirect(url, 308); // 308 ensures permanent redirect and preserves method
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
