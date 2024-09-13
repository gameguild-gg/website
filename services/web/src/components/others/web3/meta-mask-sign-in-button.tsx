'use client';

import { Button } from '@/components/ui/button';
import React from 'react';

import { useSignInWithWeb3 } from '@/components/web3/use-sign-in-with-web3';
import { getSession } from 'next-auth/react';

export default function MetaMaskSignInButton() {
  const [signInWithWeb3] = useSignInWithWeb3();

  const signInAndRedirectIfSucceed = async () => {
    const session = await getSession();
    if (!session) await signInWithWeb3();
    if (session) window.location.replace('/feed');
  };

  return (
    <Button variant="outline" onClick={signInAndRedirectIfSucceed}>
      <img
        alt="MetaMask"
        src="/assets/images/metamask-icon.svg"
        loading="lazy"
        className="w-[20px] h-[20px] m-2"
      />
      Metamask
    </Button>
  );
}
