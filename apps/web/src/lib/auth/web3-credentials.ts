import {
  AuthServiceUnavailableError,
  CredentialsSignInError,
  parseBackendAuthResponse,
  type CredentialsProviderConfig,
} from "@game-guild/client";

type CredentialsAuthorize = CredentialsProviderConfig["authorize"];

const WEB3_VERIFY_PATH = "/v1/auth/web3:verify";

/**
 * Extend the existing credentials provider with the SIWE wallet flow while
 * keeping email/password, magic-link and email-code authentication on their
 * existing implementations. When the browser submits a signed SIWE challenge
 * (`siweSignature` present) the wrapper verifies it against the backend's
 * `/v1/auth/web3:verify` endpoint and maps the response into the shared
 * session provider result.
 */
export function createWeb3CredentialsAuthorize(
  wrappedAuthorize: CredentialsAuthorize,
  apiUrl: string,
): CredentialsAuthorize {
  return async (credentials, request) => {
    if (!Object.hasOwn(credentials, "siweSignature")) {
      return wrappedAuthorize(credentials, request);
    }

    const walletAddress = credentials.siweWalletAddress;
    const challenge = credentials.siweChallenge;
    const signature = credentials.siweSignature;
    const nonce = credentials.siweNonce;
    const chainId = credentials.siweChainId;
    const invalid =
      typeof walletAddress !== "string" ||
      walletAddress.length === 0 ||
      typeof challenge !== "string" ||
      challenge.length === 0 ||
      typeof signature !== "string" ||
      signature.length === 0 ||
      typeof nonce !== "string" ||
      nonce.length === 0 ||
      typeof chainId !== "string" ||
      chainId.length === 0;
    if (invalid) {
      throw new CredentialsSignInError(
        "The wallet sign-in request is incomplete. Please try again.",
      );
    }

    const body: Record<string, unknown> = {
      walletAddress,
      challenge,
      signature,
      nonce,
      chainId,
    };
    const tenantId = credentials.tenantId;
    if (typeof tenantId === "string" && tenantId.trim().length > 0) {
      body.tenantId = tenantId.trim();
    }
    const deviceFingerprint = credentials.deviceFingerprint;
    if (typeof deviceFingerprint === "string" && deviceFingerprint.length > 0) {
      body.deviceFingerprint = deviceFingerprint;
    }
    if (credentials.rememberMe === true) {
      body.rememberMe = true;
    }

    const headers: Record<string, string> = {
      "Content-Type": "application/json",
    };
    const userAgent = request?.headers.get("user-agent");
    if (userAgent) headers["User-Agent"] = userAgent;

    let response: Response;
    try {
      response = await fetch(new URL(WEB3_VERIFY_PATH, apiUrl), {
        method: "POST",
        headers,
        body: JSON.stringify(body),
        cache: "no-store",
        credentials: "omit",
      });
    } catch (error) {
      throw new AuthServiceUnavailableError(
        "Authentication service is unreachable. Please try again.",
        error instanceof Error ? error : undefined,
      );
    }

    if (!response.ok) {
      if (response.status >= 500) throw new AuthServiceUnavailableError();
      const errorData = (await response.json().catch(() => ({}))) as {
        message?: string;
        detail?: string;
      };
      throw new CredentialsSignInError(
        errorData.message ||
          errorData.detail ||
          "Wallet sign-in failed. The challenge may have expired — please try again.",
      );
    }

    let data: unknown;
    try {
      data = await response.json();
    } catch {
      throw new AuthServiceUnavailableError(
        "The authentication service returned an invalid response.",
      );
    }

    if (typeof data !== "object" || data === null || Array.isArray(data)) {
      throw new AuthServiceUnavailableError(
        "The authentication service returned an invalid response.",
      );
    }

    const result = parseBackendAuthResponse(
      data as Record<string, unknown>,
      // Wallet accounts have no email; the backend may or may not provide one.
      "",
    );
    if (
      !result.user.id ||
      !result.tokens.accessToken ||
      !result.tokens.refreshToken
    ) {
      throw new AuthServiceUnavailableError(
        "The authentication service returned an incomplete sign-in response.",
      );
    }

    return {
      ...result,
      rememberMe: credentials.rememberMe === true,
    };
  };
}
