import { describe, expect, it, vi } from 'vitest';
import { createWalletConnectAdapter } from './walletconnect';
import type { WalletConnectEthereumProviderLike } from './walletconnect';
import { authenticateWithSiwe } from '../lib/siwe';
import { COMMON_CHAINS, TEST_ADDRESS, TEST_SIGNATURE, createMockEip1193Provider } from '../testing';
import type { Eip1193Provider, Eip1193RequestArguments, SiweTransport } from '../types';

const PROJECT_ID = 'gg-test-project-id';

interface ProviderHarness {
  readonly provider: WalletConnectEthereumProviderLike & {
    emit(event: string, ...args: unknown[]): void;
  };
  readonly connect: ReturnType<typeof vi.fn>;
  readonly disconnect: ReturnType<typeof vi.fn>;
  readonly events: Map<string, Set<(...args: never[]) => void>>;
}

const createProviderHarness = (
  eip1193: Eip1193Provider = createMockEip1193Provider(),
): ProviderHarness => {
  const events = new Map<string, Set<(...args: never[]) => void>>();
  const connect = vi.fn(async () => undefined);
  const disconnect = vi.fn(async () => undefined);
  const provider = {
    request: (args: Eip1193RequestArguments) => eip1193.request(args),
    on: (event: string, listener: (...args: never[]) => void) => {
      const set = events.get(event) ?? new Set();
      set.add(listener);
      events.set(event, set);
    },
    removeListener: (event: string, listener: (...args: never[]) => void) => {
      events.get(event)?.delete(listener);
    },
    connect,
    disconnect,
    emit(event: string, ...args: unknown[]) {
      for (const listener of events.get(event) ?? []) {
        (listener as (...a: unknown[]) => void)(...args);
      }
    },
  };
  return { provider, connect, disconnect, events };
};

describe('WalletConnectAdapter (fail-closed gate)', () => {
  it('is unavailable and fails closed when the deployment gate is off', async () => {
    const harness = createProviderHarness();
    const adapter = createWalletConnectAdapter({
      projectId: PROJECT_ID,
      loadProvider: async () => harness.provider,
    });

    await expect(adapter.isAvailable()).resolves.toBe(false);
    await expect(adapter.connect()).rejects.toMatchObject({ code: 'wallet_not_found' });
    expect(harness.connect).not.toHaveBeenCalled();
  });

  it('is unavailable and fails closed without a project id', async () => {
    const harness = createProviderHarness();
    const adapter = createWalletConnectAdapter({
      enabled: true,
      loadProvider: async () => harness.provider,
    });

    await expect(adapter.isAvailable()).resolves.toBe(false);
    await expect(adapter.connect()).rejects.toMatchObject({ code: 'wallet_not_found' });
  });

  it('is available when enabled with a project id', async () => {
    const harness = createProviderHarness();
    const adapter = createWalletConnectAdapter({
      enabled: true,
      projectId: PROJECT_ID,
      loadProvider: async () => harness.provider,
    });

    await expect(adapter.isAvailable()).resolves.toBe(true);
  });

  it('fails closed when the SDK cannot be loaded, and a retry may succeed', async () => {
    let fail = true;
    const harness = createProviderHarness();
    const adapter = createWalletConnectAdapter({
      enabled: true,
      projectId: PROJECT_ID,
      loadProvider: async () => {
        if (fail) throw new Error('Cannot find module');
        return harness.provider;
      },
    });

    await expect(adapter.connect()).rejects.toMatchObject({ code: 'wallet_not_found' });

    fail = false;
    await expect(adapter.connect()).resolves.toEqual({ address: TEST_ADDRESS, chainId: '1' });
  });
});

describe('WalletConnectAdapter (pairing and session)', () => {
  it('establishes the session through the pairing flow and returns the identity', async () => {
    const harness = createProviderHarness();
    const adapter = createWalletConnectAdapter({
      enabled: true,
      projectId: PROJECT_ID,
      loadProvider: async () => harness.provider,
    });

    await expect(adapter.connect()).resolves.toEqual({ address: TEST_ADDRESS, chainId: '1' });
    expect(harness.connect).toHaveBeenCalledTimes(1);
  });

  it('exposes the pairing URI emitted during pairing (display_uri)', async () => {
    const harness = createProviderHarness();
    const adapter = createWalletConnectAdapter({
      enabled: true,
      projectId: PROJECT_ID,
      loadProvider: async () =>
        Object.assign(harness.provider, {
          connect: async () => {
            harness.provider.emit('display_uri', 'wc:pairing-uri-v2?a=1');
          },
        }),
    });

    expect(adapter.getPairingUri()).toBeUndefined();
    await adapter.connect();

    expect(adapter.getPairingUri()).toBe('wc:pairing-uri-v2?a=1');
  });

  it('does not re-pair for later operations on the established session', async () => {
    const harness = createProviderHarness();
    const adapter = createWalletConnectAdapter({
      enabled: true,
      projectId: PROJECT_ID,
      loadProvider: async () => harness.provider,
    });

    await adapter.connect();
    await adapter.signMessage(TEST_ADDRESS, 'msg');

    expect(harness.connect).toHaveBeenCalledTimes(1);
  });

  it('maps pairing rejection to user_rejected and resets the pairing state', async () => {
    const harness = createProviderHarness();
    harness.connect.mockRejectedValue(Object.assign(new Error('User rejected the request.'), { code: 4001 }));
    const adapter = createWalletConnectAdapter({
      enabled: true,
      projectId: PROJECT_ID,
      loadProvider: async () => harness.provider,
    });

    await expect(adapter.connect()).rejects.toMatchObject({ code: 'user_rejected' });

    // The failed pairing is discarded; the next attempt pairs again.
    harness.connect.mockResolvedValue(undefined);
    await adapter.connect();
    expect(harness.connect).toHaveBeenCalledTimes(2);
  });

  it('covers session establishment errors beyond rejection as connection_failed', async () => {
    const harness = createProviderHarness();
    harness.connect.mockRejectedValue(new Error('Relay reachability failed'));
    const adapter = createWalletConnectAdapter({
      enabled: true,
      projectId: PROJECT_ID,
      loadProvider: async () => harness.provider,
    });

    await expect(adapter.connect()).rejects.toMatchObject({ code: 'connection_failed' });
  });

  it('disconnect deletes the relay session and forces a fresh pairing next time', async () => {
    const harness = createProviderHarness();
    const adapter = createWalletConnectAdapter({
      enabled: true,
      projectId: PROJECT_ID,
      loadProvider: async () => harness.provider,
    });

    await adapter.connect();
    await adapter.disconnect();

    expect(harness.disconnect).toHaveBeenCalledTimes(1);

    harness.connect.mockClear();
    await adapter.connect();
    expect(harness.connect).toHaveBeenCalledTimes(1);
  });
});

describe('WalletConnectAdapter (challenge signing and verification)', () => {
  it('forwards the server challenge for signing with a hex personal_sign payload', async () => {
    const eip1193 = createMockEip1193Provider();
    const harness = createProviderHarness(eip1193);
    const adapter = createWalletConnectAdapter({
      enabled: true,
      projectId: PROJECT_ID,
      loadProvider: async () => harness.provider,
    });
    await adapter.connect();

    await expect(adapter.signMessage(TEST_ADDRESS, 'Sign in to GameGuild')).resolves.toBe(
      TEST_SIGNATURE,
    );
    expect(eip1193.requests.find((r) => r.method === 'personal_sign')?.params).toEqual([
      '0x5369676e20696e20746f2047616d654775696c64',
      TEST_ADDRESS,
    ]);
  });

  it('covers signing failure: provider errors propagate raw for the context taxonomy', async () => {
    const eip1193 = createMockEip1193Provider();
    eip1193.failNextRequest(Object.assign(new Error('session request rejected'), { code: 4001 }));
    const harness = createProviderHarness(eip1193);
    const adapter = createWalletConnectAdapter({
      enabled: true,
      projectId: PROJECT_ID,
      loadProvider: async () => harness.provider,
    });
    await adapter.connect();

    await expect(adapter.signMessage(TEST_ADDRESS, 'msg')).rejects.toMatchObject({
      code: 4001,
      message: 'session request rejected',
    });
  });

  it('maps a rejected signature prompt to user_rejected inside the SIWE journey', async () => {
    const eip1193 = createMockEip1193Provider();
    eip1193.failNextRequest(Object.assign(new Error('User rejected'), { code: 4001 }));
    const harness = createProviderHarness(eip1193);
    const adapter = createWalletConnectAdapter({
      enabled: true,
      projectId: PROJECT_ID,
      loadProvider: async () => harness.provider,
    });
    const connection = await adapter.connect();

    const transport: SiweTransport = {
      getChallenge: async () => ({ challenge: 'challenge', nonce: 'nonce-1' }),
      verify: vi.fn(async () => undefined),
    };

    await expect(authenticateWithSiwe(adapter, connection, transport)).rejects.toMatchObject({
      code: 'user_rejected',
    });
  });

  it('runs the complete authentication journey: challenge → sign → verify', async () => {
    const eip1193 = createMockEip1193Provider();
    const harness = createProviderHarness(eip1193);
    const adapter = createWalletConnectAdapter({
      enabled: true,
      projectId: PROJECT_ID,
      loadProvider: async () => harness.provider,
    });
    const connection = await adapter.connect();

    const transport: SiweTransport = {
      getChallenge: vi.fn(async () => ({
        challenge: 'GameGuild wants you to sign in',
        nonce: 'nonce-1',
        expiresAt: '2026-10-20T12:00:00.000Z',
      })),
      verify: vi.fn(async () => undefined),
    };

    const authentication = await authenticateWithSiwe(adapter, connection, transport);

    expect(transport.getChallenge).toHaveBeenCalledWith({
      walletAddress: TEST_ADDRESS,
      chainId: '1',
    });
    expect(transport.verify).toHaveBeenCalledWith(
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

  it('switches chains through the shared wallet_switchEthereumChain fallback', async () => {
    const eip1193 = createMockEip1193Provider();
    let switchAttempts = 0;
    eip1193.onRequest((args) => {
      if (args.method === 'wallet_switchEthereumChain') {
        switchAttempts += 1;
        if (switchAttempts === 1) {
          throw Object.assign(new Error('Unrecognized chain ID.'), { code: 4902 });
        }
        return null;
      }
      return null;
    });
    const harness = createProviderHarness(eip1193);
    const adapter = createWalletConnectAdapter({
      enabled: true,
      projectId: PROJECT_ID,
      loadProvider: async () => harness.provider,
    });
    await adapter.connect();

    await adapter.switchChain(COMMON_CHAINS['11155111'] as never);

    expect(eip1193.requests.map((r) => r.method)).toContain('wallet_addEthereumChain');
    expect(switchAttempts).toBe(2);
  });
});

describe('WalletConnectAdapter (session events)', () => {
  const connectedAdapter = async (): Promise<{
    adapter: ReturnType<typeof createWalletConnectAdapter>;
    harness: ProviderHarness;
  }> => {
    const harness = createProviderHarness();
    const adapter = createWalletConnectAdapter({
      enabled: true,
      projectId: PROJECT_ID,
      loadProvider: async () => harness.provider,
    });
    await adapter.connect();
    return { adapter, harness };
  };

  it('forwards account and network changes from the paired wallet', async () => {
    const { adapter, harness } = await connectedAdapter();
    const onAccountsChanged = vi.fn();
    const onChainChanged = vi.fn();
    const onDisconnected = vi.fn();
    adapter.onEvent({ onAccountsChanged, onChainChanged, onDisconnected });
    await new Promise((resolve) => setTimeout(resolve, 0));

    harness.provider.emit('accountsChanged', ['0x1111111111111111111111111111111111111111']);
    harness.provider.emit('chainChanged', '0xaa36a7');

    expect(onAccountsChanged).toHaveBeenCalledWith(['0x1111111111111111111111111111111111111111']);
    expect(onChainChanged).toHaveBeenCalledWith('11155111');
  });

  it('handles disconnect by resetting the session for the next connect', async () => {
    const { adapter, harness } = await connectedAdapter();
    const onDisconnected = vi.fn();
    adapter.onEvent({ onAccountsChanged: vi.fn(), onChainChanged: vi.fn(), onDisconnected });
    await new Promise((resolve) => setTimeout(resolve, 0));

    harness.provider.emit('disconnect', new Error('peer disconnected'));
    expect(onDisconnected).toHaveBeenCalledTimes(1);

    harness.connect.mockClear();
    await adapter.connect();
    expect(harness.connect).toHaveBeenCalledTimes(1);
  });

  it('handles session expiration (session_expire) like a disconnect', async () => {
    const { adapter, harness } = await connectedAdapter();
    const onDisconnected = vi.fn();
    adapter.onEvent({ onAccountsChanged: vi.fn(), onChainChanged: vi.fn(), onDisconnected });
    await new Promise((resolve) => setTimeout(resolve, 0));

    harness.provider.emit('session_expire');

    expect(onDisconnected).toHaveBeenCalledTimes(1);

    harness.connect.mockClear();
    await adapter.connect();
    expect(harness.connect).toHaveBeenCalledTimes(1);
  });

  it('handles the wallet deleting the session (session_delete)', async () => {
    const { adapter, harness } = await connectedAdapter();
    const onDisconnected = vi.fn();
    adapter.onEvent({ onAccountsChanged: vi.fn(), onChainChanged: vi.fn(), onDisconnected });
    await new Promise((resolve) => setTimeout(resolve, 0));

    harness.provider.emit('session_delete', new Error('Session deleted by peer'));

    expect(onDisconnected).toHaveBeenCalledTimes(1);
  });

  it('unsubscribes cleanly', async () => {
    const { adapter, harness } = await connectedAdapter();
    const onDisconnected = vi.fn();
    const unsubscribe = adapter.onEvent({
      onAccountsChanged: vi.fn(),
      onChainChanged: vi.fn(),
      onDisconnected,
    });
    await new Promise((resolve) => setTimeout(resolve, 0));
    unsubscribe();

    harness.provider.emit('session_expire');
    expect(onDisconnected).not.toHaveBeenCalled();
  });
});
