import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
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

test("skips WSL aliases when deriving Git Bash from PATH", () => {
  const expected = win32.join("C:\\Program Files", "Git", "bin", "bash.exe");
  for (const aliasDirectory of [
    "C:\\Windows\\System32",
    "C:\\Windows\\SysWOW64",
    "C:\\Users\\Example\\AppData\\Local\\Microsoft\\WindowsApps",
  ]) {
    const alias = win32.join(aliasDirectory, "bash.exe");
    assert.equal(resolveBashExecutable({
      platform: "win32",
      env: { Path: aliasDirectory + ";C:\\Program Files\\Git\\cmd" },
      exists: (candidate) => candidate === alias || candidate === expected,
    }), expected);
  }
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
    /POSTGRES_PORT="\$\{TESTING_LAB_E2E_POSTGRES_PORT:-\$\(pick_default_port 43000\)\}"/,
  );
  assert.match(
    runner,
    /API_PORT="\$\{TESTING_LAB_E2E_API_PORT:-\$\(pick_default_port 42000\)\}"/,
  );
  assert.match(
    runner,
    /WEB_PORT="\$\{TESTING_LAB_E2E_WEB_PORT:-\$\(pick_default_port 44000\)\}"/,
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

async function readPortProbe() {
  const runner = await readFile(
    new URL("./testing-lab-browser-e2e.sh", import.meta.url),
    "utf8",
  );
  const functionSource = runner.match(
    /pick_default_port\(\) \{[\s\S]*?\r?\n\}/,
  )?.[0];
  assert.ok(functionSource, "the default port probe must be present");
  const initializers = [...runner.matchAll(/^(?:POSTGRES|API|WEB)_PORT=.*$/gm)]
    .map((match) => match[0]);
  assert.equal(initializers.length, 3);
  return { functionSource, initializers };
}

function runMockPortProbe(functionSource, commands, env = {}) {
  // Execute only the helper with a mocked node command. No socket, application,
  // database, container or SDK is started.
  const result = spawnSync(
    resolveBashExecutable({ platform: process.platform, env: process.env }),
    [
      "--noprofile",
      "--norc",
      "-c",
      ["set -eu", functionSource, ...commands].join("\n"),
    ],
    { encoding: "utf8", env: { ...process.env, ...env }, timeout: 10_000 },
  );
  assert.ifError(result.error);
  return result;
}

test("retries occupied default ports and returns the first successful probe", async (context) => {
  const { functionSource } = await readPortProbe();
  const result = runMockPortProbe(functionSource, [
    "mock_calls=0",
    'node() { mock_calls=$((mock_calls + 1)); printf "PROBE:%s\\n" "$3" >&2; test "$mock_calls" -ge 3; }',
    "pick_default_port 43000",
  ]);
  context.diagnostic("Mock Bash PID: " + result.pid);
  assert.equal(result.status, 0, result.stderr);
  const probes = result.stderr.trim().split(/\r?\n/);
  assert.equal(probes.length, 3);
  for (const probe of probes) {
    assert.match(probe, /^PROBE:43\d{3}$/);
  }
  assert.equal(result.stdout.trim(), probes[2].slice("PROBE:".length));
});

test("fails after ten unsuccessful default port probes", async (context) => {
  const { functionSource } = await readPortProbe();
  const result = runMockPortProbe(functionSource, [
    'node() { printf "PROBE:%s\\n" "$3" >&2; return 1; }',
    "pick_default_port 42000",
  ]);
  context.diagnostic("Mock Bash PID: " + result.pid);
  assert.equal(result.status, 1);
  assert.equal(result.stdout, "");
  const probes = result.stderr.split(/\r?\n/)
    .filter((line) => line.startsWith("PROBE:"));
  assert.equal(probes.length, 10);
  assert.ok(probes.every((probe) => /^PROBE:42\d{3}$/.test(probe)));
  assert.match(result.stderr, /no free port in the 42000 band after 10 attempts/);
});

test("preserves explicitly configured ports without probing defaults", async (context) => {
  const { functionSource, initializers } = await readPortProbe();
  const result = runMockPortProbe(functionSource, [
    'node() { printf "unexpected default probe\\n" >&2; return 1; }',
    ...initializers,
    'printf "%s,%s,%s\\n" "$POSTGRES_PORT" "$API_PORT" "$WEB_PORT"',
  ], {
    TESTING_LAB_E2E_POSTGRES_PORT: "43123",
    TESTING_LAB_E2E_API_PORT: "42123",
    TESTING_LAB_E2E_WEB_PORT: "44123",
  });
  context.diagnostic("Mock Bash PID: " + result.pid);
  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.stdout.trim(), "43123,42123,44123");
  assert.equal(result.stderr, "");
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
    /const \[owner, reviewer, tester\] =\s+await createTestingLabFixtureIdentities\(createFixtureIdentity\);/,
  );
  assert.doesNotMatch(journey, /Promise\.all\(\[\s*createFixtureIdentity/);
});
