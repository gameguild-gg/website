'use client';

import React from 'react';
import { useSignInWithGoogle } from '@/components/auth';

export function SignInWithGoogleButton() {
  const signInWithGoogle = useSignInWithGoogle;
  return (
    <button type="submit" onClick={() => signInWithGoogle()}>
      Sign-in with Google
    </button>
  );
}
