'use client';

import { Button } from '@/components/ui/button';
import React from 'react';

import { useSignInWithWeb3, Web3ProviderChoice } from '@/components/web3/use-sign-in-with-web3';

export default function MetaMaskSignInButton() {
  const [signInWithWeb3] = useSignInWithWeb3(Web3ProviderChoice.METAMASK);

  return (
    <Button variant="outline" onClick={signInWithWeb3} className="flex-1">
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
