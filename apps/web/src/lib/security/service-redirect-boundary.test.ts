// @vitest-environment node
import { createServer, type Server } from "node:http";
import { once } from "node:events";
import { afterAll, beforeAll, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";

vi.mock("@/auth", () => ({
  getToken: vi.fn(async () => "test-only-token"),
  getRequestAuthContext: vi.fn(async () => ({
    token: "test-only-token",
    tenantId: "test-tenant",
  })),
}));

async function listen(server: Server): Promise<string> {
  server.listen(0, "127.0.0.1");
  await once(server, "listening");
  const address = server.address();
  if (!address || typeof address === "string")
    throw new Error("Expected an owned TCP server");
  return `http://127.0.0.1:${address.port}`;
}

async function close(server: Server): Promise<void> {
  await new Promise<void>((resolve, reject) => {
    server.close((error) => (error ? reject(error) : resolve()));
    server.closeAllConnections();
  });
}

describe("authenticated service HTTP redirect boundary", () => {
  const destinationRequests: { url: string | undefined; body: string }[] = [];
  const originRequests: {
    url: string | undefined;
    authorization: string | undefined;
    tenant: string | undefined;
    body: string;
  }[] = [];
  let status = 200;
  let baseUrl: string;
  let destinationUrl: string;
  let invoke: Record<string, () => Promise<unknown>>;
  const destination = createServer(async (request, response) => {
    const chunks: Buffer[] = [];
    for await (const chunk of request) chunks.push(Buffer.from(chunk));
    destinationRequests.push({
      url: request.url,
      body: Buffer.concat(chunks).toString("utf8"),
    });
    response.writeHead(200, { "Content-Type": "application/json" });
    response.end(JSON.stringify({ accepted: true }));
  });
  const origin = createServer(async (request, response) => {
    const chunks: Buffer[] = [];
    for await (const chunk of request) chunks.push(Buffer.from(chunk));
    originRequests.push({
      url: request.url,
      authorization: request.headers.authorization,
      tenant: request.headers["x-tenant-id"] as string | undefined,
      body: Buffer.concat(chunks).toString("utf8"),
    });
    response.writeHead(
      status,
      status === 200
        ? { "Content-Type": "application/json" }
        : { Location: `${destinationUrl}/private-target` },
    );
    response.end(
      status === 200 ? JSON.stringify({ accepted: true }) : undefined,
    );
  });

  beforeAll(async () => {
    destinationUrl = await listen(destination);
    baseUrl = await listen(origin);
    vi.stubEnv("NODE_ENV", "production");
    vi.stubEnv("ALLOW_UNSAFE_REMOTE_URL", undefined);
    vi.stubEnv("API_URL", baseUrl);
    vi.resetModules();
    const authoring = await import("@/lib/learning/authoring");
    const assets = await import("@/app/api/assets/[assetId]/route");
    const blogAi =
      await import("@/app/api/blogs/authoring/[postId]/ai/[...aiPath]/route");
    invoke = {
      authoringGet: () =>
        authoring.getAuthoringDraft("course/id", "content/id"),
      authoringPost: () =>
        authoring.publishAuthoringDraft("course/id", "content/id", 17),
      assetGet: () =>
        assets.GET(
          new NextRequest(
            "https://gameguild.gg/api/assets/test?includeContent=true",
          ),
          { params: Promise.resolve({ assetId: "asset/id" }) },
        ),
      assetDelete: () =>
        assets.DELETE(
          new NextRequest("https://gameguild.gg/api/assets/test", {
            method: "DELETE",
          }),
          { params: Promise.resolve({ assetId: "asset/id" }) },
        ),
      blogAiGet: () =>
        blogAi.GET(
          new NextRequest("https://gameguild.gg/api/blogs/test?run=test-value"),
          {
            params: Promise.resolve({
              postId: "post/id",
              aiPath: ["runs", "run/id"],
            }),
          },
        ),
    };
  });

  afterAll(async () => {
    await close(origin);
    await close(destination);
    vi.unstubAllEnvs();
    vi.resetModules();
  });

  const operations = [
    "authoringGet",
    "authoringPost",
    "assetGet",
    "assetDelete",
    "blogAiGet",
  ];
  it.each(
    [301, 302, 303, 307, 308].flatMap((redirectStatus) =>
      operations.map((operation) => ({ redirectStatus, operation })),
    ),
  )(
    "rejects $redirectStatus for $operation without contacting another origin",
    async ({ redirectStatus, operation }) => {
      status = redirectStatus;
      destinationRequests.length = 0;
      originRequests.length = 0;
      await expect.soft(invoke[operation]()).rejects.toThrow();
      expect.soft(destinationRequests).toEqual([]);
      expect(originRequests).toHaveLength(1);
      expect(originRequests[0].authorization).toBe("Bearer test-only-token");
      if (operation.startsWith("authoring") || operation === "blogAiGet")
        expect(originRequests[0].tenant).toBe("test-tenant");
      if (operation === "authoringPost")
        expect(originRequests[0].body).toBe(JSON.stringify({ revision: 17 }));
    },
  );

  it.each(operations)(
    "preserves authenticated normal responses for %s",
    async (operation) => {
      status = 200;
      originRequests.length = 0;
      const result = await invoke[operation]();
      if (result instanceof Response)
        expect(await result.json()).toEqual({ accepted: true });
      else expect(result).toEqual({ success: true, data: { accepted: true } });
      expect(originRequests).toHaveLength(1);
      expect(originRequests[0].authorization).toBe("Bearer test-only-token");
      expect(originRequests[0].url).toContain("%2F");
    },
  );
});
