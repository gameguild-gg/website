import { describe, expect, it, vi } from 'vitest';
import { createTorusAdapter } from './torus';
import type { TorusEmbedLike } from './torus';
import { COMMON_CHAINS, TEST_ADDRESS, TEST_SIGNATURE, createMockEip1193Provider } from '../testing';
import type { Eip1193Provider } from '../types';

const createEmbed = (provider: Eip1193Provider): TorusEmbedLike => {
  const embed = {
    init: vi.fn(async () => undefined),
    login: vi.fn(async () => undefined),
    logout: vi.fn(async () => undefined),
    provider,
  };
  return embed;
};

describe('TorusAdapter', () => {
  it('is unavailable when the deployment gate is off (fail closed)', async () => {
    const adapter = createTorusAdapter({ loadEmbed: async () => createEmbed(createMockEip1193Provider()) });
    await expect(adapter.isAvailable()).resolves.toBe(false);
    await expect(adapter.connect()).rejects.toMatchObject({ code: 'wallet_not_found' });
  });

  it('is available when enabled and a loader is configured', async () => {
    const adapter = createTorusAdapter({
      enabled: true,
      loadEmbed: async () => createEmbed(createMockEip1193Provider()),
    });
    await expect(adapter.isAvailable()).resolves.toBe(true);
  });

  it('connect initializes the embed once, logs in, and returns the account', async () => {
    const provider = createMockEip1193Provider();
    const embed = createEmbed(provider);
    const adapter = createTorusAdapter({ enabled: true, loadEmbed: async () => embed });

    const connection = await adapter.connect();

    expect(connection).toEqual({ address: TEST_ADDRESS, chainId: '1' });
    expect(embed.init).toHaveBeenCalledTimes(1);
    expect(embed.init).toHaveBeenCalledWith(expect.objectContaining({ showTorusButton: false }));
    expect(embed.login).toHaveBeenCalledTimes(1);
  });

  it('connect does not re-initialize or re-prompt login on later operations', async () => {
    const provider = createMockEip1193Provider();
    const embed = createEmbed(provider);
    const adapter = createTorusAdapter({ enabled: true, loadEmbed: async () => embed });

    await adapter.connect();
    await adapter.signMessage(TEST_ADDRESS, 'hello');

    expect(embed.init).toHaveBeenCalledTimes(1);
    expect(embed.login).toHaveBeenCalledTimes(1);
  });

  it('maps login rejection to user_rejected', async () => {
    const provider = createMockEip1193Provider();
    const embed = createEmbed(provider);
    embed.login = vi.fn(async () => {
      throw Object.assign(new Error('user closed popup'), { code: 4001 });
    });
    const adapter = createTorusAdapter({ enabled: true, loadEmbed: async () => embed });

    await expect(adapter.connect()).rejects.toMatchObject({ code: 'user_rejected' });
  });

  it('fails closed when the Torus SDK cannot be loaded', async () => {
    const adapter = createTorusAdapter({
      enabled: true,
      loadEmbed: async () => {
        throw new Error('Cannot find module');
      },
    });
    await expect(adapter.connect()).rejects.toMatchObject({ code: 'wallet_not_found' });
  });

  it('signs SIWE messages through the embed provider with hex-encoded payloads', async () => {
    const provider = createMockEip1193Provider();
    const adapter = createTorusAdapter({ enabled: true, loadEmbed: async () => createEmbed(provider) });
    await adapter.connect();

    const signature = await adapter.signMessage(TEST_ADDRESS, 'Sign in to GameGuild');

    expect(signature).toBe(TEST_SIGNATURE);
    const personalSign = provider.requests.find((request) => request.method === 'personal_sign');
    expect(personalSign?.params).toEqual(['0x5369676e20696e20746f2047616d654775696c64', TEST_ADDRESS]);
  });

  it('switchChain falls back to adding an unknown chain', async () => {
    const provider = createMockEip1193Provider();
    let switchAttempts = 0;
    provider.onRequest((args) => {
      if (args.method === 'eth_accounts') return [TEST_ADDRESS];
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
    const adapter = createTorusAdapter({ enabled: true, loadEmbed: async () => createEmbed(provider) });
    await adapter.connect();

    await adapter.switchChain(COMMON_CHAINS['11155111'] as never);

    expect(provider.requests.map((request) => request.method)).toContain('wallet_addEthereumChain');
    expect(switchAttempts).toBe(2);
  });

  it('disconnect logs out of the embed and resets initialization state', async () => {
    const provider = createMockEip1193Provider();
    const embed = createEmbed(provider);
    const adapter = createTorusAdapter({ enabled: true, loadEmbed: async () => embed });
    await adapter.connect();
    await adapter.disconnect();

    expect(embed.logout).toHaveBeenCalledTimes(1);
    // Next connect goes through init + login again with a fresh embed lifecycle.
    await adapter.connect();
    expect(embed.init).toHaveBeenCalledTimes(2);
    expect(embed.login).toHaveBeenCalledTimes(2);
  });

  it('forwarding events works through the lazily-loaded embed provider', async () => {
    const provider = createMockEip1193Provider();
    const adapter = createTorusAdapter({ enabled: true, loadEmbed: async () => createEmbed(provider) });
    await adapter.connect();

    const onAccountsChanged = vi.fn();
    const onChainChanged = vi.fn();
    const onDisconnected = vi.fn();
    const unsubscribe = adapter.onEvent({ onAccountsChanged, onChainChanged, onDisconnected });

    // onEvent subscribes once the embed promise resolves.
    await new Promise((resolve) => setTimeout(resolve, 0));

    provider.emit('accountsChanged', []);
    provider.emit('chainChanged', '0x1');
    provider.emit('disconnect', new Error('window closed'));

    expect(onAccountsChanged).toHaveBeenCalledWith([]);
    expect(onChainChanged).toHaveBeenCalledWith('1');
    expect(onDisconnected).toHaveBeenCalledTimes(1);
    unsubscribe();
  });

  it('forwards provider exposed as `ethereum` (newer embed builds)', async () => {
    const provider = createMockEip1193Provider();
    const embed: TorusEmbedLike = {
      init: async () => undefined,
      login: async () => undefined,
      logout: async () => undefined,
      ethereum: provider,
    };
    const adapter = createTorusAdapter({ enabled: true, loadEmbed: async () => embed });

    await expect(adapter.connect()).resolves.toEqual({ address: TEST_ADDRESS, chainId: '1' });
  });
});
