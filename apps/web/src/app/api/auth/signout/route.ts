import { getToken } from 'next-auth/jwt';
import { NextResponse } from 'next/server';

// POST route handler in app directory (e.g., /app/api/[locale]/disconnect/route.ts)
export async function GET(req: Request) {
  // Get the JWT token from the request
  const token = await getToken({ req, secret: process.env.NEXTAUTH_SECRET });
  if (!token) {
    return new NextResponse('Unauthorized', { status: 401 });
  }

  const res = new NextResponse('Logged out', {
    status: 200,
  });
  res.cookies.set('authjs.session-token', '', {
    expires: new Date(0),
    path: '/',
    httpOnly: true,
  });
  res.cookies.set('authjs.csrf-token', '', {
    expires: new Date(0),
    path: '/',
    httpOnly: true,
  });
  res.cookies.set('authjs.callback-url', '', {
    expires: new Date(0),
    path: '/',
    httpOnly: true,
  });

  return res;
}
