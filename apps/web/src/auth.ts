import NextAuth from 'next-auth';
import { authConfig } from '@/configs/auth.config';

export const { handlers, signIn, signOut, auth } = NextAuth({ ...authConfig });
