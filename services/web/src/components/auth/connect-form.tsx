'use client';

import React, { useState } from 'react';
import { SignInFormState, signInWithGoogle } from '@/lib/auth';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Sparkles } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';

import { useSession } from 'next-auth/react';
import MetaMaskSignInButton from '@/components/others/web3/meta-mask-sign-in-button';
import { authApi } from '@/lib/apinest';
import { OkDto } from '@game-guild/apiclient';
import TorusSignInButton from '@/components/others/web3/torus-sign-in-button';

const initialState: SignInFormState = {};

export default function ConnectForm() {
  // const session = useSession();

  const { toast } = useToast();
  const [sendMagicLinkClicked, setSendMagicLinkClicked] = useState(false);

  const [email, setEmail] = useState<string>('');

  // web3 provider web3-context
  // todo: move this following logic to actions.ts
  const handleSendMagicLink = async (): Promise<void> => {
    setSendMagicLinkClicked(true);

    const sendingToast = toast({
      title: 'Sent',
      description: 'Requesting the magic link.',
    });

    let response: OkDto;
    try {
      response = (await authApi.authControllerMagicLink({ email: email })).data;
    } catch (error) {
      sendingToast.dismiss();
      toast({
        title: 'Error',
        description:
          'Something went wrong. Please try again or contact support. Description: ' +
          JSON.stringify(error),
      });
      return;
    }

    if (!response) {
      toast({
        title: 'Error',
        description:
          'Something went wrong. Please try again or contact support.',
      });
    }

    toast({
      title: 'Sent',
      description:
        "We've sent you a magic link to your email. Please go check it.",
    });
  };

  return (
    <div className="mx-auto grid w-[350px] gap-6">
      <div className="grid gap-2 text-center">
        <h1 className="text-3xl font-bold">Connect</h1>
      </div>
      <Button variant="outline" onClick={() => signInWithGoogle()}>
        <img
          src="/assets/images/google-icon.svg"
          loading="lazy"
          className="w-[20px] h-[20px] m-2"
        />
        Google
      </Button>
      <MetaMaskSignInButton />
      <TorusSignInButton />
      <div className="text-center text-sm text-muted-foreground">or</div>
      <p className="text-balance text-muted-foreground">
        Send a magic link to your email
      </p>
      <div className="grid gap-4">
        <div className="grid gap-2">
          <Label htmlFor="email">Email</Label>
          <Input
            id="email"
            type="email"
            placeholder="user@example.com"
            required
            disabled={sendMagicLinkClicked}
            onChange={(e) => setEmail(e.target.value)}
          />
        </div>
        <Button
          disabled={sendMagicLinkClicked}
          className="w-full"
          onClick={handleSendMagicLink}
        >
          Send me the link <Sparkles />
        </Button>
      </div>
    </div>
  );
}
