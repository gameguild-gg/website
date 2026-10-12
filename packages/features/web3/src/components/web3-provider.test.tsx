import '@testing-library/jest-dom/vitest';
import { act, cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import React from 'react';
import { Web3Provider } from './web3-provider';
import { createMetaMaskAdapter } from '../adapters/metamask';
import { useWeb3, useWallet, useWalletAddress, useChainId } from '../hooks/use-web3';
import type { SiweTransport } from '../types';
import { TEST_ADDRESS, TEST_SIGNATURE, createMockEip1193Provider } from '../testing';

afterEach(cleanup);

const click = (name: string) =>
  act(async () => {
    screen.getByText(name).click();
  });

describe('Web3Provider', () => {
  it('renders children and probes wallet availability', async () => {
    const provider = createMockEip1193Provider();
    const unavailable = createMetaMaskAdapter({ discover: async () => undefined });

    render(
      <Web3Provider adapters={[unavailable]}>
        <Probe />
      </Web3Provider>,
    );

    expect(screen.getByTestId('status').textContent).toBe('disconnected');
    await waitFor(() =>
      expect(screen.getByTestId('wallet-metamask').textContent).toBe('MetaMask:false'),
    );

    cleanup();
    const available = createMetaMaskAdapter({
      discover: async () => ({ provider, source: 'eip6963' }),
    });
    render(
      <Web3Provider adapters={[available]}>
        <Probe />
      </Web3Provider>,
    );
    await waitFor(() =>
      expect(screen.getByTestId('wallet-metamask').textContent).toBe('MetaMask:true'),
    );
  });

  it('connects a wallet and updates account/chain state', async () => {
    const provider = createMockEip1193Provider();
    const adapter = createMetaMaskAdapter({
      discover: async () => ({ provider, source: 'eip6963' }),
    });

    render(
      <Web3Provider adapters={[adapter]}>
        <Harness />
      </Web3Provider>,
    );

    await click('connect-metamask');

    await waitFor(() => expect(screen.getByTestId('status').textContent).toBe('connected'));
    expect(screen.getByTestId('account').textContent).toBe(TEST_ADDRESS);
    expect(screen.getByTestId('chain').textContent).toBe('1');
    expect(screen.getByTestId('error').textContent).toBe('none');
  });

  it('reports wallet_not_found for unregistered wallets', async () => {
    render(
      <Web3Provider adapters={[]}>
        <Harness />
      </Web3Provider>,
    );

    await click('connect-ghost');

    await waitFor(() => expect(screen.getByTestId('status').textContent).toBe('error'));
    expect(screen.getByTestId('error').textContent).toContain('wallet_not_found');
  });

  it('surfaces user rejection during connect and recovers after clearError', async () => {
    const provider = createMockEip1193Provider();
    provider.failNextRequest(Object.assign(new Error('User rejected'), { code: 4001 }));
    const adapter = createMetaMaskAdapter({
      discover: async () => ({ provider, source: 'injected' }),
    });

    render(
      <Web3Provider adapters={[adapter]}>
        <Harness />
      </Web3Provider>,
    );

    await click('connect-metamask');

    await waitFor(() => expect(screen.getByTestId('status').textContent).toBe('error'));
    expect(screen.getByTestId('error').textContent).toContain('user_rejected');
    expect(screen.getByTestId('account').textContent).toBe('none');

    await click('clear-error');
    expect(screen.getByTestId('status').textContent).toBe('disconnected');
  });

  it('completes the SIWE journey to authenticated through the injected transport', async () => {
    const provider = createMockEip1193Provider();
    const adapter = createMetaMaskAdapter({
      discover: async () => ({ provider, source: 'eip6963' }),
    });
    const transport: SiweTransport = {
      getChallenge: vi.fn(async () => ({ challenge: 'Sign in to GameGuild', nonce: 'n0nc3' })),
      verify: vi.fn(async () => undefined),
    };

    render(
      <Web3Provider adapters={[adapter]}>
        <Harness transport={transport} />
      </Web3Provider>,
    );

    await click('connect-metamask');
    await waitFor(() => expect(screen.getByTestId('status').textContent).toBe('connected'));

    await click('authenticate');
    await waitFor(() => expect(screen.getByTestId('status').textContent).toBe('authenticated'));

    expect(transport.getChallenge).toHaveBeenCalledWith({
      walletAddress: TEST_ADDRESS,
      chainId: '1',
    });
    expect(transport.verify).toHaveBeenCalledWith({
      walletAddress: TEST_ADDRESS,
      chainId: '1',
      challenge: 'Sign in to GameGuild',
      nonce: 'n0nc3',
      signature: TEST_SIGNATURE,
    });
    expect(
      provider.requests.find((request) => request.method === 'personal_sign'),
    ).toBeDefined();
  });

  it('maps SIWE failures to the error taxonomy and keeps the wallet connected', async () => {
    const provider = createMockEip1193Provider();
    const adapter = createMetaMaskAdapter({
      discover: async () => ({ provider, source: 'eip6963' }),
    });
    const transport: SiweTransport = {
      getChallenge: vi.fn(async () => ({ challenge: 'msg', nonce: 'n' })),
      verify: vi.fn(async () => {
        throw new Error('Invalid signature');
      }),
    };

    render(
      <Web3Provider adapters={[adapter]}>
        <Harness transport={transport} />
      </Web3Provider>,
    );

    await click('connect-metamask');
    await waitFor(() => expect(screen.getByTestId('status').textContent).toBe('connected'));

    await click('authenticate');
    await waitFor(() => expect(screen.getByTestId('error').textContent).toContain('verification_failed'));
    // Wallet connection survives a failed verification.
    expect(screen.getByTestId('status').textContent).toBe('connected');
    expect(screen.getByTestId('account').textContent).toBe(TEST_ADDRESS);
  });

  it('rejects authenticate before any wallet is connected', async () => {
    render(
      <Web3Provider adapters={[]}>
        <Harness transport={{ getChallenge: vi.fn(), verify: vi.fn() }} />
      </Web3Provider>,
    );

    await click('authenticate');
    await waitFor(() => expect(screen.getByTestId('error').textContent).toContain('unauthorized'));
  });

  it('handles account and network changes emitted by the wallet', async () => {
    const provider = createMockEip1193Provider();
    const adapter = createMetaMaskAdapter({
      discover: async () => ({ provider, source: 'eip6963' }),
    });

    render(
      <Web3Provider adapters={[adapter]}>
        <Harness
          transport={{
            getChallenge: async () => ({ challenge: 'msg', nonce: 'n' }),
            verify: async () => undefined,
          }}
        />
      </Web3Provider>,
    );

    await click('connect-metamask');
    await waitFor(() => expect(screen.getByTestId('status').textContent).toBe('connected'));

    act(() => {
      provider.emit('chainChanged', '0xaa36a7');
    });
    await waitFor(() => expect(screen.getByTestId('chain').textContent).toBe('11155111'));

    act(() => {
      provider.emit('accountsChanged', ['0xdef0000000000000000000000000000000000001']);
    });
    await waitFor(() =>
      expect(screen.getByTestId('account').textContent).toBe('0xdef0000000000000000000000000000000000001'),
    );
  });

  it('downgrades authenticated to connected when the account changes', async () => {
    const provider = createMockEip1193Provider();
    const adapter = createMetaMaskAdapter({
      discover: async () => ({ provider, source: 'eip6963' }),
    });

    render(
      <Web3Provider adapters={[adapter]}>
        <Harness
          transport={{
            getChallenge: async () => ({ challenge: 'msg', nonce: 'n' }),
            verify: async () => undefined,
          }}
        />
      </Web3Provider>,
    );

    await click('connect-metamask');
    await click('authenticate');
    await waitFor(() => expect(screen.getByTestId('status').textContent).toBe('authenticated'));

    act(() => {
      provider.emit('accountsChanged', ['0xdef0000000000000000000000000000000000001']);
    });
    await waitFor(() => expect(screen.getByTestId('status').textContent).toBe('connected'));
  });

  it('fully cleans up when the wallet disconnects all accounts', async () => {
    const provider = createMockEip1193Provider();
    const adapter = createMetaMaskAdapter({
      discover: async () => ({ provider, source: 'eip6963' }),
    });

    render(
      <Web3Provider adapters={[adapter]}>
        <Harness />
      </Web3Provider>,
    );

    await click('connect-metamask');
    await waitFor(() => expect(screen.getByTestId('status').textContent).toBe('connected'));

    act(() => {
      provider.emit('accountsChanged', []);
    });
    await waitFor(() => expect(screen.getByTestId('status').textContent).toBe('disconnected'));
    expect(screen.getByTestId('account').textContent).toBe('none');
    expect(screen.getByTestId('chain').textContent).toBe('none');
    expect(screen.getByTestId('error').textContent).toContain('disconnected');
  });

  it('disconnect calls the adapter and resets state', async () => {
    const provider = createMockEip1193Provider();
    const adapter = createMetaMaskAdapter({
      discover: async () => ({ provider, source: 'eip6963' }),
    });
    const disconnectSpy = vi.spyOn(adapter, 'disconnect');

    render(
      <Web3Provider adapters={[adapter]}>
        <Harness />
      </Web3Provider>,
    );

    await click('connect-metamask');
    await waitFor(() => expect(screen.getByTestId('status').textContent).toBe('connected'));

    await click('disconnect');
    await waitFor(() => expect(screen.getByTestId('status').textContent).toBe('disconnected'));
    expect(screen.getByTestId('account').textContent).toBe('none');
    expect(disconnectSpy).toHaveBeenCalledTimes(1);
    expect(screen.getByTestId('error').textContent).toBe('none');
  });

  it('cleans up listeners when the provider unmounts', async () => {
    const provider = createMockEip1193Provider();
    const adapter = createMetaMaskAdapter({
      discover: async () => ({ provider, source: 'eip6963' }),
    });

    render(
      <Web3Provider adapters={[adapter]}>
        <Harness />
      </Web3Provider>,
    );
    await click('connect-metamask');
    await waitFor(() => expect(screen.getByTestId('status').textContent).toBe('connected'));
    cleanup();

    // Events after unmount must not throw or resurrect state.
    expect(() => {
      provider.emit('accountsChanged', ['0xdef0000000000000000000000000000000000001']);
      provider.emit('disconnect', new Error('gone'));
    }).not.toThrow();
  });
});

/** Minimal consumer that shows availability probing without actions. */
function Probe() {
  const { wallets, state } = useWeb3();
  return (
    <div>
      {wallets.map((wallet) => (
        <div key={wallet.id} data-testid={`wallet-${wallet.id}`}>
          {wallet.name}:{String(wallet.available)}
        </div>
      ))}
      <div data-testid="status">{state.status}</div>
    </div>
  );
}

/** Interactive consumer exercising the full common contract. */
function Harness({ transport }: { transport?: SiweTransport }) {
  const snapshot = useWallet();
  const address = useWalletAddress();
  const chainId = useChainId();
  const { authenticate } = useWeb3();

  return (
    <div>
      <button type="button" onClick={() => void snapshot.connect('metamask').catch(() => undefined)}>
        connect-metamask
      </button>
      <button type="button" onClick={() => void snapshot.connect('ghost').catch(() => undefined)}>
        connect-ghost
      </button>
      <button type="button" onClick={() => void snapshot.disconnect()}>
        disconnect
      </button>
      <button
        type="button"
        onClick={() => void (transport && authenticate(transport).catch(() => undefined))}
      >
        authenticate
      </button>
      <button type="button" onClick={() => snapshot.clearError()}>
        clear-error
      </button>
      <div data-testid="status">{snapshot.status}</div>
      <div data-testid="account">{address ?? 'none'}</div>
      <div data-testid="chain">{chainId ?? 'none'}</div>
      <div data-testid="error">
        {snapshot.error ? `${snapshot.error.code}:${snapshot.error.message}` : 'none'}
      </div>
      <div data-testid="pending">{String(snapshot.isPending)}</div>
      <div data-testid="authenticated">{String(snapshot.isAuthenticated)}</div>
    </div>
  );
}
