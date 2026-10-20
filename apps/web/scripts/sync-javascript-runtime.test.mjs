/**
 * @file Verify that the QuickJS runtime deployment writes the exact installed
 * WASM module and is deterministic across repeated runs.
 */

import assert from "node:assert/strict";
import { mkdtemp, readFile, rm } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";
import { gunzip } from "node:zlib";
import { promisify } from "node:util";
import { syncJavaScriptRuntime } from "./sync-javascript-runtime.mjs";

const decompress = promisify(gunzip);

test("deploys the exact installed runtime and is deterministic", async () => {
  const directory = await mkdtemp(path.join(os.tmpdir(), "gameguild-quickjs-"));
  try {
    const result = await syncJavaScriptRuntime(directory);
    const first = await readFile(result.output);
    const uncompressed = await decompress(first);
    assert.deepEqual(uncompressed, await readFile(result.source));
    assert.equal((await WebAssembly.compile(uncompressed)) instanceof WebAssembly.Module, true);
    await syncJavaScriptRuntime(directory);
    assert.deepEqual(await readFile(result.output), first);
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});
