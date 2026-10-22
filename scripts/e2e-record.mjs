/**
 * Wrap `playwright codegen` with repo defaults.
 *
 * Boots the lean dev stack (pnpm dev:fast) if the web app isn't already serving,
 * records a session, saves the spec under e2e/, tears the stack down
 * (only if this script started it).
 *
 * Usage:
 *   pnpm e2e:record                    # -> e2e/recorded-<HHMMSS>.spec.ts
 *   pnpm e2e:record login              # -> e2e/login.spec.ts (overwrites)
 *   pnpm e2e:record -- --browser firefox --save-storage=auth.json
 *
 * Target: PLAYWRIGHT_WEB_BASE_URL ?? http://localhost:3000
 * Recorded specs miss the E2E_RUN gate — add
 *   test.skip(!process.env.E2E_RUN, "set E2E_RUN=1 (needs web on :3000)");
 */
import { spawn, spawnSync } from "node:child_process";
import { existsSync, mkdirSync } from "node:fs";
import path from "node:path";

const args = process.argv.slice(2);
const passthrough = args.includes("--") ? args.slice(args.indexOf("--") + 1) : [];
const positionals = args.includes("--") ? args.slice(0, args.indexOf("--")) : args;

const name = positionals[0] ?? `recorded-${new Date().toTimeString().slice(0, 8).replace(/:/g, "")}`;
const baseUrl = (process.env.PLAYWRIGHT_WEB_BASE_URL ?? "http://localhost:3000").replace(/\/$/, "");

const e2eDir = path.resolve(import.meta.dirname, "../e2e");
mkdirSync(e2eDir, { recursive: true });
const outFile = path.join(e2eDir, `${name}.spec.ts`);

if (positionals[0] && existsSync(outFile)) {
  console.warn(`! overwriting existing ${path.relative(process.cwd(), outFile)}`);
}

async function isUp(url) {
  try {
    const res = await fetch(url, { method: "HEAD" });
    return res.status < 500;
  } catch {
    return false;
  }
}

const stackUp = async () => {
  const api = await fetch(process.env.E2E_API_HEALTH_URL ?? "http://localhost:8080/health").then(
    (r) => r.status >= 200 && r.status < 300,
    () => false,
  );
  return api && (await isUp(baseUrl));
};

const startedStack = !(await stackUp());
let stack = null;

if (startedStack) {
  console.log(`[e2e:record] web/api not healthy — starting stack supervisor...`);
  stack = spawn("node", [path.join(import.meta.dirname, "e2e-stack.mjs")], {
    stdio: "inherit",
    detached: process.platform !== "win32",
  });
  const deadline = Date.now() + 600_000;
  while (!(await stackUp())) {
    if (Date.now() > deadline) {
      console.error("[e2e:record] stack did not come up within 10min — aborting");
      process.exit(1);
    }
    await new Promise((r) => setTimeout(r, 2000));
  }
  console.log("[e2e:record] stack up — opening codegen");
}

const teardown = () => {
  if (!stack || stack.exitCode !== null) return;
  console.log("\n[e2e:record] tearing down stack...");
  // SIGTERM to the supervisor — it forwards to scripts/dev-fast.mjs, which kills
  // its own tree (its detached reapers finish any stragglers).
  try {
    stack.kill("SIGTERM");
  } catch {
    /* already gone */
  }
};
process.on("exit", teardown);
process.on("SIGINT", () => process.exit(130));
process.on("SIGTERM", () => process.exit(143));

const result = spawnSync("pnpm", ["exec", "playwright", "codegen", baseUrl, "-o", outFile, ...passthrough], {
  stdio: "inherit",
});
process.exit(result.status ?? 1);
