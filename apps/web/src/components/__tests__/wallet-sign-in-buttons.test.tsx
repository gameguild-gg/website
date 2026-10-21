import '@testing-library/jest-dom/vitest';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, waitFor } from '@testing-library/react';
import {
  createMetaMaskAdapter,
  createWalletConnectAdapter,
  shortenAddress,
} from '@game-guild/web3';
import type { WalletAdapter } from '@game-guild/web3';
import {
  TEST_ADDRESS,
  TEST_SIGNATURE,
  createMockEip1193Provider,
} from '@game-guild/web3/testing';
import { createMockUseAuth, renderWithUser, type MockUseAuthReturn } from '@/test/auth-test-helpers';
import type { Eip1193Provider } from '@game-guild/web3';

let mockAuth: MockUseAuthReturn;

vi.mock('@game-guild/client/react', () => ({
  useAuth: () => mockAuth,
}));

const { WalletSignInButtons } = await import('@/components/wallet-sign-in-buttons');

const LABELS = {
  continueWithWallet: 'Continue with {wallet}',
  connecting: 'Connecting…',
  signing: 'Signing…',
  connected: 'Connected: {address}',
  disconnect: 'Disconnect',
};

const asWalletConnectProvider = (provider: Eip1193Provider) => ({
  request: (args: { method: string; params?: unknown }) => provider.request(args),
  on: () => undefined,
  removeListener: () => undefined,
  connect: async () => undefined,
  disconnect: async () => undefined,
});

const buildAdapters = (metamask: Eip1193Provider, walletconnect: Eip1193Provider): WalletAdapter[] => [
  createMetaMaskAdapter({ discover: async () => ({ provider: metamask, source: 'injected' }) }),
  createWalletConnectAdapter({
    enabled: true,
    projectId: 'test-project-id',
    loadProvider: async () => asWalletConnectProvider(walletconnect),
  }),
];

const mockChallengeEndpoint = (): ReturnType<typeof vi.fn> =>
  vi.fn(async () =>
    Response.json({
      challenge: 'GameGuild wants you to sign in',
      nonce: 'nonce-1',
      expiresAt: '2026-10-20T12:00:00.000Z',
    }),
  );

describe('WalletSignInButtons', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    mockAuth = createMockUseAuth();
    fetchMock = mockChallengeEndpoint();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it('renders one button per available wallet from the registry', async () => {
    const adapters = buildAdapters(createMockEip1193Provider(), createMockEip1193Provider());
    renderWithUser(
      <WalletSignInButtons labels={LABELS} adapters={adapters} />,
    );

    await waitFor(() => {
      expect(screen.getByTestId('metamask-sign-in-button')).toBeInTheDocument();
      expect(screen.getByTestId('walletconnect-sign-in-button')).toBeInTheDocument();
    });
    expect(screen.getByTestId('metamask-sign-in-button')).toHaveTextContent('Continue with MetaMask');
    expect(screen.getByTestId('walletconnect-sign-in-button')).toHaveTextContent(
      'Continue with WalletConnect',
    );
  });

  it('renders nothing when no wallet is available', async () => {
    renderWithUser(
      <WalletSignInButtons labels={LABELS} adapters={[createMetaMaskAdapter({ discover: async () => undefined })]} />,
    );

    await waitFor(() => {
      expect(screen.queryByTestId('wallet-sign-in-buttons')).not.toBeInTheDocument();
    });
  });

  it('runs the full SIWE journey: challenge fetch → personal_sign → signIn credentials', async () => {
    const metamask = createMockEip1193Provider();
    const adapters = buildAdapters(metamask, createMockEip1193Provider());
    const { user } = renderWithUser(<WalletSignInButtons labels={LABELS} adapters={adapters} />);

    await user.click(await screen.findByTestId('metamask-sign-in-button'));

    await waitFor(() => {
      expect(screen.getByTestId('wallet-connected')).toBeInTheDocument();
    });
    expect(screen.getByTestId('wallet-connected')).toHaveTextContent(
      `Connected: ${shortenAddress(TEST_ADDRESS)}`,
    );

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/auth/web3/challenge',
      expect.objectContaining({ method: 'POST' }),
    );
    expect(
      metamask.requests.find((request) => request.method === 'personal_sign')?.params,
    ).toEqual(['0x47616d654775696c642077616e747320796f7520746f207369676e20696e', TEST_ADDRESS]);
    expect(mockAuth.signIn).toHaveBeenCalledWith(
      'credentials',
      expect.objectContaining({
        siweWalletAddress: TEST_ADDRESS,
        siweChallenge: 'GameGuild wants you to sign in',
        siweNonce: 'nonce-1',
        siweSignature: TEST_SIGNATURE,
        siweChainId: '1',
      }),
    );
  });

  it('surfaces a friendly error and keeps the buttons when the wallet rejects', async () => {
    const metamask = createMockEip1193Provider();
    metamask.failNextRequest(Object.assign(new Error('User rejected'), { code: 4001 }));
    const adapters = buildAdapters(metamask, createMockEip1193Provider());
    const { user } = renderWithUser(<WalletSignInButtons labels={LABELS} adapters={adapters} />);

    await user.click(await screen.findByTestId('metamask-sign-in-button'));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('You rejected the request in your wallet.');
    expect(screen.getByTestId('metamask-sign-in-button')).toBeInTheDocument();
    expect(mockAuth.signIn).not.toHaveBeenCalled();
  });

  it('disconnects back to the wallet buttons', async () => {
    const adapters = buildAdapters(createMockEip1193Provider(), createMockEip1193Provider());
    const { user } = renderWithUser(<WalletSignInButtons labels={LABELS} adapters={adapters} />);

    await user.click(await screen.findByTestId('metamask-sign-in-button'));
    await waitFor(() => {
      expect(screen.getByTestId('wallet-connected')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('wallet-disconnect'));

    await waitFor(() => {
      expect(screen.getByTestId('metamask-sign-in-button')).toBeInTheDocument();
    });
  });
});
