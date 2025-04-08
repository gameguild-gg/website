import createIntlMiddleware from 'next-intl/middleware';
import { locales } from '@/data/locales';
import { NextRequest, NextResponse } from 'next/server';

// Create the i18n middleware
const i18nMiddleware = createIntlMiddleware({
  locales: locales,
  localeDetection: true,
  localePrefix: 'as-needed',
  defaultLocale: 'en',
});

// Common auth cookies to check for authentication
const AUTH_COOKIES = [
  'authjs.session-token',
  'next-auth.session-token',
  '__Secure-next-auth.session-token'
];

// All auth cookies to clear on logout
const ALL_AUTH_COOKIES = [
  // Auth.js cookies
  'authjs.session-token',
  'authjs.csrf-token',
  'authjs.callback-url',
  'authjs.refresh-token',
  'authjs.pkce.code_verifier',
  'authjs.pkce.state',
  // Next-auth cookies
  'next-auth.session-token',
  'next-auth.csrf-token',
  'next-auth.callback-url',
  '__Secure-next-auth.session-token',
  '__Secure-next-auth.callback-url',
  '__Secure-next-auth.csrf-token',
  '__Host-next-auth.csrf-token',
  // Additional cookies that might be used
  'session',
  'token',
  'user'
];

// Cache control headers for no-cache responses
const NO_CACHE_HEADERS = {
  'Cache-Control': 'no-store, no-cache, must-revalidate, proxy-revalidate',
  'Pragma': 'no-cache',
  'Expires': '0',
  'Surrogate-Control': 'no-store'
};

// CORS headers for API routes
const CORS_HEADERS = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type, Authorization',
  'Cross-Origin-Resource-Policy': 'cross-origin'
};

// Wasmer headers
const WASMER_HEADERS = {
  'Cross-Origin-Opener-Policy': 'same-origin',
  'Cross-Origin-Embedder-Policy': 'credentialless',
  'Cross-Origin-Resource-Policy': 'cross-origin'
};

export async function middleware(request: NextRequest) {
  const { pathname, searchParams } = request.nextUrl;
  
  // Handle content negotiation for compressed static files
  if (pathname.match(/\.(js|css|html|svg|json|wasm|jpg|png|ttf|otf|woff|woff2)$/)) {
    const acceptEncoding = request.headers.get('Accept-Encoding') || '';
    const response = NextResponse.next();
    
    // Set Vary header to inform caches that response depends on Accept-Encoding
    response.headers.set('Vary', 'Accept-Encoding');
    
    // If client accepts brotli and .br version exists, serve it
    if (acceptEncoding.includes('br')) {
      response.headers.set('Content-Encoding', 'br');
    } 
    // Otherwise, if client accepts gzip and .gz version exists, serve it
    else if (acceptEncoding.includes('gzip')) {
      response.headers.set('Content-Encoding', 'gzip');
    }
    
    return response;
  }
  
  // Domain redirect handling
  const oldDomains = ['web.gameguild.gg', 'gamedevguild.org'];
  const newDomain = 'gameguild.gg';
  const host = request.headers.get('X-Request-Domain') || 
               request.headers.get('x-forwarded-host') || 
               request.headers.get('host') || 
               request.nextUrl.hostname;

  // Redirect from old domains to new domain
  if (host && oldDomains.includes(host) && request.method === 'GET') {
    const newUrl = new URL(request.url);
    newUrl.hostname = newDomain;
    newUrl.port = '';
    return NextResponse.redirect(newUrl, 308);
  }

  // Skip middleware for API routes
  if (pathname.startsWith('/api/auth') || pathname.startsWith('/api/version')) {
    return NextResponse.next();
  }

  // Handle disconnect page
  if (pathname.startsWith('/disconnect')) {
    // Check if user is authenticated
    const hasAuthCookies = AUTH_COOKIES.some(cookieName => request.cookies.has(cookieName));
    
    // If not authenticated, redirect to root
    if (!hasAuthCookies) {
      return NextResponse.redirect(new URL('/', request.url));
    }
    
    const response = NextResponse.next();
    
    // Add CORS headers
    Object.entries(CORS_HEADERS).forEach(([key, value]) => {
      response.headers.set(key, value);
    });
    
    // Clear all auth cookies
    ALL_AUTH_COOKIES.forEach(cookieName => {
      response.cookies.set(cookieName, '', {
        expires: new Date(0),
        path: '/',
        httpOnly: true,
      });
    });
    
    return response;
  }

  // Handle pages with cache busting
  if ((pathname === '/' || pathname.startsWith('/connect')) && searchParams.has('cb')) {
    const response = NextResponse.next();
    
    // Add no-cache headers
    Object.entries(NO_CACHE_HEADERS).forEach(([key, value]) => {
      response.headers.set(key, value);
    });
    
    return response;
  }

  // Handle Wasmer page
  if (pathname.startsWith('/wasmer')) {
    const response = NextResponse.next();
    
    // Add Wasmer headers
    Object.entries(WASMER_HEADERS).forEach(([key, value]) => {
      response.headers.set(key, value);
    });
    
    // Add CORS headers to allow external scripts
    response.headers.set('Access-Control-Allow-Origin', '*');
    
    return response;
  }

  // Handle internationalization
  const i18nResponse = await i18nMiddleware(request);
  if (i18nResponse) {
    return i18nResponse;
  }

  // Default response
  return NextResponse.next();
}

export const config = {
  matcher: [
    // Include all paths except static assets
    '/((?!_next/static|_next/image|assets|favicon.ico).*)',
    // Include API routes that need special handling
    '/api/auth/:path*',
    '/api/version'
  ],
};
