import { getToken } from "@/auth";
import { type NextRequest, NextResponse } from "next/server";

export const dynamic = "force-dynamic";

const FORWARDED_RESPONSE_HEADERS = [
  "accept-ranges",
  "content-length",
  "content-range",
  "content-type",
  "etag",
  "last-modified",
] as const;

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ assetId: string }> },
): Promise<Response> {
  const token = await getToken();
  if (!token) {
    return NextResponse.json(
      { error: "You must be signed in to view this media." },
      { status: 401 },
    );
  }

  const { assetId } = await params;
  const apiBaseUrl = (
    process.env.API_URL ||
    process.env.NEXT_PUBLIC_API_URL ||
    "http://localhost:8080"
  ).replace(/\/$/, "");
  const upstreamUrl = new URL(
    `/api/assets/${encodeURIComponent(assetId)}/content`,
    `${apiBaseUrl}/`,
  );
  const transform = request.nextUrl.searchParams.get("transform");
  if (transform) upstreamUrl.searchParams.set("transform", transform);

  const range = request.headers.get("range");
  const upstream = await fetch(upstreamUrl, {
    method: "GET",
    headers: {
      authorization: `Bearer ${token}`,
      ...(range ? { range } : {}),
    },
    cache: "no-store",
    redirect: "follow",
  });

  const headers = new Headers();
  for (const name of FORWARDED_RESPONSE_HEADERS) {
    const value = upstream.headers.get(name);
    if (value) headers.set(name, value);
  }
  headers.set("cache-control", "private, max-age=300");
  headers.set("x-content-type-options", "nosniff");

  return new Response(upstream.body, {
    status: upstream.status,
    statusText: upstream.statusText,
    headers,
  });
}
