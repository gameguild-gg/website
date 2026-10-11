import {
  AuthServiceUnavailableError,
  CredentialsSignInError,
  MfaRequiredError,
  parseBackendAuthResponse,
  type CredentialsProviderConfig,
} from "@game-guild/client";

type CredentialsAuthorize = CredentialsProviderConfig["authorize"];

const MAGIC_LINK_CONSUME_PATH = "/v1/auth/magic-link:consume";

/**
 * Extend the existing credentials provider with the one-time magic-link flow
 * while keeping email/password authentication on its existing implementation.
 */
export function createMagicLinkCredentialsAuthorize(
  passwordAuthorize: CredentialsAuthorize,
  apiUrl: string,
): CredentialsAuthorize {
  return async (credentials, request) => {
    const rawToken = credentials.magicLinkToken;
    if (Object.hasOwn(credentials, "mfaToken") || rawToken === undefined) {
      return passwordAuthorize(credentials, request);
    }

    if (typeof rawToken !== "string" || rawToken.trim().length === 0) {
      throw new CredentialsSignInError(
        "This sign-in link is invalid or has expired.",
      );
    }

    const tenantId = credentials.tenantId;
    const body: Record<string, string> = { token: rawToken.trim() };
    if (typeof tenantId === "string" && tenantId.trim().length > 0) {
      body.tenantId = tenantId.trim();
    }

    const headers: Record<string, string> = {
      "Content-Type": "application/json",
    };
    const userAgent = request?.headers.get("user-agent");
    if (userAgent) headers["User-Agent"] = userAgent;

    let response: Response;
    try {
      response = await fetch(
        new URL(MAGIC_LINK_CONSUME_PATH, apiUrl),
        {
          method: "POST",
          headers,
          body: JSON.stringify(body),
          cache: "no-store",
          credentials: "omit",
        },
      );
    } catch (error) {
      throw new AuthServiceUnavailableError(
        "Authentication service is unreachable. Please try again.",
        error instanceof Error ? error : undefined,
      );
    }

    if (!response.ok) {
      if (response.status >= 500) throw new AuthServiceUnavailableError();
      throw new CredentialsSignInError(
        "This sign-in link is invalid or has expired.",
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

    const payload = data as Record<string, unknown>;
    if (payload.requiresMfa === true) {
      throw new MfaRequiredError("Multi-factor authentication required", {
        mfaToken: typeof payload.mfaToken === "string" ? payload.mfaToken : undefined,
        availableMethods: Array.isArray(payload.availableMethods)
          ? payload.availableMethods.filter((method): method is string => typeof method === "string")
          : undefined,
      });
    }
    const result = parseBackendAuthResponse(payload);
    if (
      !result.user.id ||
      !result.tokens.accessToken ||
      !result.tokens.refreshToken
    ) {
      throw new AuthServiceUnavailableError(
        "The authentication service returned an incomplete sign-in response.",
      );
    }

    return result;
  };
}
