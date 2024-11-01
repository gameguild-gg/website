'use client';

import React, { useEffect, useState } from 'react';
import { SignInFormState, signInWithGoogle } from '@/lib/auth';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Sparkles } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';

import MetaMaskSignInButton from '@/components/others/web3/meta-mask-sign-in-button';
import { Api, AuthApi } from '@game-guild/apiclient';
import { useSearchParams } from 'next/navigation';
import { signInWithMagicLink } from '@/lib/auth/sign-in-with-magic-link';
import { getSession } from 'next-auth/react';
import { Session } from 'next-auth';
import TorusSignInButton from '@/components/others/web3/torus-sign-in-button';

export default function ConnectForm() {
  const searchParams = useSearchParams();
  let session: Session | null;
  const api = new AuthApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });
  const { toast } = useToast();
  const [sendMagicLinkClicked, setSendMagicLinkClicked] = useState(false);
  const [email, setEmail] = useState<string>('');

  async function magicLinkProcess(token: string | null) {
    if (token) {
      await signInWithMagicLink(token);
    }
    session = await getSession();
    if (session) {
      window.location.href = '/feed';
    }
  }

  //on mount
  useEffect(() => {
    const token = searchParams.get('token');
    const err = searchParams.get('error');
    // show the error in red
    if (err) toast({ title: 'Error', description: err });

    magicLinkProcess(token).then();
  }, []);

  // web3 provider web3-context
  // todo: move this following logic to actions.ts
  const handleSendMagicLink = async (): Promise<void> => {
    setSendMagicLinkClicked(true);

    const sendingToast = toast({
      title: 'Sent',
      description: 'Requesting the magic link.',
    });

    const response = await api.authControllerMagicLink({ email: email });

    if (response.status >= 400) {
      sendingToast.dismiss();
      toast({
        title: 'Error',
        description:
          'Something went wrong. Please try again or contact support. Description: ' +
          JSON.stringify(response.body),
      });
      return;
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
      <div className="flex w-full">
        <MetaMaskSignInButton />
        <TorusSignInButton />
      </div>
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
