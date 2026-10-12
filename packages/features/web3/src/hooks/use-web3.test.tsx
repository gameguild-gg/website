import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { useChainId, useWallet, useWalletAddress, useWeb3 } from './use-web3';
import { Web3Provider } from '../components/web3-provider';
import { createMetaMaskAdapter } from '../adapters/metamask';
import { TEST_ADDRESS, createMockEip1193Provider } from '../testing';

afterEach(cleanup);

describe('web3 hooks', () => {
  it('throw when used outside of a Web3Provider', () => {
    // Silence the expected React error boundary noise for this assertion.
    const consoleError = console.error;
    console.error = () => undefined;
    try {
      expect(() => render(<Outside />)).toThrow(/must be used within a Web3Provider/);
    } finally {
      console.error = consoleError;
    }
  });

  it('expose address and chain through the typed hooks', async () => {
    const provider = createMockEip1193Provider();
    const adapter = createMetaMaskAdapter({
      discover: async () => ({ provider, source: 'eip6963' }),
    });

    render(
      <Web3Provider adapters={[adapter]}>
        <Readout />
      </Web3Provider>,
    );

    expect(screen.getByTestId('address').textContent).toBe('none');
    expect(screen.getByTestId('chain').textContent).toBe('none');

    await screen.findByText('connect').then((button) => button.click());

    expect(await screen.findByText(TEST_ADDRESS)).not.toBeNull();
    expect(screen.getByTestId('chain').textContent).toBe('1');
    // No wallet action was ever in flight at rest.
    expect(screen.getByTestId('pending').textContent).toBe('false');
  });
});

function Outside() {
  useWallet();
  return null;
}

function Readout() {
  const { connect } = useWeb3();
  const address = useWalletAddress();
  const chainId = useChainId();
  const wallet = useWallet();
  return (
    <div>
      <button type="button" onClick={() => void connect('metamask').catch(() => undefined)}>
        connect
      </button>
      <div data-testid="address">{address ?? 'none'}</div>
      <div data-testid="chain">{chainId ?? 'none'}</div>
      <div data-testid="pending">{String(wallet.isPending)}</div>
    </div>
  );
}
