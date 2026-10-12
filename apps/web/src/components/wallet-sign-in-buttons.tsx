"use client";

import { useMemo } from "react";
import { useAuth } from "@game-guild/client/react";
import {
  Web3Provider,
  createMetaMaskAdapter,
  createTorusAdapter,
  shortenAddress,
  useWallet,
} from "@game-guild/web3";
import type { SiweTransport, WalletAdapter } from "@game-guild/web3";

/**
 * Wallet sign-in button group (issues #16/#38/#39 under umbrella #291).
 *
 * Renders one button per available wallet — MetaMask whenever the injected
 * provider is detected, Torus only behind NEXT_PUBLIC_WALLET_TORUS_ENABLED
 * (the adapter additionally fails closed when the Torus SDK is absent).
 * Clicking runs the full SIWE journey against the existing backend
 * endpoints: same-origin /api/auth/web3/challenge proxy → personal_sign →
 * signIn("credentials", …) which the server-side web3 credentials wrapper
 * verifies via /v1/auth/web3:verify.
 */

const CHALLENGE_ENDPOINT = "/api/auth/web3/challenge";

const TORUS_ENABLED = process.env.NEXT_PUBLIC_WALLET_TORUS_ENABLED === "true";

export interface WalletSignInLabels {
  continueWithWallet: string;
  connecting: string;
  signing: string;
  connected: string;
  disconnect: string;
}

const ERROR_MESSAGES: Record<string, string> = {
  user_rejected: "You rejected the request in your wallet. Nothing was signed.",
  wallet_not_found: "This wallet was not detected in your browser.",
  challenge_failed: "Could not start wallet sign-in. Please try again.",
  verification_failed: "Wallet sign-in failed. The challenge may have expired — please try again.",
  unauthorized: "Connect your wallet first.",
  disconnected: "Your wallet disconnected.",
};

const friendlyError = (code: string, detail: string): string => {
  const base = ERROR_MESSAGES[code] ?? "Wallet sign-in failed.";
  return detail && !ERROR_MESSAGES[code] ? `${base} ${detail}` : base;
};

const walletIcon = (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true" className="size-5">
    <path d="M3 7.5A2.5 2.5 0 0 1 5.5 5H18a2 2 0 0 1 2 2v1" strokeLinecap="round" />
    <path d="M3 7.5V17a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-3" strokeLinecap="round" />
    <path d="M21 10h-4.5a2.5 2.5 0 0 0 0 5H21v-5Z" strokeLinecap="round" />
  </svg>
);

export function WalletSignInButtons({
  redirectTo,
  labels,
}: {
  redirectTo?: string;
  labels: WalletSignInLabels;
}) {
  const adapters = useMemo<WalletAdapter[]>(() => {
    const registered: WalletAdapter[] = [createMetaMaskAdapter()];
    if (TORUS_ENABLED) {
      // Torus loads its embed SDK lazily inside the adapter; when the SDK
      // cannot be loaded the adapter reports unavailable and the button
      // stays hidden (fail closed).
      registered.push(createTorusAdapter({ enabled: true, network: { host: "mainnet" } }));
    }
    return registered;
  }, []);

  return (
    <Web3Provider adapters={adapters}>
      <WalletButtonGroup redirectTo={redirectTo} labels={labels} />
    </Web3Provider>
  );
}

function WalletButtonGroup({
  redirectTo,
  labels,
}: {
  redirectTo?: string;
  labels: WalletSignInLabels;
}) {
  const { signIn } = useAuth();
  // Accessed through the context object (not destructured): the linter's unbound-method
  // rule fires on functions separated from their owning object.
  const web3 = useWallet();

  const transport = useMemo<SiweTransport>(
    () => ({
      getChallenge: async (request) => {
        const response = await fetch(CHALLENGE_ENDPOINT, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ walletAddress: request.walletAddress, chainId: request.chainId }),
        });
        const data = (await response.json().catch(() => null)) as {
          challenge?: string;
          nonce?: string;
          expiresAt?: string;
          message?: string;
        } | null;
        if (!response.ok || !data?.challenge || !data.nonce) {
          throw new Error(data?.message ?? "challenge_failed");
        }
        return { challenge: data.challenge, nonce: data.nonce, expiresAt: data.expiresAt };
      },
      verify: async (request) => {
        // The credentials server action verifies the signature against
        // /v1/auth/web3:verify and establishes the session cookie. Any
        // failure is rethrown so the context lands in verification_failed.
        await signIn("credentials", {
          siweWalletAddress: request.walletAddress,
          siweChallenge: request.challenge,
          siweSignature: request.signature,
          siweNonce: request.nonce,
          siweChainId: request.chainId,
          rememberMe: true,
          redirectTo,
        });
      },
    }),
    [signIn, redirectTo],
  );

  async function handleConnect(walletId: string) {
    web3.clearError();
    try {
      await web3.connect(walletId);
      await web3.authenticate(transport);
    } catch {
      // State machine already surfaced the error below the buttons.
    }
  }

  const availableWallets = web3.wallets.filter((wallet) => wallet.available);

  if (availableWallets.length === 0) return null;

  if (web3.account) {
    return (
      <div className="flex flex-col gap-2" data-testid="wallet-connected">
        <div className="flex h-10 w-full items-center justify-center gap-2 rounded-md border border-white/10 bg-white/5 text-sm font-medium text-white">
          {walletIcon}
          {labels.connected.replace("{address}", shortenAddress(web3.account))}
        </div>
        <button
          type="button"
          data-testid="wallet-disconnect"
          onClick={() => void web3.disconnect().catch(() => undefined)}
          className="text-xs text-slate-400 underline-offset-4 hover:text-slate-200 hover:underline"
        >
          {labels.disconnect}
        </button>
        {web3.error ? <WalletError error={web3.error} /> : null}
      </div>
    );
  }

  return (
    <div className="contents">
      {availableWallets.map((wallet) => (
        <button
          key={wallet.id}
          type="button"
          data-testid={`${wallet.id}-sign-in-button`}
          disabled={web3.isPending}
          onClick={() => void handleConnect(wallet.id)}
          className="flex h-10 w-full items-center justify-center gap-2 rounded-md border border-white/10 bg-white/10 text-sm font-medium text-white shadow-xs transition hover:bg-white/15 focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 active:translate-y-px disabled:cursor-not-allowed disabled:opacity-60"
        >
          {walletIcon}
          {web3.status === "signing"
            ? labels.signing
            : web3.status === "connecting" && web3.isPending
              ? labels.connecting
              : labels.continueWithWallet.replace("{wallet}", wallet.name)}
        </button>
      ))}
      {web3.error ? <WalletError error={web3.error} /> : null}
    </div>
  );
}

function WalletError({ error }: { error: { code: string; message: string } }) {
  return (
    <p role="alert" data-testid="wallet-error" className="text-xs text-red-300">
      {friendlyError(error.code, error.message)}
    </p>
  );
}
