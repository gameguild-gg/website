import { loadSocialFeed, SocialFeedRequestError } from "@/lib/feed/queries";
import type { FeedScope } from "@/lib/feed/contracts";
import { NextRequest, NextResponse } from "next/server";

export const dynamic = "force-dynamic";

const scopes = new Set<FeedScope>(["for-you", "following", "community", "saved"]);
const NO_STORE_HEADERS = { "cache-control": "private, no-store" };

function unavailable(status: number): Response {
  return NextResponse.json(
    { error: "The social feed is unavailable." },
    { status, headers: NO_STORE_HEADERS },
  );
}

function safeStatus(status: number): number {
  return Number.isInteger(status) && status >= 400 && status <= 599 ? status : 500;
}

export async function GET(request: NextRequest): Promise<Response> {
  const { searchParams } = request.nextUrl;
  const scope = searchParams.get("scope");
  const takeValue = searchParams.get("take");
  const take = takeValue === null ? undefined : Number(takeValue);

  if (!scope || !scopes.has(scope as FeedScope) || (take !== undefined && (!Number.isInteger(take) || take < 1 || take > 30))) {
    return unavailable(400);
  }

  try {
    const page = await loadSocialFeed({
      scope: scope as FeedScope,
      cursor: searchParams.get("cursor"),
      take,
      tag: searchParams.get("tag"),
      signal: request.signal,
    });
    return NextResponse.json(page, { headers: NO_STORE_HEADERS });
  } catch (error) {
    return unavailable(
      error instanceof SocialFeedRequestError ? safeStatus(error.status) : 500,
    );
  }
}
