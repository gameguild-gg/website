import { randomUUID } from "node:crypto";
import {
  chmod,
  mkdir,
  open,
  readFile,
  rename,
  rm,
  writeFile,
} from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import {
  CHECKPOINTS,
  DEFINITION_SCHEMA_VERSION,
  getScenarioDefinition,
} from "./definitions.mjs";

export const MANIFEST_SCHEMA_VERSION = 1;

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
export const WEB_ROOT = resolve(SCRIPT_DIR, "../..");
export const REPO_ROOT = resolve(WEB_ROOT, "../..");
export const RESULTS_ROOT = resolve(
  WEB_ROOT,
  "test-results/quiz-grading-scenarios",
);

export function scenarioDirectory(key) {
  getScenarioDefinition(key);
  return resolve(RESULTS_ROOT, key);
}

export function manifestPath(key) {
  return resolve(scenarioDirectory(key), "manifest.json");
}

export function reportPath(key) {
  return resolve(scenarioDirectory(key), "report.json");
}

export function evidenceDirectory(key) {
  return resolve(scenarioDirectory(key), "evidence");
}

export function storageStatePath(key, personaKey) {
  assertSafeSegment(personaKey, "persona key");
  return resolve(scenarioDirectory(key), `storage-${personaKey}.json`);
}

export function assertLocalUrl(value, label = "URL") {
  let url;
  try {
    url = new URL(value);
  } catch {
    throw new Error(`${label} must be a valid absolute URL.`);
  }
  if (!["http:", "https:"].includes(url.protocol)) {
    throw new Error(`${label} must use http or https.`);
  }
  const host = url.hostname.toLowerCase();
  const local =
    host === "localhost" ||
    host === "127.0.0.1" ||
    host === "::1" ||
    host.endsWith(".localhost");
  if (!local) {
    throw new Error(
      `${label} resolves to non-local host "${url.hostname}". The scenario runner intentionally refuses remote environments.`,
    );
  }
  return url.toString().replace(/\/$/, "");
}

export function createManifest({
  definitionKey,
  apiBaseUrl,
  webBaseUrl,
  runId = randomUUID(),
  now = new Date(),
}) {
  const definition = getScenarioDefinition(definitionKey);
  const marker = `qgs-${definition.key}-${runId.replaceAll("-", "").slice(0, 12)}`;
  const timestamp = now.toISOString();
  const personaKeys = ["instructor", "learnerA", "outsider"];
  if (definition.subject === "collective") personaKeys.splice(2, 0, "learnerB");

  return {
    schemaVersion: MANIFEST_SCHEMA_VERSION,
    definitionSchemaVersion: DEFINITION_SCHEMA_VERSION,
    definitionKey,
    runId,
    marker,
    createdAt: timestamp,
    updatedAt: timestamp,
    environment: {
      apiBaseUrl: assertLocalUrl(apiBaseUrl, "API base URL"),
      webBaseUrl: assertLocalUrl(webBaseUrl, "web base URL"),
      tenantId: null,
    },
    checkpoint: "empty",
    personas: Object.fromEntries(
      personaKeys.map((key) => [key, createPersona(key)]),
    ),
    resources: {
      courseId: null,
      courseSlug: null,
      gradingGroupId: null,
      contentId: null,
      contentVersion: null,
      contentDraftRevision: null,
      assessmentId: null,
      assessmentVersion: null,
      candidateRevisionId: null,
      publishedRevisionId: null,
      groupSetId: null,
      courseGroupId: null,
      enrollmentIds: [],
      submissionId: null,
      gradingExecutionId: null,
      gradeResultId: null,
      testRunId: null,
    },
    commands: {},
    urls: {},
  };
}

function createPersona(key) {
  const identity = {
    instructor: {
      email: "instructor1@qgs.test",
      username: "instructor-1",
    },
    learnerA: { email: "learner1@qgs.test", username: "learner-a" },
    learnerB: { email: "learner2@qgs.test", username: "learner-b" },
    outsider: { email: "outsider1@qgs.test", username: "outsider-1" },
  }[key];
  if (!identity) throw new Error(`Unsupported scenario persona "${key}".`);
  return {
    ...identity,
    password: "Qgs!1234",
    userId: null,
    enrollmentId: null,
  };
}

export function validateManifest(value, expectedKey) {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new Error("Scenario manifest must be a JSON object.");
  }
  if (value.schemaVersion !== MANIFEST_SCHEMA_VERSION) {
    throw new Error(
      `Unsupported manifest schemaVersion ${String(value.schemaVersion)}. Run reset for the old scenario and recreate it; no legacy conversion is provided.`,
    );
  }
  if (value.definitionSchemaVersion !== DEFINITION_SCHEMA_VERSION) {
    throw new Error(
      `Manifest definition schema ${String(value.definitionSchemaVersion)} does not match ${DEFINITION_SCHEMA_VERSION}. Recreate the scenario.`,
    );
  }
  if (typeof value.definitionKey !== "string") {
    throw new Error("Scenario manifest has no definitionKey.");
  }
  getScenarioDefinition(value.definitionKey);
  if (expectedKey && value.definitionKey !== expectedKey) {
    throw new Error(
      `Manifest belongs to "${value.definitionKey}", not "${expectedKey}".`,
    );
  }
  if (!CHECKPOINTS.includes(value.checkpoint)) {
    throw new Error(
      `Manifest has unknown checkpoint "${String(value.checkpoint)}".`,
    );
  }
  if (
    !value.marker ||
    !String(value.marker).startsWith(`qgs-${value.definitionKey}-`)
  ) {
    throw new Error("Manifest ownership marker is missing or invalid.");
  }
  if (
    !value.environment ||
    !value.personas ||
    !value.resources ||
    !value.commands
  ) {
    throw new Error("Manifest is missing required sections.");
  }
  value.environment.apiBaseUrl = assertLocalUrl(
    value.environment.apiBaseUrl,
    "manifest API base URL",
  );
  value.environment.webBaseUrl = assertLocalUrl(
    value.environment.webBaseUrl,
    "manifest web base URL",
  );
  for (const [key, persona] of Object.entries(value.personas)) {
    if (!persona?.email || !persona?.username || !persona?.password) {
      throw new Error(`Manifest persona "${key}" is incomplete.`);
    }
  }
  return value;
}

export async function readManifest(key, { optional = false } = {}) {
  try {
    const raw = await readFile(manifestPath(key), "utf8");
    return validateManifest(JSON.parse(raw), key);
  } catch (error) {
    if (optional && error?.code === "ENOENT") return null;
    if (error instanceof SyntaxError) {
      throw new Error(
        `Manifest for "${key}" is not valid JSON: ${error.message}`,
      );
    }
    throw error;
  }
}

export async function writeManifest(manifest) {
  validateManifest(manifest, manifest.definitionKey);
  manifest.updatedAt = new Date().toISOString();
  const directory = scenarioDirectory(manifest.definitionKey);
  const target = manifestPath(manifest.definitionKey);
  await mkdir(directory, { recursive: true, mode: 0o700 });
  await chmod(directory, 0o700).catch(() => {});
  await writeJsonAtomic(target, manifest);
}

export async function writeJsonAtomic(target, value) {
  const temporary = `${target}.${process.pid}.${randomUUID()}.tmp`;
  await mkdir(dirname(target), { recursive: true, mode: 0o700 });
  await writeFile(temporary, `${JSON.stringify(value, null, 2)}\n`, {
    encoding: "utf8",
    mode: 0o600,
  });
  await rename(temporary, target);
  await chmod(target, 0o600).catch(() => {});
}

export async function writeReport(key, report) {
  const directory = scenarioDirectory(key);
  await mkdir(directory, { recursive: true, mode: 0o700 });
  await writeFile(reportPath(key), `${JSON.stringify(report, null, 2)}\n`, {
    encoding: "utf8",
    mode: 0o600,
  });
}

export async function withScenarioLock(
  key,
  work,
  { root = RESULTS_ROOT, onStaleLock = () => {} } = {},
) {
  const directory = scenarioDirectory(key);
  const lockPath = resolve(root, `${key}.lock`);
  await mkdir(root, { recursive: true, mode: 0o700 });
  const owner = {
    pid: process.pid,
    token: randomUUID(),
    acquiredAt: new Date().toISOString(),
    directory,
  };
  await acquireScenarioLock(key, lockPath, owner, onStaleLock);

  let stopping = false;
  const releaseOnSignal = (exitCode) => {
    if (stopping) return;
    stopping = true;
    void releaseOwnedLock(lockPath, owner.token).finally(() => {
      process.exit(exitCode);
    });
  };
  const onSigint = () => releaseOnSignal(130);
  const onSigterm = () => releaseOnSignal(143);
  process.once("SIGINT", onSigint);
  process.once("SIGTERM", onSigterm);

  try {
    return await work();
  } finally {
    process.removeListener("SIGINT", onSigint);
    process.removeListener("SIGTERM", onSigterm);
    await releaseOwnedLock(lockPath, owner.token);
  }
}

async function acquireScenarioLock(key, lockPath, owner, onStaleLock) {
  for (let attempt = 0; attempt < 3; attempt += 1) {
    let handle;
    try {
      handle = await open(lockPath, "wx", 0o600);
      await handle.writeFile(`${JSON.stringify(owner)}\n`);
      await handle.close();
      return;
    } catch (error) {
      await handle?.close().catch(() => {});
      if (error?.code !== "EEXIST") throw error;

      const existing = await readLockOwner(lockPath);
      if (existing && isProcessAlive(existing.pid)) {
        throw new Error(
          `Scenario "${key}" is locked by active process ${existing.pid} since ${existing.acquiredAt ?? "an unknown time"} (${lockPath}).`,
        );
      }
      if (!existing) {
        throw new Error(
          `Scenario "${key}" has an unreadable lock (${lockPath}). Confirm no runner is active, then remove that file.`,
        );
      }

      await rm(lockPath, { force: true });
      onStaleLock(existing);
    }
  }
  throw new Error(`Could not acquire scenario lock ${lockPath}.`);
}

async function readLockOwner(lockPath) {
  try {
    const value = JSON.parse(await readFile(lockPath, "utf8"));
    return Number.isInteger(value?.pid) && value.pid > 0 ? value : null;
  } catch (error) {
    if (error?.code === "ENOENT") return null;
    return null;
  }
}

function isProcessAlive(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return error?.code !== "ESRCH";
  }
}

async function releaseOwnedLock(lockPath, token) {
  const current = await readLockOwner(lockPath);
  if (current?.token === token) await rm(lockPath, { force: true });
}

export async function removeScenarioSecrets(key) {
  let manifest = null;
  try {
    manifest = await readManifest(key, { optional: true });
  } catch {
    // A cleanup report is more useful than retaining an unreadable secret file.
  }
  if (manifest) {
    for (const persona of Object.values(manifest.personas)) {
      persona.password = "<removed>";
    }
    await writeReport(key, {
      kind: "reset",
      scenario: key,
      runId: manifest.runId,
      completedAt: new Date().toISOString(),
    });
  }
  const entries = Object.keys(manifest?.personas ?? {}).map((persona) =>
    storageStatePath(key, persona),
  );
  await Promise.all(entries.map((entry) => rm(entry, { force: true })));
  await rm(manifestPath(key), { force: true });
}

function assertSafeSegment(value, label) {
  if (!/^[a-zA-Z0-9_-]+$/.test(value)) {
    throw new Error(`Invalid ${label} "${value}".`);
  }
}
