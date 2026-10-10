import { getRequestAuthContext } from "@/auth";
import { assertSafeServiceUrl } from "@/lib/security/safe-remote-url";
import { NextRequest } from "next/server";

const apiBaseUrl = (
  process.env.API_URL ||
  process.env.NEXT_PUBLIC_API_URL ||
  "http://localhost:8080"
).replace(/\/$/, "");

/**
 * Authenticated GET passthrough for blog AI reads (run status, entitlement,
 * conversations) that the browser client needs between SSE frames. Actions in
 * `lib/blogs/actions.ts` own POST/PUT/DELETE; this route covers the read trio
 * without growing the file set of other todos.
 */
export async function GET(
  request: NextRequest,
  context: { params: Promise<{ postId: string; aiPath: string[] }> },
) {
  const { token, tenantId } = await getRequestAuthContext();
  if (!token || !tenantId) {
    return Response.json(
      { code: "UNAUTHENTICATED", detail: "You must be signed in." },
      { status: 401 },
    );
  }

  const { postId, aiPath } = await context.params;
  const suffix = (aiPath ?? []).map(encodeURIComponent).join("/");
  const query = request.nextUrl.search;
  const response = await fetch(
    assertSafeServiceUrl(`${apiBaseUrl}/api/social/blog/posts/${encodeURIComponent(postId)}/ai${suffix ? `/${suffix}` : ""}${query}`, apiBaseUrl),
    {
      redirect: "error",
      headers: {
        Authorization: `Bearer ${token}`,
        "X-Tenant-Id": tenantId,
        Accept: "application/json",
      },
      cache: "no-store",
      signal: request.signal,
    },
  );

  const body = await response.text();
  return new Response(body, {
    status: response.status,
    headers: { "Content-Type": "application/json" },
  });
}
