"use client";

import React, { useState } from "react";
import { useFormState } from "react-dom";
import { SignInFormState, signInWithEmailAndPassword } from "@/lib/auth/actions";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { SubmitButton } from "../ui/submit-button";
import { Sparkles } from "lucide-react";
import { useToast } from "@/components/ui/use-toast"

const initialState: SignInFormState = {};

export default function SignInForm() {
  const [state, formAction] = useFormState(signInWithEmailAndPassword, initialState);
  const { toast } = useToast();
  const [sendMagicLinkClicked, setSendMagicLinkClicked] = useState(false);

  const handleSendMagicLink = async () => {
    setSendMagicLinkClicked(true);
    toast({
      title: "Sent",
      description: "We've sent you a magic link to your email."
    });
  }

  return (
    <div className="mx-auto grid w-[350px] gap-6">
      <div className="grid gap-2 text-center">
        <h1 className="text-3xl font-bold">Connect</h1>
      </div>
      <Button variant="outline">
        <img
          src="assets/images/google-icon.svg"
          loading="lazy"
          className="w-[20px] h-[20px] m-2"
        />
        Google
      </Button>
      <Button variant="outline">
        <img alt="MetaMask"
             src="assets/images/metamask-icon.svg"
             loading="lazy"
             className="w-[20px] h-[20px] m-2" />
        Metamask
      </Button>
      <div className="text-center text-sm text-muted-foreground">or</div>
      <p className="text-balance text-muted-foreground">
        Send a magic link to your email
      </p>
      <div className="grid gap-4">
        <div className="grid gap-2">
          <Label htmlFor="email">Email</Label>
          <Input id="email" type="email" placeholder="user@example.com" required disabled={sendMagicLinkClicked} />
        </div>
        <Button disabled={sendMagicLinkClicked} className="w-full" onClick={handleSendMagicLink}>Send me the link <Sparkles /></Button>
      </div>
    </div>
  );
}
