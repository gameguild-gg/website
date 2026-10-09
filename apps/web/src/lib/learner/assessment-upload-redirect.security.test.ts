// @vitest-environment node
import { once } from "node:events";
import { createServer, type Server } from "node:http";
import {
  afterAll,
  afterEach,
  beforeAll,
  describe,
  expect,
  it,
  vi,
} from "vitest";

const mocks = vi.hoisted(() => ({ submit: vi.fn() }));
vi.mock("@/auth", () => ({
  getToken: vi.fn(async () => "owned-upload-token"),
}));
vi.mock("next/cache", () => ({ revalidatePath: vi.fn() }));
vi.mock("@game-guild/client", () => ({
  createServerClient: vi.fn(() => ({})),
  GeneratedApi: {
    LearningAssessmentsModule: class {
      async getAssessmentsMySubmissions() {
        return {
          ok: true,
          data: [
            {
              id: "submission-owned",
              assessmentId: "assessment-owned",
              status: "InProgress",
            },
          ],
        };
      }
      postAssessmentsSubmissionsSubmit = mocks.submit;
    },
  },
}));

import { submitAssessment } from "./activity-actions";

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

function submissionForm(): FormData {
  const data = new FormData();
  data.set("assessmentId", "assessment-owned");
  data.set("enrollmentId", "enrollment-owned");
  data.set("modality", "File");
  data.set(
    "file",
    new File(["owned-private-file-content"], "answer.txt", {
      type: "text/plain",
    }),
  );
  return data;
}

describe("assessment file upload HTTP redirect boundary", () => {
  const originRequests: {
    url: string | undefined;
    authorization: string | undefined;
    body: string;
  }[] = [];
  const destinationRequests: {
    url: string | undefined;
    authorization: string | undefined;
    body: string;
  }[] = [];
  let originUrl: string;
  let destinationUrl: string;
  let status = 201;
  let location: string;
  const destination = createServer(async (request, response) => {
    const chunks: Buffer[] = [];
    for await (const chunk of request) chunks.push(Buffer.from(chunk));
    destinationRequests.push({
      url: request.url,
      authorization: request.headers.authorization,
      body: Buffer.concat(chunks).toString("utf8"),
    });
    response.writeHead(200, { "Content-Type": "application/json" });
    response.end(JSON.stringify({ assetReferenceId: "asset-owned" }));
  });
  const origin = createServer(async (request, response) => {
    const chunks: Buffer[] = [];
    for await (const chunk of request) chunks.push(Buffer.from(chunk));
    const record = {
      url: request.url,
      authorization: request.headers.authorization,
      body: Buffer.concat(chunks).toString("utf8"),
    };
    if (request.url?.startsWith("/private-target")) {
      destinationRequests.push(record);
      response.writeHead(200, { "Content-Type": "application/json" });
      response.end(JSON.stringify({ assetReferenceId: "asset-owned" }));
      return;
    }
    originRequests.push(record);
    response.writeHead(
      status,
      status >= 300 && status < 400
        ? { Location: location }
        : { "Content-Type": "application/json" },
    );
    response.end(
      status === 201
        ? JSON.stringify({ assetReferenceId: "asset-owned" })
        : status === 400
          ? JSON.stringify({ detail: "Asset upload was rejected." })
          : undefined,
    );
  });

  beforeAll(async () => {
    originUrl = await listen(origin);
    destinationUrl = await listen(destination);
    vi.stubEnv("NODE_ENV", "production");
    vi.stubEnv("ALLOW_UNSAFE_REMOTE_URL", undefined);
    vi.stubEnv("API_URL", originUrl);
    mocks.submit.mockResolvedValue({ ok: true, data: {} });
  });
  afterEach(() => {
    originRequests.length = 0;
    destinationRequests.length = 0;
    status = 201;
    vi.stubEnv("API_URL", originUrl);
    mocks.submit.mockClear();
  });
  afterAll(async () => {
    await close(origin);
    await close(destination);
    vi.unstubAllEnvs();
  });

  it.each(
    [301, 302, 303, 307, 308].flatMap((redirectStatus) =>
      ["same origin", "other origin"].map((target) => ({
        redirectStatus,
        target,
      })),
    ),
  )(
    "rejects $redirectStatus to $target before forwarding a private file or credentials",
    async ({ redirectStatus, target }) => {
      status = redirectStatus;
      location = `${target === "same origin" ? originUrl : destinationUrl}/private-target`;
      const result = await submitAssessment(
        { success: false },
        submissionForm(),
      );
      expect.soft(result.success).toBe(false);
      expect.soft(destinationRequests).toEqual([]);
      expect.soft(mocks.submit).not.toHaveBeenCalled();
      expect(originRequests).toHaveLength(1);
      expect(originRequests[0].authorization).toBe("Bearer owned-upload-token");
      expect(originRequests[0].body).toContain("owned-private-file-content");
    },
  );

  it("preserves a successful private upload and concrete submission parenting", async () => {
    const result = await submitAssessment({ success: false }, submissionForm());
    expect(result).toEqual({ success: true });
    expect(destinationRequests).toEqual([]);
    expect(originRequests).toHaveLength(1);
    const target = new URL(originRequests[0].url!, originUrl);
    expect(target.pathname).toBe("/v1/assets");
    expect(target.searchParams.get("accessPolicy")).toBe("Private");
    expect(target.searchParams.get("parentResourceType")).toBe(
      "AssessmentSubmission",
    );
    expect(target.searchParams.get("parentResourceId")).toBe(
      "submission-owned",
    );
    expect(originRequests[0].body).toContain("owned-private-file-content");
    expect(mocks.submit).toHaveBeenCalledWith("submission-owned", {
      filePayload: "asset-owned",
    });
  });

  it("preserves an API upload failure without submitting an answer", async () => {
    status = 400;
    expect(
      await submitAssessment({ success: false }, submissionForm()),
    ).toEqual({ success: false, error: "Asset upload was rejected." });
    expect(destinationRequests).toEqual([]);
    expect(mocks.submit).not.toHaveBeenCalled();
    expect(originRequests).toHaveLength(1);
  });

  it.each(["credentials", "ftp", "relative"])(
    "rejects invalid %s API configuration without exposing its value",
    async (kind) => {
      vi.stubEnv(
        "API_URL",
        kind === "credentials"
          ? originUrl.replace("http://", "http://owned-user:owned-password@")
          : kind === "ftp"
            ? originUrl.replace("http:", "ftp:")
            : "/owned-relative-api",
      );
      const result = await submitAssessment(
        { success: false },
        submissionForm(),
      );
      expect(result).toEqual({
        success: false,
        error: "The file could not be uploaded.",
      });
      expect(JSON.stringify(result)).not.toContain("owned-password");
      expect(originRequests).toEqual([]);
      expect(destinationRequests).toEqual([]);
      expect(mocks.submit).not.toHaveBeenCalled();
    },
  );
});
