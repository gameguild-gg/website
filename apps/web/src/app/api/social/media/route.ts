import { getToken } from "@/auth";
import { NextRequest, NextResponse } from "next/server";

export const dynamic = "force-dynamic";

export async function POST(request: NextRequest): Promise<Response> {
  const token = await getToken();
  if (!token) {
    return NextResponse.json({ error: "You must be signed in to upload media." }, { status: 401 });
  }

  const incoming = await request.formData();
  const files = incoming.getAll("file");
  const file = files[0];
  if (files.length !== 1 || !file || typeof file === "string") {
    return NextResponse.json({ error: "A media file is required." }, { status: 400 });
  }
  const formData = new FormData();
  formData.append("file", file);
  const apiBaseUrl = (process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080").replace(/\/$/, "");
  let upstream: Response;
  try {
    upstream = await fetch(new URL("/v1/assets/social-media", `${apiBaseUrl}/`), {
      method: "POST",
      headers: { authorization: `Bearer ${token}` },
      body: formData,
      signal: request.signal,
      cache: "no-store",
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      return NextResponse.json({ error: "Upload cancelled." }, { status: 499, headers: { "cache-control": "private, no-store" } });
    }
    return NextResponse.json({ error: "Media upload is temporarily unavailable." }, { status: 502, headers: { "cache-control": "private, no-store" } });
  }
  const contentType = upstream.headers.get("content-type") || "application/json";
  return new Response(upstream.body, { status: upstream.status, statusText: upstream.statusText, headers: { "content-type": contentType, "cache-control": "private, no-store" } });
}
