"use client";

import React, {useState} from "react";
import {SignInFormState, signInWithGoogle} from "@/lib/auth/actions";
import {Label} from "@/components/ui/label";
import {Input} from "@/components/ui/input";
import {Button} from "@/components/ui/button";
import {Sparkles} from "lucide-react";
import {useToast} from "@/components/ui/use-toast"

import {useSession} from "next-auth/react";
import MetaMaskSignInButton from "@/components/others/web3/meta-mask-sign-in-button";


// import { api } from "@/api";

const initialState: SignInFormState = {};

export default function SignInForm() {
  const session = useSession();

  const {toast} = useToast();
  const [sendMagicLinkClicked, setSendMagicLinkClicked] = useState(false);

  // web3 provider web3-context
  // todo: move this following logic to actions.ts
  const handleSendMagicLink = async () => {
    setSendMagicLinkClicked(true);

    const sendingToast = toast({
      title: "Sent",
      description: "We've sent you a magic link to your email."
    });

    // todo: send api request to send magic link

    sendingToast.dismiss();

    const t = toast({
      title: "Sent",
      description: "We've sent you a magic link to your email."
    });
  }

  return (
    <div className="mx-auto grid w-[350px] gap-6">
      <div className="grid gap-2 text-center">
        <h1 className="text-3xl font-bold">Connect</h1>
      </div>
      <Button variant="outline" onClick={() => signInWithGoogle()}>
        <img
          src="assets/images/google-icon.svg"
          loading="lazy"
          className="w-[20px] h-[20px] m-2"
        />
        Google
      </Button>
      <MetaMaskSignInButton/>
      <div className="text-center text-sm text-muted-foreground">or</div>
      <p className="text-balance text-muted-foreground">
        Send a magic link to your email
      </p>
      <div className="grid gap-4">
        <div className="grid gap-2">
          <Label htmlFor="email">Email</Label>
          <Input id="email" type="email" placeholder="user@example.com" required disabled={sendMagicLinkClicked}/>
        </div>
        <Button disabled={sendMagicLinkClicked} className="w-full" onClick={handleSendMagicLink}>Send me the
          link <Sparkles/></Button>
      </div>
    </div>
  );
}
