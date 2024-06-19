'use server';

import { signIn } from '@/auth';

export async function signInWithWeb3(message: string, signature: string) {
  await signIn('web-3', { message, signature });
}

