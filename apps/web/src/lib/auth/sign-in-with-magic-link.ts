'use server';

import { signIn } from '@/auth';
import { redirect } from 'next/navigation';

export async function signInWithMagicLink(token: string) {
  const u = await signIn('magic-link', { token });
  if (u) {
    redirect('/feed');
  }
}
