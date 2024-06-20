'use server';

import { signIn } from '@/auth';

// todo: we just need the signature, all other data is derived from the signature.
export async function signInWithWeb3(
  message: string,
  signature: string,
  address: string,
) {
  await signIn('web-3', { message, signature, address });
}
