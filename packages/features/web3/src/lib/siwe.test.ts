import { describe, expect, it, vi } from 'vitest';
import { authenticateWithSiwe } from './siwe';
import type { SiweTransport, WalletAdapter } from '../types';
import { TEST_ADDRESS, TEST_SIGNATURE } from '../testing';

const CHALLENGE = {
  challenge: 'Service: GameGuild wants you to sign in\nNonce: abc123',
  nonce: 'abc123',
};

const createAdapter = (
  overrides: Partial<Pick<WalletAdapter, 'signMessage'>> = {},
): WalletAdapter => ({
  id: 'metamask',
  name: 'MetaMask',
  isAvailable: async () => true,
  connect: async () => ({ address: TEST_ADDRESS, chainId: '1' }),
  disconnect: async () => undefined,
  signMessage: overrides.signMessage ?? (async () => TEST_SIGNATURE),
  switchChain: async () => undefined,
  onEvent: () => () => undefined,
});

const successTransport: SiweTransport = {
  getChallenge: vi.fn(async () => CHALLENGE),
  verify: vi.fn(async () => undefined),
};

describe('authenticateWithSiwe', () => {
  it('runs challenge → personal_sign → verify and returns the authentication', async () => {
    const transport: SiweTransport = {
      getChallenge: vi.fn(async () => CHALLENGE),
      verify: vi.fn(async () => undefined),
    };
    const signMessage = vi.fn(async () => TEST_SIGNATURE);
    const adapter = createAdapter({ signMessage });

    const authentication = await authenticateWithSiwe(
      adapter,
      { address: TEST_ADDRESS, chainId: '1' },
      transport,
    );

    expect(transport.getChallenge).toHaveBeenCalledWith({
      walletAddress: TEST_ADDRESS,
      chainId: '1',
    });
    expect(signMessage).toHaveBeenCalledWith(TEST_ADDRESS, CHALLENGE.challenge);
    expect(transport.verify).toHaveBeenCalledWith({
      walletAddress: TEST_ADDRESS,
      chainId: '1',
      challenge: CHALLENGE.challenge,
      nonce: CHALLENGE.nonce,
      signature: TEST_SIGNATURE,
    });
    expect(authentication).toEqual({
      walletAddress: TEST_ADDRESS,
      chainId: '1',
      challenge: CHALLENGE.challenge,
      nonce: CHALLENGE.nonce,
      signature: TEST_SIGNATURE,
    });
  });

  it('maps challenge failures to challenge_failed', async () => {
    const transport: SiweTransport = {
      getChallenge: vi.fn(async () => {
        throw new Error('backend down');
      }),
      verify: vi.fn(),
    };
    await expect(
      authenticateWithSiwe(createAdapter(), { address: TEST_ADDRESS, chainId: '1' }, transport),
    ).rejects.toMatchObject({ code: 'challenge_failed', message: 'backend down' });
    expect(transport.verify).not.toHaveBeenCalled();
  });

  it('rejects malformed challenge payloads as challenge_failed', async () => {
    const transport: SiweTransport = {
      getChallenge: vi.fn(async () => ({ challenge: '', nonce: '' })),
      verify: vi.fn(),
    };
    await expect(
      authenticateWithSiwe(createAdapter(), { address: TEST_ADDRESS, chainId: '1' }, transport),
    ).rejects.toMatchObject({ code: 'challenge_failed' });
  });

  it('maps EIP-1193 4001 during signing to user_rejected without calling verify', async () => {
    const rejection = Object.assign(new Error('User rejected'), { code: 4001 });
    const adapter = createAdapter({
      signMessage: vi.fn(async () => {
        throw rejection;
      }),
    });
    const transport: SiweTransport = {
      getChallenge: async () => CHALLENGE,
      verify: vi.fn(),
    };
    await expect(
      authenticateWithSiwe(adapter, { address: TEST_ADDRESS, chainId: '1' }, transport),
    ).rejects.toMatchObject({ code: 'user_rejected' });
    expect(transport.verify).not.toHaveBeenCalled();
  });

  it('maps generic signing failures to sign_failed', async () => {
    const adapter = createAdapter({
      signMessage: vi.fn(async () => {
        throw new Error('wallet exploded');
      }),
    });
    await expect(
      authenticateWithSiwe(adapter, { address: TEST_ADDRESS, chainId: '1' }, successTransport),
    ).rejects.toMatchObject({ code: 'sign_failed' });
  });

  it('maps verification rejections to verification_failed', async () => {
    const transport: SiweTransport = {
      getChallenge: async () => CHALLENGE,
      verify: vi.fn(async () => {
        throw new Error('Invalid signature');
      }),
    };
    await expect(
      authenticateWithSiwe(createAdapter(), { address: TEST_ADDRESS, chainId: '1' }, transport),
    ).rejects.toMatchObject({ code: 'verification_failed', message: 'Invalid signature' });
  });
});
