import { describe, expect, it, vi } from 'vitest';
import {
  createMetaMaskAdapter,
  selectMetaMaskFromProviders,
} from './metamask';
import type { MetaMaskDiscoveryFn } from './metamask';
import { COMMON_CHAINS, TEST_ADDRESS, TEST_SIGNATURE, createMockEip1193Provider } from '../testing';
import type { ChainInfo, Eip1193Provider } from '../types';

const discoveryWith = (provider: Eip1193Provider): MetaMaskDiscoveryFn => async () => ({
  provider,
  source: 'eip6963',
});

describe('selectMetaMaskFromProviders', () => {
  it('prefers entries flagged isMetaMask', () => {
    const other = { isMetaMask: false, request: async () => null };
    const mask = { isMetaMask: true, request: async () => null };
    expect(selectMetaMaskFromProviders([other, mask])).toBe(mask);
  });

  it('returns undefined when nothing is MetaMask-like', () => {
    expect(selectMetaMaskFromProviders([{ isMetaMask: false, request: async () => null }])).toBeUndefined();
    expect(selectMetaMaskFromProviders([{}])).toBeUndefined();
    expect(selectMetaMaskFromProviders([])).toBeUndefined();
  });
});

describe('MetaMaskAdapter', () => {
  it('isAvailable reflects discovery results', async () => {
    const found = createMetaMaskAdapter({ discover: discoveryWith(createMockEip1193Provider()) });
    await expect(found.isAvailable()).resolves.toBe(true);

    const notFound = createMetaMaskAdapter({ discover: async () => undefined });
    await expect(notFound.isAvailable()).resolves.toBe(false);
  });

  it('isAvailable swallows discovery errors', async () => {
    const adapter = createMetaMaskAdapter({
      discover: async () => {
        throw new Error('extension crashed');
      },
    });
    await expect(adapter.isAvailable()).resolves.toBe(false);
  });

  it('connect requests accounts and the chain id', async () => {
    const provider = createMockEip1193Provider();
    const adapter = createMetaMaskAdapter({ discover: discoveryWith(provider) });

    const connection = await adapter.connect();

    expect(connection).toEqual({ address: TEST_ADDRESS, chainId: '1' });
    expect(provider.requests.map((request) => request.method)).toEqual([
      'eth_requestAccounts',
      'eth_chainId',
    ]);
  });

  it('connect maps a missing wallet to wallet_not_found', async () => {
    const adapter = createMetaMaskAdapter({ discover: async () => undefined });
    await expect(adapter.connect()).rejects.toMatchObject({ code: 'wallet_not_found' });
  });

  it('signMessage hex-encodes the UTF-8 message for personal_sign', async () => {
    const provider = createMockEip1193Provider();
    const adapter = createMetaMaskAdapter({ discover: discoveryWith(provider) });
    await adapter.connect();

    const signature = await adapter.signMessage(TEST_ADDRESS, 'Sign in to GameGuild');

    expect(signature).toBe(TEST_SIGNATURE);
    const personalSign = provider.requests.find((request) => request.method === 'personal_sign');
    expect(personalSign?.params).toEqual([
      '0x5369676e20696e20746f2047616d654775696c64',
      TEST_ADDRESS,
    ]);
  });

  it('disconnect drops the provider reference (next connect re-requests)', async () => {
    const provider = createMockEip1193Provider();
    const adapter = createMetaMaskAdapter({ discover: discoveryWith(provider) });
    await adapter.connect();
    await adapter.disconnect();

    expect(provider.requests.length).toBe(2);
    await adapter.connect();
    expect(provider.requests.length).toBe(4);
  });

  it('switchChain switches directly when the chain is known', async () => {
    const provider = createMockEip1193Provider();
    const adapter = createMetaMaskAdapter({ discover: discoveryWith(provider) });
    await adapter.connect();

    await adapter.switchChain(COMMON_CHAINS['1'] as ChainInfo);

    const methods = provider.requests.map((request) => request.method);
    expect(methods).toContain('wallet_switchEthereumChain');
    expect(methods).not.toContain('wallet_addEthereumChain');
  });

  it('switchChain falls back to wallet_addEthereumChain on 4902', async () => {
    const provider = createMockEip1193Provider();
    let switchAttempts = 0;
    provider.onRequest((args) => {
      if (args.method === 'eth_requestAccounts') return [TEST_ADDRESS];
      if (args.method === 'eth_chainId') return '0x1';
      if (args.method === 'wallet_switchEthereumChain') {
        switchAttempts += 1;
        if (switchAttempts === 1) {
          throw Object.assign(new Error('Unrecognized chain ID.'), { code: 4902 });
        }
        return null;
      }
      return null;
    });
    const adapter = createMetaMaskAdapter({ discover: discoveryWith(provider) });
    await adapter.connect();

    const sepolia = COMMON_CHAINS['11155111'] as ChainInfo;
    await adapter.switchChain(sepolia);

    const addRequest = provider.requests.find((request) => request.method === 'wallet_addEthereumChain');
    expect(addRequest?.params).toMatchObject([
      { chainId: '0xaa36a7', chainName: 'Sepolia Testnet' },
    ]);
    expect(switchAttempts).toBe(2);
  });

  it('switchChain surfaces user rejections as user_rejected', async () => {
    const provider = createMockEip1193Provider();
    provider.onRequest((args) => {
      if (args.method === 'eth_requestAccounts') return [TEST_ADDRESS];
      if (args.method === 'eth_chainId') return '0x1';
      if (args.method === 'wallet_switchEthereumChain') {
        throw Object.assign(new Error('User rejected'), { code: 4001 });
      }
      return null;
    });
    const adapter = createMetaMaskAdapter({ discover: discoveryWith(provider) });
    await adapter.connect();

    await expect(adapter.switchChain(COMMON_CHAINS['1'] as ChainInfo)).rejects.toMatchObject({
      code: 'user_rejected',
    });
  });

  it('forwards accountsChanged/chainChanged/disconnect events', async () => {
    const provider = createMockEip1193Provider();
    const adapter = createMetaMaskAdapter({ discover: discoveryWith(provider) });
    await adapter.connect();

    const onAccountsChanged = vi.fn();
    const onChainChanged = vi.fn();
    const onDisconnected = vi.fn();
    const unsubscribe = adapter.onEvent({ onAccountsChanged, onChainChanged, onDisconnected });

    provider.emit('accountsChanged', ['0xdef0000000000000000000000000000000000001']);
    provider.emit('chainChanged', '0xaa36a7');
    provider.emit('disconnect', new Error('chain reset'));

    expect(onAccountsChanged).toHaveBeenCalledWith(['0xdef0000000000000000000000000000000000001']);
    expect(onChainChanged).toHaveBeenCalledWith('11155111');
    expect(onDisconnected).toHaveBeenCalledTimes(1);

    unsubscribe();
    provider.emit('chainChanged', '0x1');
    expect(onChainChanged).toHaveBeenCalledTimes(1);
  });

  it('onEvent before connect subscribes to nothing but stays harmless', async () => {
    const provider = createMockEip1193Provider();
    const adapter = createMetaMaskAdapter({ discover: discoveryWith(provider) });
    const unsubscribe = adapter.onEvent({
      onAccountsChanged: vi.fn(),
      onChainChanged: vi.fn(),
      onDisconnected: vi.fn(),
    });
    expect(() => unsubscribe()).not.toThrow();
  });
});
