import assert from "node:assert/strict";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";
import { CODE_TOOLCHAIN_V1 } from "../../../tools/emception/grading/toolchain-binding.mjs";
import { verifyCodingCycleToolchain } from "./coding-cycle-toolchain-support.mjs";

async function withManifest(manifest, run) {
  const directory = await mkdtemp(join(tmpdir(), "code-toolchain-preflight-"));
  try {
    const filename = join(directory, "manifest.json");
    await writeFile(filename, typeof manifest === "string" ? manifest : JSON.stringify(manifest));
    await run(filename);
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
}

function verifiedManifest() {
  return {
    schemaVersion: 2,
    ...CODE_TOOLCHAIN_V1,
    buildReceiptHash: "a".repeat(64),
    buildFingerprint: "b".repeat(64),
  };
}

test("legacy published artifacts cannot start the official Code cycle", async () => {
  await withManifest({ version: 1, files: {} }, async (filename) => {
    await assert.rejects(verifyCodingCycleToolchain(filename), /verified artifacts matching its frozen toolchain/);
  });
});
test("the same frozen compiler identity accepted by the worker passes preflight", async () => {
  await withManifest(verifiedManifest(), async (filename) => {
    await assert.doesNotReject(verifyCodingCycleToolchain(filename));
  });
});
for (const [name, patch] of Object.entries({
  compiler: { artifactVersion: "999" },
  abi: { runtimeAbi: "another-abi" },
  lock: { toolchainLockHash: "c".repeat(64) },
  receipt: { buildReceiptHash: undefined },
  fingerprint: { buildFingerprint: "invalid" },
})) {
  test(`changed ${name} prevents Code setup without relaxing worker validation`, async () => {
    await withManifest({ ...verifiedManifest(), ...patch }, async (filename) => {
      await assert.rejects(verifyCodingCycleToolchain(filename), /verified artifacts matching its frozen toolchain/);
    });
  });
}
test("malformed manifest fails before any academic data is created", async () => {
  await withManifest("{not-json", async (filename) => {
    await assert.rejects(verifyCodingCycleToolchain(filename), SyntaxError);
  });
});
test("the actual runner verifies synced artifacts before starting PostgreSQL", async () => {
  const source = await readFile(new URL("./coding-cycle-browser-e2e.mjs", import.meta.url), "utf8");
  const boot = source.slice(source.indexOf("async function bootStack()"));
  const verify = boot.indexOf("await verifyCodingCycleToolchain(");
  const postgres = boot.indexOf("// --- disposable postgres ---");
  assert.ok(verify > boot.indexOf('log("syncing canonical emception Toolchain release")'));
  assert.ok(verify < postgres);
});
