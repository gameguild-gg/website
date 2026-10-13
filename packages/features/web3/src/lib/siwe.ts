/**
 * Shared Sign-In with Ethereum (SIWE) flow for every wallet adapter
 * (issues #16/#39 use this against the GameGuild backend endpoints).
 *
 * The transport abstraction keeps the flow framework-agnostic and testable:
 * the application decides how `POST /v1/auth/web3/challenge` and
 * `POST /v1/auth/web3:verify` are reached.
 */
import { isUserRejectedError, toWeb3Error } from '../types';
import type { SiweAuthentication, SiweTransport, WalletAdapter } from '../types';

/**
 * Run challenge → `personal_sign` → verify over a connected adapter.
 *
 * The adapter must already be connected (`connect()` resolved) so the
 * account and chain id are known before the challenge is requested.
 * Rejects with a `Web3Error`-shaped `{ code, message }` object:
 * - `user_rejected` — the wallet user dismissed the signature prompt
 * - `challenge_failed` — the backend refused to issue a challenge
 * - `verification_failed` — the backend rejected the signature
 * - `sign_failed` — the wallet failed to produce a signature
 */
export async function authenticateWithSiwe(
  adapter: WalletAdapter,
  connection: { address: string; chainId: string },
  transport: SiweTransport,
): Promise<SiweAuthentication> {
  let challenge;
  try {
    challenge = await transport.getChallenge({
      walletAddress: connection.address,
      chainId: connection.chainId,
    });
  } catch (error) {
    if (isUserRejectedError(error)) {
      throw toWeb3Error(error, 'user_rejected');
    }
    throw toWeb3Error(error, 'challenge_failed');
  }

  if (
    typeof challenge.challenge !== 'string' ||
    challenge.challenge.length === 0 ||
    typeof challenge.nonce !== 'string' ||
    challenge.nonce.length === 0
  ) {
    throw toWeb3Error(new Error('The authentication service returned an invalid challenge.'), 'challenge_failed');
  }

  let signature: string;
  try {
    signature = await adapter.signMessage(connection.address, challenge.challenge);
  } catch (error) {
    if (isUserRejectedError(error)) {
      throw toWeb3Error(error, 'user_rejected');
    }
    throw toWeb3Error(error, 'sign_failed');
  }

  const authentication: SiweAuthentication = {
    walletAddress: connection.address,
    chainId: connection.chainId,
    challenge: challenge.challenge,
    nonce: challenge.nonce,
    signature,
  };

  try {
    await transport.verify({
      walletAddress: authentication.walletAddress,
      chainId: authentication.chainId,
      challenge: authentication.challenge,
      nonce: authentication.nonce,
      signature: authentication.signature,
    });
  } catch (error) {
    if (isUserRejectedError(error)) {
      throw toWeb3Error(error, 'user_rejected');
    }
    throw toWeb3Error(error, 'verification_failed');
  }

  return authentication;
}
