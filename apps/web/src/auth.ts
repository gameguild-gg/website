import NextAuth from 'next-auth';
import { authConfig } from '@/config/auth.config';

// auth is serverside only. if you are using this in a client component
export const { handlers, signIn, signOut, auth } = NextAuth({ ...authConfig });
