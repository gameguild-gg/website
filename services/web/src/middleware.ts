import NextAuth from "next-auth";
import createIntlMiddleware from "next-intl/middleware";
import { locales } from "@/lib/locales";
import { authConfig } from "@/auth";

export default NextAuth(authConfig).auth((request) => {
  const handleI18nRouting = createIntlMiddleware({
    localeDetection: true,
    localePrefix: "as-needed",
    defaultLocale: "en",
    locales: locales
  });
  // TODO: Handle the request here.
  console.log(`ROUTE: ${request.nextUrl.pathname}`);
  console.log(`SIGNED-IN: ${!!request.auth}`);

  return handleI18nRouting(request);
});


// TODO: Fix the matcher.
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
    // "/((?!api|_next/static|_next/image|favicon.ico).*)",
    '/', '/(en|pt)/:path*'
  ]
};
