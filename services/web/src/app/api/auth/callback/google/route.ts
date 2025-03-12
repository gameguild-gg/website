import { NextRequest } from 'next/server';
import { handlers } from '@/auth';

// Use the Next.js Auth.js handler for Google callback
export const GET = handlers.GET; 