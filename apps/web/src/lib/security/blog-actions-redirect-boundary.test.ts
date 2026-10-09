// @vitest-environment node
import { once } from "node:events";
import { createServer, type Server } from "node:http";
import { afterAll, beforeAll, describe, expect, it, vi } from "vitest";

vi.mock("@/auth", () => ({
  getRequestAuthContext: vi.fn(async () => ({
    token: "test-only-token",
    tenantId: "test-tenant",
    session: { user: { id: "user/id" } },
  })),
}));

async function listen(server: Server): Promise<string> {
  server.listen(0, "127.0.0.1");
  await once(server, "listening");
  const address = server.address();
  if (!address || typeof address === "string") throw new Error("Expected an owned TCP server");
  return `http://127.0.0.1:${address.port}`;
}

async function close(server: Server): Promise<void> {
  await new Promise<void>((resolve, reject) => {
    server.close((error) => (error ? reject(error) : resolve()));
    server.closeAllConnections();
  });
}

describe("blog action HTTP redirect boundary", () => {
  let redirectStatus = 200;
  let destinationUrl: string;
  const destinationRequests: string[] = [];
  const originRequests: { url: string; method: string; authorization?: string; tenant?: string; body: string }[] = [];
  let invoke: Record<string, () => Promise<unknown>>;
  const post = { id: "post/id", slug: "post", primaryAuthorId: "user/id" };
  const destination = createServer((request, response) => {
    destinationRequests.push(request.url ?? "");
    response.writeHead(200, { "Content-Type": "application/json" });
    response.end(JSON.stringify({ userId: "user/id", handle: "jane", type: "Like" }));
  });
  const origin = createServer(async (request, response) => {
    const chunks: Buffer[] = [];
    for await (const chunk of request) chunks.push(Buffer.from(chunk));
    originRequests.push({
      url: request.url ?? "",
      method: request.method ?? "",
      authorization: request.headers.authorization,
      tenant: request.headers["x-tenant-id"] as string | undefined,
      body: Buffer.concat(chunks).toString("utf8"),
    });
    // Creation succeeds first so its private profile lookup is also exercised.
    const creatingPost = request.url === "/api/social/blog/posts" && request.method === "POST";
    const status = creatingPost ? 200 : redirectStatus;
    response.writeHead(status, status === 200
      ? { "Content-Type": "application/json" }
      : { Location: `${destinationUrl}/private-target` });
    response.end(status === 200 ? JSON.stringify(creatingPost ? post
      : request.url?.includes("/profiles/") ? { userId: "user/id", handle: "jane" }
      : { type: "Like", accepted: true }) : undefined);
  });

  beforeAll(async () => {
    destinationUrl = await listen(destination);
    const baseUrl = await listen(origin);
    vi.stubEnv("NODE_ENV", "production");
    vi.stubEnv("ALLOW_UNSAFE_REMOTE_URL", undefined);
    vi.stubEnv("API_URL", baseUrl);
    vi.resetModules();
    const actions = await import("@/lib/blogs/actions");
    invoke = {
      reactionGet: () => actions.fetchViewerReaction("post/id"),
      reactionPut: () => actions.setReaction("post/id", true),
      reactionDelete: () => actions.setReaction("post/id", false),
      profileByHandle: () => actions.resolveProfileByHandle("jane/author"),
      viewerProfile: () => actions.getViewerBlogAuthor(),
      createdPostProfile: () => actions.createPost({ title: "Fixture", format: "Markdown" }),
    };
  });

  afterAll(async () => {
    await close(origin);
    await close(destination);
    vi.unstubAllEnvs();
    vi.resetModules();
  });

  const operations = ["reactionGet", "reactionPut", "reactionDelete", "profileByHandle", "viewerProfile", "createdPostProfile"];
  it.each([301, 302, 303, 307, 308].flatMap((status) => operations.map((operation) => ({ status, operation }))))(
    "rejects $status for $operation without contacting another origin",
    async ({ status, operation }) => {
      redirectStatus = status;
      destinationRequests.length = 0;
      originRequests.length = 0;
      await expect.soft(invoke[operation]()).rejects.toThrow();
      expect.soft(destinationRequests).toEqual([]);
      expect(originRequests).toHaveLength(operation === "createdPostProfile" ? 2 : 1);
      for (const request of originRequests) expect(request.authorization).toBe("Bearer test-only-token");
      if (operation === "reactionPut" || operation === "reactionDelete") {
        expect(originRequests[0].tenant).toBe("test-tenant");
        expect(JSON.parse(originRequests[0].body)).toMatchObject({ targetType: "BlogPost", targetId: "post/id" });
      }
    },
  );

  const normalResults: Record<string, unknown> = {
    reactionGet: { reacted: true },
    reactionPut: { ok: true },
    reactionDelete: { ok: true },
    profileByHandle: { success: true, userId: "user/id" },
    viewerProfile: { userId: "user/id", handle: "jane" },
    createdPostProfile: { success: true, data: post, editUrl: "/blogs/jane/post/edit" },
  };
  it.each(operations)("preserves normal authenticated responses for %s", async (operation) => {
    redirectStatus = 200;
    destinationRequests.length = 0;
    originRequests.length = 0;
    expect(await invoke[operation]()).toEqual(normalResults[operation]);
    expect(destinationRequests).toEqual([]);
    expect(originRequests).toHaveLength(operation === "createdPostProfile" ? 2 : 1);
    expect(originRequests.at(-1)?.url).toContain(operation.startsWith("reaction") && operation !== "reactionGet" ? "/api/social/reactions" : "%2F");
    for (const request of originRequests) expect(request.authorization).toBe("Bearer test-only-token");
  });
});
