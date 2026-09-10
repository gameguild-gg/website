import { loadSocialFeed } from "@/lib/feed/queries";
import type { FeedScope } from "@/lib/feed/contracts";
import { NextRequest, NextResponse } from "next/server";

export const dynamic = "force-dynamic";

const scopes = new Set<FeedScope>(["for-you", "following", "community", "saved"]);

export async function GET(request: NextRequest): Promise<Response> {
  const { searchParams } = request.nextUrl;
  const scope = searchParams.get("scope");
  const takeValue = searchParams.get("take");
  const take = takeValue === null ? undefined : Number(takeValue);

  if (!scope || !scopes.has(scope as FeedScope) || (take !== undefined && (!Number.isInteger(take) || take < 1 || take > 30))) {
    return NextResponse.json({ error: "Invalid social feed pagination." }, { status: 400 });
  }

  const page = await loadSocialFeed({
    scope: scope as FeedScope,
    cursor: searchParams.get("cursor"),
    take,
    tag: searchParams.get("tag"),
    signal: request.signal,
  });
  return NextResponse.json(page, { headers: { "cache-control": "private, no-store" } });
}
