import { NextRequest, NextResponse } from 'next/server';
import { getToken } from 'next-auth/jwt';
import createIntlMiddleware from 'next-intl/middleware';
import { locales } from '@/data/locales';

// Create the i18n middleware
const i18nMiddleware = createIntlMiddleware({
  locales: locales,
  localeDetection: true,
  localePrefix: 'as-needed',
  defaultLocale: 'en',
});

export async function middleware(request: NextRequest) {
  const secret = process.env.NEXTAUTH_SECRET as string;

  // Get the token from the request, passing the secret
  const token = await getToken({ req: request, secret: secret });

  // Log the request path and authentication status
  console.log(`ROUTE: ${request.nextUrl.pathname}`);
  console.log(`SIGNED-IN: ${Boolean(token)}`);

  const pathname = request.nextUrl.pathname;

  // Handle private routes that require authentication
  if (pathname.startsWith('/src/app/') && pathname.includes('(private)')) {
    if (!token) {
      return NextResponse.redirect(new URL(`/connect`, request.url));
    } // ${request.nextUrl.locale}
  }

  // For public and auth routes, no redirection needed
  // For example, you could add specific rules here if needed for the public or auth routes

  // Handle internationalization middleware
  const response = i18nMiddleware(request);

  return response;
}

export const config = {
  matcher: ['/((?!api|_next/static|_next/image|assets|favicon.ico).*)'],
};
