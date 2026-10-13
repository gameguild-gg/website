import { describe, expect, it } from 'vitest';
import { isUnrecognizedChainError, requestChainSwitch } from './chain-switch';
import { COMMON_CHAINS, TEST_ADDRESS, createMockEip1193Provider } from '../testing';
import type { ChainInfo } from '../types';

const chain: ChainInfo = {
  id: '11155111',
  name: 'Sepolia Testnet',
  rpcUrls: ['https://rpc.sepolia.org'],
  nativeCurrency: { name: 'Sepolia Ether', symbol: 'ETH', decimals: 18 },
};

describe('isUnrecognizedChainError', () => {
  it('recognizes the 4902 code', () => {
    expect(isUnrecognizedChainError(Object.assign(new Error('chain'), { code: 4902 }))).toBe(true);
  });

  it('recognizes 4902 wrapped in a -32603 internal error', () => {
    expect(
      isUnrecognizedChainError(
        Object.assign(new Error('Unrecognized chain ID. Try adding the chain first.'), {
          code: -32603,
        }),
      ),
    ).toBe(true);
  });

  it('rejects unrelated errors', () => {
    expect(isUnrecognizedChainError(new Error('something else'))).toBe(false);
    expect(isUnrecognizedChainError(Object.assign(new Error('User rejected'), { code: 4001 }))).toBe(
      false,
    );
  });
});

describe('requestChainSwitch', () => {
  it('adds the chain before retrying when the wallet does not know it', async () => {
    const provider = createMockEip1193Provider();
    let attempts = 0;
    provider.onRequest((args) => {
      if (args.method === 'eth_requestAccounts') return [TEST_ADDRESS];
      if (args.method === 'eth_chainId') return '0x1';
      if (args.method === 'wallet_switchEthereumChain') {
        attempts += 1;
        if (attempts === 1) throw Object.assign(new Error('Unrecognized chain ID.'), { code: 4902 });
        return null;
      }
      return null;
    });

    await requestChainSwitch(provider, chain);

    expect(attempts).toBe(2);
    const add = provider.requests.find((request) => request.method === 'wallet_addEthereumChain');
    expect(add?.params).toEqual([
      {
        chainId: '0xaa36a7',
        chainName: 'Sepolia Testnet',
        rpcUrls: ['https://rpc.sepolia.org'],
        nativeCurrency: { name: 'Sepolia Ether', symbol: 'ETH', decimals: 18 },
        blockExplorerUrls: undefined,
      },
    ]);
  });

  it('throws user_rejected when the user dismisses the switch', async () => {
    const provider = createMockEip1193Provider();
    provider.onRequest((args) => {
      if (args.method === 'wallet_switchEthereumChain') {
        throw Object.assign(new Error('User rejected'), { code: 4001 });
      }
      return null;
    });

    await expect(requestChainSwitch(provider, COMMON_CHAINS['1'] as ChainInfo)).rejects.toMatchObject({
      code: 'user_rejected',
    });
  });

  it('throws chain_switch_failed when the wallet keeps refusing', async () => {
    const provider = createMockEip1193Provider();
    const fail = Object.assign(new Error('nope'), { code: -32002 });
    provider.onRequest((args) => {
      if (args.method === 'wallet_switchEthereumChain' || args.method === 'wallet_addEthereumChain') {
        throw fail;
      }
      return null;
    });

    await expect(requestChainSwitch(provider, chain)).rejects.toMatchObject({
      code: 'chain_switch_failed',
    });
  });

  it('uses the default mainnet chain when available', () => {
    expect(COMMON_CHAINS['1']?.name).toBe('Ethereum Mainnet');
    expect(COMMON_CHAINS['11155111']?.rpcUrls).toEqual(['https://rpc.sepolia.org']);
  });
});
