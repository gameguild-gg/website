import { useWeb3 } from '@/components/web3/use-web3';
import { useConnectToWallet } from '@/components/web3/use-connect-to-wallet';
import { useCallback, useEffect } from 'react';
import { getCsrfToken } from 'next-auth/react';
import { signIn } from '@/auth';
import { authApi } from '@/lib/apinest';

export function useSignInWithWeb3() {
  const { state, dispatch } = useWeb3();
  const [connectToWallet] = useConnectToWallet();

  const signInWithWeb3 = useCallback(async () => {
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
          domain: window.location.host,
          address: state.accountAddress,
          uri: window.location.origin,
          version: '1',
          chainId: chain?.chainId.toString(),
          nonce: await getCsrfToken(),
        });

        const message = response.data.message;

        // Eip1193Provider.
        const signature = await state.provider.send('personal_sign', [
          message,
          state.accountAddress,
        ]);

        await signIn('web-3', { message, signature });
      }
    };

    tryToSignIn();
  }, [state, dispatch]);

  return [signInWithWeb3];
}
