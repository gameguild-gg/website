import assert from "node:assert/strict";
import { mkdtemp, readFile, rm, stat, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { resolve } from "node:path";
import test from "node:test";
import {
  CHECKPOINTS,
  assertApplicableCheckpoint,
  checkpointIndex,
  getScenarioDefinition,
  nextApplicableCheckpoint,
  requestHash,
} from "./definitions.mjs";
import { resolveClientBuildTarget } from "./api.mjs";
import {
  assertLocalUrl,
  createManifest,
  validateManifest,
  withScenarioLock,
  writeJsonAtomic,
} from "./manifest.mjs";
import { buildScenarioUrls } from "./urls.mjs";

test("scenario definitions expose canonical workflows and checkpoint applicability", () => {
  assert.equal(getScenarioDefinition("automated").reviewMethodValue, 4);
  assert.equal(getScenarioDefinition("instructor").reviewMethodValue, 8);
  assert.equal(
    getScenarioDefinition("automated-instructor").reviewMethodValue,
    12,
  );
  assert.equal(checkpointIndex("released"), CHECKPOINTS.length - 1);
  assert.throws(
    () =>
      assertApplicableCheckpoint(
        getScenarioDefinition("automated"),
        "review-ready",
      ),
    /does not apply/,
  );
  assert.equal(
    nextApplicableCheckpoint(
      getScenarioDefinition("automated"),
      "learner-ready",
    ),
    "release-ready",
  );
  assert.equal(
    nextApplicableCheckpoint(
      getScenarioDefinition("automated-instructor"),
      "learner-ready",
    ),
    "review-ready",
  );
});

test("stable request hashes do not depend on object key order", () => {
  assert.equal(
    requestHash({ b: 2, a: { y: 2, x: 1 } }),
    requestHash({ a: { x: 1, y: 2 }, b: 2 }),
  );
});

test("local URL guard rejects remote and non-HTTP targets", () => {
  assert.equal(
    assertLocalUrl("http://localhost:8080/"),
    "http://localhost:8080",
  );
  assert.equal(
    assertLocalUrl("http://127.0.0.1:3000"),
    "http://127.0.0.1:3000",
  );
  assert.throws(() => assertLocalUrl("https://example.com"), /non-local host/);
  assert.throws(() => assertLocalUrl("file:///tmp/api"), /http or https/);
});

test("generated client loader supports both regular and dev-fast output", () => {
  const repoRoot = resolve("/workspace", "gameguild");
  const standard = resolveClientBuildTarget({
    repoRoot,
    fileExists: (path) => path.endsWith("/dist/index.js"),
  });
  const fast = resolveClientBuildTarget({
    repoRoot,
    fileExists: (path) => path.endsWith("/dist-fast/index.js"),
  });
  assert.deepEqual(standard, {
    label: "dist",
    specifier: "@game-guild/client",
  });
  assert.equal(fast.label, "dist-fast");
  assert.match(fast.specifier, /dist-fast\/index\.js$/);
});

test("manifest validation fails closed for unknown versions and ownership", () => {
  const manifest = fixtureManifest();
  assert.equal(validateManifest(manifest, "automated").marker, manifest.marker);
  assert.throws(
    () => validateManifest({ ...manifest, schemaVersion: 99 }, "automated"),
    /Unsupported manifest schemaVersion/,
  );
  assert.throws(
    () =>
      validateManifest(
        { ...manifest, marker: "foreign-resource" },
        "automated",
      ),
    /ownership marker/,
  );
});

test("persona credentials are stable, compact, and unique where required", () => {
  const manifest = createManifest({
    definitionKey: "collective-automated-instructor",
    apiBaseUrl: "http://localhost:8080",
    webBaseUrl: "http://localhost:3000",
    runId: "11111111-2222-4333-8444-555555555555",
  });
  const usernames = Object.values(manifest.personas).map(
    (persona) => persona.username,
  );
  const emails = Object.values(manifest.personas).map(
    (persona) => persona.email,
  );
  const passwords = Object.values(manifest.personas).map(
    (persona) => persona.password,
  );
  assert.equal(new Set(usernames).size, usernames.length);
  assert.equal(new Set(emails).size, emails.length);
  assert.equal(new Set(passwords).size, 1);
  assert.ok(usernames.every((username) => username.length <= 12));
  assert.ok(emails.every((email) => email.length <= 20));
  assert.ok(passwords.every((password) => password === "Qgs!1234"));
  assert.deepEqual(manifest.personas.instructor, {
    email: "instructor1@qgs.test",
    username: "instructor-1",
    password: "Qgs!1234",
    userId: null,
    enrollmentId: null,
  });
  assert.deepEqual(manifest.personas.learnerA, {
    email: "learner1@qgs.test",
    username: "learner-a",
    password: "Qgs!1234",
    userId: null,
    enrollmentId: null,
  });
});

test("atomic JSON writer replaces the target and restricts file permissions", async () => {
  const root = await mkdtemp(resolve(tmpdir(), "qgs-atomic-"));
  const target = resolve(root, "nested", "manifest.json");
  try {
    await writeJsonAtomic(target, { version: 1 });
    await writeJsonAtomic(target, { version: 2 });
    assert.deepEqual(JSON.parse(await readFile(target, "utf8")), {
      version: 2,
    });
    if (process.platform !== "win32") {
      assert.equal((await stat(target)).mode & 0o777, 0o600);
    }
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("scenario lock rejects concurrent work for the same definition", async () => {
  const root = await mkdtemp(resolve(tmpdir(), "qgs-lock-"));
  let release;
  const blocker = new Promise((resolveBlocker) => {
    release = resolveBlocker;
  });
  let acquired;
  const ready = new Promise((resolveReady) => {
    acquired = resolveReady;
  });
  const first = withScenarioLock(
    "automated",
    async () => {
      acquired();
      await blocker;
    },
    { root },
  );
  try {
    await ready;
    await assert.rejects(
      withScenarioLock("automated", async () => {}, { root }),
      /locked by active process/,
    );
  } finally {
    release();
    await first;
    await rm(root, { recursive: true, force: true });
  }
});

test("scenario lock recovers a lock owned by a dead process", async () => {
  const root = await mkdtemp(resolve(tmpdir(), "qgs-stale-lock-"));
  const lockPath = resolve(root, "automated.lock");
  const recovered = [];
  try {
    await writeFile(
      lockPath,
      `${JSON.stringify({ pid: 2_147_483_647, acquiredAt: "2026-10-09T00:00:00.000Z" })}\n`,
    );
    await withScenarioLock("automated", async () => {}, {
      root,
      onStaleLock: (owner) => recovered.push(owner.pid),
    });
    assert.deepEqual(recovered, [2_147_483_647]);
    await assert.rejects(stat(lockPath), { code: "ENOENT" });
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("all browser URLs come from the centralized manifest builder", () => {
  const manifest = fixtureManifest();
  Object.assign(manifest.resources, {
    courseSlug: "scenario-course",
    contentId: "content-id",
    assessmentId: "assessment-id",
    submissionId: "submission-id",
  });
  const urls = buildScenarioUrls(manifest);
  assert.equal(urls.learnerHome, "http://localhost:3000/en-US/learn");
  assert.equal(
    urls.learnerCourses,
    "http://localhost:3000/en-US/learn/courses",
  );
  assert.equal(
    urls.learnerCourse,
    "http://localhost:3000/en-US/learn/courses/scenario-course",
  );
  assert.equal(
    urls.learnerActivity,
    "http://localhost:3000/en-US/learn/courses/scenario-course/activities/assessment-assessment-id",
  );
  assert.match(urls.speedGrader, /submission=submission-id/);
  assert.match(
    urls.assessmentEditor,
    /workspace\/learning\/courses\/scenario-course/,
  );
});

function fixtureManifest() {
  return createManifest({
    definitionKey: "automated",
    apiBaseUrl: "http://localhost:8080",
    webBaseUrl: "http://localhost:3000",
    runId: "11111111-2222-4333-8444-555555555555",
    now: new Date("2026-10-09T00:00:00.000Z"),
  });
}
