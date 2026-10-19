const DEFAULT_API_URL =
  process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080";

/**
 * Request a sign-in email directly from the public API so the API can apply
 * its normal request-IP protections. The response body is deliberately
 * discarded: development preview tokens must never enter the browser flow.
 */
export async function requestMagicLink(
  email: string,
  apiUrl = DEFAULT_API_URL,
  redirectTo = "/",
  locale = "en-US",
): Promise<boolean> {
  const response = await fetch(
    `${apiUrl.replace(/\/+$/, "")}/v1/auth/magic-link:request`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email: email.trim(), redirectTo, locale }),
      credentials: "omit",
      cache: "no-store",
    },
  );

  return response.ok;
}
