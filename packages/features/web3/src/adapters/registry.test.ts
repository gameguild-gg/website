import { describe, expect, it } from 'vitest';
import { createWalletAdapters, dedupeAdapters } from './registry';
import type { WalletAdapter } from '../types';

describe('createWalletAdapters (wallet registry)', () => {
  it('registers MetaMask and Trust Wallet by default (detection-driven)', () => {
    const adapters = createWalletAdapters();

    expect(adapters.map((adapter) => adapter.id)).toEqual(['metamask', 'trustwallet']);
  });

  it('can drop the injected wallets entirely', () => {
    const adapters = createWalletAdapters({ metamask: false, trustWallet: false });

    expect(adapters).toEqual([]);
  });

  it('registers Trust Wallet with custom options (deep-link disabled)', async () => {
    const adapters = createWalletAdapters({
      metamask: false,
      trustWallet: { deepLink: { enabled: false } },
    });

    expect(adapters).toHaveLength(1);
    // No injected provider and no deep-link fallback → unavailable.
    await expect(adapters[0]?.isAvailable()).resolves.toBe(false);
  });

  it('registers WalletConnect when the deployment opts in', () => {
    const adapters = createWalletAdapters({
      metamask: false,
      trustWallet: false,
      walletConnect: { enabled: true, projectId: 'project-id' },
    });

    expect(adapters.map((adapter) => adapter.id)).toEqual(['walletconnect']);
  });

  it('registers Torus when the deployment opts in', () => {
    const adapters = createWalletAdapters({
      metamask: false,
      trustWallet: false,
      torus: { enabled: true, loadEmbed: async () => ({}) as never },
    });

    expect(adapters.map((adapter) => adapter.id)).toEqual(['torus']);
  });

  it('composes the full registry in stable order', () => {
    const adapters = createWalletAdapters({
      walletConnect: { enabled: true, projectId: 'project-id' },
      torus: { enabled: true, loadEmbed: async () => ({}) as never },
    });

    expect(adapters.map((adapter) => adapter.id)).toEqual([
      'metamask',
      'trustwallet',
      'walletconnect',
      'torus',
    ]);
  });

  it('never lets a duplicate registration shadow another adapter', () => {
    const first = { id: 'metamask', name: 'First' } as WalletAdapter;
    const second = { id: 'metamask', name: 'Second' } as WalletAdapter;
    const trust = { id: 'trustwallet', name: 'Trust Wallet' } as WalletAdapter;

    expect(dedupeAdapters([first, second, trust])).toEqual([first, trust]);
  });
});
