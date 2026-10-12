import { getRequestAuthContext } from "@/auth";
import { assertSafeServiceUrl } from "@/lib/security/safe-remote-url";
import type {
  TestingLabQuestionnaireOutput,
  TestingLabQuestionnaireSchema,
  TestingLabTestingProjectApplicationProjection,
  TestingLabTestingProjectBrief,
} from "@game-guild/client";
import { NextRequest, NextResponse } from "next/server";

interface SaveApplicationDraftPayload {
  eventId?: string;
  projectId?: string;
  applicationId?: string;
  projectVersionId?: string;
  brief?: TestingLabTestingProjectBrief;
  feedbackQuestionnaire?: TestingLabQuestionnaireSchema;
  eventApplicationResponse?: TestingLabQuestionnaireOutput;
  acceptedRules?: boolean;
  preferredAvailability?: string;
  submittedAssetReferenceIds?: string[];
  intent?: "save" | "submit";
}

const apiBaseUrl = (
  process.env.API_URL ||
  process.env.NEXT_PUBLIC_API_URL ||
  "http://localhost:8080"
).replace(/\/$/, "");

function errorMessage(body: unknown, fallback: string) {
  if (typeof body !== "object" || body === null) return fallback;
  const value = body as Record<string, unknown>;
  return (
    [value.detail, value.title, value.message, value.error].find(
      (candidate): candidate is string =>
        typeof candidate === "string" && candidate.trim().length > 0,
    ) ?? fallback
  );
}

async function backendRequest<T>(
  path: string,
  method: "POST" | "PUT",
  token: string,
  tenantId: string,
  body?: unknown,
): Promise<{ data: T } | { error: string; status: number }> {
  // SSRF (Codacy rule-node-ssrf) — false positive: the target origin is pinned
  // to the env-configured API base by assertSafeServiceUrl, and the only
  // request-derived data is encodeURIComponent'd path segments that cannot
  // escape the path (origin/protocol/credentials re-checked by the guard).
  const response = await fetch(assertSafeServiceUrl(`${apiBaseUrl}${path}`, apiBaseUrl), {
    redirect: "error",
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
      "X-Tenant-Id": tenantId,
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    cache: "no-store",
  });
  const responseBody = await response.json().catch(() => null);
  if (!response.ok) {
    return {
      error: errorMessage(
        responseBody,
        response.statusText || "Testing Lab request failed.",
      ),
      status: response.status,
    };
  }
  return { data: responseBody as T };
}

function sameOrigin(request: NextRequest): boolean {
  const origin = request.headers.get("origin");
  if (!origin) return true;
  if (!URL.canParse(origin)) return false;

  const forwardedHost = request.headers
    .get("x-forwarded-host")
    ?.split(",")[0]
    ?.trim();
  const host = forwardedHost || request.headers.get("host");
  if (!host) return false;

  const forwardedProtocol = request.headers
    .get("x-forwarded-proto")
    ?.split(",")[0]
    ?.trim();
  const protocol = forwardedProtocol
    ? `${forwardedProtocol}:`
    : request.nextUrl.protocol;
  const originUrl = new URL(origin);
  return originUrl.host === host && originUrl.protocol === protocol;
}

export async function POST(request: NextRequest): Promise<NextResponse> {
  if (!sameOrigin(request)) {
    return NextResponse.json(
      { success: false, error: "Cross-origin requests are not allowed." },
      { status: 403 },
    );
  }

  const payload = (await request
    .json()
    .catch(() => null)) as SaveApplicationDraftPayload | null;
  const eventId = payload?.eventId?.trim() ?? "";
  const projectId = payload?.projectId?.trim() ?? "";
  if (!eventId || !projectId) {
    return NextResponse.json(
      { success: false, error: "Event and project are required." },
      { status: 400 },
    );
  }

  const { token, tenantId } = await getRequestAuthContext();
  if (!token || !tenantId) {
    return NextResponse.json(
      {
        success: false,
        error: "You must be signed in to save a project application.",
      },
      { status: 401 },
    );
  }

  let applicationId = payload?.applicationId?.trim() ?? "";
  if (!applicationId) {
    const created =
      await backendRequest<TestingLabTestingProjectApplicationProjection>(
        `/v1/testing/events/${encodeURIComponent(eventId)}/applications/drafts`,
        "POST",
        token,
        tenantId,
        { projectId },
      );
    if ("error" in created) {
      return NextResponse.json(
        { success: false, error: created.error },
        { status: created.status },
      );
    }
    applicationId = created.data.id ?? "";
  }

  if (!applicationId) {
    return NextResponse.json(
      { success: false, error: "The application draft could not be created." },
      { status: 502 },
    );
  }

  const saved =
    await backendRequest<TestingLabTestingProjectApplicationProjection>(
      `/v1/testing/events/applications/${encodeURIComponent(applicationId)}/draft`,
      "PUT",
      token,
      tenantId,
      {
        projectVersionId: payload?.projectVersionId || undefined,
        brief: payload?.brief,
        feedbackQuestionnaire: payload?.feedbackQuestionnaire,
        eventApplicationResponse: payload?.eventApplicationResponse,
        acceptedRules: payload?.acceptedRules,
        preferredAvailability: payload?.preferredAvailability || null,
        submittedAssetReferenceIds: payload?.submittedAssetReferenceIds ?? [],
      },
    );
  if ("error" in saved) {
    return NextResponse.json(
      { success: false, error: saved.error },
      { status: saved.status },
    );
  }

  if (payload?.intent === "submit" && saved.data.status === "Draft") {
    const submitted =
      await backendRequest<TestingLabTestingProjectApplicationProjection>(
        `/v1/testing/events/applications/${encodeURIComponent(applicationId)}:submit`,
        "POST",
        token,
        tenantId,
      );
    if ("error" in submitted) {
      return NextResponse.json(
        { success: false, error: submitted.error },
        { status: submitted.status },
      );
    }
    return NextResponse.json({
      success: true,
      data: submitted.data,
      message: "Project application submitted.",
    });
  }

  return NextResponse.json({
    success: true,
    data: saved.data,
    message: "Application draft saved.",
  });
}
