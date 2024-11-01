import { NextRequest, NextResponse } from 'next/server';
import { getToken } from 'next-auth/jwt';
import createIntlMiddleware from 'next-intl/middleware';
import { locales } from '@/data/locales';
import { pathMapping } from '@/pathMapping';

// Create the i18n middleware
const i18nMiddleware = createIntlMiddleware({
  locales: locales,
  localeDetection: true,
  localePrefix: 'as-needed',
  defaultLocale: 'en',
});

export async function middleware(request: NextRequest) {
  // const secret = process.env.NEXTAUTH_SECRET as string;
  //
  // // Get the token from the request, passing the secret
  // const token = await getToken({ req: request, secret: secret });
  // let url = request.nextUrl.pathname;
  // // locale is optional 1st param if it is there, remove it from the url
  // const firstParam = url.split('/')[1];
  // // remove locale from url
  // if (locales.includes(firstParam)) url = url.replace(`/${firstParam}`, '');
  //
  // let page: string = '';
  // // remove the last part of the url while no page is found or we reach / (root)
  // while (url.length > 0) {
  //   if (pathMapping[url]) {
  //     page = pathMapping[url];
  //     break;
  //   }
  //   url = url.substring(0, url.lastIndexOf('/'));
  // }

  // Log the request path and authentication status
  console.log(`ROUTE: ${request.nextUrl.pathname}`);
  // console.log(`PAGE: ${page}`);
  // console.log(`SIGNED-IN: ${Boolean(token)}`);

  // Handle private routes that require authentication
  // todo: please try to improve this
  // if (page.includes('(private)')) {
  //   if (
  //     !token ||
  //     !token.user ||
  //     !token.user['accessToken'] ||
  //     !token.user['refreshToken']
  //   ) {
  //     return NextResponse.redirect(new URL(`/connect`, request.url));
  //   }
  //   // check if the token.user.accessToken is expired
  //   // decode the token.user.refereshToken to get the exp
  // }

  // For public and auth routes, no redirection needed
  // For example, you could add specific rules here if needed for the public or auth routes

  // Handle internationalization middleware
  const response = i18nMiddleware(request);

  return response;
}

export const config = {
  matcher: ['/((?!api|_next/static|_next/image|assets|favicon.ico).*)'],
};
