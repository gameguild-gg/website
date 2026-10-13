/**
 * Shared `wallet_switchEthereumChain` + `wallet_addEthereumChain` fallback
 * used by every adapter (MetaMask, Torus, ...).
 */
import { toProviderError, toWeb3Error } from '../types';
import type { ChainInfo, Eip1193Provider } from '../types';
import { formatChainId } from '../lib/utils';

/** True when a failure is the "chain not added" signal in any of its wrappings. */
export const isUnrecognizedChainError = (error: unknown): boolean => {
  const providerError = toProviderError(error);
  if (providerError.code === 4902) return true;
  // Some wallets wrap the 4902 inside a -32603 internal error whose message
  // names the chain (e.g. Torus/older MetaMask builds).
  if (
    providerError.code === -32603 &&
    /unrecognized chain|chain (?:is )?not (?:yet )?(?:added|found)/i.test(
      providerError.message,
    )
  ) {
    return true;
  }
  return false;
};

/**
 * Ask `provider` to switch to `chain`, adding the chain definition first
 * when the wallet does not know it yet. Throws a `Web3Error`-shaped error
 * (`user_rejected` / `chain_switch_failed`) on failure.
 */
export async function requestChainSwitch(
  provider: Eip1193Provider,
  chain: ChainInfo,
): Promise<void> {
  const hexChainId = formatChainId(chain.id);
  try {
    await provider.request({
      method: 'wallet_switchEthereumChain',
      params: [{ chainId: hexChainId }],
    });
    return;
  } catch (error) {
    const providerError = toProviderError(error);
    if (providerError.code === 4001) {
      throw toWeb3Error(providerError, 'user_rejected');
    }
    if (!isUnrecognizedChainError(error)) {
      throw toWeb3Error(
        new Error(`Failed to switch chain: ${providerError.message}`),
        'chain_switch_failed',
      );
    }
  }
  // Chain unknown to the wallet: add it, then switch again.
  await provider.request({
    method: 'wallet_addEthereumChain',
    params: [
      {
        chainId: hexChainId,
        chainName: chain.name,
        rpcUrls: [...chain.rpcUrls],
        nativeCurrency: chain.nativeCurrency ? { ...chain.nativeCurrency } : undefined,
        blockExplorerUrls: chain.blockExplorerUrls ? [...chain.blockExplorerUrls] : undefined,
      },
    ],
  });
  try {
    await provider.request({
      method: 'wallet_switchEthereumChain',
      params: [{ chainId: hexChainId }],
    });
  } catch (error) {
    const providerError = toProviderError(error);
    throw toWeb3Error(
      providerError.code === 4001 ? providerError : new Error(`Failed to switch chain: ${providerError.message}`),
      providerError.code === 4001 ? 'user_rejected' : 'chain_switch_failed',
    );
  }
}
