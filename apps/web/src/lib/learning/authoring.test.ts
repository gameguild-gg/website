import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  authContext: vi.fn(),
}));

vi.mock("@/auth", () => ({ getRequestAuthContext: mocks.authContext }));

import { createAiAuthoringRun, saveAuthoringDraft } from "./authoring";

describe("authoring server actions", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    mocks.authContext.mockResolvedValue({ token: "signed-token", tenantId: "tenant-1" });
  });

  it("derives billing identity exclusively from the authenticated server context", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(JSON.stringify({ id: "run-1" }), {
        status: 202,
        headers: { "Content-Type": "application/json" },
      }),
    );

    await createAiAuthoringRun("course-1", "lesson-1", {
      draftRevision: 2,
      instruction: "Expand this explanation",
      proposalKind: "ReplaceDocument",
      idempotencyKey: "request-1",
    });

    const [, init] = fetchMock.mock.calls[0]!;
    expect(init?.headers).toMatchObject({
      Authorization: "Bearer signed-token",
      "X-Tenant-Id": "tenant-1",
    });
    const body = JSON.parse(String(init?.body)) as Record<string, unknown>;
    expect(body).not.toHaveProperty("actorId");
    expect(body).not.toHaveProperty("userId");
    expect(body).not.toHaveProperty("tenantId");
  });

  it("does not call the API when no authenticated tenant actor exists", async () => {
    mocks.authContext.mockResolvedValue({ token: null, tenantId: null });
    const fetchMock = vi.spyOn(globalThis, "fetch");

    const result = await saveAuthoringDraft("course-1", "lesson-1", 1, {} as never);

    expect(result).toMatchObject({ success: false, status: 401 });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("surfaces the server's current revision on optimistic concurrency conflicts", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(
        JSON.stringify({ code: "AUTHORING_REVISION_CONFLICT", detail: "stale", currentRevision: 7 }),
        { status: 409, headers: { "Content-Type": "application/json" } },
      ),
    );

    const result = await saveAuthoringDraft("course-1", "lesson-1", 4, {} as never);

    expect(result).toEqual({
      success: false,
      error: "stale",
      status: 409,
      code: "AUTHORING_REVISION_CONFLICT",
      currentRevision: 7,
    });
  });
});
