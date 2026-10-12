const DEFAULT_API_URL =
  process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080";

/**
 * Request a one-time email sign-in code directly from the public API so the
 * API can apply its normal request-IP protections. The response body is
 * deliberately discarded: the generic confirmation must be the only signal.
 */
export async function requestEmailCode(
  email: string,
  apiUrl = DEFAULT_API_URL,
): Promise<boolean> {
  const response = await fetch(
    `${apiUrl.replace(/\/+$/, "")}/v1/auth/email-code:request`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email: email.trim() }),
      credentials: "omit",
      cache: "no-store",
    },
  );

  return response.ok;
}
