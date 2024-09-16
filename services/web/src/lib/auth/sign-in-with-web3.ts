'use server';

import {signIn} from '@/auth';
import {redirect} from 'next/navigation';

// todo: we just need the signature, all other data is derived from the signature.
export async function signInWithWeb3(signature: string, address: string) {
  const u = await signIn('web-3', {signature, address});
  if (u) redirect('/feed');
}
