import NextAuth from "next-auth"
import { authConfig } from '@/config/auth.config';

export const {handlers, signIn, signOut, auth} = NextAuth({...authConfig});
