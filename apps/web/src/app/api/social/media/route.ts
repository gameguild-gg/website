import { getToken } from "@/auth";
import { NextRequest, NextResponse } from "next/server";

export const dynamic = "force-dynamic";

export async function POST(request: NextRequest): Promise<Response> {
  const token = await getToken();
  if (!token) {
    return NextResponse.json({ error: "You must be signed in to upload media." }, { status: 401 });
  }

  const formData = await request.formData();
  const file = formData.get("file");
  if (!file || typeof file === "string") {
    return NextResponse.json({ error: "A media file is required." }, { status: 400 });
  }
  const apiBaseUrl = (process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080").replace(/\/$/, "");
  const upstream = await fetch(new URL("/v1/assets/social-media", `${apiBaseUrl}/`), {
    method: "POST",
    headers: { authorization: `Bearer ${token}` },
    body: formData,
    cache: "no-store",
  });
  const contentType = upstream.headers.get("content-type") || "application/json";
  return new Response(upstream.body, { status: upstream.status, statusText: upstream.statusText, headers: { "content-type": contentType, "cache-control": "private, no-store" } });
}
