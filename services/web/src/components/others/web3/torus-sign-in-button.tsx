'use client';

import { Button } from '@/components/ui/button';
import React from 'react';

export default function TorusSignInButton() {
  // const [signInWithWeb3] = useSignInWithWeb3();

  // const signInAndRedirectIfSucceed = async () => {
  //   const session = await getSession();
  //   if (!session) await signInWithWeb3();
  //   if (session) window.location.replace('/feed');
  // };

  return (
    <Button
      variant="outline"
      onClick={() => {
        alert(
          'Torus is not supported yet! Help us develop this feature! Talk to us on Discord!',
        );
      }}
      className="flex-1"
    >
      <img
        alt="Torus"
        src="/assets/images/torus-icon.svg"
        loading="lazy"
        className="w-[20px] h-[20px] m-2"
      />
      Torus
    </Button>
  );
}
