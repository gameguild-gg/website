#!/usr/bin/env node
// @ts-check
/**
 * Self-contained full-cycle coding-assignment browser E2E.
 *
 * One command boots an ISOLATED stack (disposable postgres on 5433, dotnet
 * API on 8180, Next.js web on 3012) so it never clobbers the user's running
 * dev env, then drives the full coding-assignment cycle through a REAL
 * Chromium + REAL WASM toolchain — zero mocks of app code:
 *
 *   Phase 1 SEED (API, as instructor/admin):
 *     - unique instructor + student users, tenant, published course
 *     - ProgramContent(type=Code) + assessment(contentId, modality=Code)
 *     - PUT v1 coding-assignment content: Public = 1 stdio + 1 functional,
 *       Private = 1 stdio; one modifiable main.cpp starter that fails
 *     - rubric PUT (criteria summing to maxScore)
 *     - enroll the student via POST /api/learning/enrollments
 *
 *   Phase 2 STUDENT UI:
 *     - browser credentials session → activity URL → IDE boots (REAL WASM)
 *     - learner workspace never renders the private test name
 *     - Run public tests → public tests FAIL (starter code)
 *     - TYPE real code into Monaco (keyboard.type) → Run public tests → public
 *       tests PASS → Submit → success redirect
 *
 *   Phase 3 PAYLOAD CHECK (API): GET the official submission → assert
 *     codePayload CONTAINS the typed code from the immutable response.
 *
 *   Phase 4 INSTRUCTOR UI: sign in → SpeedGrader → IDE shows
 *     student submission and its frozen private definition → Run Tests
 *     → full plan (public+private; assert private name appears, 3 rows) →
 *     rubric grid → fill points → submit grade.
 *
 *   Phase 5 GRADE CHECK (API + DB): submission status Graded, score,
 *     RubricScoresPayload non-null (DB probe — the submission DTO does not
 *     expose the rubric scores payload).
 *
 * Teardown (trap exit/SIGINT/SIGTERM): kill web + api, docker rm -f the
 * disposable postgres. Artifacts/logs/screenshots under
 * apps/web/test-results/coding-cycle/.
 *
 * Run: pnpm --filter @game-guild/web test:browser:coding-cycle
 *      (or) node apps/web/scripts/coding-cycle-browser-e2e.mjs
 */

import { spawn, spawnSync } from "node:child_process";
import { randomUUID } from "node:crypto";
import { mkdir, writeFile } from "node:fs/promises";
import { existsSync, readFileSync, createWriteStream, rmSync } from "node:fs";
import { resolve } from "node:path";
import { createClient, GeneratedApi } from "@game-guild/client";
import { chromium } from "playwright";
import { scoreValueFromPoints } from "../../../packages/features/grading/src/values.ts";
import { resolveChromiumExecutablePath } from "./browser-executable.mjs";
import {
  assertSharedAuthCookie,
  trackAppHttpFailures,
} from "./learning-browser-e2e-support.mjs";

// ---------------------------------------------------------------------------
// Config
// ---------------------------------------------------------------------------

const SCRIPT_DIR = import.meta.dirname;
const WEB_DIR = resolve(SCRIPT_DIR, "..");
const REPO_ROOT = resolve(WEB_DIR, "../..");

const PG_PORT = Number(process.env.CODING_CYCLE_PG_PORT ?? 5433);
const API_PORT = Number(process.env.CODING_CYCLE_API_PORT ?? 8180);
const WEB_PORT = Number(process.env.CODING_CYCLE_WEB_PORT ?? 3012);
// The Ubuntu runner's installed Chrome has a root-owned AppArmor userns profile.
// The grading worker keeps its mandatory sandbox; no host security setting changes.
const CODE_GRADING_BROWSER_CHANNEL = process.env.CODING_CYCLE_GRADING_BROWSER_CHANNEL ??
  (process.platform === "linux" && process.env.GITHUB_ACTIONS === "true" ? "chrome" : undefined);
if (CODE_GRADING_BROWSER_CHANNEL !== undefined && CODE_GRADING_BROWSER_CHANNEL !== "chrome") {
  throw new Error("The Code cycle supports only its bundled browser or the installed Chrome channel.");
}
const PG_USER = "gameguild_e2e";
const PG_PASSWORD = "gameguild_e2e_password";
const PG_DB = "gameguild_e2e";
const PG_IMAGE = "postgres:17-alpine";
const PG_CONTAINER = `gg-e2e-pg-coding-${process.pid}-${Date.now()}`;

// Concurrent-run lock: mkdir is atomic, so EEXIST means another run holds it.
const LOCK_DIR = resolve(WEB_DIR, "test-results/coding-cycle.lock");

const API_BASE = `http://127.0.0.1:${API_PORT}`;
// MUST browse via `localhost`, never 127.0.0.1: Next 16.3 dev treats the
// 127.0.0.1 origin as disallowed (allowedDevOrigins) — the HMR websocket is
// rejected and React hydration never completes, leaving forms dead.
const WEB_BASE = (
  process.env.CODING_CYCLE_WEB_BASE ??
  `http://localhost:${WEB_PORT}`
).replace(/\/$/, "");

const ADMIN_EMAIL = process.env.E2E_SYSTEM_ADMIN_EMAIL ?? "admin@game-guild.com";
const ADMIN_PASSWORD = process.env.E2E_SYSTEM_ADMIN_PASSWORD ?? "Admin123!";

const ARTIFACTS = resolve(WEB_DIR, "test-results/coding-cycle");
const EVIDENCE = resolve(ARTIFACTS, "evidence");
const RUNTIME = resolve(ARTIFACTS, "runtime");
const API_LOG = resolve(RUNTIME, "api.log");
const WEB_LOG = resolve(RUNTIME, "web.log");
const PG_LOG = resolve(RUNTIME, "pg.log");

const HEADLESS = !["0", "false", "no"].includes(
  (process.env.CODING_CYCLE_HEADLESS ?? "true").toLowerCase(),
);
// CI uses Playwright's managed browser. Local Windows machines may instead
// point this at an installed Chrome when the matching Playwright download is
// intentionally absent (for example after clearing disk space).
const CHROMIUM_EXECUTABLE_PATH = resolveChromiumExecutablePath({ exists: existsSync });
// A developer can reuse a known-current API build when validating only the
// browser flow. This is opt-in: CI and the default command still compile API
// sources before the isolated cycle starts.
const API_NO_BUILD = process.env.CODING_CYCLE_API_NO_BUILD === "1";
// A disposable database starts empty and the API currently applies more than
// one hundred migrations. Four minutes is insufficient on a cold Windows
// volume; callers can lower or raise this without editing the runner.
const API_BOOT_TIMEOUT_MS = Number(
  process.env.CODING_CYCLE_API_BOOT_TIMEOUT_MS ?? 900_000,
);

// Generous: first WASM boot downloads ~tens of MB of brotli bundles from
// /emception/* (localhost) + JITs clang.wasm. Each run-tests compile is
// clang -cc1 + wasm-ld, slow first time per context.
const IDE_BOOT_TIMEOUT_MS = 240_000;
const RUN_TESTS_TIMEOUT_MS = 300_000;

// ---------------------------------------------------------------------------
// Process / boot orchestration
// ---------------------------------------------------------------------------

/** @type {{ proc: import('child_process').ChildProcess, label: string }[]} */
const children = [];
let pgContainerStarted = false;
let tornDown = false;
let lockHeld = false;

function log(msg) {
  console.log(`[coding-cycle-e2e ${new Date().toISOString()}] ${msg}`);
}

async function acquireRunLock() {
  await mkdir(resolve(WEB_DIR, "test-results"), { recursive: true });
  try {
    await mkdir(LOCK_DIR);
  } catch (error) {
    if (error?.code === "EEXIST") {
      console.error(`Another coding-cycle e2e run holds ${LOCK_DIR} — aborting.`);
      process.exit(75);
    }
    throw error;
  }
  lockHeld = true;
}

function releaseRunLock() {
  if (!lockHeld) return;
  lockHeld = false;
  try { rmSync(LOCK_DIR, { recursive: true, force: true }); } catch { /* best effort */ }
}

function stopChild(entry) {
  if (!entry?.proc || entry.proc.exitCode != null || entry.proc.killed) return;
  if (process.platform === "win32") {
    // `dotnet run` starts MSBuild children. Killing only the launcher leaks
    // those processes and prevents a second isolated cycle from starting.
    try {
      spawnSync("taskkill", ["/pid", String(entry.proc.pid), "/t", "/f"], {
        stdio: "ignore",
      });
      return;
    } catch {
      try { entry.proc.kill("SIGTERM"); } catch { /* already gone */ }
      return;
    }
  }
  try {
    // Negative pid kills the whole process group (spawned detached).
    process.kill(-entry.proc.pid, "SIGTERM");
  } catch {
    try { entry.proc.kill("SIGTERM"); } catch { /* already gone */ }
  }
}

async function teardown(code) {
  if (tornDown) return;
  tornDown = true;
  releaseRunLock();
  for (const entry of children) stopChild(entry);
  // Give them a beat, then force.
  await new Promise((r) => setTimeout(r, 800));
  for (const entry of children) {
    if (entry.proc.exitCode == null && !entry.proc.killed) {
      try { process.kill(-entry.proc.pid, "SIGKILL"); } catch { /* gone */ }
    }
  }
  if (pgContainerStarted) {
    await new Promise((res) => {
      const rm = spawn("docker", ["rm", "-f", PG_CONTAINER], { stdio: "ignore" });
      rm.on("exit", () => res());
      rm.on("error", () => res());
      // Don't hang forever if docker is stuck.
      setTimeout(res, 5000);
    });
    log(`removed postgres container ${PG_CONTAINER}`);
  }
  log(`exit ${code}`);
}

process.on("exit", () => {
  // Synchronous fallback: the async teardown may not finish before exit.
  // Never deletes a lock we do not own (EEXIST abort exits with lockHeld=false).
  if (pgContainerStarted) {
    try { spawnSync("docker", ["rm", "-f", PG_CONTAINER], { stdio: "ignore" }); } catch { /* gone */ }
  }
  releaseRunLock();
});
process.on("SIGINT", () => { void teardown(130).finally(() => process.exit(130)); });
process.on("SIGTERM", () => { void teardown(143).finally(() => process.exit(143)); });

function assertPortAvailable(port, label) {
  // Reuse the testing-lab helper inline (no extra file dep).
  const out = spawnSyncSafe(port, label);
  if (out !== 0) {
    console.error(`${label} port ${port} is already in use.`);
    process.exit(1);
  }
}

function spawnSyncSafe(port, label) {
  const r = spawnSync(
    "node",
    [
      "-e",
      `const net=require("node:net");const s=net.createServer();s.once("error",()=>{console.error("${label} port ${port} in use");process.exit(1)});s.listen(${port},"0.0.0.0",()=>s.close())`,
    ],
    { stdio: "inherit" },
  );
  return r.status ?? 0;
}

function waitForHttp(url, label, logFile, timeoutMs, proc) {
  return new Promise((resolveP, rejectP) => {
    const deadline = Date.now() + timeoutMs;
    const tick = async () => {
      if (proc && proc.exitCode != null) {
        rejectP(new Error(`${label} process exited before readiness`));
        return;
      }
      try {
        const r = await fetch(url, { redirect: "manual" });
        if (r.ok || (r.status >= 200 && r.status < 400) || r.status === 401) {
          resolveP();
          return;
        }
      } catch { /* not ready */ }
      if (Date.now() > deadline) {
        const tail = readTail(logFile, 160);
        rejectP(new Error(`${label} not ready at ${url} after ${timeoutMs}ms\n${tail}`));
        return;
      }
      setTimeout(tick, 1000);
    };
    tick();
  });
}

function readTail(file, lines) {
  try {
    const data = readFileSync(file, "utf8");
    return data.split("\n").slice(-lines).join("\n");
  } catch { return ""; }
}

async function bootStack() {
  // Build the runtime used by official Code submissions. The existing release
  // gate stages verified compiler artifacts before this isolated browser cycle.
  const gradingBuild = spawnSync(process.execPath, ["grading/build.mjs"], {
    cwd: resolve(REPO_ROOT, "tools/emception"), stdio: "inherit",
  });
  if (gradingBuild.status !== 0) throw new Error("Trusted Code runtime build failed.");
  await mkdir(ARTIFACTS, { recursive: true });
  await mkdir(EVIDENCE, { recursive: true });
  await mkdir(RUNTIME, { recursive: true });

  assertPortAvailable(PG_PORT, "PostgreSQL");
  assertPortAvailable(API_PORT, "GameGuild API");
  assertPortAvailable(WEB_PORT, "GameGuild web");

  log("syncing canonical emception Toolchain release");
  const sync = spawn("node", ["scripts/sync-emception-cdn.mjs"], {
    cwd: WEB_DIR,
    stdio: "inherit",
  });
  await new Promise((res, rej) => {
    sync.on("exit", (c) => (c === 0 ? res() : rej(new Error(`sync:emception exit ${c}`))));
    sync.on("error", rej);
  });

  // --- disposable postgres ---
  log(`starting disposable postgres on ${PG_PORT} (${PG_IMAGE})`);
  const pg = spawn(
    "docker",
    [
      "run", "--detach", "--rm",
      "--name", PG_CONTAINER,
      "--publish", `127.0.0.1:${PG_PORT}:5432`,
      "-e", `POSTGRES_USER=${PG_USER}`,
      "-e", `POSTGRES_PASSWORD=${PG_PASSWORD}`,
      "-e", `POSTGRES_DB=${PG_DB}`,
      PG_IMAGE,
    ],
    { stdio: ["ignore", "pipe", "pipe"] },
  );
  let pgOut = "";
  pg.stdout?.on("data", (d) => { pgOut += d.toString(); });
  pg.stderr?.on("data", (d) => { pgOut += d.toString(); });
  await new Promise((res, rej) => {
    pg.on("exit", (c) => {
      if (c === 0) res();
      else rej(new Error(`docker run postgres exit ${c}: ${pgOut.slice(-400)}`));
    });
  });
  pgContainerStarted = true;
  await writeFile(PG_LOG, pgOut);

  // wait pg_isready
  const pgReadyDeadline = Date.now() + 60_000;
  while (Date.now() < pgReadyDeadline) {
    const r = spawnSync("docker", ["exec", PG_CONTAINER, "pg_isready", "-U", PG_USER, "-d", PG_DB], { stdio: "ignore" });
    if (r.status === 0) break;
    await new Promise((r) => setTimeout(r, 1000));
  }
  if (Date.now() >= pgReadyDeadline) {
    throw new Error(`postgres not ready after 60s\n${readTail(PG_LOG, 80)}`);
  }
  log("postgres ready");

  // --- dotnet API ---
  const connStr = `Host=127.0.0.1;Port=${PG_PORT};Database=${PG_DB};Username=${PG_USER};Password=${PG_PASSWORD};Include Error Detail=true`;
  const apiEnv = [
    `ASPNETCORE_ENVIRONMENT=Development`,
    `ASPNETCORE_URLS=http://127.0.0.1:${API_PORT}`,
    `ConnectionStrings__DefaultConnection=${connStr}`,
    `ConnectionStrings__AuthenticationDb=${connStr}`,
    `ConnectionStrings__MigrationConnection=${connStr}`,
    `POSTGRES_HOST=127.0.0.1`,
    `POSTGRES_PORT=${PG_PORT}`,
    `POSTGRES_DB=${PG_DB}`,
    `POSTGRES_USER=${PG_USER}`,
    `POSTGRES_PASSWORD=${PG_PASSWORD}`,
    `Database__RunStartupInitialization=true`,
    `Database__FailStartupOnMigrationFailure=true`,
    `Database__FailStartupOnSeedFailure=true`,
    `Redis__Enabled=false`,
    `SeedData__ImportSnapshotCourses=false`,
    `Seed__AdminPassword=${ADMIN_PASSWORD}`,
    `Jwt__SecretKey=coding-cycle-e2e-jwt-secret-key-at-least-32-characters`,
    `Authentication__JwtSecretKey=coding-cycle-e2e-jwt-secret-key-at-least-32-characters`,
    `EmailDelivery__Enabled=false`,
    `CodeGradingWorker__NodeExecutable=${process.execPath}`,
    `CodeGradingWorker__ScriptPath=${resolve(REPO_ROOT, "tools/emception/grading/worker.mjs")}`,
    `CodeGradingWorker__RuntimeDirectory=${resolve(REPO_ROOT, "tools/emception/artifacts/grading/runtime")}`,
    `CodeGradingWorker__CdnDirectory=${resolve(REPO_ROOT, "apps/web/public/emception")}`,
    ...(CODE_GRADING_BROWSER_CHANNEL ? [`CodeGradingWorker__BrowserChannel=${CODE_GRADING_BROWSER_CHANNEL}`] : []),
    ...(process.env.PLAYWRIGHT_BROWSERS_PATH
      ? [`CodeGradingWorker__BrowserDirectory=${resolve(process.env.PLAYWRIGHT_BROWSERS_PATH)}`] : []),
  ];
  log(`starting API on ${API_PORT}`);
  const api = spawn(
    "dotnet",
    [
      "run", "--no-launch-profile", ...(API_NO_BUILD ? ["--no-build"] : []),
      "--project", "apps/api/Source/GameGuild.API/GameGuild.API.csproj",
      "--urls", `http://127.0.0.1:${API_PORT}`,
    ],
    {
      cwd: REPO_ROOT,
      env: { ...process.env, ...envArrayToObject(apiEnv) },
      detached: process.platform !== "win32",
      stdio: ["ignore", "pipe", "pipe"],
    },
  );
  children.push({ proc: api, label: "api" });
  pipeLog(api, API_LOG);
  await waitForHttp(
    `${API_BASE}/ready`,
    "GameGuild API",
    API_LOG,
    API_BOOT_TIMEOUT_MS,
    api,
  );
  log("API ready");

  // --- Next.js web ---
  const webEnv = [
    `NODE_ENV=development`,
    `CODING_CYCLE_E2E=1`,
    `API_URL=${API_BASE}`,
    `NEXT_PUBLIC_API_URL=${API_BASE}`,
    // The student activity page reads this env with NO fallback — without it
    // the IDE manifest 404s and the student IDE never boots.
    `NEXT_PUBLIC_EMCEPTION_MANIFEST_URL=/emception/manifest.json`,
    `NEXT_PUBLIC_APP_URL=${WEB_BASE}`,
    `NEXTAUTH_URL=${WEB_BASE}`,
    `AUTH_SECRET=coding-cycle-e2e-auth-secret-at-least-32-characters`,
    `NEXTAUTH_SECRET=coding-cycle-e2e-auth-secret-at-least-32-characters`,
    `AUTH_TRUST_HOST=true`,
  ];
  log(`starting web on ${WEB_PORT} (${WEB_BASE})`);
  // NO --hostname: binding 127.0.0.1 breaks Next 16.3 dev middleware URL
  // construction — next-intl's default-locale handling then 307-loops
  // unprefixed routes onto themselves (/sign-in → /sign-in). The default
  // bind (*:port) serves both localhost and 127.0.0.1 correctly.
  const web = spawn(
    process.platform === "win32" ? "pnpm.cmd" : "pnpm",
    ["exec", "next", "dev", "--webpack", "--port", String(WEB_PORT)],
    {
      cwd: WEB_DIR,
      env: { ...process.env, ...envArrayToObject(webEnv) },
      detached: process.platform !== "win32",
      // Node cannot spawn a .cmd file with pipes directly on Windows. The
      // command is fixed in this runner, and taskkill below still tears down
      // the shell's complete process tree.
      shell: process.platform === "win32",
      stdio: ["ignore", "pipe", "pipe"],
    },
  );
  children.push({ proc: web, label: "web" });
  pipeLog(web, WEB_LOG);
  await waitForHttp(`${WEB_BASE}/api/health`, "GameGuild web", WEB_LOG, 240_000, web);
  log("web ready");
}

function envArrayToObject(arr) {
  const o = {};
  for (const line of arr) {
    const i = line.indexOf("=");
    if (i > 0) o[line.slice(0, i)] = line.slice(i + 1);
  }
  return o;
}

function pipeLog(proc, file) {
  const stream = createWriteStream(file);
  proc.stdout?.on("data", (d) => stream.write(d));
  proc.stderr?.on("data", (d) => stream.write(d));
  proc.on("exit", () => stream.end());
}

// ---------------------------------------------------------------------------
// API helpers
// ---------------------------------------------------------------------------

function unique() {
  return `${Date.now()}-${randomUUID().slice(0, 8)}`;
}

function formatApiError(error) {
  if (!error) return "unknown API error";
  const detail = typeof error.detail === "string" ? ` ${error.detail}` : "";
  return `${error.status ?? "unknown"} ${error.message ?? "request failed"}${detail}`.trim();
}

function unwrap(result, label) {
  if (result.ok) return result.data;
  throw new Error(`${label} failed: ${formatApiError(result.error)}`);
}

function createApiClient(accessToken, tenantId) {
  return createClient({
    baseUrl: API_BASE,
    timeout: 30_000,
    devtools: { enabled: false },
    ...(accessToken ? { auth: { getAccessToken: async () => accessToken } } : {}),
    ...(tenantId ? { tenant: { getTenantId: async () => tenantId } } : {}),
  });
}

async function rawRequest(client, path, init = {}) {
  const result = await client.request({
    method: init.method ?? "GET",
    path,
    body: init.body,
    requiresAuth: true,
  });
  if (!result.ok) {
    console.error(`[rawRequest] ${init.method ?? "GET"} ${path} ->`, JSON.stringify(result.error));
    throw new Error(`${init.method ?? "GET"} ${path} failed: ${formatApiError(result.error)}`);
  }
  return result.data;
}

// ---------------------------------------------------------------------------
// Phase 1 — seed
// ---------------------------------------------------------------------------

/**
 * The v1 coding-assignment content payload (PascalCase + lowercase `kind`).
 * main.cpp starter FAILS every test; the student edits `add` to return a+b.
 *
 *   Public[0] stdio 'prints-sum'   : stdout '5\\n' (starter prints '0\\n' → fail)
 *   Public[1] functional 'add-fn'  : CHECK(add(2,3)==5) (starter 0 → fail)
 *   Private[0] stdio 'secret-exit' : stdout '5\\n' + exit 5 (starter 0/exit 0 → fail)
 *
 * No stdin on any stdio case → works without SharedArrayBuffer (SpeedGrader
 * route is not cross-origin-isolated; stdin degrades to EOF there).
 */
function buildCodingContent(maxScore) {
  return {
    Type: "coding-assignment",
    Version: 1,
    Environment: {
      Language: "cpp",
      Tools: "clang",
      LibBundle: null,
      AllowStudentCreateFiles: false,
    },
    Data: {
      Files: {
        "/user/main.cpp": {
          Content:
            "extern \"C\" int printf(const char*, ...);\n" +
            "extern \"C\" int add(int a, int b) {\n" +
            "    return 0; // TODO: replace with your implementation\n" +
            "}\n" +
            "\n" +
            "int main() {\n" +
            "    printf(\"%d\\n\", add(2, 3));\n" +
            "    return add(2, 3);\n" +
            "}\n",
          Encoding: "text",
          Visibility: "Public",
          Modifiable: true,
        },
      },
    },
    Tests: {
      Public: [
        {
          kind: "standard",
          Name: "prints-sum",
          Stdin: "",
          Stdout: "5\n",
          Weight: 1,
        },
        {
          kind: "functional",
          Name: "add-fn",
          Function: {
            FunctionName: "add",
            Parameters: [
              { Type: "integer", Name: "a" },
              { Type: "integer", Name: "b" },
            ],
            ReturnType: { Type: "integer" },
          },
          Cases: [
            {
              Inputs: [
                { Type: "integer", Content: 2 },
                { Type: "integer", Content: 3 },
              ],
              Expected: { Type: "integer", Content: 5 },
            },
          ],
          Weight: 1,
        },
      ],
      Private: [
        {
          kind: "standard",
          Name: "secret-exit",
          Stdin: "",
          Stdout: "5\n",
          ExitCode: 5,
          Weight: 1,
        },
      ],
    },
    Grading: { MaxScore: maxScore },
  };
}

/** The corrected program the student types into Monaco. */
const STUDENT_FIX_SOURCE =
  "// e2e-student-fix\n" +
  "extern \"C\" int printf(const char*, ...);\n" +
  "extern \"C\" int add(int a, int b) {\n" +
  "    return a + b;\n" +
  "}\n" +
  "int main() {\n" +
  "    printf(\"%d\\n\", add(2, 3));\n" +
  "    return add(2, 3);\n" +
  "}\n";

async function seedFixture() {
  const tag = unique();
  const password = "Str0ng!Passw0rd123!";

  // Admin (instructor) sign-in.
  const adminSignIn = unwrap(
    await createApiClient().request({
      method: "POST",
      path: "/v1/auth/sign-in",
      body: { email: ADMIN_EMAIL, password: ADMIN_PASSWORD },
      requiresAuth: false,
    }),
    "Admin sign-in",
  );
  const adminToken = adminSignIn.accessToken;
  const tenantId = adminSignIn.tenantId;
  const adminClient = createApiClient(adminToken, tenantId);

  // Course.
  const slug = `coding-cycle-${tag}`;
  const course = unwrap(
    await new GeneratedApi.LearningCoursesProgramModule(adminClient).postCourses({
      title: `Coding Cycle E2E ${tag}`,
      description: "Full-cycle coding assignment browser E2E.",
      slug,
      thumbnail:
        "https://images.unsplash.com/photo-1550745165-9bc0b252726f?w=1400&h=900&fit=crop",
    }),
    "Create course",
  );
  if (!course.id) throw new Error("Create course returned no id.");

  // ProgramContent(type=Code) — the v1 coding-assignment storage home.
  const content = unwrap(
    await new GeneratedApi.LearningCoursesProgramContentModule(adminClient).postCoursesContent(
      course.id,
      {
        programId: course.id,
        title: "Add two integers",
        description: "Implement add(a,b) and a main that prints add(2,3).",
        type: "Code",
        sortOrder: 1,
        isRequired: true,
        visibility: "Public",
      },
    ),
    "Create ProgramContent(Code)",
  );
  if (!content.id) throw new Error("Create content returned no id.");

  // Create the coding assignment through the content authoring route. That route
  // owns and synchronizes the linked assessment; the general assessment endpoint
  // deliberately rejects direct creation with a contentId.
  const maxScore = 100;
  await rawRequest(
    adminClient,
    `/v1.0/courses/${course.id}/content/${content.id}/coding-assignment`,
    { method: "PUT", body: buildCodingContent(maxScore) },
  );

  const courseAssessments = unwrap(
    await adminClient.request({
      method: "GET",
      path: `/v1/assessments/course/${course.id}`,
    }),
    "List course assessments",
  );
  const assessment = courseAssessments.find(
    (candidate) => candidate.contentId === content.id,
  );
  if (!assessment?.id) {
    throw new Error("Coding assignment did not create its linked assessment.");
  }

  // Lifecycle: submit → approve → publish.
  const lifecycle = new GeneratedApi.LearningCoursesProgramLifecycleModule(adminClient);
  unwrap(await lifecycle.postCoursesSubmit(course.id), "Submit course");
  unwrap(await lifecycle.postCoursesApprove(course.id), "Approve course");
  unwrap(await lifecycle.postCoursesPublish(course.id), "Publish course");

  // Rubric: 2 criteria summing to maxScore. Points live here so the grader
  // UI fill step reads them from the fixture instead of hardcoding 60/40.
  // API ScoreValue integers are hundredths of a point; the grader UI uses points.
  const criterionPoints = [60, 40];
  const rubric = unwrap(
    await new GeneratedApi.LearningAssessmentsRubricsModule(adminClient).putAssessmentsRubric(
      assessment.id,
      {
        title: "Add two integers rubric",
        criteria: [
          { description: "Correctness", points: criterionPoints[0] * 100, order: 0 },
          { description: "Code quality", points: criterionPoints[1] * 100, order: 1 },
        ],
      },
    ),
    "PUT rubric",
  );
  const criterionIds = (rubric.criteria ?? []).map((c) => c.id).filter(Boolean);

  // Publishing a course does not publish an executable assessment. Freeze the
  // Code definition and the concrete rubric, then activate that exact revision.
  const state = await rawRequest(adminClient, `/v1.0/assessments/${assessment.id}/authoring-state`);
  const prepared = await rawRequest(adminClient, `/v1.0/assessments/${assessment.id}/revisions/prepare`, {
    method: "POST", body: { expectedAssessmentVersion: state.assessmentVersion },
  });
  await rawRequest(adminClient, `/v1.0/assessments/${assessment.id}/revisions/publish`, {
    method: "POST", body: { revisionId: prepared.revisionId, expectedAssessmentVersion: state.assessmentVersion },
  });

  // Student sign-up, then a fresh sign-in for a fully-activated session
  // (sign-up may return a temp token; sign-in guarantees a usable one and
  // the canonical userId the enrollment + HasStudentAccess checks key on).
  const studentEmail = `coding-cycle-student-${tag}@example.test`;
  const studentUsername = `coding_cycle_student_${tag.replace(/[^a-z0-9]/gi, "_")}`;
  await createApiClient().request({
    method: "POST",
    path: "/v1/auth/sign-up",
    body: {
      username: studentUsername,
      email: studentEmail,
      password,
      ...(tenantId ? { tenantId } : {}),
    },
    requiresAuth: false,
  }).then((r) => {
    if (!r.ok) throw new Error(`Student sign-up failed: ${formatApiError(r.error)}`);
  });
  const studentSignIn = unwrap(
    await createApiClient().request({
      method: "POST",
      path: "/v1/auth/sign-in",
      body: { email: studentEmail, password, ...(tenantId ? { tenantId } : {}) },
      requiresAuth: false,
    }),
    "Student sign-in",
  );
  const studentUserId = studentSignIn.userId;
  const studentToken = studentSignIn.accessToken;
  if (!studentUserId || !studentToken) {
    throw new Error(`Student sign-in missing userId/token: ${JSON.stringify(studentSignIn).slice(0, 300)}`);
  }

  // Enroll the student. HasStudentAccessAsync keys on UserProgress, which
  // `POST /v1/courses/{id}:self-enroll` creates (the /api/learning/enrollments
  // route only writes the enrollments row, not UserProgress — so the public
  // coding-assignment fetch would 403 without self-enroll). F3 learning.
  const studentSelfEnroll = await new GeneratedApi.LearningCoursesProgramModule(
    createApiClient(studentToken, tenantId),
  ).postCoursesSelfEnroll(course.id);
  if (!studentSelfEnroll.ok) {
    throw new Error(`Student self-enroll failed: ${formatApiError(studentSelfEnroll.error)}`);
  }

  // Sanity: student fetches the PUBLIC coding assignment — Private tests +
  // Private files must be stripped by the server.
  const studentClient = createApiClient(studentToken, tenantId);
  const publicContent = await rawRequest(
    studentClient,
    `/v1.0/courses/${course.id}/content/${content.id}/coding-assignment`,
  );
  const privateTests = publicContent?.tests?.private ?? [];
  const privateFiles = Object.entries(publicContent?.data?.files ?? {}).filter(
    ([, meta]) => meta?.visibility === "Private",
  );
  if (privateTests.length !== 0) {
    throw new Error(
      `Student public fetch leaked Private tests: ${JSON.stringify(privateTests).slice(0, 200)}`,
    );
  }
  if (privateFiles.length !== 0) {
    throw new Error(`Student public fetch leaked Private files: ${JSON.stringify(privateFiles)}`);
  }
  const publicTests = publicContent?.tests?.public ?? [];
  if (publicTests.length !== 2) {
    throw new Error(`Expected 2 public tests, got ${publicTests.length}: ${JSON.stringify(publicTests)}`);
  }

  return {
    tag,
    courseId: course.id,
    slug,
    contentId: content.id,
    assessmentId: assessment.id,
    criterionIds,
    criterionPoints,
    maxScore,
    studentEmail,
    studentPassword: password,
    studentUserId,
    studentToken,
    tenantId,
    adminToken,
  };
}

// ---------------------------------------------------------------------------
// Browser helpers
// ---------------------------------------------------------------------------

function attachErrorTracking(page, label) {
  // Console errors + pageerrors are OBSERVATIONS, not hard assertions — the
  // WASM toolchain emits benign stderr that surfaces as console noise.
  const runtimeErrors = [];
  page.on("pageerror", (error) => {
    runtimeErrors.push(`${label}: ${error.message}`);
    console.error(`[browser page error] ${label}: ${error.message}`);
  });
  page.on("console", (message) => {
    if (
      message.type() === "error" &&
      !/favicon|cloudflareinsights|webpack-hmr|Download the React DevTools|coi-serviceworker/i.test(
        message.text(),
      )
    ) {
      runtimeErrors.push(`${label} console: ${message.text()}`);
    }
  });
  return { errors: () => runtimeErrors };
}

async function assertNoErrorSurface(page, label) {
  await page.waitForLoadState("domcontentloaded");
  await page
    .waitForFunction(() => document.body && document.body.innerText.trim().length > 0, undefined, {
      timeout: 30_000,
    })
    .catch(() => {});
  const body = await page.locator("body").innerText().catch(() => "");
  if (/Unhandled Runtime Error|Build Error|Application error|Internal server error/i.test(body)) {
    throw new Error(`${label} rendered an error surface:\n${body.slice(0, 1500)}`);
  }
}

async function signIn(page, email, password) {
  await page.goto(`${WEB_BASE}/sign-in`, { waitUntil: "domcontentloaded" });
  await assertNoErrorSurface(page, "sign-in");
  // The credentials endpoint is invoked in the actual browser page, sharing
  // its CSRF and cookie storage. This keeps the coding-cycle test focused on
  // activity creation/execution instead of coupling it to an unrelated cold
  // route hydration race in the general-purpose sign-in form.
  const result = await page.evaluate(async ({ email: emailValue, password: passwordValue }) => {
    const csrfResponse = await fetch("/api/auth/csrf", { credentials: "include" });
    const csrf = await csrfResponse.json().catch(() => null);
    if (!csrfResponse.ok || typeof csrf?.csrfToken !== "string") {
      return { ok: false, stage: "csrf", status: csrfResponse.status };
    }

    const response = await fetch("/api/auth/signin/credentials", {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        email: emailValue,
        password: passwordValue,
        csrfToken: csrf.csrfToken,
        redirect: false,
        redirectTo: "/",
      }),
    });
    return { ok: response.ok, stage: "credentials", status: response.status };
  }, { email, password });

  if (!result.ok) {
    throw new Error(`browser credentials sign-in failed at ${result.stage} (HTTP ${result.status})`);
  }
  assertSharedAuthCookie(await page.context().cookies([WEB_BASE]));
}

async function waitForIdeReady(page, scope, timeoutMs, label) {
  const status = scope.locator('[data-testid="status"]').first();
  await status.waitFor({ state: "attached", timeout: 30_000 });
  // Assessment runs leave the neutral IDE status untouched; only its normal
  // ready/error states establish that the underlying toolchain booted.
  await page.waitForFunction(
    (sel) => {
      const el = document.querySelector(sel);
      const t = el?.textContent ?? "";
      return /Ready|Error:/.test(t);
    },
    '[data-testid="status"]',
    { timeout: timeoutMs },
  );
  const text = (await status.textContent()) ?? "";
  if (/Error:/.test(text) && !/Ready/.test(text)) {
    throw new Error(`${label} IDE boot error: ${text}`);
  }
}

async function studentRunTestsAndWait(page) {
  // Assessment execution is a host extension over the neutral IDE, so it
  // reports completion through its results panel rather than the IDE's legacy
  // built-in test status. The temporarily disabled button proves the new run
  // has settled before its result rows are inspected below.
  const runBtn = page.getByRole("button", { name: "Run public tests" }).first();
  await runBtn.waitFor({ state: "visible", timeout: 15_000 });
  await runBtn.click();
  await page.getByTestId("test-results-panel").waitFor({
    state: "visible",
    timeout: RUN_TESTS_TIMEOUT_MS,
  });
  await page.waitForFunction(
    () =>
      Array.from(document.querySelectorAll("button")).some(
        (button) => button.textContent?.trim() === "Run public tests" && !button.disabled,
      ),
    undefined,
    { timeout: RUN_TESTS_TIMEOUT_MS },
  );
}

async function graderRunTestsAndWait(graderPanel) {
  // Grading uses the same session composition as the learner, but runs the
  // full plan (public plus private) and projects the computed score to its
  // host panel.
  const runBtn = graderPanel.getByRole("button", { name: "Run full tests" }).first();
  await runBtn.waitFor({ state: "visible", timeout: 15_000 });
  await runBtn.click();
  await Promise.any([
    graderPanel.getByTestId("computed-score").waitFor({ state: "visible", timeout: RUN_TESTS_TIMEOUT_MS }),
    graderPanel.locator('[role="alert"]').waitFor({ state: "visible", timeout: RUN_TESTS_TIMEOUT_MS }),
  ]);
}

/**
 * Reads each visible result and opens diagnostics before collecting it. This
 * keeps a red browser E2E actionable: the phase report includes the actual
 * compiler/runtime mismatch, not only a red row label.
 */
async function readTestResultRows(resultsPanel) {
  const count = await resultsPanel.locator('[data-testid^="test-case-"]').evaluateAll((nodes) =>
    Array.from(nodes).filter((node) => /^test-case-\d+$/.test(node.getAttribute("data-testid") ?? "")).length,
  );
  const rows = [];
  for (let index = 0; index < count; index++) {
    const row = resultsPanel.getByTestId(`test-case-${index}`);
    const rowText = (await row.textContent()) ?? "";
    let diagnostic = "";
    if (/[▼▲]/.test(rowText)) {
      await row.click();
      diagnostic = (await resultsPanel.getByTestId(`test-case-diagnostic-${index}`).textContent().catch(() => "")) ?? "";
      await row.click();
    }
    rows.push(diagnostic ? `${rowText} :: ${diagnostic}` : rowText);
  }
  return rows;
}

async function screenshot(page, name) {
  await page.screenshot({ path: resolve(EVIDENCE, `${name}.png`), fullPage: true });
}

// ---------------------------------------------------------------------------
// Phase 2 — student UI
// ---------------------------------------------------------------------------

async function studentJourney(fixture, browser) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  const page = await context.newPage();
  const httpFailures = trackAppHttpFailures(page, [WEB_BASE]);
  const errors = attachErrorTracking(page, "student");

  page.setDefaultTimeout(45_000);
  // Fresh distDir (.next-e2e-coding-cycle) compiles every route from scratch.
  page.setDefaultNavigationTimeout(300_000);

  const activityUrl = `${WEB_BASE}/learn/courses/${fixture.slug}/activities/assessment-${fixture.assessmentId}`;
  const result = { phase: "Phase 2 — Student UI", assertions: [] };
  const record = (name, ok, detail, kind) =>
    result.assertions.push({ name, ok, detail, knownRed: kind === "knownRed", observed: kind === "observed" });

  try {
    await signIn(page, fixture.studentEmail, fixture.studentPassword);
    record("student browser credentials session", true, page.url());

    await page.goto(activityUrl, { waitUntil: "domcontentloaded" });
    await assertNoErrorSurface(page, "student activity");
    await page.locator('[data-testid="ide-fullwidth-mount"]').waitFor({ timeout: 30_000 });
    record("activity page mounts ide-fullwidth-mount", true);

    // Cross-origin isolation is required for SharedArrayBuffer (WASM stdin).
    const isolated = await page.evaluate(() => window.crossOriginIsolated === true);
    record("page is cross-origin isolated (SharedArrayBuffer available)", isolated, `crossOriginIsolated=${isolated}`);
    if (!isolated) {
      throw new Error(
        `Page is NOT cross-origin isolated on ${WEB_BASE} — WASM stdin will degrade. ` +
          `Set CODING_CYCLE_WEB_BASE to a *.localhost host or run on a secure context.`,
      );
    }

    await waitForIdeReady(page, page, IDE_BOOT_TIMEOUT_MS, "student");
    record("IDE boots (status Ready)", true);
    await screenshot(page, "student-ide-booted");

    // The neutral IDE no longer owns GameGuild's test-case panel. The learner
    // sees public outcomes only after an explicit public run.
    const bodyText = await page.locator("body").innerText();
    record("private test 'secret-exit' ABSENT from student view", !/secret-exit/.test(bodyText), "must not appear");

    // Run public tests #1 — starter code fails both public tests.
    await studentRunTestsAndWait(page);
    const resultsPanel = page.getByTestId("test-results-panel");
    await resultsPanel.waitFor({ state: "visible", timeout: 15_000 });
    const rows1 = await resultsPanel.locator('[data-testid^="test-case-"]').filter({ hasNot: page.locator('[data-testid^="test-case-diagnostic-"]') }).count();
    record("run #1 produces 2 result rows", rows1 === 2, `rows=${rows1}`);
    const rowTexts1 = await readTestResultRows(resultsPanel);
    log(`student public run #1 results: ${rowTexts1.join(" | ")}`);
    const printsSumFailing = rowTexts1.some((t) => /prints-sum/.test(t) && /\u2717/.test(t));
    const addFnFailing = rowTexts1.some((t) => /add-fn/.test(t) && /\u2717/.test(t));
    record("public test 'prints-sum' ran", rowTexts1.some((t) => /prints-sum/.test(t)), rowTexts1.join(" | "));
    record("public test 'add-fn' ran", rowTexts1.some((t) => /add-fn/.test(t)), rowTexts1.join(" | "));
    record("starter code: 'prints-sum' FAILS", printsSumFailing, rowTexts1.join(" | "));
    record("starter code: 'add-fn' FAILS", addFnFailing, rowTexts1.join(" | "));
    await screenshot(page, "student-run1-failing");

    // TYPE real code into Monaco. Click the editor, select all, insert the fix.
    // insertText (IME-style atomic insert) — per-key typing drops characters
    // inside Monaco's C++ tokenization pipeline (observed '#icude').
    const editor = page.locator(".monaco-editor").first();
    await editor.waitFor({ state: "visible", timeout: 15_000 });
    await editor.click();
    // Select all (Meta on mac, Control elsewhere — try both, harmless).
    await page.keyboard.press(process.platform === "darwin" ? "Meta+A" : "Control+A");
    await page.keyboard.press("Delete");
    await page.keyboard.insertText(STUDENT_FIX_SOURCE);
    // Give Monaco a beat to flush the model, then HARD-assert the typed
    // content landed in a monaco model (window.monaco is exposed for e2e).
    await page.waitForTimeout(500);
    const typedInModel = await page.evaluate((needle) => {
      const monaco = window.monaco;
      if (!monaco?.editor?.getModels) return false;
      return monaco.editor.getModels().some((m) => m.getValue().includes(needle));
    }, "e2e-student-fix");
    if (!typedInModel) {
      throw new Error("Typing failed: no Monaco model contains 'e2e-student-fix'.");
    }
    record("typed corrected code into Monaco (model contains fix)", true);
    await screenshot(page, "student-typed-fix");

    // Run Tests #2 — public tests now pass.
    await studentRunTestsAndWait(page);
    await resultsPanel.waitFor({ state: "visible", timeout: 15_000 });
    const rowTexts2 = await readTestResultRows(resultsPanel);
    log(`student public run #2 results: ${rowTexts2.join(" | ")}`);
    const printsSumPassing = rowTexts2.some((t) => /prints-sum/.test(t) && /\u2713/.test(t));
    const addFnPassing = rowTexts2.some((t) => /add-fn/.test(t) && /\u2713/.test(t));
    record("after fix: 'prints-sum' PASSES", printsSumPassing, rowTexts2.join(" | "));
    record("after fix: 'add-fn' PASSES", addFnPassing, rowTexts2.join(" | "));
    await screenshot(page, "student-run2-passing");

    // Submit.
    const submitBtn = page.getByRole("button", { name: /^Submit$/ }).first();
    await submitBtn.waitFor({ state: "visible", timeout: 15_000 });
    await submitBtn.click();
    // Success = redirect to the activities list (handleSubmit → router.push).
    let submitted = false;
    try {
      await page.waitForURL(
        (url) => /\/learn\/courses\/[^/]+\/activities\/?$/.test(url.pathname + url.search) ||
          /\/learn\/courses\/[^/]+\/activities$/.test(url.pathname),
        // The submission now includes trusted compilation and full test execution.
        // Preserve the success assertion while matching the bounded API deadline.
        { timeout: 330_000 },
      );
      submitted = true;
    } catch {
      // Fallback: success status text may have rendered before redirect.
      const body = await page.locator("body").innerText().catch(() => "");
      submitted = /Submission received/.test(body);
    }
    const submissionAlerts = await page.getByRole("alert").allTextContents();
    record(
      "student Submit → success (redirect to activities)",
      submitted,
      `${page.url()} | alerts=${submissionAlerts.join(" | ") || "none"}`,
    );
    await screenshot(page, "student-submitted");

    assertSharedAuthCookie(await context.cookies([WEB_BASE]));
    // 4xx/5xx on the app origin stay hard.
    httpFailures.assertNone("Student journey");
    // Console errors + pageerrors are logged as OBSERVATIONS only.
    const browserErrors = [...new Set(errors.errors())];
    const ssrWindowErrors = browserErrors.filter((entry) => /window is not defined/i.test(entry));
    record(
      "coding activity does not invoke the IDE during SSR",
      ssrWindowErrors.length === 0,
      ssrWindowErrors.join(" | ") || "none",
    );
    record(
      "browser console/page errors (observation)",
      true,
      browserErrors.length > 0 ? browserErrors.join(" | ").slice(0, 600) : "none",
      "observed",
    );
  } finally {
    await context.close();
  }
  return result;
}

// ---------------------------------------------------------------------------
// Phase 3 — payload check
// ---------------------------------------------------------------------------

async function payloadCheck(fixture) {
  const adminClient = createApiClient(fixture.adminToken, fixture.tenantId);
  const submissions = unwrap(
    await new GeneratedApi.LearningAssessmentsModule(adminClient).getAssessmentsSubmissionsForGetAssessmentsByAssessmentIdSubmissions(
      fixture.assessmentId,
    ),
    "List submissions",
  );
  const sub = (submissions ?? []).find(
    (s) => s.userId === fixture.studentUserId && s.status === "Submitted",
  );
  const result = { phase: "Phase 3 — Payload check", assertions: [] };
  const record = (name, ok, detail, kind) =>
    result.assertions.push({ name, ok, detail, knownRed: kind === "knownRed", observed: kind === "observed" });
  record("student submission exists (status Submitted)", Boolean(sub), `found=${Boolean(sub)}`);
  if (!sub) {
    record("codePayload contains typed code", false, "no submission found");
    return result;
  }
  const payload = sub.codePayload ?? "";
  let parsed = null;
  try { parsed = JSON.parse(payload); } catch { parsed = null; }
  const mainContent =
    parsed && typeof parsed === "object" ?
      (parsed["/user/main.cpp"]?.content ?? parsed["/user/main.cpp"] ?? "")
      : "";
  const containsFix = /return a \+ b/.test(mainContent) || /e2e-student-fix/.test(mainContent);
  record("codePayload contains typed code", containsFix, `payload=${payload.slice(0, 120)}`);
  return result;
}

// ---------------------------------------------------------------------------
// Phase 4 — instructor UI (best-effort)
// ---------------------------------------------------------------------------

async function instructorJourney(fixture, browser) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  const page = await context.newPage();
  const httpFailures = trackAppHttpFailures(page, [WEB_BASE]);
  const errors = attachErrorTracking(page, "instructor");

  page.setDefaultTimeout(45_000);
  page.setDefaultNavigationTimeout(300_000);

  const speedgraderUrl = `${WEB_BASE}/speedgrader/assessments/${fixture.assessmentId}?course=${fixture.slug}&nav=0`;
  const result = { phase: "Phase 4 — Instructor UI", assertions: [] };
  const record = (name, ok, detail, kind) =>
    result.assertions.push({ name, ok, detail, knownRed: kind === "knownRed", observed: kind === "observed" });

  try {
    await signIn(page, ADMIN_EMAIL, ADMIN_PASSWORD);
    record("instructor browser credentials session", true, page.url());

    await page.goto(speedgraderUrl, { waitUntil: "domcontentloaded" });
    await assertNoErrorSurface(page, "speedgrader");
    await page.getByTestId("speedgrader-header").waitFor({ timeout: 30_000 });
    record("speedgrader header renders", true);
    const counter = await page.getByTestId("item-counter").textContent().catch(() => "");
    record("queue has 1 submission", /1 of 1/.test(counter ?? ""), counter);
    await screenshot(page, "instructor-speedgrader-loaded");

    const graderPanel = page.getByTestId("code-grader-panel");
    await graderPanel.waitFor({ state: "visible", timeout: 30_000 });
    record("code-grader-panel renders", true);

    // The grader IDE boots the same WASM toolchain.
    await waitForIdeReady(page, graderPanel, IDE_BOOT_TIMEOUT_MS, "instructor");
    record("grader IDE boots", true);
    await screenshot(page, "instructor-grader-ide-booted");

    // Student typed code should be visible now the payload carries it.
    // Retry the read: Monaco renders view-lines asynchronously after boot.
    let editorText = "";
    const editorDeadline = Date.now() + 30_000;
    while (Date.now() < editorDeadline) {
      editorText = await graderPanel.locator(".monaco-editor .view-lines").first().innerText().catch(() => "");
      if (/e2e-student-fix/.test(editorText) || /return a \+ b/.test(editorText)) break;
      await page.waitForTimeout(1000);
    }
    const studentCodeVisible = /e2e-student-fix/.test(editorText) || /return a \+ b/.test(editorText);
    record("grader IDE shows student typed code", studentCodeVisible, editorText.slice(0, 200));
    const noStudentCodeNotice = await graderPanel.getByTestId("no-student-code").count();
    record("'no-student-code' notice absent (student code present)", noStudentCodeNotice === 0, `count=${noStudentCodeNotice}`);

    // Run Tests — full plan (public + private).
    await graderRunTestsAndWait(graderPanel);
    // The result panel is contributed by CodingAssessmentEditor itself. The
    // grading host only adds the score projection above the neutral IDE.
    const graderResults = graderPanel.getByTestId("test-results-panel");
    await graderResults.waitFor({ state: "visible", timeout: 30_000 });
    const graderRowTexts = await readTestResultRows(graderResults);
    const rowCount = graderRowTexts.length;
    record("full plan produces 3 result rows", rowCount === 3, `rows=${rowCount}; ${graderRowTexts.join(" | ")}`);
    const privateVisible = graderRowTexts.some((t) => /secret-exit/.test(t));
    record("private test 'secret-exit' visible to instructor", privateVisible, graderRowTexts.join(" | "));
    // Per-row pass/fail: the grader shares the student pipeline — the
    // student's fixed code must PASS every case in the full plan.
    for (const rowText of graderRowTexts) {
      const name = (rowText.match(/(prints-sum|add-fn|secret-exit)/) ?? ["?"])[0];
      const passed = /^\s*✓/.test(rowText);
      record(`grader row '${name}' passes`, passed, rowText.replace(/\s+/g, " ").slice(0, 140));
    }
    await screenshot(page, "instructor-run-tests");

    // Rubric grid.
    const gradingPanel = page.getByTestId("grading-panel");
    await gradingPanel.waitFor({ state: "visible", timeout: 15_000 });
    const rubricGrid = gradingPanel.getByTestId("rubric-grid");
    await rubricGrid.waitFor({ state: "visible", timeout: 15_000 });
    const criterionRows = await rubricGrid.locator('[data-testid^="criterion-row-"]').count();
    record("rubric grid renders 2 criteria", criterionRows === 2, `rows=${criterionRows}`);

    // Fill points: each criterion at its fixture cap (sum = maxScore).
    const caps = fixture.criterionPoints;
    for (let i = 0; i < fixture.criterionIds.length; i++) {
      const id = fixture.criterionIds[i];
      const input = gradingPanel.getByTestId(`criterion-points-${id}`);
      await input.waitFor({ state: "visible", timeout: 10_000 });
      await input.fill(String(caps[i] ?? 0));
    }
    const totalText = await gradingPanel.getByTestId("rubric-total").textContent().catch(() => "");
    record("rubric total = maxScore (100)", /100/.test(totalText ?? ""), totalText);
    await screenshot(page, "instructor-rubric-filled");

    // Submit grade.
    const submitGrade = gradingPanel.getByTestId("submit-grade");
    await submitGrade.waitFor({ state: "visible", timeout: 10_000 });
    await submitGrade.click();
    // router.refresh() re-renders; the queue item status flips to Graded.
    let graded = false;
    const gradeDeadline = Date.now() + 60_000;
    while (Date.now() < gradeDeadline) {
      const badge = await page.getByTestId("needs-grading-badge").textContent().catch(() => "");
      if (/0 to grade/.test(badge ?? "")) { graded = true; break; }
      await page.waitForTimeout(1500);
    }
    record("submit grade → needs-grading drops to 0", graded, "polled needs-grading-badge");
    await screenshot(page, "instructor-grade-submitted");

    httpFailures.assertNone("Instructor journey");
    const browserErrors = [...new Set(errors.errors())];
    record(
      "browser console/page errors (observation)",
      true,
      browserErrors.length > 0 ? browserErrors.join(" | ").slice(0, 600) : "none",
      "observed",
    );
  } catch (error) {
    record("instructor journey completed without throwing", false, error?.message ?? String(error));
  } finally {
    await context.close();
  }
  return result;
}

// ---------------------------------------------------------------------------
// Phase 5 — grade check (API + DB)
// ---------------------------------------------------------------------------

async function gradeCheck(fixture) {
  const adminClient = createApiClient(fixture.adminToken, fixture.tenantId);
  const submissions = unwrap(
    await new GeneratedApi.LearningAssessmentsModule(adminClient).getAssessmentsSubmissionsForGetAssessmentsByAssessmentIdSubmissions(
      fixture.assessmentId,
    ),
    "List submissions (grade check)",
  );
  const sub = (submissions ?? []).find((s) => s.userId === fixture.studentUserId);
  const result = { phase: "Phase 5 — Grade check", assertions: [] };
  const record = (name, ok, detail) => result.assertions.push({ name, ok, detail });
  record("submission status Graded", sub?.status === "Graded", sub?.status ?? "no submission");
  // Keep the exact 100-point requirement. The API's ScoreValue wire contract
  // contains hundredths, so the required stored value is 10,000 units.
  record("submission score == 100", sub?.score === scoreValueFromPoints(String(fixture.maxScore)),
    `scoreUnits=${sub?.score}; expectedPoints=${fixture.maxScore}`);
  record("submission feedback non-null", Boolean(sub?.feedback), `feedback=${sub?.feedback ? "present" : "null"}`);

  // RubricScoresPayload is not exposed on the submission DTO — probe the DB.
  let rubricPayload = null;
  let dbError = null;
  try {
    rubricPayload = await queryPg(
      PG_CONTAINER,
      PG_USER,
      PG_DB,
      `SELECT "RubricScoresPayload" FROM "AssessmentSubmissions" WHERE "Id" = '${sub.id}';`,
    );
  } catch (error) {
    dbError = error?.message ?? String(error);
  }
  const payloadNonEmpty = Boolean(rubricPayload) && rubricPayload !== "NULL" && rubricPayload !== "";
  record("DB RubricScoresPayload non-null", payloadNonEmpty, rubricPayload ?? dbError ?? "no row");
  return result;
}

function queryPg(container, user, db, sql) {
  return new Promise((resolveP, rejectP) => {
    const p = spawn("docker", ["exec", container, "psql", "-U", user, "-d", db, "-t", "-A", "-c", sql], {
      stdio: ["ignore", "pipe", "pipe"],
    });
    let out = "";
    let err = "";
    p.stdout.on("data", (d) => { out += d.toString(); });
    p.stderr.on("data", (d) => { err += d.toString(); });
    p.on("exit", (c) => {
      if (c === 0) resolveP(out.trim());
      else rejectP(new Error(`psql exit ${c}: ${err}`));
    });
    p.on("error", rejectP);
  });
}

// ---------------------------------------------------------------------------
// Report
// ---------------------------------------------------------------------------

function summarize(results) {
  const lines = [];
  lines.push("=".repeat(78));
  lines.push("CODING-CYCLE BROWSER E2E — PHASE REPORT");
  lines.push("=".repeat(78));
  let anyRed = false;
  let knownRedCount = 0;
  let observedCount = 0;
  for (const phase of results) {
    lines.push("");
    lines.push(`### ${phase.phase}`);
    for (const a of phase.assertions) {
      let tag;
      if (a.observed) {
        tag = "OBSERVED";
        observedCount += 1;
      } else if (a.ok) {
        tag = "GREEN   ";
      } else if (a.knownRed) {
        tag = "KNOWN-RED";
        knownRedCount += 1;
      } else {
        tag = "RED     ";
        anyRed = true;
      }
      lines.push(`  [${tag}] ${a.name}${a.detail ? ` — ${a.detail}` : ""}`);
    }
  }
  lines.push("");
  lines.push("=".repeat(78));
  if (anyRed) {
    lines.push("OVERALL: RED (one or more non-known assertions failed)");
  } else if (knownRedCount > 0) {
    lines.push(`OVERALL: GREEN (${knownRedCount} known-red — tracked product bug(s), not harness failures)`);
  } else {
    lines.push("OVERALL: GREEN");
  }
  lines.push(`assertions: ${results.reduce((n, p) => n + p.assertions.length, 0)} total, ${knownRedCount} known-red, ${observedCount} observed`);
  lines.push("=".repeat(78));
  return { text: lines.join("\n"), anyRed };
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function main() {
  await acquireRunLock();
  log(`booting isolated stack (pg=${PG_PORT} api=${API_PORT} web=${WEB_PORT})`);
  await bootStack();
  log("stack ready — seeding fixture");

  const fixture = await seedFixture();
  log(
    `seeded: course=${fixture.slug} assessment=${fixture.assessmentId} student=${fixture.studentEmail}`,
  );

  const results = [];
  // ONE browser, TWO contexts (student / instructor) — clean separation.
  const browser = await chromium.launch({
    headless: HEADLESS,
    ...(CHROMIUM_EXECUTABLE_PATH
      ? { executablePath: CHROMIUM_EXECUTABLE_PATH }
      : {}),
  });
  try {
    // Phase 2 — student UI (hard).
    try {
      results.push(await studentJourney(fixture, browser));
    } catch (error) {
      results.push({
        phase: "Phase 2 — Student UI",
        assertions: [{ name: "student journey completed without throwing", ok: false, detail: error?.message ?? String(error) }],
      });
    }

    // Phase 3 — payload check (EXPECTED RED, known bug).
    try {
      results.push(await payloadCheck(fixture));
    } catch (error) {
      results.push({
        phase: "Phase 3 — Payload check",
        assertions: [{ name: "payload check completed without throwing", ok: false, detail: error?.message ?? String(error) }],
      });
    }

    // Phase 4 — instructor UI (best-effort).
    try {
      results.push(await instructorJourney(fixture, browser));
    } catch (error) {
      results.push({
        phase: "Phase 4 — Instructor UI",
        assertions: [{ name: "instructor journey completed without throwing", ok: false, detail: error?.message ?? String(error) }],
      });
    }

    // Phase 5 — grade check.
    try {
      results.push(await gradeCheck(fixture));
    } catch (error) {
      results.push({
        phase: "Phase 5 — Grade check",
        assertions: [{ name: "grade check completed without throwing", ok: false, detail: error?.message ?? String(error) }],
      });
    }
  } finally {
    await browser.close();
  }

  const { text, anyRed } = summarize(results);
  console.log("\n" + text);
  await writeFile(resolve(ARTIFACTS, "report.txt"), text);

  // Exit non-zero ONLY on non-known failures (known-red = tracked product bugs).
  return anyRed ? 1 : 0;
}

let finalCode = 0;
try {
  finalCode = await main();
} catch (error) {
  console.error(error instanceof Error ? error.stack ?? error.message : error);
  if (error && typeof error === "object") console.error(JSON.stringify(error, null, 2));
  finalCode = 1;
} finally {
  // Teardown is AWAITED on both success and failure paths before exiting.
  await teardown(finalCode);
}
process.exit(finalCode);
