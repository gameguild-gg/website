import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { win32 } from "node:path";
import test from "node:test";

import { resolveBashExecutable } from "./run-testing-lab-browser-e2e.mjs";

const packageJson = JSON.parse(
  await readFile(new URL("../package.json", import.meta.url), "utf8"),
);

test("runs Testing Lab browser E2E through the portable isolated runner", () => {
  assert.equal(
    packageJson.scripts["test:browser:testing-lab"],
    "node scripts/run-testing-lab-browser-e2e.mjs",
  );
});

test("resolves Git Bash without falling through to the WSL app alias", () => {
  const expected = win32.join("C:\\Program Files", "Git", "bin", "bash.exe");

  assert.equal(
    resolveBashExecutable({
      platform: "win32",
      env: { ProgramFiles: "C:\\Program Files" },
      exists: (candidate) => candidate === expected,
    }),
    expected,
  );
});

test("isolates the browser journey in a disposable PostgreSQL database", async () => {
  const runner = await readFile(
    new URL("./testing-lab-browser-e2e.sh", import.meta.url),
    "utf8",
  );

  assert.match(runner, /^#!\/usr\/bin\/env bash/m);
  assert.match(runner, /set -euo pipefail/);
  assert.match(runner, /postgres:16-alpine/);
  assert.match(
    runner,
    /POSTGRES_PORT="\$\{TESTING_LAB_E2E_POSTGRES_PORT:-\$\(\(43000 \+ RANDOM % 1000\)\)\}"/,
  );
  assert.match(
    runner,
    /API_PORT="\$\{TESTING_LAB_E2E_API_PORT:-\$\(\(42000 \+ RANDOM % 1000\)\)\}"/,
  );
  assert.match(
    runner,
    /WEB_PORT="\$\{TESTING_LAB_E2E_WEB_PORT:-\$\(\(44000 \+ RANDOM % 1000\)\)\}"/,
  );
  assert.match(runner, /TESTING_LAB_E2E_DATABASE_MODE=disposable/);
  assert.match(runner, /POSTGRES_HOST=127\.0\.0\.1/);
  assert.match(runner, /POSTGRES_PORT=\$\{POSTGRES_PORT\}/);
  assert.match(runner, /POSTGRES_DB=\$\{POSTGRES_DATABASE\}/);
  assert.match(runner, /POSTGRES_USER=\$\{POSTGRES_USER\}/);
  assert.match(runner, /POSTGRES_PASSWORD=\$\{POSTGRES_PASSWORD\}/);
  assert.match(runner, /next dev --turbopack/);
  assert.match(runner, /pnpm --filter @game-guild\/client build/);
  assert.match(runner, /TESTING_LAB_E2E_SKIP_CLIENT_BUILD/);
  assert.match(runner, /TESTING_LAB_E2E_LOCK_DIR/);
  assert.match(runner, /mkdir "\$\{LOCK_DIR\}"/);
  assert.match(runner, /rmdir "\$\{LOCK_DIR\}"/);
  assert.match(runner, /NEXT_BUILD_DIR="\$\{WEB_DIR\}\/\.next"/);
  assert.match(runner, /NEXT_STANDALONE_ROOT=/);
  assert.match(runner, /stage_standalone_assets\(\)/);
  assert.match(runner, /cp -R "\$\{NEXT_BUILD_DIR\}\/static"/);
  assert.match(runner, /cp -R "\$\{WEB_DIR\}\/public\/\."/);
  assert.match(runner, /rm -rf -- "\$\{NEXT_BUILD_DIR\}"/);
  assert.match(runner, /MSYS_NO_PATHCONV=1 taskkill\.exe \/PID/);
  assert.match(runner, /ps -W -p/);
  assert.match(runner, /win_pid/);
  assert.match(runner, /stop_port_listener\(\)/);
  assert.match(
    runner,
    /if \[\[ "\$\(uname -s\)" != MINGW\* && "\$\(uname -s\)" != CYGWIN\* \]\]; then\s+stop_process "\$\{WEB_PID\}"\s+stop_process "\$\{API_PID\}"/,
  );
  assert.match(runner, /netstat\.exe -ano -p tcp \| tr -d '\\r'/);
  assert.match(runner, /stop_port_listener "\$\{WEB_PORT\}"/);
  assert.match(runner, /stop_port_listener "\$\{API_PORT\}"/);
  assert.match(runner, /GAMEGUILD_DISABLE_WEBPACK_CACHE=1/);
  assert.match(runner, /AUTH_COOKIE_SECURE=false/);
  assert.match(runner, /assert_port_available "\$\{POSTGRES_PORT\}"/);
  assert.match(runner, /assert_port_available "\$\{API_PORT\}"/);
  assert.match(runner, /assert_port_available "\$\{WEB_PORT\}"/);
  assert.match(runner, /trap cleanup EXIT INT TERM/);
  assert.match(runner, /ARTIFACTS_DIR=.*REPO_ROOT.*\/artifacts\/test-results\/testing-lab/);
  assert.match(runner, /RUNTIME_DIR=.*ARTIFACTS_DIR.*\/runtime/);
  assert.match(runner, /mkdir -p.*dirname -- "\$\{LOCK_DIR\}"/);
  assert.match(runner, /runner-result\.json/);
  assert.match(
    runner,
    /API_READY_TIMEOUT_SECONDS="\$\{TESTING_LAB_E2E_API_READY_TIMEOUT_SECONDS:-600\}"/,
  );
  assert.match(
    runner,
    /wait_for_http "http:\/\/127\.0\.0\.1:\$\{API_PORT\}\/ready"/,
  );
  assert.match(
    runner,
    /"GameGuild API" "\$\{API_LOG\}" "\$\{API_READY_TIMEOUT_SECONDS\}" "\$\{API_PID\}"/,
  );
  assert.match(runner, /wait_for_http .* "\$\{API_PID\}"/);
  assert.match(runner, /kill -0 "\$\{process_pid\}"/);
  assert.match(runner, /docker rm -f/);
  assert.doesNotMatch(runner, /docker compose down/);
});

test("allows cold SSR route compilation without aborting browser navigation", async () => {
  const journey = await readFile(
    new URL("./testing-lab-browser-e2e.mjs", import.meta.url),
    "utf8",
  );

  assert.match(journey, /page\.setDefaultNavigationTimeout\(120_000\)/);
  assert.match(
    journey,
    /const reviewerPage = await reviewerContext\.newPage\(\);\s+reviewerPage\.setDefaultNavigationTimeout\(120_000\)/,
  );
  assert.match(journey, /async function warmTestingLabSsr()/);
  assert.ok(
    journey.includes(
      'await page.locator("h1").first().waitFor({ state: "visible" });',
    ),
  );
  assert.match(
    journey,
    /await page\.reload\(\{ waitUntil: ["']domcontentloaded["'] \}\);\s+await waitForClientHydration\(page\);\s+await assertNoErrorSurface/,
  );
  assert.match(
    journey,
    /warmSsr\(["']\/en-US\/testing-lab["'], ["']Testing Lab SSR["']\)/,
  );
  assert.ok(
    journey.indexOf("const fixture = await bootstrap();") <
      journey.indexOf("await warmTestingLabSsr();"),
  );
});

test("keeps the raw browser command explicitly unsafe for shared environments", () => {
  assert.equal(
    packageJson.scripts["test:browser:testing-lab:existing"],
    "node scripts/testing-lab-browser-e2e.mjs",
  );
});
test("waits for hydration before every client-side Testing Lab mutation", async () => {
  const journey = await readFile(
    new URL("./testing-lab-browser-e2e.mjs", import.meta.url),
    "utf8",
  );
  const scenarios = [
    [
      '"project-owner public Testing Lab event"',
      "await waitForClientHydration(ownerPage);",
      'name: "Join", exact: true',
    ],
    [
      '"Testing Lab manager applications"',
      "await waitForClientHydration(page);",
      'name: "Review", exact: true',
    ],
    [
      '"committee review applications"',
      "await waitForClientHydration(reviewerPage);",
      'name: "Vote", exact: true',
    ],
    [
      '"scheduled public Testing Lab event"',
      "await waitForClientHydration(testerPage);",
      'name: "Join", exact: true',
    ],
  ];

  for (const [visitMarker, hydrationMarker, actionMarker] of scenarios) {
    const visitIndex = journey.indexOf(visitMarker);
    const hydrationIndex = journey.indexOf(hydrationMarker, visitIndex);
    const actionIndex = journey.indexOf(actionMarker, visitIndex);
    assert.ok(
      visitIndex >= 0 &&
        hydrationIndex > visitIndex &&
        actionIndex > hydrationIndex,
    );
  }
});
test("covers the complete Testing Lab operational browser matrix", async () => {
  const journey = await readFile(
    new URL("./testing-lab-browser-e2e.mjs", import.meta.url),
    "utf8",
  );

  for (const scenario of [
    "general settings persistence",
    "location lifecycle",
    "role and member access lifecycle",
    "attendance and required feedback",
    "event filters search and pagination",
    "event cancellation and read-only history",
  ]) {
    assert.ok(
      journey.includes(`[testing-lab-browser-e2e] ${scenario}`),
      `missing browser scenario: ${scenario}`,
    );
  }

  for (const expectedInteraction of [
    'name: "Save settings", exact: true',
    'name: "New location", exact: true',
    'name: "New role", exact: true',
    'name: "Manage access", exact: true',
    'name: "Start playtest", exact: true',
    'name: "Assign tested project", exact: true',
    'name: "Submit required feedback", exact: true',
    'name: "Cancel event", exact: true',
  ]) {
    assert.ok(
      journey.includes(expectedInteraction),
      `missing interaction: ${expectedInteraction}`,
    );
  }
});
test("waits for hydration after SSR reloads before editing locations and roles", async () => {
  const journey = await readFile(
    new URL("./testing-lab-browser-e2e.mjs", import.meta.url),
    "utf8",
  );

  for (const scenarioMarker of [
    "[testing-lab-browser-e2e] location lifecycle",
    "[testing-lab-browser-e2e] role and member access lifecycle",
  ]) {
    const scenarioIndex = journey.indexOf(scenarioMarker);
    const editIndex = journey.indexOf(
      'name: "Edit", exact: true',
      scenarioIndex,
    );
    const reloadIndex = journey.lastIndexOf(
      'page.reload({ waitUntil: "domcontentloaded" })',
      editIndex,
    );
    const hydrationIndex = journey.indexOf(
      "await waitForClientHydration(page);",
      reloadIndex,
    );
    assert.ok(
      scenarioIndex >= 0 &&
        editIndex > scenarioIndex &&
        reloadIndex > scenarioIndex &&
        hydrationIndex > reloadIndex &&
        hydrationIndex < editIndex,
    );
  }
});

test("waits for the canonical workspace redirect after browser sign-in", async () => {
  const journey = await readFile(
    new URL("./testing-lab-browser-e2e.mjs", import.meta.url),
    "utf8",
  );

  const signInStart = journey.indexOf("async function signIn(");
  const submitIndex = journey.indexOf(
    ".click({ noWaitAfter: true });",
    signInStart,
  );
  const workspaceIndex = journey.indexOf(
    'url.pathname.endsWith("/workspace")',
    submitIndex,
  );

  assert.ok(signInStart >= 0);
  assert.ok(submitIndex > signInStart);
  assert.ok(
    workspaceIndex > submitIndex,
    "the canonical workspace redirect must settle before the next journey navigation",
  );
});

test("sequences fixture sign-ins that share the source-IP lockout budget", async () => {
  const journey = await readFile(
    new URL("./testing-lab-browser-e2e.mjs", import.meta.url),
    "utf8",
  );

  assert.match(
    journey,
    /const owner = await createFixtureIdentity\("owner"\);\s+const reviewer = await createFixtureIdentity\("reviewer"\);\s+const tester = await createFixtureIdentity\("tester"\);/,
  );
  assert.doesNotMatch(journey, /Promise\.all\(\[\s*createFixtureIdentity/);
});
