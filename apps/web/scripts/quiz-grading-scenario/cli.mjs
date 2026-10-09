#!/usr/bin/env node
import { parseArgs } from "node:util";
import {
  assertApplicableCheckpoint,
  getScenarioDefinition,
  listScenarioDefinitions,
} from "./definitions.mjs";
import {
  assertLocalUrl,
  readManifest,
  withScenarioLock,
  writeReport,
} from "./manifest.mjs";

const { positionals, values } = parseArgs({
  args: process.argv.slice(2),
  allowPositionals: true,
  strict: true,
  options: {
    scenario: { type: "string", short: "s" },
    checkpoint: { type: "string", short: "c" },
    fresh: { type: "boolean", default: false },
    headed: { type: "boolean", default: false },
    cleanup: { type: "boolean", default: false },
    json: { type: "boolean", default: false },
    help: { type: "boolean", short: "h", default: false },
    "api-base-url": { type: "string" },
    "web-base-url": { type: "string" },
  },
});

if (values.help || positionals.length === 0) {
  printHelp();
  process.exit(values.help ? 0 : 2);
}

const command = positionals[0];
const apiBaseUrl = assertLocalUrl(
  values["api-base-url"] ??
    process.env.QUIZ_GRADING_API_BASE_URL ??
    process.env.NEXT_PUBLIC_API_URL ??
    "http://localhost:8080",
  "API base URL",
);
const webBaseUrl = assertLocalUrl(
  values["web-base-url"] ??
    process.env.QUIZ_GRADING_WEB_BASE_URL ??
    process.env.PLAYWRIGHT_WEB_BASE_URL ??
    "http://localhost:3000",
  "web base URL",
);

try {
  if (command === "list") {
    await list();
  } else {
    const scenarioKey = requireScenario(values.scenario);
    if (command === "prepare") {
      await prepare(scenarioKey, values.checkpoint ?? "learner-ready");
    } else if (command === "status") {
      await status(scenarioKey);
    } else if (command === "verify") {
      await verify(scenarioKey);
    } else if (command === "reset") {
      await reset(scenarioKey);
    } else if (command === "test") {
      await test(scenarioKey, values.checkpoint ?? "learner-ready");
    } else {
      throw new Error(`Unknown command "${command}".`);
    }
  }
} catch (error) {
  console.error(
    values.json
      ? JSON.stringify({ ok: false, error: error.message })
      : `\n[grading:scenario] ${error.message}`,
  );
  process.exitCode = 1;
}

async function prepare(scenarioKey, checkpoint) {
  const definition = getScenarioDefinition(scenarioKey);
  assertApplicableCheckpoint(definition, checkpoint);
  if (values.fresh) {
    const existing = await readManifest(scenarioKey, { optional: true });
    if (existing) await reset(scenarioKey);
  }
  const { prepareScenario } = await import("./provision.mjs");
  const { verifyScenario } = await import("./verify.mjs");
  progress(`Preparing "${scenarioKey}" through ${checkpoint}.`);
  const manifest = await withCliScenarioLock(scenarioKey, () =>
    prepareScenario({
      scenarioKey,
      checkpoint,
      apiBaseUrl,
      webBaseUrl,
      onProgress: progress,
    }),
  );
  progress("Verifying the persisted scenario state.");
  const { report } = await verifyScenario(scenarioKey);
  await writeReport(scenarioKey, report);
  progress("Scenario is ready. Use the URLs and persona credentials below.");
  outputScenario(manifest, report, { credentials: true });
}

async function status(scenarioKey) {
  const manifest = await readManifest(scenarioKey, { optional: true });
  if (!manifest) {
    output({ scenario: scenarioKey, status: "not-prepared" });
    return;
  }
  try {
    const { verifyScenario } = await import("./verify.mjs");
    const { report } = await verifyScenario(scenarioKey, { strict: false });
    outputScenario(manifest, report, { credentials: false });
  } catch (error) {
    output({
      scenario: scenarioKey,
      declaredCheckpoint: manifest.checkpoint,
      healthy: false,
      diagnostic: error.message,
      urls: manifest.urls,
    });
  }
}

async function verify(scenarioKey) {
  const { verifyScenario } = await import("./verify.mjs");
  const { manifest, report } = await verifyScenario(scenarioKey);
  await writeReport(scenarioKey, report);
  outputScenario(manifest, report, { credentials: false });
}

async function reset(scenarioKey) {
  const { resetScenario } = await import("./cleanup.mjs");
  const report = await withCliScenarioLock(scenarioKey, () =>
    resetScenario(scenarioKey),
  );
  output(report);
}

async function test(scenarioKey, checkpoint) {
  const definition = getScenarioDefinition(scenarioKey);
  assertApplicableCheckpoint(definition, checkpoint);
  if (values.fresh) {
    const existing = await readManifest(scenarioKey, { optional: true });
    if (existing) await reset(scenarioKey);
  }
  await prepare(scenarioKey, checkpoint);
  const { runBrowserScenario } = await import("./browser.mjs");
  await withCliScenarioLock(scenarioKey, () =>
    runBrowserScenario({ scenarioKey, headed: values.headed }),
  );
  await verify(scenarioKey);
  if (values.cleanup) await reset(scenarioKey);
}

async function list() {
  const entries = [];
  for (const definition of listScenarioDefinitions()) {
    const manifest = await readManifest(definition.key, { optional: true });
    entries.push({
      key: definition.key,
      subject: definition.subject,
      workflow: definition.reviewMethods.join(" + "),
      prepared: Boolean(manifest),
      checkpoint: manifest?.checkpoint ?? null,
      urls: manifest
        ? {
            assessmentEditor: manifest.urls.assessmentEditor,
            learnerCourses: manifest.urls.learnerCourses,
            learnerActivity: manifest.urls.learnerActivity,
            speedGrader: manifest.urls.speedGrader,
          }
        : {},
    });
  }
  output(entries);
}

function outputScenario(manifest, report, { credentials }) {
  output({
    scenario: manifest.definitionKey,
    runId: manifest.runId,
    marker: manifest.marker,
    declaredCheckpoint: manifest.checkpoint,
    confirmedCheckpoint: report.confirmedCheckpoint,
    healthy: report.healthy,
    nextCheckpoint: report.nextCheckpoint,
    urls: manifest.urls,
    ...(credentials
      ? {
          personas: Object.fromEntries(
            Object.entries(manifest.personas).map(([key, persona]) => [
              key,
              {
                email: persona.email,
                username: persona.username,
                password: persona.password,
                userId: persona.userId,
              },
            ]),
          ),
        }
      : {}),
    checks: report.checks,
  });
}

function output(value) {
  if (values.json) {
    console.log(JSON.stringify(value, null, 2));
    return;
  }
  console.log(JSON.stringify(value, null, 2));
}

function progress(message) {
  console.error(`[grading:scenario] ${message}`);
}

function withCliScenarioLock(scenarioKey, work) {
  return withScenarioLock(scenarioKey, work, {
    onStaleLock: (owner) =>
      progress(
        `Recovered stale lock from process ${owner.pid} (acquired ${owner.acquiredAt ?? "at an unknown time"}).`,
      ),
  });
}

function requireScenario(value) {
  if (!value) throw new Error("--scenario is required for this command.");
  getScenarioDefinition(value);
  return value;
}

function printHelp() {
  console.log(`Quiz Grading Scenario Runner

Usage:
  pnpm grading:scenario list
  pnpm grading:scenario prepare --scenario <key> --checkpoint <checkpoint>
  pnpm grading:scenario status --scenario <key>
  pnpm grading:scenario verify --scenario <key>
  pnpm grading:scenario test --scenario <key> [--headed] [--cleanup]
  pnpm grading:scenario reset --scenario <key>

Options:
  --fresh                 Reset the owned execution before preparing it
  --api-base-url <url>    Defaults to http://localhost:8080
  --web-base-url <url>    Defaults to http://localhost:3000
  --json                  Machine-readable output
`);
}
