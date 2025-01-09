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
