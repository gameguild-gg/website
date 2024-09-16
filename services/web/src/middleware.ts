import NextAuth from 'next-auth';
import {authConfig} from '@/config/auth.config';
import createIntlMiddleware from 'next-intl/middleware';
import {locales} from '@/data/locales';

export default NextAuth(authConfig).auth((request) => {
  // TODO: Handle the request here.
  console.log(`ROUTE: ${request.nextUrl.pathname}`);
  console.log(`SIGNED-IN: ${!!request.auth}`);

  const handleI18nRouting = createIntlMiddleware({
    locales: locales,
    localeDetection: true,
    localePrefix: 'as-needed',
    defaultLocale: 'en',
  });

  return handleI18nRouting(request);
});

// TODO: Fix the config to allow the i18n middleware to work properly.
export const config = {
  matcher: [
    // https://nextjs.org/docs/app/building-your-application/routing/middleware#matcher
    /*
     * Match all request paths except for the ones starting with:
     * - api (API routes)
     * - _next/static (static files)
     * - _next/image (image optimization files)
     * - favicon.ico (favicon file)
     */
    // Enable a redirect to a matching locale at the root
    // "/",
    // "/(en|pt)/:path*",
    // Enable redirects that add missing locales
    // (e.g. `/pathnames` -> `/en/pathnames`)
    // '/((?!_next|.*\\..*).*)',
    '/((?!api|_next/static|_next/image|assets|favicon.ico).*)',
  ],
};
