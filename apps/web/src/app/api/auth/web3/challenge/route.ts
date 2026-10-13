import { NextResponse } from "next/server";

const WEB3_CHALLENGE_PATH = "/v1/auth/web3/challenge";

/**
 * POST /api/auth/web3/challenge — same-origin proxy for the backend SIWE
 * challenge endpoint.
 *
 * The wallet signs in the browser (personal_sign), so the challenge must be
 * fetchable from the page origin. Proxying keeps the API host private and
 * avoids CORS on the anonymous backend endpoint; validation and rate limiting
 * remain the backend's responsibility.
 */
export async function POST(request: Request): Promise<NextResponse> {
  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ error: "invalid_body" }, { status: 400 });
  }

  const payload = (body ?? {}) as { walletAddress?: unknown; chainId?: unknown };
  const walletAddress =
    typeof payload.walletAddress === "string" ? payload.walletAddress.trim() : "";
  if (!/^0x[0-9a-fA-F]{40}$/.test(walletAddress)) {
    return NextResponse.json({ error: "invalid_wallet_address" }, { status: 400 });
  }
  const chainId =
    typeof payload.chainId === "string" && payload.chainId.trim().length > 0
      ? payload.chainId.trim()
      : "1";

  const apiUrl = (
    process.env.API_URL ||
    process.env.NEXT_PUBLIC_API_URL ||
    "http://localhost:8080"
  ).replace(/\/$/, "");

  let response: Response;
  try {
    response = await fetch(new URL(WEB3_CHALLENGE_PATH, `${apiUrl}/`), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ walletAddress, chainId }),
      cache: "no-store",
    });
  } catch {
    return NextResponse.json({ error: "auth_service_unavailable" }, { status: 503 });
  }

  if (!response.ok) {
    return NextResponse.json(
      { error: "challenge_failed", message: await safeMessage(response) },
      { status: response.status >= 500 ? 502 : response.status },
    );
  }

  const data = (await response.json().catch(() => null)) as {
    challenge?: unknown;
    nonce?: unknown;
    expiresAt?: unknown;
  } | null;
  if (!data || typeof data.challenge !== "string" || typeof data.nonce !== "string") {
    return NextResponse.json({ error: "invalid_challenge_response" }, { status: 502 });
  }

  return NextResponse.json(
    {
      challenge: data.challenge,
      nonce: data.nonce,
      ...(typeof data.expiresAt === "string" ? { expiresAt: data.expiresAt } : {}),
    },
    { headers: { "Cache-Control": "no-store" } },
  );
}

async function safeMessage(response: Response): Promise<string | undefined> {
  const data = (await response.json().catch(() => null)) as {
    message?: unknown;
    detail?: unknown;
    title?: unknown;
  } | null;
  const message = data?.message ?? data?.detail ?? data?.title;
  return typeof message === "string" ? message : undefined;
}
