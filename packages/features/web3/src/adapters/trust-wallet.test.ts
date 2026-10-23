import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  buildTrustWalletDeepLink,
  createTrustWalletAdapter,
  discoverTrustWallet,
  isMobileBrowser,
  selectTrustWalletFromProviders,
} from './trust-wallet';
import { authenticateWithSiwe } from '../lib/siwe';
import { COMMON_CHAINS, TEST_ADDRESS, TEST_SIGNATURE, createMockEip1193Provider } from '../testing';
import type { Eip6963ProviderDetail } from './metamask';
import type { Eip1193Provider } from '../types';

const MOBILE_UA = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) Mobile/15E148 Safari/604.1';
const DESKTOP_UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/126.0';

interface WindowState {
  ethereum?: unknown;
  trustwallet?: unknown;
  userAgent?: string;
}

const installWindow = (state: WindowState): void => {
  Object.defineProperty(window, 'ethereum', {
    configurable: true,
    writable: true,
    value: state.ethereum,
  });
  Object.defineProperty(window, 'trustwallet', {
    configurable: true,
    writable: true,
    value: state.trustwallet,
  });
  Object.defineProperty(window.navigator, 'userAgent', {
    configurable: true,
    get: () => state.userAgent ?? DESKTOP_UA,
  });
};

const restoreWindow = (): void => {
  delete (window as { ethereum?: unknown }).ethereum;
  delete (window as { trustwallet?: unknown }).trustwallet;
  delete (window.navigator as { userAgent?: string }).userAgent;
};

const announce = (rdns: string, provider: Eip1193Provider): void => {
  const detail: Eip6963ProviderDetail = {
    info: { uuid: 'uuid-1', name: rdns, icon: 'data:image/png;base64,==', rdns },
    provider,
  };
  window.dispatchEvent(new CustomEvent('eip6963:announceProvider', { detail }));
};

const flaggedTrustProvider = (provider: Eip1193Provider) => ({
  isTrust: true,
  request: provider.request,
  on: provider.on,
  removeListener: provider.removeListener,
});

describe('trust wallet deep-link helpers', () => {
  it('builds the universal link with the EVM coin id and encoded page url', () => {
    expect(buildTrustWalletDeepLink('https://gameguild.gg/sign-in?x=1 2')).toBe(
      'https://link.trustwallet.com/open_url?coin_id=60&url=https%3A%2F%2Fgameguild.gg%2Fsign-in%3Fx%3D1%202',
    );
  });

  it('classifies mobile user agents', () => {
    expect(isMobileBrowser(MOBILE_UA)).toBe(true);
    expect(isMobileBrowser(DESKTOP_UA)).toBe(false);
  });
});

describe('selectTrustWalletFromProviders', () => {
  it('picks the isTrust-flagged entry out of a multi-wallet array', () => {
    const trust = { ...flaggedTrustProvider(createMockEip1193Provider()) };
    const other = { isMetaMask: true, request: async () => null };
    expect(selectTrustWalletFromProviders([other, trust])).toBe(trust);
  });

  it('returns undefined when no entry is Trust-flagged', () => {
    expect(selectTrustWalletFromProviders([{ isMetaMask: true, request: async () => null }])).toBeUndefined();
  });
});

describe('discoverTrustWallet', () => {
  afterEach(restoreWindow);

  it('returns undefined with no injected provider on desktop', async () => {
    installWindow({});
    await expect(discoverTrustWallet()).resolves.toBeUndefined();
  });

  it('detects the EIP-6963 announcement with the Trust rdns', async () => {
    const provider = createMockEip1193Provider();
    installWindow({});
    setTimeout(() => announce('com.trustwallet.app', provider), 0);
    await expect(discoverTrustWallet()).resolves.toMatchObject({
      source: 'eip6963',
      provider,
    });
  });

  it('detects the dedicated window.trustwallet object (in-app browser / extension)', async () => {
    const provider = createMockEip1193Provider();
    installWindow({ trustwallet: flaggedTrustProvider(provider) });
    await expect(discoverTrustWallet()).resolves.toMatchObject({ source: 'window.trustwallet' });
  });

  it('detects a flagged window.ethereum and a providers[] entry', async () => {
    const provider = createMockEip1193Provider();
    installWindow({ ethereum: flaggedTrustProvider(provider) });
    await expect(discoverTrustWallet()).resolves.toMatchObject({ source: 'window.ethereum' });

    const trustEntry = flaggedTrustProvider(createMockEip1193Provider());
    installWindow({
      ethereum: {
        // The outer multi-wallet provider must never answer account calls.
        request: async () => {
          throw new Error('wrong provider answered');
        },
        providers: [{ isMetaMask: true, request: async () => null }, trustEntry],
      },
    });
    const discovery = await discoverTrustWallet();
    expect(discovery?.source).toBe('window.ethereum');
    // The discovered provider delegates to the Trust-flagged providers[]
    // entry, not the outer multi-wallet object (discovery wraps the entry,
    // so assert delegation rather than function identity).
    await expect(discovery?.provider.request({ method: 'eth_accounts' })).resolves.toEqual([
      TEST_ADDRESS,
    ]);
  });
});

describe('TrustWalletAdapter', () => {
  afterEach(restoreWindow);

  it('connects through the injected provider and returns the identity used for the challenge', async () => {
    const provider = createMockEip1193Provider();
    installWindow({ trustwallet: flaggedTrustProvider(provider) });
    const adapter = createTrustWalletAdapter();

    await expect(adapter.isAvailable()).resolves.toBe(true);
    await expect(adapter.connect()).resolves.toEqual({ address: TEST_ADDRESS, chainId: '1' });
    expect(provider.requests[0]?.method).toBe('eth_requestAccounts');
  });

  it('is unavailable on desktop without the wallet and reports wallet_not_found', async () => {
    installWindow({});
    const adapter = createTrustWalletAdapter({ deepLink: { enabled: false } });

    await expect(adapter.isAvailable()).resolves.toBe(false);
    await expect(adapter.connect()).rejects.toMatchObject({ code: 'wallet_not_found' });
  });

  it('propagates account prompt rejection raw (context maps it to user_rejected)', async () => {
    const provider = createMockEip1193Provider();
    provider.failNextRequest(Object.assign(new Error('User rejected'), { code: 4001 }));
    installWindow({ trustwallet: flaggedTrustProvider(provider) });
    const adapter = createTrustWalletAdapter();

    await expect(adapter.connect()).rejects.toMatchObject({ code: 4001 });
  });

  it('covers connection failure when no account is returned', async () => {
    const provider = createMockEip1193Provider({ accounts: [] });
    installWindow({ trustwallet: flaggedTrustProvider(provider) });
    const adapter = createTrustWalletAdapter();

    await expect(adapter.connect()).rejects.toMatchObject({ code: 'connection_failed' });
  });

  it('signs SIWE messages with a hex-encoded personal_sign payload', async () => {
    const provider = createMockEip1193Provider();
    installWindow({ trustwallet: flaggedTrustProvider(provider) });
    const adapter = createTrustWalletAdapter();
    await adapter.connect();

    await expect(adapter.signMessage(TEST_ADDRESS, 'Sign in to GameGuild')).resolves.toBe(TEST_SIGNATURE);
    expect(provider.requests.find((r) => r.method === 'personal_sign')?.params).toEqual([
      '0x5369676e20696e20746f2047616d654775696c64',
      TEST_ADDRESS,
    ]);
  });

  it('covers signing failure: provider errors propagate raw for the context taxonomy', async () => {
    const provider = createMockEip1193Provider();
    installWindow({ trustwallet: flaggedTrustProvider(provider) });
    const adapter = createTrustWalletAdapter();
    await adapter.connect();
    // Armed after connect so the failure targets the personal_sign request.
    provider.failNextRequest(Object.assign(new Error('sign transport closed'), { code: -32603 }));

    await expect(adapter.signMessage(TEST_ADDRESS, 'msg')).rejects.toMatchObject({
      code: -32603,
      message: 'sign transport closed',
    });
  });

  it('integrates with the common wallet authentication flow end to end (challenge → sign → verify)', async () => {
    const provider = createMockEip1193Provider();
    installWindow({ trustwallet: flaggedTrustProvider(provider) });
    const adapter = createTrustWalletAdapter();
    const connection = await adapter.connect();

    const getChallenge = vi.fn(async () => ({
      challenge: 'GameGuild wants you to sign in',
      nonce: 'nonce-1',
    }));
    const verify = vi.fn(async () => undefined);
    const authentication = await authenticateWithSiwe(adapter, connection, { getChallenge, verify });

    expect(getChallenge).toHaveBeenCalledWith({ walletAddress: TEST_ADDRESS, chainId: '1' });
    expect(verify).toHaveBeenCalledWith(
      expect.objectContaining({
        walletAddress: TEST_ADDRESS,
        chainId: '1',
        challenge: 'GameGuild wants you to sign in',
        nonce: 'nonce-1',
        signature: TEST_SIGNATURE,
      }),
    );
    expect(authentication.signature).toBe(TEST_SIGNATURE);
  });

  it('deep-links mobile browsers without a provider into the Trust in-app browser', async () => {
    installWindow({ userAgent: MOBILE_UA });
    const navigate = vi.fn();
    const adapter = createTrustWalletAdapter({
      deepLink: { enabled: true, getCurrentUrl: () => 'https://gameguild.gg/sign-in', navigate },
    });

    await expect(adapter.isAvailable()).resolves.toBe(true);
    await expect(adapter.connect()).rejects.toMatchObject({ code: 'connection_failed' });
    expect(navigate).toHaveBeenCalledWith(
      'https://link.trustwallet.com/open_url?coin_id=60&url=https%3A%2F%2Fgameguild.gg%2Fsign-in',
    );
  });

  it('does not deep-link on desktop when the wallet is simply absent', async () => {
    installWindow({});
    const navigate = vi.fn();
    const adapter = createTrustWalletAdapter({ deepLink: { navigate } });

    await expect(adapter.isAvailable()).resolves.toBe(false);
    await expect(adapter.connect()).rejects.toMatchObject({ code: 'wallet_not_found' });
    expect(navigate).not.toHaveBeenCalled();
  });

  it('switches chains with the shared add-chain fallback', async () => {
    const provider = createMockEip1193Provider();
    let switchAttempts = 0;
    provider.onRequest((args) => {
      if (args.method === 'eth_requestAccounts') return [TEST_ADDRESS];
      if (args.method === 'eth_chainId') return '0x1';
      if (args.method === 'wallet_switchEthereumChain') {
        switchAttempts += 1;
        if (switchAttempts === 1) throw Object.assign(new Error('Unrecognized chain ID.'), { code: 4902 });
        return null;
      }
      return null;
    });
    installWindow({ trustwallet: flaggedTrustProvider(provider) });
    const adapter = createTrustWalletAdapter();
    await adapter.connect();

    await adapter.switchChain(COMMON_CHAINS['11155111'] as never);

    expect(provider.requests.map((r) => r.method)).toContain('wallet_addEthereumChain');
    expect(switchAttempts).toBe(2);
  });

  it('disconnect drops the provider so the next connect rediscovers', async () => {
    const provider = createMockEip1193Provider();
    installWindow({ trustwallet: flaggedTrustProvider(provider) });
    const adapter = createTrustWalletAdapter();
    await adapter.connect();
    await adapter.disconnect();

    expect(provider.requests).toHaveLength(2); // only the first connect's calls

    await adapter.connect();
    expect(provider.requests).toHaveLength(4);
  });

  it('forwards account, network and disconnect events while connected', async () => {
    const provider = createMockEip1193Provider();
    installWindow({ trustwallet: flaggedTrustProvider(provider) });
    const adapter = createTrustWalletAdapter();
    await adapter.connect();

    const onAccountsChanged = vi.fn();
    const onChainChanged = vi.fn();
    const onDisconnected = vi.fn();
    const unsubscribe = adapter.onEvent({ onAccountsChanged, onChainChanged, onDisconnected });

    provider.emit('accountsChanged', [TEST_ADDRESS]);
    provider.emit('chainChanged', '0x1');
    provider.emit('disconnect', new Error('extension removed'));

    expect(onAccountsChanged).toHaveBeenCalledWith([TEST_ADDRESS]);
    expect(onChainChanged).toHaveBeenCalledWith('1');
    expect(onDisconnected).toHaveBeenCalledTimes(1);
    unsubscribe();
  });
});
