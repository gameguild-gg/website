'use client';

import { useWeb3 } from '@/components/web3/use-web3';
import { useConnectToWallet } from '@/components/web3/use-connect-to-wallet';
import { useCallback, useEffect } from 'react';
import { signInWithWeb3 } from '@/lib/auth/sign-in-with-web3';
import { getSession } from 'next-auth/react';
import { AuthApi } from '@game-guild/apiclient';
import { redirect } from 'next/navigation';

export function useSignInWithWeb3() {
  const api = new AuthApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  const { state, dispatch } = useWeb3();
  const [connectToWallet] = useConnectToWallet();

  const signIn = useCallback(async () => {
    if (!state.provider) {
      await connectToWallet();
    }
  }, [state.provider, connectToWallet]);

  useEffect(() => {
    const tryToSignIn = async () => {
      try {
        if (state.provider && state.accountAddress) {
          // TODO: validate the chain id.
          const chain = await state.provider.getNetwork();

          const response = await api.authControllerGetWeb3SignInChallenge({
            address: state.accountAddress,
          });

          const message = response.message;

          // Eip1193Provider.
          const signature = await state.provider.send('personal_sign', [
            message,
            state.accountAddress,
          ]);

          await signInWithWeb3(signature, state.accountAddress);

          // todo: move this elsewhere!!
          // the await on the signInAndRedirectIfSucceed on metamask-sign-in-button.tsx is not working
          const session = await getSession();
          if (session) {
            window.location.href = '/feed';
          }
        }
      } catch (error) {
        console.error(error);
      }
    };

    tryToSignIn();
  }, [state, dispatch]);

  return [signIn];
}
