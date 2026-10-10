import {
  AuthServiceUnavailableError,
  CredentialsSignInError,
  parseBackendAuthResponse,
  type CredentialsProviderConfig,
} from "@game-guild/client";

type CredentialsAuthorize = CredentialsProviderConfig["authorize"];

const EMAIL_CODE_CONSUME_PATH = "/v1/auth/email-code:consume";

/**
 * Extend the credentials provider with the one-time email-code flow while
 * keeping other credentials (magic link, email/password) on their existing
 * implementations. Codes are exchanged server-side by the same API session
 * issuance used for the magic-link consume.
 */
export function createEmailCodeCredentialsAuthorize(
  fallbackAuthorize: CredentialsAuthorize,
  apiUrl: string,
): CredentialsAuthorize {
  return async (credentials, request) => {
    const rawCode = credentials.emailCode;
    if (rawCode === undefined) return fallbackAuthorize(credentials, request);

    const email = credentials.emailCodeEmail;
    if (
      typeof rawCode !== "string" ||
      rawCode.trim().length === 0 ||
      typeof email !== "string" ||
      email.trim().length === 0
    ) {
      throw new CredentialsSignInError(
        "This sign-in code is invalid or has expired.",
      );
    }

    const body: Record<string, string> = {
      email: email.trim(),
      code: rawCode.trim(),
    };
    const tenantId = credentials.tenantId;
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
      response = await fetch(new URL(EMAIL_CODE_CONSUME_PATH, apiUrl), {
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
      throw new CredentialsSignInError(
        "This sign-in code is invalid or has expired.",
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

    const result = parseBackendAuthResponse(data as Record<string, unknown>);
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
