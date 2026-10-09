/**
 * @file Deploy the installed QuickJS WebAssembly runtime into the web app's
 * public directory as a gzipped asset for the sandboxed legacy JavaScript
 * execution environment.
 */

import { createRequire } from "node:module";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { gzipSync } from "node:zlib";
import path from "node:path";
import { fileURLToPath } from "node:url";

const webRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

/**
 * Copy the exact installed QuickJS runtime, verify its WASM magic bytes, and
 * write a deterministic gzipped copy at the destination.
 *
 * @param {string} [destination] - Target directory for the runtime asset.
 * @returns {Promise<{source: string, output: string, uncompressedBytes: number}>} Deployed artifact paths and size.
 */
export async function syncJavaScriptRuntime(destination = path.join(webRoot, "public", "langs")) {
  const require = createRequire(path.join(webRoot, "package.json"));
  const dependencyRequire = createRequire(require.resolve("quickjs-emscripten"));
  const source = dependencyRequire.resolve("@jitl/quickjs-wasmfile-release-asyncify/wasm");
  const bytes = await readFile(source);
  if (!bytes.subarray(0, 4).equals(Buffer.from([0, 97, 115, 109]))) {
    throw new Error("The installed QuickJS runtime is not a WASM module.");
  }
  await mkdir(destination, { recursive: true });
  const output = path.join(destination, "quickjs-asyncify.wasm.gz");
  await writeFile(output, gzipSync(bytes));
  return { source, output, uncompressedBytes: bytes.length };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const result = await syncJavaScriptRuntime();
  console.log(`Prepared installed QuickJS runtime (${result.uncompressedBytes} bytes).`);
}
