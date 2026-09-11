import { getRequestAuthContext } from "@/auth";
import { NextRequest } from "next/server";

const apiBaseUrl = (
  process.env.API_URL ||
  process.env.NEXT_PUBLIC_API_URL ||
  "http://localhost:8080"
).replace(/\/$/, "");

export async function GET(
  request: NextRequest,
  context: {
    params: Promise<{ courseId: string; contentId: string; runId: string }>;
  },
) {
  const { token, tenantId } = await getRequestAuthContext();
  if (!token || !tenantId) {
    return Response.json(
      { code: "UNAUTHENTICATED", detail: "You must be signed in." },
      { status: 401 },
    );
  }

  const { courseId, contentId, runId } = await context.params;
  const lastEventId = request.headers.get("last-event-id");
  const response = await fetch(
    `${apiBaseUrl}/v1/courses/${encodeURIComponent(courseId)}/content/${encodeURIComponent(contentId)}/authoring/ai/runs/${encodeURIComponent(runId)}/stream`,
    {
      headers: {
        Authorization: `Bearer ${token}`,
        "X-Tenant-Id": tenantId,
        Accept: "text/event-stream",
        ...(lastEventId ? { "Last-Event-ID": lastEventId } : {}),
      },
      cache: "no-store",
      signal: request.signal,
    },
  );

  if (!response.ok || !response.body) {
    return Response.json(
      { code: "AI_STREAM_UNAVAILABLE", detail: "The AI stream is unavailable." },
      { status: response.status || 502 },
    );
  }

  return new Response(response.body, {
    status: response.status,
    headers: {
      "Content-Type": "text/event-stream",
      "Cache-Control": "no-cache, no-transform",
      Connection: "keep-alive",
      "X-Accel-Buffering": "no",
    },
  });
}
