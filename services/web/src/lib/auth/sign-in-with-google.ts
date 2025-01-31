'use server';

import { signIn } from '@/auth';

export async function signInWithGoogle() {
  await signIn('google');
}
