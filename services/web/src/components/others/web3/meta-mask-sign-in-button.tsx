'use client';

import {Button} from "@/components/ui/button";
import React from "react";
import {useSignInWithWeb3} from "@/components/web3/web3-context";


export default function MetaMaskSignInButton() {
  const [signInWithWeb3] = useSignInWithWeb3();

  return (
    <Button variant="outline" onClick={signInWithWeb3}>
      <img alt="MetaMask" src="assets/images/metamask-icon.svg" loading="lazy" className="w-[20px] h-[20px] m-2"/>
      Metamask
    </Button>
  )
}