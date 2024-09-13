'use client';

import { useWeb3 } from '@/components/web3/use-web3';
import { useConnectToWallet } from '@/components/web3/use-connect-to-wallet';
import { useCallback, useEffect } from 'react';
import { signIn } from '@/auth';
import { authApi } from '@/lib/apinest';
import { signInWithWeb3 } from '@/lib/auth/sign-in-with-web3';
import { getSession } from 'next-auth/react';

export function useSignInWithWeb3() {
  const { state, dispatch } = useWeb3();
  const [connectToWallet] = useConnectToWallet();

  const signIn = useCallback(async () => {
    if (!state.provider) {
      await connectToWallet();
    }
  }, [state.provider, connectToWallet]);

  useEffect(() => {
    const tryToSignIn = async () => {
      if (state.provider && state.accountAddress) {
        // TODO: validate the chain id.
        const chain = await state.provider.getNetwork();

        const response = await authApi.authControllerGetWeb3SignInChallenge({
          address: state.accountAddress,
        });

        const message = response.data.message;

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
          window.location.replace('/feed');
        }
      }
    };

    tryToSignIn();
  }, [state, dispatch]);

  return [signIn];
}
