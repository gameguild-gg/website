import { chromium } from "playwright";
import rootGlobalSetup from "../../../../scripts/e2e-global-setup.mjs";
import { getValidationCase } from "../../../../apps/web/src/lib/grading-scenarios/adapters/quiz/capability-catalog.mjs";
import { getScenarioDefinition } from "../../../../apps/web/src/lib/grading-scenarios/adapters/quiz/definitions.mjs";
import { readManifest } from "../../../../apps/web/src/lib/grading-scenarios/adapters/quiz/manifest.server.mjs";
import { verifyQuizGradingScenario } from "../../../../apps/web/src/lib/grading-scenarios/adapters/quiz/operations.server.mjs";
import { listValidationEvidence } from "../../../../apps/web/src/lib/grading-scenarios/adapters/quiz/report.server.mjs";
import { assertSharedAuthCookie } from "../../../../apps/web/scripts/learning-browser-e2e-support.mjs";
import {
  completeMfaSignInWithReplayRetry,
  createMfaSignInContinuation,
  E2E_ADMIN_TOTP_SECRET,
} from "../../../../apps/web/scripts/sign-in-mfa-support.mjs";
import { readBrowserResultJournal } from "./validation-reporter.mjs";
import { resolveScenarioTargetKeys } from "./selection.mjs";

const ADMIN_EMAIL =
  process.env.E2E_SYSTEM_ADMIN_EMAIL ?? "admin@game-guild.com";
const ADMIN_PASSWORD = process.env.E2E_SYSTEM_ADMIN_PASSWORD ?? "Admin123!";
const continueMfaSignIn = createMfaSignInContinuation();

export default async function gradingValidationSetup(config) {
  // The API provisions this deterministic factor only for the E2E process.
  // Authentication still completes through the native MFA challenge.
  process.env.Seed__AdminTotpSecret = E2E_ADMIN_TOTP_SECRET;
  const stopStack = await rootGlobalSetup(config);
  const scenarioKey = requiredEnvironment("QUIZ_GRADING_SCENARIO");
  const checkpoint = process.env.QUIZ_GRADING_CHECKPOINT ?? "learner-ready";
  const webBaseUrl = (
    process.env.PLAYWRIGHT_WEB_BASE_URL ?? "http://localhost:3000"
  ).replace(/\/$/, "");
  const targetKeys = resolveScenarioTargetKeys(scenarioKey);
  process.env.QUIZ_GRADING_QUIZZES = targetKeys.join(",");

  try {
    await prepareThroughLab({
      webBaseUrl,
      scenarioKey,
      checkpoint,
      targetKeys,
      fresh: process.env.QUIZ_GRADING_FRESH !== "0",
    });
  } catch (error) {
    await stopStack?.();
    throw error;
  }

  return async () => {
    try {
      await finalizeBrowserResults(scenarioKey, targetKeys);
      if (process.env.QUIZ_GRADING_KEEP_SCENARIO !== "1") {
        await resetThroughLab({ webBaseUrl, scenarioKey });
      }
    } finally {
      await stopStack?.();
    }
  };
}

async function prepareThroughLab({
  webBaseUrl,
  scenarioKey,
  checkpoint,
  targetKeys,
  fresh,
}) {
  await withAdminPage(webBaseUrl, async (page) => {
    await page.goto(`${webBaseUrl}/workspace/learning/grading-validation`, {
      waitUntil: "domcontentloaded",
      timeout: 180_000,
    });
    await page
      .getByRole("heading", { name: "Grading validation" })
      .waitFor({ state: "visible", timeout: 180_000 });
    await waitForLabHydration(page);
    await page
      .getByLabel("Grading adapter", { exact: true })
      .selectOption("quiz");
    await page
      .getByLabel("Scenario", { exact: true })
      .selectOption(scenarioKey);
    await page
      .getByLabel("Checkpoint", { exact: true })
      .selectOption(checkpoint);

    const definition = getScenarioDefinition(scenarioKey);
    const selected = new Set(targetKeys);
    for (const target of definition.quizzes) {
      const checkbox = page.getByTestId(`grading-target-${target.key}`);
      await checkbox.waitFor({ state: "visible" });
      const checked = (await checkbox.getAttribute("aria-checked")) === "true";
      if (checked !== selected.has(target.key)) await checkbox.click();
    }

    const recreate = page.getByRole("switch", {
      name: "Recreate owned scenario",
    });
    const recreateChecked =
      (await recreate.getAttribute("aria-checked")) === "true";
    if (recreateChecked !== fresh) await recreate.click();

    await page.getByRole("button", { name: "Prepare", exact: true }).click();
    await page
      .locator("#dashboard-main")
      .getByText(`Scenario prepared through ${checkpoint}.`, { exact: true })
      .waitFor({ state: "visible", timeout: 900_000 });
  });

  const manifest = await readManifest(scenarioKey);
  const prepared = new Set(manifest.quizKeys);
  for (const targetKey of targetKeys) {
    if (!prepared.has(targetKey)) {
      throw new Error(
        `The web lab did not prepare target "${targetKey}" for "${scenarioKey}".`,
      );
    }
  }
}

async function resetThroughLab({ webBaseUrl, scenarioKey }) {
  await withAdminPage(webBaseUrl, async (page) => {
    await page.goto(`${webBaseUrl}/workspace/learning/grading-validation`, {
      waitUntil: "domcontentloaded",
      timeout: 180_000,
    });
    await page
      .getByRole("heading", { name: "Grading validation" })
      .waitFor({ state: "visible", timeout: 180_000 });
    await waitForLabHydration(page);
    await page
      .getByLabel("Grading adapter", { exact: true })
      .selectOption("quiz");
    await page
      .getByLabel("Scenario", { exact: true })
      .selectOption(scenarioKey);
    const reset = page.getByTestId("grading-scenario-reset-trigger");
    await reset.waitFor({ state: "visible", timeout: 180_000 });
    if (await reset.isDisabled()) return;
    await reset.click();
    const confirm = page.getByTestId("grading-scenario-reset-confirm");
    await confirm.waitFor({ state: "visible", timeout: 30_000 });
    await confirm.click();
    await page
      .locator("#dashboard-main")
      .getByText(
        /Removed \d+ owned resources|No owned resources required removal/,
      )
      .waitFor({ state: "visible", timeout: 300_000 });
  });
}

async function waitForLabHydration(page) {
  await page
    .locator('[data-testid="grading-scenario-lab"][data-hydrated="true"]')
    .waitFor({ state: "visible", timeout: 180_000 });
}

async function finalizeBrowserResults(scenarioKey, targetKeys) {
  const manifest = await readManifest(scenarioKey, { optional: true });
  if (!manifest) return;
  const journal = readBrowserResultJournal(scenarioKey);
  const results = [];
  for (const [caseId, executions] of Object.entries(journal.cases ?? {})) {
    const belongsToSelection = targetKeys.includes(
      getValidationCase(caseId).quizKey ?? getValidationCase(caseId).key,
    );
    if (!belongsToSelection || executions.length === 0) continue;
    if (executions.every((execution) => execution.status === "passed")) {
      results.push({
        caseId,
        status: "pass",
        evidence: await listValidationEvidence(manifest.runId, caseId),
      });
    }
  }
  await verifyQuizGradingScenario({
    scenarioKey,
    completedAutomations: ["api", "browser"],
    results,
  });
}

async function withAdminPage(webBaseUrl, work) {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext();
  try {
    await signIn(context, webBaseUrl);
    const page = await context.newPage();
    await work(page);
  } finally {
    await context.close();
    await browser.close();
  }
}

async function signIn(context, webBaseUrl) {
  await completeMfaSignInWithReplayRetry({
    accountKey: ADMIN_EMAIL,
    begin: async () => {
      const firstFactor = await postCredentials(context, webBaseUrl, {
        email: ADMIN_EMAIL,
        password: ADMIN_PASSWORD,
      });
      if (!firstFactor.ok && firstFactor.data?.error !== "MfaRequired") {
        throw new Error(
          `SystemAdmin browser sign-in failed (HTTP ${firstFactor.status}).`,
        );
      }
      return firstFactor.data;
    },
    continuation: continueMfaSignIn,
    isReplayFailure: (error) =>
      error instanceof SystemAdminMfaHttpError && error.status === 401,
    send: async (action, body) => {
      if (action !== "complete") {
        throw new Error(`Unsupported SystemAdmin MFA action "${action}".`);
      }
      const completed = await postCredentials(context, webBaseUrl, body);
      if (!completed.ok) {
        throw new SystemAdminMfaHttpError(completed.status);
      }
      return completed.data;
    },
  });

  assertSharedAuthCookie(await context.cookies([webBaseUrl]));
}

class SystemAdminMfaHttpError extends Error {
  constructor(status) {
    super(`SystemAdmin browser MFA failed (HTTP ${status}).`);
    this.name = "SystemAdminMfaHttpError";
    this.status = status;
  }
}

async function postCredentials(context, webBaseUrl, credentials) {
  const csrfResponse = await context.request.get(
    `${webBaseUrl}/api/auth/csrf`,
    { timeout: 180_000 },
  );
  const csrf = await csrfResponse.json().catch(() => null);
  if (!csrfResponse.ok() || typeof csrf?.csrfToken !== "string") {
    throw new Error(
      `SystemAdmin CSRF request failed (HTTP ${csrfResponse.status()}).`,
    );
  }

  const response = await context.request.post(
    `${webBaseUrl}/api/auth/signin/credentials`,
    {
      headers: { "Content-Type": "application/json" },
      timeout: 180_000,
      data: {
        ...credentials,
        csrfToken: csrf.csrfToken,
        redirect: false,
        redirectTo: "/workspace/learning/grading-validation",
      },
    },
  );
  return {
    ok: response.ok(),
    status: response.status(),
    data: await response.json().catch(() => ({})),
  };
}

function requiredEnvironment(name) {
  const value = process.env[name]?.trim();
  if (!value) throw new Error(`${name} is required by grading Playwright.`);
  return value;
}
