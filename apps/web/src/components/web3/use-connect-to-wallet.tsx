import { BrowserProvider } from 'ethers';

import { useWeb3 } from '@/components/web3/use-web3';

export function useConnectToWallet() {
  const { dispatch } = useWeb3();

  const connectToWallet = async () => {
    dispatch({ type: 'CONNECT_TO_PROVIDER_INITIAL' });

    try {
      if (window.ethereum) {
        const ethereum = window.ethereum;

        await ethereum.request({ method: 'eth_requestAccounts' });

        const provider = new BrowserProvider(window.ethereum);

        const signer = await provider.getSigner();
        const accountAddress = await signer.getAddress();

        dispatch({
          type: 'CONNECT_TO_PROVIDER_SUCCESS',
          payload: { provider, accountAddress },
        });
      } else {
        dispatch({
          type: 'CONNECT_TO_PROVIDER_FAILURE',
          payload: { error: 'MetaMask is not detected.' },
        });
      }
    } catch (error) {
      dispatch({
        type: 'CONNECT_TO_PROVIDER_FAILURE',
        payload: { error: (error as Error).message },
      });
    }
  };

  return [connectToWallet];
}
